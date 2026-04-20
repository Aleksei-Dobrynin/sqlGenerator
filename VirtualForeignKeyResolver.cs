using System;
using System.Collections.Generic;
using System.Linq;

namespace SQLFileGenerator
{
    /// <summary>
    /// Пост-процессор для вывода виртуальных внешних ключей из конвенций именования колонок.
    /// Работает над результатом любого парсера (regex, LLM, агент).
    /// Распознаёт паттерны: *_id (суффикс), id_* (префикс), id* (слитный префикс).
    /// </summary>
    public static class VirtualForeignKeyResolver
    {
        /// <summary>
        /// Анализирует все таблицы и заполняет VirtualForeignKeys для колонок,
        /// которые подразумевают связь по именованию, но не имеют явного REFERENCES.
        /// </summary>
        public static void ResolveVirtualForeignKeys(List<TableSchema> tables)
        {
            // Построить lookup-словари по именам таблиц
            var fullNameLookup = new Dictionary<string, TableSchema>(StringComparer.OrdinalIgnoreCase);
            var bareNameLookup = new Dictionary<string, TableSchema>(StringComparer.OrdinalIgnoreCase);
            var bareNameAmbiguous = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var table in tables)
            {
                if (string.IsNullOrEmpty(table.TableName))
                    continue;

                fullNameLookup[table.TableName] = table;

                var bareName = GetBareName(table.TableName);
                if (bareNameLookup.ContainsKey(bareName))
                {
                    bareNameAmbiguous.Add(bareName);
                }
                else
                {
                    bareNameLookup[bareName] = table;
                }
            }

            // Удалить неоднозначные bare-имена
            foreach (var ambiguous in bareNameAmbiguous)
            {
                bareNameLookup.Remove(ambiguous);
            }

            // Для каждой таблицы — вывести виртуальные FK
            foreach (var table in tables)
            {
                ResolveForTable(table, bareNameLookup, fullNameLookup);
            }
        }

        private static void ResolveForTable(
            TableSchema table,
            Dictionary<string, TableSchema> bareNameLookup,
            Dictionary<string, TableSchema> fullNameLookup)
        {
            // Собрать множество колонок, уже являющихся явными FK
            var explicitFkColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var fk in table.ForeignKeys)
            {
                explicitFkColumns.Add(fk.ColumnName);
            }

            foreach (var col in table.Columns)
            {
                if (col.IsForeignKey)
                {
                    explicitFkColumns.Add(col.Name);
                }
            }

            foreach (var column in table.Columns)
            {
                if (column.IsPrimaryKey)
                    continue;

                if (explicitFkColumns.Contains(column.Name))
                    continue;

                var candidate = ExtractCandidate(column.Name);
                if (candidate == null)
                    continue;

                var matchedTable = TryMatchTable(candidate, bareNameLookup, fullNameLookup);
                if (matchedTable == null)
                    continue;

                table.VirtualForeignKeys.Add(new ForeignKeyInfo
                {
                    ColumnName = column.Name,
                    CSharpType = column.CSharpType,
                    ReferencesTable = matchedTable.TableName,
                    ReferencesColumn = "id",
                    ConstraintName = null
                });
            }
        }

        /// <summary>
        /// Извлекает имя-кандидат таблицы из имени колонки по трём паттернам:
        /// *_id (суффикс), id_* (префикс с разделителем), id* (слитный префикс)
        /// </summary>
        private static string? ExtractCandidate(string columnName)
        {
            var lower = columnName.ToLowerInvariant();

            // Паттерн 1: суффикс *_id (наиболее распространённый)
            if (lower.EndsWith("_id") && lower.Length > 3)
            {
                return lower.Substring(0, lower.Length - 3);
            }

            // Паттерн 2: префикс id_*
            if (lower.StartsWith("id_") && lower.Length > 3)
            {
                return lower.Substring(3);
            }

            // Паттерн 3: слитный префикс id* (без разделителя)
            if (lower.StartsWith("id") && lower.Length > 2)
            {
                return lower.Substring(2);
            }

            return null;
        }

        /// <summary>
        /// Пытается найти таблицу по кандидату с учётом плюрализации.
        /// Порядок: точное → +s → +es → y→ies
        /// </summary>
        private static TableSchema? TryMatchTable(
            string candidate,
            Dictionary<string, TableSchema> bareNameLookup,
            Dictionary<string, TableSchema> fullNameLookup)
        {
            // 1. Точное совпадение
            var result = TryMatch(candidate, bareNameLookup, fullNameLookup);
            if (result != null) return result;

            // 2. Простое множественное (+s)
            result = TryMatch(candidate + "s", bareNameLookup, fullNameLookup);
            if (result != null) return result;

            // 3. es-множественное (+es)
            result = TryMatch(candidate + "es", bareNameLookup, fullNameLookup);
            if (result != null) return result;

            // 4. ies-множественное (y → ies)
            if (candidate.EndsWith("y") && candidate.Length > 1)
            {
                result = TryMatch(candidate.Substring(0, candidate.Length - 1) + "ies", bareNameLookup, fullNameLookup);
                if (result != null) return result;
            }

            return null;
        }

        private static TableSchema? TryMatch(
            string name,
            Dictionary<string, TableSchema> bareNameLookup,
            Dictionary<string, TableSchema> fullNameLookup)
        {
            if (bareNameLookup.TryGetValue(name, out var result)) return result;
            if (fullNameLookup.TryGetValue(name, out result)) return result;
            return null;
        }

        /// <summary>
        /// Извлекает "голое" имя таблицы (после последней точки в schema-qualified имени)
        /// </summary>
        private static string GetBareName(string tableName)
        {
            var dotIndex = tableName.LastIndexOf('.');
            return dotIndex >= 0 && dotIndex < tableName.Length - 1
                ? tableName.Substring(dotIndex + 1)
                : tableName;
        }
    }
}
