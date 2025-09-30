using Scriban.Runtime;
using Scriban;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SQLFileGenerator.structures;

namespace SQLFileGenerator
{
    // Создаем отдельный статический класс для методов расширения
    public static class StringExtensions
    {
        /// <summary>
        /// Преобразует строку в PascalCase.
        /// Например: "user profile data" -> "UserProfileData"
        /// </summary>
        public static string ToPascalCase(this string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            // Разбиваем строку по пробелам, дефисам и подчёркиваниям
            var words = input.Split(new char[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();

            foreach (var word in words)
            {
                if (word.Length > 0)
                {
                    // Первую букву делаем заглавной, остальное – в нижнем регистре
                    sb.Append(char.ToUpper(word[0]));
                    if (word.Length > 1)
                    {
                        sb.Append(word.Substring(1).ToLower());
                    }
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Преобразует строку в camelCase.
        /// Например: "user profile data" -> "userProfileData"
        /// </summary>
        public static string ToCamelCase(this string input)
        {
            // Сначала получаем строку в PascalCase
            var pascal = input.ToPascalCase();
            if (string.IsNullOrEmpty(pascal))
                return pascal;

            // Первая буква в нижнем регистре
            return char.ToLower(pascal[0]) + pascal.Substring(1);
        }

        /// <summary>
        /// Преобразует строку в snake_case.
        /// Например: "User Profile Data" -> "user_profile_data"
        /// </summary>
        public static string ToSnakeCase(this string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            // Разбиваем строку по пробелам, дефисам и подчёркиваниям, приводим каждое слово к нижнему регистру
            var words = input.Split(new char[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
                             .Select(w => w.ToLower());
            return string.Join("_", words);
        }
    }

    //TODO настроить папку templates чтобы она не участвовала в build action и не обрабатывалась дебагером и переносилась в output папку 
    /// <summary>
    /// Класс для генерации файлов на основе шаблонов Scriban.
    /// Позволяет создавать множество файлов для каждой таблицы из базы данных,
    /// заменяя плейсхолдеры в шаблонах значениями из объектов TableSchema.
    /// </summary>
    public class FileGenerator
    {
        /// <summary>
        /// Генерирует файлы для каждой таблицы базы данных на основе шаблонов.
        /// </summary>
        /// <param name="tables">Список таблиц, для которых будут сгенерированы файлы</param>
        /// <param name="templatesDir">Путь к директории с шаблонами</param>
        /// <param name="resultDir">Путь к директории для сохранения результатов</param>
        public static void GenerateOtherFiles(List<TableSchema> tables, string templatesDir, string resultDir)
        {
            foreach (var table in tables)
            {
                ProcessTemplatesDirectory(templatesDir, resultDir, table);
            }
        }

        /// <summary>
        /// Преобразует тип SQL в соответствующий тип C#.
        /// </summary>
        /// <param name="sqlType">Тип данных SQL</param>
        /// <returns>Строка, представляющая соответствующий тип C#</returns>
        public static string MapType(string sqlType)
        {
            return sqlType switch
            {
                "int" => "int",
                "varchar" => "string",
                "date" => "DateTime",
                _ => "object" // По умолчанию
            };
        }

        /// <summary>
        /// Обрабатывает директорию с шаблонами для конкретной таблицы.
        /// Сканирует все файлы в директории, применяет к ним шаблонизацию
        /// и сохраняет результаты в указанную директорию.
        /// Поддерживает плейсхолдер $table$ как в именах файлов, так и в именах директорий.
        /// </summary>
        /// <param name="templatesDir">Путь к директории с шаблонами</param>
        /// <param name="resultDir">Путь к директории для сохранения результатов</param>
        /// <param name="table">Информация о таблице для шаблонизации</param>
        public static void ProcessTemplatesDirectory(string templatesDir, string resultDir, TableSchema table)
        {
            // Получаем все файлы (независимо от расширения)
            var templateFiles = Directory.GetFiles(templatesDir, "*", SearchOption.AllDirectories);

            foreach (var templateFile in templateFiles)
            {
                // Относительный путь к файлу (чтобы сохранить структуру директорий)
                var relativePath = templateFile.Substring(templatesDir.Length + 1);

                // Получаем директорию шаблона
                var directory = Path.GetDirectoryName(relativePath);

                // Заменяем плейсхолдер $table$ в пути директории на имя сущности
                if (!string.IsNullOrEmpty(directory))
                {
                    directory = directory.Replace("$table$", table.EntityName);
                }

                // Формируем имя выходного файла
                string fileName = Path.GetFileName(relativePath);

                // Проверяем, является ли файл шаблоном Scriban
                bool isTemplate = fileName.EndsWith(".sbn");

                if (isTemplate)
                {
                    // Удаляем расширение .sbn
                    fileName = fileName.Substring(0, fileName.Length - 4);
                }

                // Заменяем плейсхолдер $table$ на имя сущности в имени файла
                fileName = fileName.Replace("$table$", table.EntityName);

                // Составляем полный путь к результату
                string resultPath = !string.IsNullOrEmpty(directory)
                    ? Path.Combine(resultDir, directory, fileName)
                    : Path.Combine(resultDir, fileName);

                // Создание папки, если ее нет
                var resultDirectory = Path.GetDirectoryName(resultPath);
                if (!string.IsNullOrEmpty(resultDirectory))
                {
                    Directory.CreateDirectory(resultDirectory);
                }

                // Если это не шаблон .sbn, просто копируем файл
                if (!isTemplate)
                {
                    File.Copy(templateFile, resultPath, overwrite: true);
                    Console.WriteLine($"Copied {resultPath}");
                    continue;
                }

                // Читаем содержимое шаблона
                var templateContent = File.ReadAllText(templateFile);
                var template = Template.Parse(templateContent);

                if (template.HasErrors)
                {
                    Console.WriteLine($"Error in template: {templateFile}");
                    Console.WriteLine("Template Errors:");
                    foreach (var message in template.Messages)
                    {
                        Console.WriteLine($"  - {message}");
                    }
                    continue;
                }

                // Формируем данные для шаблона
                var scriptObject = new ScriptObject();
                scriptObject["entity_name"] = table.EntityName;
                scriptObject["table_name"] = table.TableName;
                scriptObject["columns"] = table.Columns.Select(c => new Dictionary<string, object>
                {
                    ["Name"] = c.Name,
                    ["CSharpType"] = c.CSharpType,
                    ["IsPrimaryKey"] = c.IsPrimaryKey,
                    ["IsForeignKey"] = c.IsForeignKey,
                    ["IsNullable"] = c.IsNullable
                }).ToArray();

                // Отладочный вывод
                Console.WriteLine($"Processing: Table {table.EntityName} with {table.Columns.Count} columns");

                scriptObject["foreign_keys"] = table.ForeignKeys.Select(fk => new Dictionary<string, object>
                {
                    ["column_name"] = fk.ColumnName,
                    ["csharp_type"] = fk.CSharpType,
                    ["references_table"] = fk.ReferencesTable,
                    ["references_column"] = fk.ReferencesColumn
                }).ToArray();

                // Добавляем информацию о первичном ключе
                var primaryKeyColumn = table.Columns.FirstOrDefault(c => c.IsPrimaryKey);
                if (primaryKeyColumn != null)
                {
                    scriptObject["primary_key"] = new Dictionary<string, object>
                    {
                        ["Name"] = primaryKeyColumn.Name,
                        ["CSharpType"] = primaryKeyColumn.CSharpType
                    };
                }
                else
                {
                    scriptObject["primary_key"] = null;
                }

                // Импортируем вспомогательные функции
                scriptObject.Import("map_type", new Func<string, string>(MapType));
                scriptObject.Import("to_pascal_case", new Func<string, string>(StringExtensions.ToPascalCase));
                scriptObject.Import("to_camel_case", new Func<string, string>(StringExtensions.ToCamelCase));
                scriptObject.Import("to_snake_case", new Func<string, string>(StringExtensions.ToSnakeCase));

                var context = new TemplateContext();
                context.PushGlobal(scriptObject);

                try
                {
                    // Генерируем код
                    var renderedTemplate = template.Render(context);
                    File.WriteAllText(resultPath, renderedTemplate);
                    Console.WriteLine($"Generated: {resultPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error rendering template {templateFile}: {ex.Message}");
                }
            }
        }
    }
}