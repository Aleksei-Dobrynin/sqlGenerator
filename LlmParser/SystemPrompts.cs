namespace SQLFileGenerator.LlmParser
{
    /// <summary>
    /// Системные промпты для LLM парсера SQL
    /// </summary>
    public static class SystemPrompts
    {
        /// <summary>
        /// Основной системный промпт для парсинга SQL
        /// </summary>
        public const string SqlParsingSystemPrompt = @"You are a SQL schema parser. Your task is to analyze PostgreSQL CREATE TABLE statements and extract structured information.

IMPORTANT RULES:
1. Output ONLY valid JSON array, no explanations, no markdown code blocks, no additional text
2. Parse ONLY explicit foreign key relationships:
   - REFERENCES in column definition
   - ALTER TABLE ... ADD CONSTRAINT ... FOREIGN KEY
3. DO NOT infer relationships from column names (e.g., user_id does NOT automatically mean FK unless REFERENCES is explicitly present)
4. Convert PostgreSQL types to C# types using this mapping:
   - SERIAL, INTEGER, INT -> int
   - BIGSERIAL, BIGINT -> long
   - SMALLINT, SMALLSERIAL -> short
   - TEXT, VARCHAR, CHARACTER VARYING, CHAR -> string
   - BOOLEAN, BOOL -> bool
   - TIMESTAMP, TIMESTAMP WITHOUT TIME ZONE, TIMESTAMP WITH TIME ZONE -> DateTime
   - DATE -> DateTime
   - TIME -> TimeSpan
   - REAL, FLOAT4 -> float
   - DOUBLE PRECISION, FLOAT8 -> double
   - NUMERIC, DECIMAL, MONEY -> decimal
   - UUID -> Guid
   - BYTEA -> byte[]
   - JSON, JSONB -> string
   - Other types -> object
5. EntityName should be PascalCase version of table_name (e.g., user_profile -> UserProfile, application_status -> ApplicationStatus)
6. Column names should remain in their original case (usually snake_case)
7. IsNullable = true unless NOT NULL is specified or column is PRIMARY KEY
8. IsPrimaryKey = true if PRIMARY KEY constraint is present
9. IsForeignKey = true only if REFERENCES is present
10. **Virtual Foreign Keys**: After parsing explicit ForeignKeys, populate ""VirtualForeignKeys"" array for columns that imply relationships by naming convention but have NO explicit REFERENCES:
    - Naming patterns to detect: *_id suffix (e.g. user_id), id_* prefix (e.g. id_user), id* prefix without separator (e.g. iduser)
    - Skip columns already in ForeignKeys (explicit FK)
    - Skip primary key columns
    - For matching columns, strip the id part to get candidate table name, then match against other parsed table names (try exact match, then plural forms: +s, +es, y->ies)
    - Use ""id"" as ReferencesColumn, set ConstraintName to null
    - If no matching table is found, do NOT add a virtual FK for that column

OUTPUT JSON SCHEMA:
[
  {
    ""TableName"": ""original_table_name"",
    ""EntityName"": ""PascalCaseName"",
    ""Columns"": [
      {
        ""Name"": ""column_name"",
        ""CSharpType"": ""int"",
        ""IsPrimaryKey"": true,
        ""IsForeignKey"": false,
        ""IsNullable"": false
      }
    ],
    ""ForeignKeys"": [
      {
        ""ColumnName"": ""fk_column_name"",
        ""CSharpType"": ""int"",
        ""ReferencesTable"": ""referenced_table"",
        ""ReferencesColumn"": ""id"",
        ""ConstraintName"": ""constraint_name_or_null""
      }
    ],
    ""VirtualForeignKeys"": [
      {
        ""ColumnName"": ""inferred_fk_column"",
        ""CSharpType"": ""int"",
        ""ReferencesTable"": ""inferred_table"",
        ""ReferencesColumn"": ""id"",
        ""ConstraintName"": null
      }
    ]
  }
]";

        /// <summary>
        /// Промпт для парсинга chunk с контекстом предыдущих таблиц
        /// </summary>
        public const string ChunkParsingPrompt = @"Parse the following PostgreSQL CREATE TABLE statement(s) into JSON format.

SQL TO PARSE:
{0}

{1}

Output ONLY the JSON array for the tables in this chunk. Do not include tables from previous chunks.";

        /// <summary>
        /// Контекст предыдущих таблиц для chunk
        /// </summary>
        public const string PreviousTablesContext = @"Previously parsed tables in this schema (for FK reference validation): {0}";

        /// <summary>
        /// Промпт для повторной попытки при невалидном JSON
        /// </summary>
        public const string RetryInvalidJsonPrompt = @"Your previous response was not valid JSON. Please try again.

Error details: {0}

Parse the following SQL and return ONLY a valid JSON array (no markdown, no explanations):

{1}";

        /// <summary>
        /// Промпт для запроса пропущенных таблиц
        /// </summary>
        public const string MissingTablesPrompt = @"The following tables were expected but not found in your response: {0}

Please parse ONLY these missing tables from the SQL below and return the JSON array for them:

{1}";

        /// <summary>
        /// Формирует user prompt для первого chunk
        /// </summary>
        public static string FormatInitialPrompt(string sqlContent)
        {
            return string.Format(ChunkParsingPrompt, sqlContent, "This is the first chunk. No previous tables.");
        }

        /// <summary>
        /// Формирует user prompt для последующих chunks
        /// </summary>
        public static string FormatChunkPrompt(string sqlContent, IEnumerable<string> previousTableNames)
        {
            var context = previousTableNames.Any()
                ? string.Format(PreviousTablesContext, string.Join(", ", previousTableNames))
                : "No previous tables.";
            return string.Format(ChunkParsingPrompt, sqlContent, context);
        }

        /// <summary>
        /// Формирует промпт для retry после невалидного JSON
        /// </summary>
        public static string FormatRetryPrompt(string error, string sqlContent)
        {
            return string.Format(RetryInvalidJsonPrompt, error, sqlContent);
        }

        /// <summary>
        /// Формирует промпт для запроса пропущенных таблиц
        /// </summary>
        public static string FormatMissingTablesPrompt(IEnumerable<string> missingTables, string sqlContent)
        {
            return string.Format(MissingTablesPrompt, string.Join(", ", missingTables), sqlContent);
        }
    }
}
