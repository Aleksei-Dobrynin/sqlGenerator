using Microsoft.Extensions.Configuration;
using SQLFileGenerator;
using SQLFileGenerator.structures;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

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
        /// <param name="args">Аргументы командной строки. Первый аргумент - путь для сохранения результатов.</param>
        static async Task Main(string[] args)
        {
            // Инициализация логгера
            Logger.Initialize();

            try
            {
                // Загрузка конфигурации
                var configuration = LoadConfiguration();

                // Шаг 1: Определение путей к SQL-скриптам, шаблонам и результатам
                string sqlScriptPath = "sql/script.sql"; // Путь к SQL-скрипту
                string templatesDir = "templates"; // Папка с шаблонами Scriban
                string resultDir; // Папка для сохранения сгенерированных файлов

                Logger.LogInfo("=== SQL File Generator Started ===");

                // Получаем путь к результирующей папке от пользователя
                if (args.Length > 0)
                {
                    // Если путь указан в аргументах командной строки
                    resultDir = args[0];
                    Logger.LogInfo($"Output directory from arguments: {resultDir}");
                }
                else
                {
                    // Запрашиваем у пользователя
                    Console.Write("Введите путь для сохранения сгенерированных файлов (по умолчанию 'result'): ");
                    resultDir = Console.ReadLine();

                    // Если пользователь ничего не ввел, используем значение по умолчанию
                    if (string.IsNullOrWhiteSpace(resultDir))
                    {
                        resultDir = "result";
                    }
                    Logger.LogInfo($"Output directory: {resultDir}");
                }

                // Шаг 2: Чтение SQL-скрипта из файла с индикатором прогресса
                if (!File.Exists(sqlScriptPath))
                {
                    var error = $"SQL script file not found at {sqlScriptPath}";
                    Logger.LogError(error);
                    Console.WriteLine(error);
                    return;
                }

                string sqlScript = null;
                using (var indicator = new ProgressIndicator("Loading SQL script", ProgressIndicator.AnimationStyle.Dots))
                {
                    sqlScript = File.ReadAllText(sqlScriptPath);
                    indicator.Stop($"Loaded SQL script from {sqlScriptPath} ({sqlScript.Length} characters)", true);
                }
                Logger.LogInfo($"SQL script loaded successfully: {sqlScript.Length} characters");

                // Шаг 3: Выбор режима парсера
                ParserMode parserMode = GetParserMode(configuration);
                Logger.LogInfo($"Parser mode selected: {parserMode}");

                // Шаг 4: Парсинг SQL-скрипта
                Console.WriteLine($"\n=== Starting parsing with {parserMode} mode ===\n");

                var parser = ParserFactory.CreateParser(parserMode, configuration);
                List<TableSchema> tables = null;

                try
                {
                    tables = await parser.ParseAsync(sqlScript);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Parsing failed", ex);
                    Console.WriteLine($"\n❌ Error during parsing: {ex.Message}");
                    Console.WriteLine("Check the log file for more details.");
                    return;
                }

                if (tables == null || tables.Count == 0)
                {
                    var warning = "No tables were parsed from the SQL script.";
                    Logger.LogWarning(warning);
                    Console.WriteLine(warning);
                    return;
                }

                Console.WriteLine($"\n✅ Successfully parsed {tables.Count} table(s)\n");
                Logger.LogInfo($"Parsing completed: {tables.Count} tables found");

                // Выводим информацию о распарсенных таблицах
                foreach (var table in tables)
                {
                    var tableInfo = $"Table: {table.TableName} -> Entity: {table.EntityName}";
                    Console.WriteLine(tableInfo);
                    Console.WriteLine($"  Columns: {table.Columns?.Count ?? 0}");
                    Console.WriteLine($"  Foreign Keys: {table.ForeignKeys?.Count ?? 0}");

                    Logger.LogDebug(tableInfo);
                    Logger.LogDebug($"  Columns: {string.Join(", ", table.Columns?.Select(c => c.Name) ?? new List<string>())}");
                }

                // Шаг 5: Генерация шаблонных файлов
                Console.WriteLine($"\n=== Starting template generation ===\n");

                if (!Directory.Exists(templatesDir))
                {
                    var error = $"Templates directory not found at {templatesDir}";
                    Logger.LogError(error);
                    Console.WriteLine(error);
                    return;
                }

                Directory.CreateDirectory(resultDir); // Создаем папку для результатов, если её нет

                using (var indicator = new ProgressIndicator("Generating files from templates", ProgressIndicator.AnimationStyle.Bar))
                {
                    try
                    {
                        FileGenerator.GenerateOtherFiles(tables, templatesDir, resultDir);
                        indicator.Stop("Files generation completed", true);
                    }
                    catch (Exception ex)
                    {
                        indicator.Stop("Files generation failed", false);
                        Logger.LogError("Template generation failed", ex);
                        throw;
                    }
                }

                Console.WriteLine($"\n✅ Files successfully generated in: {resultDir}");
                Logger.LogInfo($"Generation completed successfully. Output directory: {resultDir}");

                // Показываем путь к логу
                Console.WriteLine($"\n📝 Log file saved to: logs/");
            }
            catch (Exception ex)
            {
                Logger.LogCritical("Unhandled exception in main", ex);
                Console.WriteLine($"\n❌ Fatal error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        /// <summary>
        /// Загружает конфигурацию из appsettings.json
        /// </summary>
        private static IConfiguration LoadConfiguration()
        {
            try
            {
                var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

                var configuration = builder.Build();
                Logger.LogInfo("Configuration loaded from appsettings.json");
                Console.WriteLine("Configuration loaded from appsettings.json");
                return configuration;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Could not load configuration: {ex.Message}. Using default settings");
                Console.WriteLine($"Warning: Could not load configuration: {ex.Message}");
                Console.WriteLine("Using default settings");
                return null;
            }
        }

        /// <summary>
        /// Получает режим парсера от пользователя или из конфигурации
        /// </summary>
        private static ParserMode GetParserMode(IConfiguration configuration)
        {
            // Пробуем получить режим из конфигурации
            var defaultMode = configuration?.GetValue<string>("Parser:DefaultMode");

            if (!string.IsNullOrWhiteSpace(defaultMode))
            {
                Console.WriteLine($"Using parser mode from configuration: {defaultMode}");
                Logger.LogInfo($"Parser mode from config: {defaultMode}");
                return ParserFactory.ParseMode(defaultMode);
            }

            // Интерактивный выбор режима
            Console.WriteLine("\n=== SELECT PARSER MODE ===");
            Console.WriteLine("1. Standard Parser (regex-based) - default");
            Console.WriteLine("2. LLM Parser (AI-powered)");
            Console.Write("\nEnter your choice (1-2): ");

            var choice = Console.ReadLine();

            var mode = choice?.Trim() switch
            {
                "2" => ParserMode.LLM,
                _ => ParserMode.Standard
            };

            Console.WriteLine($"Selected: {mode} mode");
            Logger.LogInfo($"User selected parser mode: {mode}");

            // Проверяем доступность LLM при выборе соответствующего режима
            if (mode == ParserMode.LLM)
            {
                var llmConfig = configuration?.GetSection("LLMParser").Get<LLMParserConfig>();
                if (llmConfig == null || !llmConfig.Enabled)
                {
                    Console.WriteLine("\n⚠️  Warning: LLM parser is not configured or disabled in appsettings.json");
                    Console.WriteLine("Make sure Ollama is running and configured properly.");
                    Console.Write("Continue anyway? (y/n): ");

                    if (Console.ReadLine()?.ToLower() != "y")
                    {
                        Console.WriteLine("Switching to Standard parser");
                        Logger.LogWarning("User switched to Standard parser due to LLM configuration issues");
                        return ParserMode.Standard;
                    }
                }
                else
                {
                    Console.WriteLine($"\n📋 LLM Configuration:");
                    Console.WriteLine($"  Endpoint: {llmConfig.ApiEndpoint}");
                    Console.WriteLine($"  Model: {llmConfig.Model}");
                    Console.WriteLine($"  Temperature: {llmConfig.Temperature}");
                    Console.WriteLine($"  Max Tokens: {llmConfig.MaxTokens}");

                    Logger.LogInfo($"LLM Config - Endpoint: {llmConfig.ApiEndpoint}, Model: {llmConfig.Model}");
                }
            }

            return mode;
        }
    }
}