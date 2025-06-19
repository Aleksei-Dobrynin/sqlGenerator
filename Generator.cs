using Scriban.Runtime;
using Scriban;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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

    //TODO настроить папку templates чтобы ноа не все не учавстовавал в build action и не обрабатывалась дебагером и прернасилась в output папку 
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

                // Формируем имя выходного файла
                string fileName = Path.GetFileName(relativePath);
                if (fileName.EndsWith(".sbn"))
                {
                    // Удаляем расширение .sbn
                    fileName = fileName.Substring(0, fileName.Length - 4);

                    // Заменяем плейсхолдер $table$ на имя сущности
                    fileName = fileName.Replace("$table$", table.EntityName);
                }

                // Составляем полный путь к результату
                string resultPath = directory != null
                    ? Path.Combine(resultDir, directory, fileName)
                    : Path.Combine(resultDir, fileName);

                // Создание папки, если ее нет
                var resultDirectory = Path.GetDirectoryName(resultPath);
                Directory.CreateDirectory(resultDirectory);

                // Читаем содержимое шаблона
                var templateContent = File.ReadAllText(templateFile);
                var template = Template.Parse(templateContent);
                if (template.HasErrors)
                {
                    Console.WriteLine(templateFile);
                    Console.WriteLine("Template Errors:");
                    foreach (var message in template.Messages)
                    {
                        Console.WriteLine(message);
                    }
                    //throw new InvalidOperationException("Template has errors.");
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
                //Проверка колонок
                Console.WriteLine($"Debug: Table {table.EntityName} has {table.Columns.Count} columns");
                foreach (var col in table.Columns)
                {
                    Console.WriteLine($"  Column: {col.Name} - {col.CSharpType}");
                }

                scriptObject["foreign_keys"] = table.ForeignKeys.Select(fk => new Dictionary<string, object>
                {
                    ["column_name"] = fk.ColumnName,
                    ["csharp_type"] = fk.CSharpType,
                    ["references_table"] = fk.ReferencesTable,
                    ["references_column"] = fk.ReferencesColumn
                }).ToArray();
                scriptObject["primary_key"] = table.Columns.FirstOrDefault(c => c.IsPrimaryKey);
                scriptObject.Import("map_type", new Func<string, string>(MapType));

                // Добавляем функцию для преобразования в PascalCase
                scriptObject.Import("to_pascal_case", new Func<string, string>(StringExtensions.ToPascalCase));
                scriptObject.Import("to_camel_case", new Func<string, string>(StringExtensions.ToCamelCase));
                scriptObject.Import("to_snake_case", new Func<string, string>(StringExtensions.ToSnakeCase));


                var context = new TemplateContext();
                context.PushGlobal(scriptObject);

                // Генерируем код
                var renderedTemplate = template.Render(context);
                File.WriteAllText(resultPath, renderedTemplate);
                Console.WriteLine($"Generated {resultPath}");
            }
        }
    }
}
