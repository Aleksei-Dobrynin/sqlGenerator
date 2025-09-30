using Microsoft.Extensions.Configuration;
using SQLFileGenerator.parsers;
using SQLFileGenerator.structures;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SQLFileGenerator
{
    /// <summary>
    /// Адаптер для LLM парсера
    /// </summary>
    public class LLMParserAdapter : IParser
    {
        private readonly LLMParser _llmParser;

        public string Name => "LLM Parser";

        public LLMParserAdapter(IConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration),
                    "Configuration is required for LLM parser");
            }

            _llmParser = new LLMParser(configuration);
        }

        /// <summary>
        /// Парсит SQL-скрипт с помощью LLM
        /// </summary>
        public async Task<List<TableSchema>> ParseAsync(string sqlScript)
        {
            try
            {
                Console.WriteLine($"Using {Name}...");
                var result = await _llmParser.ParsePostgresCreateTableScriptAsync(sqlScript);

                if (result == null || result.Count == 0)
                {
                    Console.WriteLine("No tables found by LLM parser");
                }
                else
                {
                    Console.WriteLine($"LLM parser found {result.Count} table(s)");

                    // Выводим дополнительную информацию
                    foreach (var table in result)
                    {
                        Console.WriteLine($"  - Table: {table.TableName} ({table.EntityName})");
                        Console.WriteLine($"    Columns: {table.Columns?.Count ?? 0}");
                        Console.WriteLine($"    Foreign Keys: {table.ForeignKeys?.Count ?? 0}");
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LLM parser error: {ex.Message}");
                throw;
            }
        }


    }

    /// <summary>
    /// Адаптер для существующего стандартного парсера
    /// </summary>
    public class StandardParserAdapter : IParser
    {
        public string Name => "Standard Parser";

        /// <summary>
        /// Асинхронная обертка над существующим синхронным парсером
        /// </summary>
        public Task<List<TableSchema>> ParseAsync(string sqlScript)
        {
            try
            {
                Logger.LogInfo($"Starting {Name}...");

                using (var indicator = new ProgressIndicator("Parsing SQL with regex patterns", ProgressIndicator.AnimationStyle.Spinner))
                {
                    var result = SqlParser.ParsePostgresCreateTableScript(sqlScript);

                    if (result == null || result.Count == 0)
                    {
                        indicator.Stop("No tables found", false);
                        Logger.LogWarning("Standard parser found no tables");
                    }
                    else
                    {
                        indicator.Stop($"Found {result.Count} table(s)", true);
                        Logger.LogInfo($"Standard parser successfully found {result.Count} table(s)");
                    }

                    return Task.FromResult(result);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Standard parser error", ex);
                throw;
            }
        }
    }
}