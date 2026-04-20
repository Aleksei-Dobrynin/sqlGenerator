using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SQLFileGenerator.LlmParser
{
    /// <summary>
    /// HTTP клиент для OpenAI-совместимых API (Ollama, OpenAI, Azure OpenAI, LM Studio и др.)
    /// </summary>
    public class OpenAiCompatibleClient : ILlmClient, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly LlmConfiguration _config;
        private bool _disposed;

        public OpenAiCompatibleClient(LlmConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds)
            };

            // Настройка базового URL
            var baseUrl = config.ApiUrl.TrimEnd('/');
            if (!baseUrl.EndsWith("/v1"))
            {
                baseUrl += "/v1";
            }
            _httpClient.BaseAddress = new Uri(baseUrl + "/");

            // Добавляем API ключ если указан
            if (!string.IsNullOrEmpty(config.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", config.ApiKey);
            }

            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <inheritdoc />
        public async Task<string> SendCompletionRequestAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken = default)
        {
            var request = new
            {
                model = _config.ModelName,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = _config.Temperature,
                max_tokens = _config.MaxTokens,
                stream = false
            };

            var jsonContent = JsonConvert.SerializeObject(request);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var retryCount = 0;
            Exception? lastException = null;

            while (retryCount < _config.MaxRetries)
            {
                try
                {
                    var response = await _httpClient.PostAsync("chat/completions", httpContent, cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                        return ExtractContentFromResponse(responseBody);
                    }

                    // Обработка ошибок HTTP
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);

                    if ((int)response.StatusCode == 429) // Rate limit
                    {
                        var delay = CalculateRetryDelay(retryCount, isRateLimit: true);
                        Console.WriteLine($"Rate limit hit. Waiting {delay}ms before retry...");
                        await Task.Delay(delay, cancellationToken);
                        retryCount++;
                        continue;
                    }

                    if ((int)response.StatusCode >= 500) // Server errors
                    {
                        var delay = CalculateRetryDelay(retryCount, isRateLimit: false);
                        Console.WriteLine($"Server error {response.StatusCode}. Retrying in {delay}ms...");
                        await Task.Delay(delay, cancellationToken);
                        retryCount++;
                        lastException = new HttpRequestException($"HTTP {response.StatusCode}: {errorBody}");
                        continue;
                    }

                    // Клиентские ошибки (401, 403, 400) - не retry
                    throw new HttpRequestException($"HTTP {response.StatusCode}: {errorBody}");
                }
                catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Timeout
                    var delay = CalculateRetryDelay(retryCount, isRateLimit: false);
                    Console.WriteLine($"Request timeout. Retrying in {delay}ms...");
                    await Task.Delay(delay, cancellationToken);
                    retryCount++;
                    lastException = new TimeoutException("Request timed out");
                }
                catch (HttpRequestException ex)
                {
                    // Сетевые ошибки
                    var delay = CalculateRetryDelay(retryCount, isRateLimit: false);
                    Console.WriteLine($"Network error: {ex.Message}. Retrying in {delay}ms...");
                    await Task.Delay(delay, cancellationToken);
                    retryCount++;
                    lastException = ex;
                }
            }

            throw lastException ?? new Exception("Max retries exceeded");
        }

        /// <summary>
        /// Извлекает content из ответа OpenAI API
        /// </summary>
        private string ExtractContentFromResponse(string responseBody)
        {
            try
            {
                var json = JObject.Parse(responseBody);

                // Стандартный формат OpenAI
                var content = json["choices"]?[0]?["message"]?["content"]?.ToString();

                if (string.IsNullOrEmpty(content))
                {
                    // Thinking-модели (Gemma 4, DeepSeek R1 и др.) помещают reasoning в отдельное поле,
                    // а content может быть пустым если finish_reason=length
                    var reasoningContent = json["choices"]?[0]?["message"]?["reasoning_content"]?.ToString();
                    if (!string.IsNullOrEmpty(reasoningContent))
                    {
                        // Пытаемся извлечь JSON из reasoning_content
                        var jsonStart = reasoningContent.IndexOf('[');
                        var jsonEnd = reasoningContent.LastIndexOf(']');
                        if (jsonStart >= 0 && jsonEnd > jsonStart)
                        {
                            content = reasoningContent.Substring(jsonStart, jsonEnd - jsonStart + 1);
                        }
                    }
                }

                if (string.IsNullOrEmpty(content))
                {
                    // Альтернативный формат (некоторые API)
                    content = json["response"]?.ToString()
                           ?? json["content"]?.ToString()
                           ?? json["text"]?.ToString();
                }

                if (string.IsNullOrEmpty(content))
                {
                    throw new InvalidOperationException($"Could not extract content from response: {responseBody}");
                }

                // Очистка от возможных markdown блоков
                content = CleanJsonResponse(content);

                return content;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Failed to parse API response: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Очищает ответ от markdown блоков кода
        /// </summary>
        private string CleanJsonResponse(string content)
        {
            content = content.Trim();

            // Удаляем ```json ... ``` блоки
            if (content.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                content = content.Substring(7);
                var endIndex = content.LastIndexOf("```", StringComparison.Ordinal);
                if (endIndex > 0)
                {
                    content = content.Substring(0, endIndex);
                }
            }
            else if (content.StartsWith("```"))
            {
                content = content.Substring(3);
                var endIndex = content.LastIndexOf("```", StringComparison.Ordinal);
                if (endIndex > 0)
                {
                    content = content.Substring(0, endIndex);
                }
            }

            return content.Trim();
        }

        /// <summary>
        /// Рассчитывает задержку для retry с экспоненциальным backoff
        /// </summary>
        private int CalculateRetryDelay(int attempt, bool isRateLimit)
        {
            var baseDelay = _config.RetryDelayMs * (int)Math.Pow(2, attempt);

            if (isRateLimit)
            {
                baseDelay *= 5; // Увеличенная задержка для rate limit
            }

            // Добавляем jitter (случайность) для предотвращения thundering herd
            var jitter = new Random().Next(0, 500);

            return Math.Min(baseDelay + jitter, 30000); // Max 30 секунд
        }

        /// <inheritdoc />
        public async Task WarmupAsync(CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"Warming up model '{_config.ModelName}'...");
            Console.WriteLine("This may take a few minutes on first run while the model loads into memory.");

            // Создаём отдельный HttpClient с увеличенным таймаутом для warmup
            using var warmupClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(_config.WarmupTimeoutSeconds),
                BaseAddress = _httpClient.BaseAddress
            };

            // Копируем заголовки авторизации
            if (_httpClient.DefaultRequestHeaders.Authorization != null)
            {
                warmupClient.DefaultRequestHeaders.Authorization = _httpClient.DefaultRequestHeaders.Authorization;
            }
            warmupClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var request = new
            {
                model = _config.ModelName,
                messages = new[]
                {
                    new { role = "user", content = "Hi" }
                },
                max_tokens = 5,
                stream = false
            };

            var jsonContent = JsonConvert.SerializeObject(request);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var response = await warmupClient.PostAsync("chat/completions", httpContent, cancellationToken);
                stopwatch.Stop();

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Model warmed up successfully in {stopwatch.Elapsed.TotalSeconds:F1}s");
                }
                else
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    Console.WriteLine($"Warmup request failed: HTTP {response.StatusCode}");
                    Console.WriteLine($"Response: {errorBody}");
                }
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                Console.WriteLine($"Warmup timed out after {stopwatch.Elapsed.TotalSeconds:F1}s");
                Console.WriteLine("Consider increasing WarmupTimeoutSeconds in configuration.");
                throw new TimeoutException($"Model warmup timed out after {_config.WarmupTimeoutSeconds} seconds");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _httpClient.Dispose();
                _disposed = true;
            }
        }
    }
}
