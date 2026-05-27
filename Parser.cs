using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace SQLFileGenerator
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

            // Strip SQL comments before regex matching:
            // - line comments `-- ...` склеиваются с соседней колонкой при normalizeWhitespace и ломают парсинг
            // - block comments `/* ... */` мешают аналогично; убираем оба варианта
            sqlScript = Regex.Replace(sqlScript, @"/\*.*?\*/", "", RegexOptions.Singleline);
            sqlScript = Regex.Replace(sqlScript, @"--[^\r\n]*", "");

            // Регулярное выражение для поиска CREATE TABLE блоков
            var tableRegex = new Regex(@"CREATE\s+TABLE\s+([\w.]+)\s*\((.+?)\);", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // Регулярное выражение для ALTER TABLE FK
            var alterTableFkRegex = new Regex(@"ALTER\s+TABLE\s+([\w.]+)\s+ADD\s+CONSTRAINT\s+(\w+)\s+FOREIGN\s+KEY\s+\((\w+)\)\s+REFERENCES\s+([\w.]+)(?:\s*\((\w+)\))?;",
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

                var (column, foreignKey) = ParseSingleColumn(trimmedPart);
                if (column != null)
                {
                    tableSchema.Columns.Add(column);
                    if (foreignKey != null)
                        tableSchema.ForeignKeys.Add(foreignKey);
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

        private static (ColumnSchema? Column, ForeignKeyInfo? ForeignKey) ParseSingleColumn(string columnDefinition)
        {
            // Удаляем лишние пробелы
            var parts = Regex.Split(columnDefinition.Trim(), @"\s+");

            if (parts.Length < 2) return (null, null);

            var columnName = parts[0].Trim('"');
            var dataType = parts[1];

            // Склеиваем multi-word типы PostgreSQL: `timestamp with time zone`, `double precision`,
            // `character varying` и т.п. Иначе тип определяется только по первому слову и
            // `timestamp with time zone` ошибочно маппится в `DateTime` вместо `DateTimeOffset`.
            if (parts.Length >= 3)
            {
                var twoWord = (parts[1] + " " + parts[2]).ToLowerInvariant();
                var twoWordKnown = new HashSet<string>
                {
                    "double precision",
                    "character varying",
                    "bit varying",
                    "time without",
                    "time with",
                    "timestamp without",
                    "timestamp with"
                };
                if (twoWordKnown.Contains(twoWord))
                {
                    if (parts.Length >= 5 &&
                        (parts[2].Equals("without", StringComparison.OrdinalIgnoreCase) ||
                         parts[2].Equals("with", StringComparison.OrdinalIgnoreCase)) &&
                        parts[3].Equals("time", StringComparison.OrdinalIgnoreCase) &&
                        parts[4].Equals("zone", StringComparison.OrdinalIgnoreCase))
                    {
                        dataType = parts[1] + " " + parts[2] + " " + parts[3] + " " + parts[4];
                    }
                    else
                    {
                        dataType = parts[1] + " " + parts[2];
                    }
                }
            }

            // Проверяем атрибуты
            var upperDefinition = columnDefinition.ToUpper();
            var isNullable = !upperDefinition.Contains("NOT NULL");
            var isPrimaryKey = upperDefinition.Contains("PRIMARY KEY");

            // Обрабатываем REFERENCES
            var referencesMatch = Regex.Match(columnDefinition, @"REFERENCES\s+([\w.]+)(?:\s*\((\w+)\))?", RegexOptions.IgnoreCase);
            var isForeignKey = referencesMatch.Success;

            var csharpType = MapPostgresToCSharpType(dataType);

            var column = new ColumnSchema
            {
                Name = columnName,
                CSharpType = csharpType,
                IsPrimaryKey = isPrimaryKey,
                IsForeignKey = isForeignKey,
                IsNullable = isNullable
            };

            ForeignKeyInfo? foreignKey = null;
            if (isForeignKey)
            {
                var referencedTable = referencesMatch.Groups[1].Value;
                var referencedColumn = referencesMatch.Groups[2].Success ? referencesMatch.Groups[2].Value : "id";
                foreignKey = new ForeignKeyInfo
                {
                    ColumnName = columnName,
                    CSharpType = csharpType,
                    ReferencesTable = referencedTable,
                    ReferencesColumn = referencedColumn
                };
            }

            return (column, foreignKey);
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
            // Strip size/precision parameters: varchar(255) → varchar, numeric(10,2) → numeric
            var baseType = postgresType.Contains('(')
                ? postgresType[..postgresType.IndexOf('(')].Trim()
                : postgresType;

            return baseType.ToLower() switch
            {
                "integer" or "int" or "int4" => "int",
                "serial" => "int",
                "bigint" or "int8" => "long",
                "bigserial" => "long",
                "smallint" or "int2" => "short",
                "smallserial" => "short",
                "text" => "string",
                "varchar" or "character varying" or "char" or "character" => "string",
                "boolean" or "bool" => "bool",
                "timestamp" or "timestamp without time zone" => "DateTime",
                "timestamp with time zone" or "timestamptz" => "DateTimeOffset",
                "date" => "DateTime",
                "time" or "time without time zone" => "TimeSpan",
                "real" or "float4" => "float",
                "double precision" or "float8" => "double",
                "numeric" or "decimal" or "money" => "decimal",
                "uuid" => "Guid",
                "bytea" => "byte[]",
                "json" or "jsonb" => "string",
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

            // Strip schema prefix if present (e.g., "org.ref_legal_form" -> "ref_legal_form")
            var dotIndex = input.LastIndexOf('.');
            if (dotIndex >= 0 && dotIndex < input.Length - 1)
                input = input.Substring(dotIndex + 1);

            var words = input.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(word => char.ToUpper(word[0]) + word.Substring(1).ToLower());
            return string.Concat(words);
        }
    }
}