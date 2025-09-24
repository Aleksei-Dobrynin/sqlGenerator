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

        /// <summary>
        /// Удаляет суффикс "_id" из строки
        /// Например: "user_id" -> "user", "type_id" -> "type"
        /// </summary>
        public static string RemoveIdSuffix(this string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            if (input.EndsWith("_id", StringComparison.OrdinalIgnoreCase))
            {
                return input.Substring(0, input.Length - 3);
            }

            return input;
        }
    }

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
        /// Создает словарь данных для колонки с поддержкой обоих стилей именования
        /// </summary>
        private static Dictionary<string, object> CreateColumnData(ColumnSchema column, bool isSystemColumn = false)
        {
            return new Dictionary<string, object>
            {
                // PascalCase версии для обратной совместимости с существующими шаблонами
                ["Name"] = column.Name,
                ["CSharpType"] = column.CSharpType,
                ["IsPrimaryKey"] = column.IsPrimaryKey,
                ["IsForeignKey"] = column.IsForeignKey,
                ["IsNullable"] = column.IsNullable,
                ["IsSystem"] = isSystemColumn,

                // snake_case версии для новых шаблонов
                ["name"] = column.Name,
                ["csharp_type"] = column.CSharpType,
                ["is_primary_key"] = column.IsPrimaryKey,
                ["is_foreign_key"] = column.IsForeignKey,
                ["is_nullable"] = column.IsNullable,
                ["is_system"] = isSystemColumn
            };
        }

        /// <summary>
        /// Создает словарь данных для внешнего ключа с поддержкой обоих стилей именования
        /// </summary>
        private static Dictionary<string, object> CreateForeignKeyData(ForeignKeyInfo fk)
        {
            return new Dictionary<string, object>
            {
                // snake_case версии
                ["column_name"] = fk.ColumnName,
                ["csharp_type"] = fk.CSharpType,
                ["references_table"] = fk.ReferencesTable,
                ["references_column"] = fk.ReferencesColumn,
                ["constraint_name"] = fk.ConstraintName
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

                // Определяем системные колонки
                var systemColumns = new HashSet<string> { "id", "created_at", "updated_at", "created_by", "updated_by" };

                // Все колонки с поддержкой ОБОИХ стилей именования
                scriptObject["columns"] = table.Columns.Select(c =>
                    CreateColumnData(c, systemColumns.Contains(c.Name.ToLower()))
                ).ToArray();

                // Добавляем отфильтрованные колонки для удобства использования в шаблонах
                scriptObject["editable_columns"] = table.Columns
                    .Where(c => !systemColumns.Contains(c.Name.ToLower()))
                    .Select(c => CreateColumnData(c, false))
                    .ToArray();

                // Отладочный вывод
                Console.WriteLine($"Processing: Table {table.EntityName} with {table.Columns.Count} columns");

                // Foreign keys с поддержкой snake_case
                scriptObject["foreign_keys"] = table.ForeignKeys.Select(fk =>
                    CreateForeignKeyData(fk)
                ).ToArray();

                // Primary key с поддержкой ОБОИХ стилей именования
                var primaryKeyColumn = table.Columns.FirstOrDefault(c => c.IsPrimaryKey);
                if (primaryKeyColumn != null)
                {
                    scriptObject["primary_key"] = new Dictionary<string, object>
                    {
                        // PascalCase версии
                        ["Name"] = primaryKeyColumn.Name,
                        ["CSharpType"] = primaryKeyColumn.CSharpType,

                        // snake_case версии
                        ["name"] = primaryKeyColumn.Name,
                        ["csharp_type"] = primaryKeyColumn.CSharpType
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
                scriptObject.Import("remove_id_suffix", new Func<string, string>(StringExtensions.RemoveIdSuffix));

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