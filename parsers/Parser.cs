using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using SQLFileGenerator.structures;

namespace SQLFileGenerator.parsers
{
    /// <summary>
    /// Класс для анализа SQL-скриптов и преобразования их в структуры данных.
    /// Позволяет извлекать информацию о таблицах, столбцах, первичных и внешних ключах.
    /// </summary>
    public class SqlParser
    {
        /// <summary>
        /// Анализирует SQL-скрипт создания таблиц PostgreSQL и преобразует его в список объектов TableSchema.
        /// Извлекает имена таблиц, информацию о столбцах, типах данных, первичных и внешних ключах.
        /// </summary>
        /// <param name="sqlScript">SQL-скрипт с командами CREATE TABLE для PostgreSQL</param>
        /// <returns>Список объектов TableSchema, представляющих структуру таблиц базы данных</returns>
        public static List<TableSchema> ParsePostgresCreateTableScript(string sqlScript)
        {
            var tables = new List<TableSchema>();

            // Регулярное выражение для поиска CREATE TABLE блоков
            var tableRegex = new Regex(@"CREATE\s+TABLE\s+(\w+)\s*\((.+?)\);", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // Регулярное выражение для ALTER TABLE FK
            var alterTableFkRegex = new Regex(@"ALTER\s+TABLE\s+(\w+)\s+ADD\s+CONSTRAINT\s+(\w+)\s+FOREIGN\s+KEY\s+\((\w+)\)\s+REFERENCES\s+(\w+)(?:\s*\((\w+)\))?;",
                                           RegexOptions.IgnoreCase | RegexOptions.Singleline);

            var tableDict = new Dictionary<string, TableSchema>(StringComparer.OrdinalIgnoreCase);

            var matches = tableRegex.Matches(sqlScript);

            foreach (Match match in matches)
            {
                var tableName = match.Groups[1].Value;
                var columnsDefinition = match.Groups[2].Value;

                Console.WriteLine($"Parsing table: {tableName}"); // Debug

                var tableSchema = new TableSchema
                {
                    TableName = tableName,
                    EntityName = ToPascalCase(tableName)
                };

                // Парсим столбцы более надежным способом
                ParseColumns(columnsDefinition, tableSchema);

                tables.Add(tableSchema);
                tableDict[tableName] = tableSchema;

                Console.WriteLine($"Parsed {tableSchema.Columns.Count} columns for {tableName}"); // Debug
            }

            // Обрабатываем ALTER TABLE для FK
            ProcessAlterTableForeignKeys(sqlScript, tableDict, alterTableFkRegex);

            return tables;
        }

        private static void ParseColumns(string columnsDefinition, TableSchema tableSchema)
        {
            // Нормализуем текст - убираем лишние пробелы и переносы
            var normalizedText = Regex.Replace(columnsDefinition, @"\s+", " ").Trim();

            // Разбиваем по запятым, учитывая скобки
            var columnParts = SplitColumnsDefinition(normalizedText);

            foreach (var columnPart in columnParts)
            {
                var trimmedPart = columnPart.Trim();

                // Пропускаем CONSTRAINT определения
                if (trimmedPart.StartsWith("CONSTRAINT", StringComparison.OrdinalIgnoreCase))
                    continue;

                var column = ParseSingleColumn(trimmedPart);
                if (column != null)
                {
                    tableSchema.Columns.Add(column);
                    Console.WriteLine($"  Parsed column: {column.Name} ({column.CSharpType})"); // Debug
                }
            }
        }

        private static List<string> SplitColumnsDefinition(string definition)
        {
            var parts = new List<string>();
            var currentPart = new StringBuilder();
            var parenthesesLevel = 0;
            var inQuotes = false;
            var quoteChar = '\0';

            for (int i = 0; i < definition.Length; i++)
            {
                var ch = definition[i];

                if ((ch == '\'' || ch == '"') && !inQuotes)
                {
                    inQuotes = true;
                    quoteChar = ch;
                }
                else if (ch == quoteChar && inQuotes)
                {
                    inQuotes = false;
                    quoteChar = '\0';
                }
                else if (!inQuotes)
                {
                    if (ch == '(') parenthesesLevel++;
                    else if (ch == ')') parenthesesLevel--;
                    else if (ch == ',' && parenthesesLevel == 0)
                    {
                        parts.Add(currentPart.ToString().Trim());
                        currentPart.Clear();
                        continue;
                    }
                }

                currentPart.Append(ch);
            }

            if (currentPart.Length > 0)
            {
                parts.Add(currentPart.ToString().Trim());
            }

            return parts;
        }

        private static ColumnSchema? ParseSingleColumn(string columnDefinition)
        {
            // Удаляем лишние пробелы
            var parts = Regex.Split(columnDefinition.Trim(), @"\s+");

            if (parts.Length < 2) return null;

            var columnName = parts[0].Trim('"');
            var dataType = parts[1];

            // Проверяем атрибуты
            var upperDefinition = columnDefinition.ToUpper();
            var isNullable = !upperDefinition.Contains("NOT NULL");
            var isPrimaryKey = upperDefinition.Contains("PRIMARY KEY");

            // Обрабатываем REFERENCES
            var referencesMatch = Regex.Match(columnDefinition, @"REFERENCES\s+(\w+)(?:\s*\((\w+)\))?", RegexOptions.IgnoreCase);
            var isForeignKey = referencesMatch.Success;

            var csharpType = MapPostgresToCSharpType(dataType);

            return new ColumnSchema
            {
                Name = columnName,
                CSharpType = csharpType,
                IsPrimaryKey = isPrimaryKey,
                IsForeignKey = isForeignKey,
                IsNullable = isNullable
            };
        }

        private static void ProcessAlterTableForeignKeys(string sqlScript, Dictionary<string, TableSchema> tableDict, Regex alterTableFkRegex)
        {
            var alterTableMatches = alterTableFkRegex.Matches(sqlScript);
            foreach (Match alterMatch in alterTableMatches)
            {
                var tableName = alterMatch.Groups[1].Value;
                var constraintName = alterMatch.Groups[2].Value;
                var columnName = alterMatch.Groups[3].Value;
                var referencedTable = alterMatch.Groups[4].Value;
                var referencedColumn = alterMatch.Groups[5].Success ? alterMatch.Groups[5].Value : "id";

                if (tableDict.TryGetValue(tableName, out var tableSchema))
                {
                    var column = tableSchema.Columns.FirstOrDefault(c =>
                        c.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));

                    if (column != null)
                    {
                        column.IsForeignKey = true;

                        var existingFk = tableSchema.ForeignKeys.FirstOrDefault(fk =>
                            fk.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase));

                        if (existingFk == null)
                        {
                            tableSchema.ForeignKeys.Add(new ForeignKeyInfo
                            {
                                ColumnName = columnName,
                                CSharpType = column.CSharpType,
                                ReferencesTable = referencedTable,
                                ReferencesColumn = referencedColumn,
                                ConstraintName = constraintName
                            });
                        }
                    }
                }
            }
        }
        /// <summary>
        /// Преобразует тип данных PostgreSQL в соответствующий тип C#.
        /// </summary>
        /// <param name="postgresType">Тип данных PostgreSQL (например, integer, text, timestamp)</param>
        /// <returns>Строка, представляющая соответствующий тип C#</returns>
        private static string MapPostgresToCSharpType(string postgresType)
        {
            return postgresType.ToLower() switch
            {
                "integer" => "int",
                "serial" => "int",
                "bigint" => "long",
                "bigserial" => "long",
                "text" => "string",
                "varchar" or "character varying" => "string",
                "boolean" => "bool",
                "timestamp" or "timestamp without time zone" => "DateTime",
                "date" => "DateTime",
                "real" => "float",
                "double precision" => "double",
                "numeric" or "decimal" => "decimal",
                _ => "object" // Default type for unmapped types
            };
        }

        /// <summary>
        /// Преобразует строку из snake_case в PascalCase.
        /// Например: "user_profile" -> "UserProfile"
        /// </summary>
        /// <param name="input">Строка в формате snake_case</param>
        /// <returns>Строка в формате PascalCase</returns>
        private static string ToPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var words = input.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(word => char.ToUpper(word[0]) + word.Substring(1).ToLower());
            return string.Concat(words);
        }
    }
}