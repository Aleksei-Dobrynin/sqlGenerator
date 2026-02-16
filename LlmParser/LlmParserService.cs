using Newtonsoft.Json;

namespace SQLFileGenerator.LlmParser
{
    /// <summary>
    /// Основной сервис для парсинга SQL через LLM
    /// </summary>
    public class LlmParserService : IDisposable
    {
        private readonly LlmConfiguration _config;
        private readonly ILlmClient _client;
        private readonly SqlChunker _chunker;
        private readonly LlmResponseValidator _validator;
        private bool _disposed;

        public LlmParserService(LlmConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _client = new OpenAiCompatibleClient(config);
            _chunker = new SqlChunker(config.MaxTablesPerChunk);
            _validator = new LlmResponseValidator();
        }

        /// <summary>
        /// Парсит SQL скрипт через LLM
        /// </summary>
        /// <param name="sqlScript">PostgreSQL CREATE TABLE скрипт</param>
        /// <param name="cancellationToken">Токен отмены</param>
        /// <returns>Список распарсенных таблиц</returns>
        public async Task<List<TableSchema>> ParseSqlAsync(
            string sqlScript,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sqlScript))
            {
                throw new ArgumentException("SQL script cannot be empty", nameof(sqlScript));
            }

            Console.WriteLine("Starting LLM SQL parsing...");
            Console.WriteLine($"API URL: {_config.ApiUrl}");
            Console.WriteLine($"Model: {_config.ModelName}");

            // Warmup модели если включено (для Ollama)
            if (_config.WarmupModel)
            {
                await _client.WarmupAsync(cancellationToken);
            }

            // Разбиваем на chunks
            var chunks = _chunker.SplitIntoChunks(sqlScript);

            if (chunks.Count == 0)
            {
                Console.WriteLine("No tables found in SQL script");
                return new List<TableSchema>();
            }

            var allTables = new List<TableSchema>();
            var processedTableNames = new List<string>();

            foreach (var chunk in chunks)
            {
                Console.WriteLine($"\nProcessing chunk {chunk.Index + 1}/{chunks.Count} " +
                                $"({chunk.TableCount} tables: {string.Join(", ", chunk.TableNames)})");

                try
                {
                    var tables = await ParseChunkAsync(chunk, processedTableNames, cancellationToken);

                    if (tables != null && tables.Any())
                    {
                        allTables.AddRange(tables);
                        processedTableNames.AddRange(tables.Select(t => t.TableName));
                        Console.WriteLine($"Successfully parsed {tables.Count} table(s) from chunk {chunk.Index + 1}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing chunk {chunk.Index + 1}: {ex.Message}");
                    throw;
                }
            }

            // Финальная валидация FK references
            ValidateForeignKeyReferences(allTables);

            Console.WriteLine($"\nLLM parsing completed. Total tables: {allTables.Count}");
            return allTables;
        }

        /// <summary>
        /// Парсит один chunk SQL
        /// </summary>
        private async Task<List<TableSchema>> ParseChunkAsync(
            SqlChunk chunk,
            List<string> previousTableNames,
            CancellationToken cancellationToken)
        {
            // Формируем промпт
            var userPrompt = chunk.IsFirst
                ? SystemPrompts.FormatInitialPrompt(chunk.SqlContent)
                : SystemPrompts.FormatChunkPrompt(chunk.SqlContent, previousTableNames);

            // Отправляем запрос к LLM
            var response = await _client.SendCompletionRequestAsync(
                SystemPrompts.SqlParsingSystemPrompt,
                userPrompt,
                cancellationToken);

            // Валидируем ответ
            var validation = _validator.Validate(response, chunk.TableNames);

            if (!validation.IsValid)
            {
                Console.WriteLine("Validation failed, attempting retry...");
                validation.PrintToConsole();

                // Retry с уточняющим промптом
                var retryResponse = await RetryWithErrorFeedbackAsync(
                    chunk.SqlContent,
                    validation.GetErrorSummary(),
                    cancellationToken);

                validation = _validator.Validate(retryResponse, chunk.TableNames);

                if (!validation.IsValid)
                {
                    throw new InvalidOperationException(
                        $"LLM response validation failed after retry: {validation.GetErrorSummary()}");
                }
            }

            // Обработка warnings
            if (validation.Warnings.Any())
            {
                Console.WriteLine("Validation warnings:");
                foreach (var warning in validation.Warnings)
                {
                    Console.WriteLine($"  [WARN] {warning}");
                }
            }

            // Проверяем пропущенные таблицы
            if (validation.HasMissingTables)
            {
                Console.WriteLine($"Missing tables detected: {string.Join(", ", validation.MissingTables)}");
                var additionalTables = await RequestMissingTablesAsync(
                    chunk.SqlContent,
                    validation.MissingTables,
                    cancellationToken);

                if (additionalTables != null && additionalTables.Any())
                {
                    validation.ParsedTables!.AddRange(additionalTables);
                }
            }

            return validation.ParsedTables ?? new List<TableSchema>();
        }

        /// <summary>
        /// Повторный запрос с информацией об ошибке
        /// </summary>
        private async Task<string> RetryWithErrorFeedbackAsync(
            string sqlContent,
            string errorMessage,
            CancellationToken cancellationToken)
        {
            var retryPrompt = SystemPrompts.FormatRetryPrompt(errorMessage, sqlContent);

            return await _client.SendCompletionRequestAsync(
                SystemPrompts.SqlParsingSystemPrompt,
                retryPrompt,
                cancellationToken);
        }

        /// <summary>
        /// Запрашивает пропущенные таблицы
        /// </summary>
        private async Task<List<TableSchema>?> RequestMissingTablesAsync(
            string sqlContent,
            List<string> missingTables,
            CancellationToken cancellationToken)
        {
            try
            {
                var prompt = SystemPrompts.FormatMissingTablesPrompt(missingTables, sqlContent);

                var response = await _client.SendCompletionRequestAsync(
                    SystemPrompts.SqlParsingSystemPrompt,
                    prompt,
                    cancellationToken);

                var validation = _validator.Validate(response, missingTables);

                if (validation.IsValid)
                {
                    return validation.ParsedTables;
                }

                Console.WriteLine($"Failed to parse missing tables: {validation.GetErrorSummary()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error requesting missing tables: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Финальная валидация FK references между всеми таблицами
        /// </summary>
        private void ValidateForeignKeyReferences(List<TableSchema> tables)
        {
            var tableNames = tables
                .Select(t => t.TableName.ToLowerInvariant())
                .ToHashSet();

            var unresolvedFks = new List<string>();

            foreach (var table in tables)
            {
                foreach (var fk in table.ForeignKeys)
                {
                    if (!tableNames.Contains(fk.ReferencesTable.ToLowerInvariant()))
                    {
                        unresolvedFks.Add(
                            $"{table.TableName}.{fk.ColumnName} -> {fk.ReferencesTable}");
                    }
                }
            }

            if (unresolvedFks.Any())
            {
                Console.WriteLine("\nWarning: Unresolved foreign key references " +
                                "(referenced tables not in parsed schema):");
                foreach (var fk in unresolvedFks)
                {
                    Console.WriteLine($"  {fk}");
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_client is IDisposable disposableClient)
                {
                    disposableClient.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
