using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using SQLFileGenerator;
using SQLFileGenerator.LlmParser;

namespace SqlToEntityGenerator
{
    /// <summary>
    /// Основной класс программы SQL File Generator, который преобразует SQL-скрипты создания таблиц
    /// в файлы по шаблонам Scriban, такие как модели, репозитории, сервисы и т.д.
    /// </summary>
    class Program
    {
        /// <summary>
        /// Точка входа в программу. Выполняет парсинг SQL-скрипта, получает информацию о таблицах
        /// и генерирует файлы на основе шаблонов с учетом структуры базы данных.
        /// </summary>
        /// <param name="args">
        /// Аргументы командной строки:
        ///   --use-llm         Использовать LLM парсер вместо regex
        ///   --config [path]   Путь к файлу конфигурации (по умолчанию appsettings.json)
        ///   --output [path]   Путь для сохранения результатов
        ///   --sql [path]      Путь к SQL скрипту (по умолчанию sql/script.sql)
        /// </param>
        static async Task Main(string[] args)
        {
            try
            {
                // Парсинг аргументов командной строки
                var useLlm = args.Contains("--use-llm");
                var configPath = GetArgValue(args, "--config") ?? "appsettings.json";
                var sqlScriptPath = GetArgValue(args, "--sql") ?? "sql/script.sql";
                var templatesDir = "templates";

                // Получаем путь к результирующей папке
                string resultDir = GetArgValue(args, "--output") ?? GetResultDirFromArgs(args) ?? GetResultDirInteractive();

                // Вывод информации о режиме работы
                Console.WriteLine("=== SQL File Generator ===");
                Console.WriteLine($"Parser mode: {(useLlm ? "LLM" : "Regex")}");
                Console.WriteLine($"SQL script: {sqlScriptPath}");
                Console.WriteLine($"Templates: {templatesDir}");
                Console.WriteLine($"Output: {resultDir}");
                Console.WriteLine();

                // Чтение SQL-скрипта из файла
                if (!File.Exists(sqlScriptPath))
                {
                    Console.WriteLine($"SQL script file not found at {sqlScriptPath}");
                    return;
                }

                string sqlScript = File.ReadAllText(sqlScriptPath);

                // Выбор парсера и парсинг SQL-скрипта
                List<TableSchema> tables;

                if (useLlm)
                {
                    Console.WriteLine("Loading LLM configuration...");
                    var config = LoadLlmConfiguration(configPath);

                    Console.WriteLine("Starting LLM parser...");
                    using var llmParser = new LlmParserService(config);
                    tables = await llmParser.ParseSqlAsync(sqlScript);
                }
                else
                {
                    Console.WriteLine("Using regex parser...");
                    tables = SqlParser.ParsePostgresCreateTableScript(sqlScript);
                }

                if (tables == null || tables.Count == 0)
                {
                    Console.WriteLine("No tables were parsed from the SQL script.");
                    return;
                }

                Console.WriteLine($"Parsed {tables.Count} table(s) from the SQL script.");

                // Генерация шаблонных файлов
                if (!Directory.Exists(templatesDir))
                {
                    Console.WriteLine($"Templates directory not found at {templatesDir}");
                    return;
                }

                Directory.CreateDirectory(resultDir);
                FileGenerator.GenerateOtherFiles(tables, templatesDir, resultDir);

                Console.WriteLine();
                Console.WriteLine($"Files successfully generated to: {resultDir}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        /// <summary>
        /// Получает значение аргумента командной строки по имени
        /// </summary>
        private static string? GetArgValue(string[] args, string argName)
        {
            var index = Array.IndexOf(args, argName);
            if (index >= 0 && index < args.Length - 1)
            {
                var value = args[index + 1];
                // Проверяем, что следующий аргумент не является флагом
                if (!value.StartsWith("--"))
                {
                    return value;
                }
            }
            return null;
        }

        /// <summary>
        /// Получает путь результата из позиционных аргументов (для обратной совместимости)
        /// </summary>
        private static string? GetResultDirFromArgs(string[] args)
        {
            // Ищем первый аргумент, который не является флагом и не следует за --config/--output/--sql
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                // Пропускаем флаги и их значения
                if (arg.StartsWith("--"))
                {
                    if (arg == "--config" || arg == "--output" || arg == "--sql")
                    {
                        i++; // Пропускаем значение
                    }
                    continue;
                }

                // Проверяем, что это не значение предыдущего флага
                if (i > 0)
                {
                    var prevArg = args[i - 1];
                    if (prevArg == "--config" || prevArg == "--output" || prevArg == "--sql")
                    {
                        continue;
                    }
                }

                return arg;
            }
            return null;
        }

        /// <summary>
        /// Интерактивно запрашивает путь к результирующей папке
        /// </summary>
        private static string GetResultDirInteractive()
        {
            Console.Write("Enter output directory (default 'result'): ");
            var input = Console.ReadLine();
            return string.IsNullOrWhiteSpace(input) ? "result" : input;
        }

        /// <summary>
        /// Загружает конфигурацию LLM из файла
        /// </summary>
        private static LlmConfiguration LoadLlmConfiguration(string configPath)
        {
            if (!File.Exists(configPath))
            {
                Console.WriteLine($"Config file not found at {configPath}, using defaults");
                return new LlmConfiguration();
            }

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(configPath, optional: true, reloadOnChange: false)
                .Build();

            var config = new LlmConfiguration();
            configuration.GetSection("LlmParser").Bind(config);

            Console.WriteLine($"Loaded config from {configPath}");
            Console.WriteLine($"  API URL: {config.ApiUrl}");
            Console.WriteLine($"  Model: {config.ModelName}");
            Console.WriteLine($"  Max tables per chunk: {config.MaxTablesPerChunk}");

            return config;
        }
    }
}
