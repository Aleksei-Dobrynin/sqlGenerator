using System;
using System.Collections.Generic;
using System.IO;
using SQLFileGenerator;

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
        static void Main(string[] args)
        {
            try
            {
                // Шаг 1: Определение путей к SQL-скриптам, шаблонам и результатам
                string sqlScriptPath = "sql/script.sql"; // Путь к SQL-скрипту
                string templatesDir = "templates"; // Папка с шаблонами Scriban
                string resultDir; // Папка для сохранения сгенерированных файлов
                
                // Получаем путь к результирующей папке от пользователя
                if (args.Length > 0)
                {
                    // Если путь указан в аргументах командной строки
                    resultDir = args[0];
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
                }

                // Шаг 2: Чтение SQL-скрипта из файла
                if (!File.Exists(sqlScriptPath))
                {
                    Console.WriteLine($"SQL script file not found at {sqlScriptPath}");
                    return;
                }

                string sqlScript = File.ReadAllText(sqlScriptPath);

                // Шаг 3: Парсинг SQL-скрипта
                var tables = SqlParser.ParsePostgresCreateTableScript(sqlScript);

                if (tables == null || tables.Count == 0)
                {
                    Console.WriteLine("No tables were parsed from the SQL script.");
                    return;
                }

                Console.WriteLine($"Parsed {tables.Count} table(s) from the SQL script.");

                // Шаг 4: Генерация шаблонных файлов
                if (!Directory.Exists(templatesDir))
                {
                    Console.WriteLine($"Templates directory not found at {templatesDir}");
                    return;
                }

                Directory.CreateDirectory(resultDir); // Создаем папку для результатов, если её нет
                FileGenerator.GenerateOtherFiles(tables, templatesDir, resultDir);

                Console.WriteLine($"Файлы успешно сгенерированы в папку: {resultDir}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
    }
}