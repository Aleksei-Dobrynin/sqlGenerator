using System.Text.RegularExpressions;

namespace SQLFileGenerator.LlmParser
{
    /// <summary>
    /// Разбивает большие SQL скрипты на части для порционной обработки LLM
    /// </summary>
    public class SqlChunker
    {
        private readonly int _maxTablesPerChunk;

        /// <summary>
        /// Regex для извлечения CREATE TABLE блоков
        /// </summary>
        private static readonly Regex CreateTableRegex = new(
            @"CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?(\w+)\s*\((.+?)\);",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        /// <summary>
        /// Regex для извлечения ALTER TABLE ADD CONSTRAINT FOREIGN KEY
        /// </summary>
        private static readonly Regex AlterTableFkRegex = new(
            @"ALTER\s+TABLE\s+(\w+)\s+ADD\s+CONSTRAINT\s+(\w+)\s+FOREIGN\s+KEY\s*\((\w+)\)\s+REFERENCES\s+(\w+)(?:\s*\((\w+)\))?[^;]*;",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        public SqlChunker(int maxTablesPerChunk = 5)
        {
            _maxTablesPerChunk = maxTablesPerChunk > 0 ? maxTablesPerChunk : 5;
        }

        /// <summary>
        /// Разбивает SQL скрипт на chunks
        /// </summary>
        /// <param name="sqlScript">Полный SQL скрипт</param>
        /// <returns>Список chunks для обработки</returns>
        public List<SqlChunk> SplitIntoChunks(string sqlScript)
        {
            if (string.IsNullOrWhiteSpace(sqlScript))
            {
                return new List<SqlChunk>();
            }

            // 1. Извлекаем все CREATE TABLE блоки
            var tableBlocks = ExtractTableBlocks(sqlScript);

            if (tableBlocks.Count == 0)
            {
                Console.WriteLine("Warning: No CREATE TABLE statements found in SQL script");
                return new List<SqlChunk>();
            }

            // 2. Извлекаем все ALTER TABLE FK и связываем с таблицами
            var alterTableStatements = ExtractAlterTableStatements(sqlScript);
            AssociateAlterTableWithTables(tableBlocks, alterTableStatements);

            // 3. Разбиваем на chunks
            var chunks = CreateChunks(tableBlocks);

            Console.WriteLine($"SQL script split into {chunks.Count} chunk(s) " +
                            $"({tableBlocks.Count} tables, max {_maxTablesPerChunk} per chunk)");

            return chunks;
        }

        /// <summary>
        /// Извлекает блоки CREATE TABLE из SQL
        /// </summary>
        private List<TableBlock> ExtractTableBlocks(string sqlScript)
        {
            var blocks = new List<TableBlock>();
            var matches = CreateTableRegex.Matches(sqlScript);

            foreach (Match match in matches)
            {
                var tableName = match.Groups[1].Value;
                var fullStatement = match.Value;

                blocks.Add(new TableBlock
                {
                    TableName = tableName,
                    CreateTableSql = fullStatement,
                    AlterTableStatements = new List<string>()
                });
            }

            return blocks;
        }

        /// <summary>
        /// Извлекает ALTER TABLE FOREIGN KEY statements
        /// </summary>
        private List<AlterTableStatement> ExtractAlterTableStatements(string sqlScript)
        {
            var statements = new List<AlterTableStatement>();
            var matches = AlterTableFkRegex.Matches(sqlScript);

            foreach (Match match in matches)
            {
                statements.Add(new AlterTableStatement
                {
                    TableName = match.Groups[1].Value,
                    FullStatement = match.Value
                });
            }

            return statements;
        }

        /// <summary>
        /// Связывает ALTER TABLE statements с соответствующими таблицами
        /// </summary>
        private void AssociateAlterTableWithTables(
            List<TableBlock> tableBlocks,
            List<AlterTableStatement> alterStatements)
        {
            var tableDict = tableBlocks.ToDictionary(
                t => t.TableName,
                t => t,
                StringComparer.OrdinalIgnoreCase);

            foreach (var alter in alterStatements)
            {
                if (tableDict.TryGetValue(alter.TableName, out var table))
                {
                    table.AlterTableStatements.Add(alter.FullStatement);
                }
            }
        }

        /// <summary>
        /// Создает chunks из блоков таблиц
        /// </summary>
        private List<SqlChunk> CreateChunks(List<TableBlock> tableBlocks)
        {
            var chunks = new List<SqlChunk>();

            for (int i = 0; i < tableBlocks.Count; i += _maxTablesPerChunk)
            {
                var chunkBlocks = tableBlocks.Skip(i).Take(_maxTablesPerChunk).ToList();

                var sqlParts = new List<string>();
                var tableNames = new List<string>();

                foreach (var block in chunkBlocks)
                {
                    sqlParts.Add(block.CreateTableSql);
                    tableNames.Add(block.TableName);

                    // Добавляем связанные ALTER TABLE
                    if (block.AlterTableStatements.Any())
                    {
                        sqlParts.AddRange(block.AlterTableStatements);
                    }
                }

                chunks.Add(new SqlChunk
                {
                    Index = chunks.Count,
                    TableNames = tableNames,
                    SqlContent = string.Join("\n\n", sqlParts),
                    TotalTablesInScript = tableBlocks.Count
                });
            }

            return chunks;
        }

        /// <summary>
        /// Внутренний класс для хранения блока таблицы
        /// </summary>
        private class TableBlock
        {
            public string TableName { get; set; } = "";
            public string CreateTableSql { get; set; } = "";
            public List<string> AlterTableStatements { get; set; } = new();

            public string FullSql =>
                string.Join("\n\n", new[] { CreateTableSql }.Concat(AlterTableStatements));
        }

        /// <summary>
        /// Внутренний класс для ALTER TABLE statement
        /// </summary>
        private class AlterTableStatement
        {
            public string TableName { get; set; } = "";
            public string FullStatement { get; set; } = "";
        }
    }

    /// <summary>
    /// Chunk SQL для обработки LLM
    /// </summary>
    public class SqlChunk
    {
        /// <summary>
        /// Индекс chunk (0-based)
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Имена таблиц в этом chunk
        /// </summary>
        public List<string> TableNames { get; set; } = new();

        /// <summary>
        /// SQL содержимое chunk
        /// </summary>
        public string SqlContent { get; set; } = "";

        /// <summary>
        /// Общее количество таблиц в скрипте
        /// </summary>
        public int TotalTablesInScript { get; set; }

        /// <summary>
        /// Является ли это первым chunk
        /// </summary>
        public bool IsFirst => Index == 0;

        /// <summary>
        /// Количество таблиц в chunk
        /// </summary>
        public int TableCount => TableNames.Count;
    }
}
