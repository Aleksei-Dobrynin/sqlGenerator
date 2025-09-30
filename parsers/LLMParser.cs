using Microsoft.Extensions.Configuration;
using SQLFileGenerator.structures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SQLFileGenerator
{
    /// <summary>
    /// Парсер SQL-скриптов с использованием LLM
    /// </summary>
    public class LLMParser
    {
        private readonly HttpClient _httpClient;
        private readonly LLMParserConfig _config;
        private readonly LLMRequestBuilder _requestBuilder;
        private readonly Dictionary<string, List<TableSchema>> _cache;

        public LLMParser(IConfiguration configuration)
        {
            _config = configuration?.GetSection("LLMParser").Get<LLMParserConfig>()
                ?? new LLMParserConfig();

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds)
            };

            _requestBuilder = new LLMRequestBuilder();
            _cache = new Dictionary<string, List<TableSchema>>();
        }

        /// <summary>
        /// Парсит SQL-скрипт с помощью LLM
        /// </summary>
        public async Task<List<TableSchema>> ParsePostgresCreateTableScriptAsync(string sqlScript)
        {
            var startTime = DateTime.Now;
            Logger.LogInfo("Starting LLM parsing...");

            if (string.IsNullOrWhiteSpace(sqlScript))
            {
                var error = "SQL script cannot be empty";
                Logger.LogError(error);
                throw new ArgumentException(error, nameof(sqlScript));
            }

            Logger.LogDebug($"SQL script length: {sqlScript.Length} characters");

            // Проверяем кэш
            if (_config.CacheResponses)
            {
                var cacheKey = ComputeHash(sqlScript);
                if (_cache.TryGetValue(cacheKey, out var cachedResult))
                {
                    Logger.LogInfo("Using cached LLM response");
                    return cachedResult;
                }
            }

            // Подготавливаем промпт
            var userPrompt = BuildUserPrompt(sqlScript);
            Logger.LogDebug($"User prompt prepared, length: {userPrompt.Length}");

            // Отправляем запрос с retry логикой
            string jsonResponse = null;
            Exception lastException = null;

            for (int attempt = 1; attempt <= _config.RetryCount; attempt++)
            {
                try
                {
                    if (attempt > 1)
                    {
                        Logger.LogWarning($"Retry attempt {attempt}/{_config.RetryCount}");
                        await Task.Delay(_config.RetryDelayMs);
                    }

                    // Отправляем запрос с индикатором прогресса
                    jsonResponse = await Progress.RunWithProgress(
                        $"Sending request to LLM (attempt {attempt}/{_config.RetryCount})",
                        async () => await SendRequestToLLM(userPrompt),
                        ProgressIndicator.AnimationStyle.Dots
                    );

                    if (!string.IsNullOrWhiteSpace(jsonResponse))
                    {
                        Logger.LogInfo($"Successfully received response from LLM on attempt {attempt}");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    Logger.LogError($"Attempt {attempt} failed", ex);
                }
            }

            if (string.IsNullOrWhiteSpace(jsonResponse))
            {
                var error = $"Failed to get response from LLM after {_config.RetryCount} attempts";
                Logger.LogCritical(error, lastException);
                throw new Exception(error, lastException);
            }

            // Парсим ответ с индикатором прогресса
            List<TableSchema> tables = null;
            using (var indicator = new ProgressIndicator("Parsing LLM response", ProgressIndicator.AnimationStyle.Bar))
            {
                try
                {
                    tables = ParseLLMResponse(jsonResponse);
                    indicator.Stop("LLM response parsed successfully", true);
                }
                catch (Exception ex)
                {
                    indicator.Stop("Failed to parse LLM response", false);
                    Logger.LogError("Failed to parse LLM response", ex);
                    throw;
                }
            }

            // Кэшируем результат
            if (_config.CacheResponses && tables != null && tables.Count > 0)
            {
                var cacheKey = ComputeHash(sqlScript);
                _cache[cacheKey] = tables;
                Logger.LogDebug("Response cached for future use");
            }

            // Логируем статистику
            var elapsed = DateTime.Now - startTime;
            var totalColumns = tables?.Sum(t => t.Columns?.Count ?? 0) ?? 0;
            var totalForeignKeys = tables?.Sum(t => t.ForeignKeys?.Count ?? 0) ?? 0;
            Logger.LogParsingStats(tables?.Count ?? 0, totalColumns, totalForeignKeys, elapsed);

            return tables;
        }

        /// <summary>
        /// Строит промпт для пользователя
        /// </summary>
        private string BuildUserPrompt(string sqlScript)
        {
            // Подсчитываем количество таблиц для подсказки LLM
            var tableCount = System.Text.RegularExpressions.Regex.Matches(
                sqlScript,
                @"CREATE\s+TABLE",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            ).Count;

            return $@"Parse the following PostgreSQL CREATE TABLE script and return JSON array with table schemas.

The script contains approximately {tableCount} table(s).

{sqlScript}

Remember: 
- Return ONLY valid JSON array: [{(tableCount == 1 ? "single_table_object" : "table1, table2, ...")}]
- ALWAYS wrap response in square brackets [ ] even for single table
- NO markdown formatting or ```json``` blocks
- NO explanations or text outside JSON
- Include ALL tables, columns, primary keys, and foreign keys from the script";
        }

        /// <summary>
        /// Отправляет запрос к LLM API
        /// </summary>
        private async Task<string> SendRequestToLLM(string userPrompt)
        {
            var request = _requestBuilder.BuildChatCompletionRequest(
                _config.SystemPrompt,
                userPrompt,
                _config
            );

            var jsonRequest = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            if (_config.LogRequests)
            {
                Logger.LogJson("Request to LLM", jsonRequest);
            }

            Logger.LogInfo($"Sending request to {_config.ApiEndpoint} (timeout: {_config.TimeoutSeconds}s)");
            Console.WriteLine($"⏳ Note: First request may take longer as model loads into memory...");

            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(_config.ApiEndpoint, content);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Logger.LogError($"LLM API error: {response.StatusCode} - {error}");

                    // Проверяем специфичные ошибки
                    if (error.Contains("model") && error.Contains("not found"))
                    {
                        throw new Exception($"Model '{_config.Model}' not found. Please run: ollama pull {_config.Model}");
                    }

                    throw new Exception($"LLM API error: {response.StatusCode} - {error}");
                }

                var responseJson = await response.Content.ReadAsStringAsync();

                if (_config.LogRequests)
                {
                    Logger.LogJson("Response from LLM", responseJson);
                }

                // Извлекаем content из ответа
                var completionResponse = JsonSerializer.Deserialize<ChatCompletionResponse>(responseJson);

                if (completionResponse?.Choices?.FirstOrDefault()?.Message?.Content == null)
                {
                    throw new Exception("Invalid response from LLM: no content in response");
                }

                return completionResponse.Choices.First().Message.Content;
            }
            catch (TaskCanceledException ex)
            {
                var timeoutMessage = $"Request timeout after {_config.TimeoutSeconds} seconds. " +
                                    "Try increasing 'TimeoutSeconds' in appsettings.json or using a smaller SQL script.";
                Logger.LogError(timeoutMessage, ex);
                throw new TimeoutException(timeoutMessage, ex);
            }
            catch (HttpRequestException ex)
            {
                var connectionMessage = $"Cannot connect to LLM at {_config.ApiEndpoint}. " +
                                      "Please ensure Ollama is running (ollama serve) and the endpoint is correct.";
                Logger.LogError(connectionMessage, ex);
                throw new Exception(connectionMessage, ex);
            }
        }

        /// <summary>
        /// Парсит JSON-ответ от LLM в список TableSchema
        /// </summary>
        private List<TableSchema> ParseLLMResponse(string jsonResponse)
        {
            try
            {
                // Очищаем ответ от возможных markdown блоков
                jsonResponse = CleanJsonResponse(jsonResponse);

                // Парсим JSON
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                List<TableSchema> tables = null;

                // Пробуем парсить как массив
                if (jsonResponse.TrimStart().StartsWith("["))
                {
                    Logger.LogDebug("Parsing response as array of tables");
                    tables = JsonSerializer.Deserialize<List<TableSchema>>(jsonResponse, options);
                }
                // Пробуем парсить как один объект
                else if (jsonResponse.TrimStart().StartsWith("{"))
                {
                    Logger.LogDebug("Parsing response as single table object");
                    var singleTable = JsonSerializer.Deserialize<TableSchema>(jsonResponse, options);
                    if (singleTable != null)
                    {
                        tables = new List<TableSchema> { singleTable };
                    }
                }
                else
                {
                    throw new Exception($"Unexpected JSON format. Response starts with: {jsonResponse.Substring(0, Math.Min(50, jsonResponse.Length))}");
                }

                if (tables == null || tables.Count == 0)
                {
                    throw new Exception("No tables found in LLM response");
                }

                // Валидируем и нормализуем результат
                foreach (var table in tables)
                {
                    // Убеждаемся, что EntityName установлен
                    if (string.IsNullOrWhiteSpace(table.EntityName))
                    {
                        table.EntityName = ToPascalCase(table.TableName);
                    }

                    // Проверяем и исправляем колонки
                    if (table.Columns != null)
                    {
                        foreach (var column in table.Columns)
                        {
                            // Нормализуем типы C#
                            column.CSharpType = NormalizeCSharpType(column.CSharpType);

                            // Если колонка является PK, она не может быть nullable
                            if (column.IsPrimaryKey)
                            {
                                column.IsNullable = false;
                            }
                        }
                    }

                    // Инициализируем пустые коллекции если null
                    table.Columns ??= new List<ColumnSchema>();
                    table.ForeignKeys ??= new List<ForeignKeyInfo>();
                }

                Logger.LogInfo($"Successfully parsed {tables.Count} table(s) from LLM response");
                foreach (var table in tables)
                {
                    Logger.LogDebug($"  Table: {table.TableName} -> {table.EntityName}");
                    Logger.LogDebug($"    Columns: {table.Columns?.Count ?? 0}");
                    Logger.LogDebug($"    Foreign Keys: {table.ForeignKeys?.Count ?? 0}");
                }

                return tables;
            }
            catch (JsonException ex)
            {
                Logger.LogError($"Failed to parse JSON from LLM: {ex.Message}");
                Logger.LogDebug($"Raw response that failed to parse: {jsonResponse}");

                // Пытаемся дать более детальную информацию об ошибке
                var preview = jsonResponse.Length > 200
                    ? jsonResponse.Substring(0, 200) + "..."
                    : jsonResponse;

                var errorDetails = $"JSON parsing failed. Response format: " +
                    $"{(jsonResponse.TrimStart().StartsWith("[") ? "Array" : jsonResponse.TrimStart().StartsWith("{") ? "Object" : "Unknown")}. " +
                    $"Preview: {preview}";

                Logger.LogError(errorDetails);

                throw new Exception($"Failed to parse LLM response as valid JSON. {errorDetails}", ex);
            }
        }

        /// <summary>
        /// Очищает JSON-ответ от markdown форматирования
        /// </summary>
        private string CleanJsonResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return response;

            // Удаляем markdown code blocks
            response = response.Trim();

            if (response.StartsWith("```json"))
            {
                response = response.Substring(7);
            }
            else if (response.StartsWith("```"))
            {
                response = response.Substring(3);
            }

            if (response.EndsWith("```"))
            {
                response = response.Substring(0, response.Length - 3);
            }

            return response.Trim();
        }

        /// <summary>
        /// Нормализует тип C# к ожидаемому формату
        /// </summary>
        private string NormalizeCSharpType(string type)
        {
            if (string.IsNullOrWhiteSpace(type))
                return "object";

            return type.ToLower() switch
            {
                "integer" or "int32" => "int",
                "bigint" or "int64" => "long",
                "text" or "varchar" or "nvarchar" => "string",
                "boolean" => "bool",
                "timestamp" or "date" or "datetime" => "DateTime",
                "real" or "single" => "float",
                "double" or "double precision" => "double",
                "numeric" or "money" => "decimal",
                _ => type // Оставляем как есть если уже правильный
            };
        }

        /// <summary>
        /// Преобразует строку в PascalCase
        /// </summary>
        private string ToPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var words = input.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(word => char.ToUpper(word[0]) + word.Substring(1).ToLower());
            return string.Concat(words);
        }

        /// <summary>
        /// Вычисляет хеш для кэширования
        /// </summary>
        private string ComputeHash(string input)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                return Convert.ToBase64String(bytes);
            }
        }
    }
}