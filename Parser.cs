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

            // Регулярное выражение для поиска CREATE TABLE блоков
            var tableRegex = new Regex(@"CREATE\s+TABLE\s+(\w+)\s*\(([^;]+)\);", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // Регулярное выражение для анализа столбцов внутри CREATE TABLE
            var columnRegex = new Regex(@"(\w+)\s+([\w()]+)(\s+NOT\s+NULL)?(\s+PRIMARY\s+KEY)?(\s+REFERENCES\s+(\w+)\s*\((\w+)\))?.*?(?:,|$)",
                                        RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // Регулярное выражение для анализа отдельных ALTER TABLE команд для ограничений внешнего ключа
            var alterTableFkRegex = new Regex(@"ALTER\s+TABLE\s+(\w+)\s+ADD\s+CONSTRAINT\s+(\w+)\s+FOREIGN\s+KEY\s+\((\w+)\)\s+REFERENCES\s+(\w+)(?:\s*\((\w+)\))?;",
                                           RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // Регулярное выражение для поиска ограничений PRIMARY KEY внутри CREATE TABLE
            var pkConstraintRegex = new Regex(@"CONSTRAINT\s+(\w+)\s+PRIMARY\s+KEY\s*\((\w+)\)",
                                           RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // Словарь для хранения схем таблиц по имени для последующего обновления
            var tableDict = new Dictionary<string, TableSchema>(StringComparer.OrdinalIgnoreCase);

            // Поиск всех CREATE TABLE блоков
            var matches = tableRegex.Matches(sqlScript);

            foreach (Match match in matches)
            {
                var tableName = match.Groups[1].Value;
                var columnsDefinition = match.Groups[2].Value;

                var tableSchema = new TableSchema
                {
                    TableName = tableName,
                    EntityName = ToPascalCase(tableName)
                };

                // Поиск ограничения PRIMARY KEY внутри CREATE TABLE
                var pkConstraintMatches = pkConstraintRegex.Matches(columnsDefinition);
                foreach (Match pkMatch in pkConstraintMatches)
                {
                    var pkColumnName = pkMatch.Groups[2].Value;
                    // Отмечаем, что эта колонка будет первичным ключом
                    // (Колонка будет создана позже в основном проходе)
                    foreach (var column in tableSchema.Columns)
                    {
                        if (column.Name.Equals(pkColumnName, StringComparison.OrdinalIgnoreCase))
                        {
                            column.IsPrimaryKey = true;
                            break;
                        }
                    }
                }

                // Анализ определений столбцов
                var columnMatches = columnRegex.Matches(columnsDefinition);
                foreach (Match columnMatch in columnMatches)
                {
                    var columnName = columnMatch.Groups[1].Value;

                    // Пропускаем строки, которые начинаются с CONSTRAINT
                    if (columnName.Equals("CONSTRAINT", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var dataType = columnMatch.Groups[2].Value;
                    var isNullable = !columnMatch.Groups[3].Success;
                    var isPrimaryKey = columnMatch.Groups[4].Success;
                    var isForeignKey = columnMatch.Groups[5].Success;

                    var csharpType = MapPostgresToCSharpType(dataType);

                    var column = new ColumnSchema
                    {
                        Name = columnName,
                        CSharpType = csharpType,
                        IsPrimaryKey = isPrimaryKey,
                        IsForeignKey = isForeignKey,
                        IsNullable = isNullable
                    };

                    tableSchema.Columns.Add(column);

                    if (isForeignKey)
                    {
                        var referencedTable = columnMatch.Groups[6].Value;
                        var referencedColumn = columnMatch.Groups[7].Value;

                        tableSchema.ForeignKeys.Add(new ForeignKeyInfo
                        {
                            ColumnName = columnName,
                            CSharpType = csharpType,
                            ReferencesTable = referencedTable,
                            ReferencesColumn = referencedColumn
                        });
                    }
                }

                tables.Add(tableSchema);
                tableDict[tableName] = tableSchema;
            }

            // Обработка ALTER TABLE команд для внешних ключей
            var alterTableMatches = alterTableFkRegex.Matches(sqlScript);
            foreach (Match alterMatch in alterTableMatches)
            {
                var tableName = alterMatch.Groups[1].Value;
                var constraintName = alterMatch.Groups[2].Value;
                var columnName = alterMatch.Groups[3].Value;
                var referencedTable = alterMatch.Groups[4].Value;
                var referencedColumn = alterMatch.Groups[5].Success ? alterMatch.Groups[5].Value : "id"; // Если не указан, обычно это id

                // Находим соответствующую таблицу
                if (tableDict.TryGetValue(tableName, out var tableSchema))
                {
                    // Находим соответствующую колонку
                    var column = tableSchema.Columns.FirstOrDefault(c =>
                        c.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));

                    if (column != null)
                    {
                        column.IsForeignKey = true;

                        // Проверяем, существует ли уже такой внешний ключ
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

            return tables;
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