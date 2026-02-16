using Newtonsoft.Json;

namespace SQLFileGenerator.LlmParser
{
    /// <summary>
    /// Валидатор ответов LLM для проверки корректности JSON и структуры данных
    /// </summary>
    public class LlmResponseValidator
    {
        /// <summary>
        /// Допустимые C# типы для колонок
        /// </summary>
        private static readonly HashSet<string> ValidCSharpTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "int", "long", "short", "byte",
            "string", "bool", "DateTime", "TimeSpan",
            "float", "double", "decimal",
            "Guid", "byte[]", "object",
            // Nullable версии
            "int?", "long?", "short?", "byte?",
            "bool?", "DateTime?", "TimeSpan?",
            "float?", "double?", "decimal?", "Guid?"
        };

        /// <summary>
        /// Валидирует JSON ответ от LLM
        /// </summary>
        /// <param name="jsonResponse">JSON строка от LLM</param>
        /// <param name="expectedTableNames">Ожидаемые имена таблиц (опционально)</param>
        /// <returns>Результат валидации</returns>
        public ValidationResult Validate(string jsonResponse, IEnumerable<string>? expectedTableNames = null)
        {
            var result = new ValidationResult();

            if (string.IsNullOrWhiteSpace(jsonResponse))
            {
                result.AddError("Empty response from LLM");
                return result;
            }

            try
            {
                // 1. Проверка валидности JSON
                var tables = JsonConvert.DeserializeObject<List<TableSchema>>(jsonResponse);

                if (tables == null)
                {
                    result.AddError("Failed to deserialize JSON response to List<TableSchema>");
                    return result;
                }

                if (tables.Count == 0)
                {
                    result.AddWarning("Response contains empty array (no tables)");
                }

                // 2. Проверка наличия ожидаемых таблиц
                if (expectedTableNames != null)
                {
                    var parsedTableNames = tables.Select(t => t.TableName?.ToLowerInvariant()).ToHashSet();
                    foreach (var expected in expectedTableNames)
                    {
                        if (!parsedTableNames.Contains(expected.ToLowerInvariant()))
                        {
                            result.AddWarning($"Expected table '{expected}' not found in response");
                            result.MissingTables.Add(expected);
                        }
                    }
                }

                // 3. Проверка каждой таблицы
                foreach (var table in tables)
                {
                    ValidateTable(table, result);
                }

                // 4. Проверка FK ссылок между таблицами
                ValidateForeignKeyReferences(tables, result);

                result.ParsedTables = tables;
            }
            catch (JsonReaderException ex)
            {
                result.AddError($"JSON parsing error at position {ex.LinePosition}: {ex.Message}");
            }
            catch (JsonSerializationException ex)
            {
                result.AddError($"JSON serialization error: {ex.Message}");
            }
            catch (Exception ex)
            {
                result.AddError($"Unexpected error: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Валидирует отдельную таблицу
        /// </summary>
        private void ValidateTable(TableSchema table, ValidationResult result)
        {
            var tableId = table.TableName ?? "(unknown)";

            // Обязательные поля
            if (string.IsNullOrWhiteSpace(table.TableName))
            {
                result.AddError($"Table has empty or null TableName");
                return;
            }

            if (string.IsNullOrWhiteSpace(table.EntityName))
            {
                result.AddError($"Table '{tableId}' has empty or null EntityName");
            }

            // Проверка формата EntityName (должен быть PascalCase)
            if (!string.IsNullOrEmpty(table.EntityName) && !IsPascalCase(table.EntityName))
            {
                result.AddWarning($"Table '{tableId}': EntityName '{table.EntityName}' is not in PascalCase");
            }

            // Проверка колонок
            if (table.Columns == null || table.Columns.Count == 0)
            {
                result.AddWarning($"Table '{tableId}' has no columns");
            }
            else
            {
                var columnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var hasPrimaryKey = false;

                foreach (var column in table.Columns)
                {
                    ValidateColumn(column, tableId, result);

                    // Проверка дубликатов
                    if (!string.IsNullOrEmpty(column.Name))
                    {
                        if (!columnNames.Add(column.Name))
                        {
                            result.AddError($"Table '{tableId}': duplicate column name '{column.Name}'");
                        }
                    }

                    if (column.IsPrimaryKey)
                    {
                        hasPrimaryKey = true;
                    }
                }

                if (!hasPrimaryKey)
                {
                    result.AddWarning($"Table '{tableId}' has no primary key column");
                }
            }

            // Проверка FK
            if (table.ForeignKeys != null)
            {
                foreach (var fk in table.ForeignKeys)
                {
                    ValidateForeignKey(fk, table, result);
                }
            }
        }

        /// <summary>
        /// Валидирует отдельную колонку
        /// </summary>
        private void ValidateColumn(ColumnSchema column, string tableName, ValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(column.Name))
            {
                result.AddError($"Table '{tableName}': column has empty or null Name");
                return;
            }

            if (string.IsNullOrWhiteSpace(column.CSharpType))
            {
                result.AddError($"Table '{tableName}': column '{column.Name}' has empty CSharpType");
            }
            else if (!ValidCSharpTypes.Contains(column.CSharpType))
            {
                result.AddWarning($"Table '{tableName}': column '{column.Name}' has non-standard type '{column.CSharpType}'");
            }

            // Primary key не должен быть nullable
            if (column.IsPrimaryKey && column.IsNullable)
            {
                result.AddWarning($"Table '{tableName}': primary key column '{column.Name}' is marked as nullable");
            }
        }

        /// <summary>
        /// Валидирует внешний ключ
        /// </summary>
        private void ValidateForeignKey(ForeignKeyInfo fk, TableSchema table, ValidationResult result)
        {
            var tableId = table.TableName ?? "(unknown)";

            if (string.IsNullOrWhiteSpace(fk.ColumnName))
            {
                result.AddError($"Table '{tableId}': FK has empty ColumnName");
                return;
            }

            if (string.IsNullOrWhiteSpace(fk.ReferencesTable))
            {
                result.AddError($"Table '{tableId}': FK '{fk.ColumnName}' has empty ReferencesTable");
            }

            if (string.IsNullOrWhiteSpace(fk.ReferencesColumn))
            {
                result.AddWarning($"Table '{tableId}': FK '{fk.ColumnName}' has empty ReferencesColumn (defaulting to 'id')");
            }

            // Проверяем, что колонка FK существует в таблице
            if (table.Columns != null && !table.Columns.Any(c =>
                c.Name?.Equals(fk.ColumnName, StringComparison.OrdinalIgnoreCase) == true))
            {
                result.AddWarning($"Table '{tableId}': FK column '{fk.ColumnName}' not found in table columns");
            }

            // Проверяем флаг IsForeignKey на соответствующей колонке
            var fkColumn = table.Columns?.FirstOrDefault(c =>
                c.Name?.Equals(fk.ColumnName, StringComparison.OrdinalIgnoreCase) == true);
            if (fkColumn != null && !fkColumn.IsForeignKey)
            {
                result.AddWarning($"Table '{tableId}': column '{fk.ColumnName}' is in ForeignKeys but IsForeignKey=false");
            }
        }

        /// <summary>
        /// Проверяет FK ссылки между таблицами
        /// </summary>
        private void ValidateForeignKeyReferences(List<TableSchema> tables, ValidationResult result)
        {
            var tableNames = tables
                .Where(t => !string.IsNullOrEmpty(t.TableName))
                .Select(t => t.TableName!.ToLowerInvariant())
                .ToHashSet();

            foreach (var table in tables)
            {
                if (table.ForeignKeys == null) continue;

                foreach (var fk in table.ForeignKeys)
                {
                    if (!string.IsNullOrEmpty(fk.ReferencesTable) &&
                        !tableNames.Contains(fk.ReferencesTable.ToLowerInvariant()))
                    {
                        // Это не обязательно ошибка - таблица может быть в другом chunk
                        result.AddWarning($"Table '{table.TableName}': FK '{fk.ColumnName}' references table '{fk.ReferencesTable}' which is not in current result set");
                    }
                }
            }
        }

        /// <summary>
        /// Проверяет, является ли строка PascalCase
        /// </summary>
        private bool IsPascalCase(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            return char.IsUpper(value[0]) && !value.Contains('_') && !value.Contains('-');
        }
    }

    /// <summary>
    /// Результат валидации ответа LLM
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Критические ошибки (требуют retry)
        /// </summary>
        public List<string> Errors { get; } = new();

        /// <summary>
        /// Предупреждения (логируются, но не блокируют)
        /// </summary>
        public List<string> Warnings { get; } = new();

        /// <summary>
        /// Таблицы, которые ожидались, но не найдены
        /// </summary>
        public List<string> MissingTables { get; } = new();

        /// <summary>
        /// Распарсенные таблицы (если валидация прошла успешно)
        /// </summary>
        public List<TableSchema>? ParsedTables { get; set; }

        /// <summary>
        /// Валидация прошла без критических ошибок
        /// </summary>
        public bool IsValid => Errors.Count == 0;

        /// <summary>
        /// Есть пропущенные таблицы
        /// </summary>
        public bool HasMissingTables => MissingTables.Count > 0;

        public void AddError(string error) => Errors.Add(error);
        public void AddWarning(string warning) => Warnings.Add(warning);

        /// <summary>
        /// Формирует строку с описанием всех ошибок
        /// </summary>
        public string GetErrorSummary()
        {
            return string.Join("; ", Errors);
        }

        /// <summary>
        /// Выводит результат валидации в консоль
        /// </summary>
        public void PrintToConsole()
        {
            if (Errors.Any())
            {
                Console.WriteLine("Validation ERRORS:");
                foreach (var error in Errors)
                {
                    Console.WriteLine($"  [ERROR] {error}");
                }
            }

            if (Warnings.Any())
            {
                Console.WriteLine("Validation WARNINGS:");
                foreach (var warning in Warnings)
                {
                    Console.WriteLine($"  [WARN] {warning}");
                }
            }

            if (IsValid)
            {
                Console.WriteLine($"Validation passed. Parsed {ParsedTables?.Count ?? 0} table(s).");
            }
        }
    }
}
