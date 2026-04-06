using ModelContextProtocol.Server;
using System.ComponentModel;

namespace SqlGenerator.Mcp.Prompts;

[McpServerPromptType]
public class SqlParsingPrompts
{
    [McpServerPrompt]
    [Description("Instructions for parsing PostgreSQL CREATE TABLE SQL into JSON schema format")]
    public static string SqlParsingInstructions(
        [Description("The SQL script to parse")]
        string sql = "")
    {
        var prompt = @"# SQL to Schema Parsing Instructions

Parse the provided PostgreSQL CREATE TABLE SQL script into a JSON schema format.

## Output Format

Return a JSON array of table schemas:

```json
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
        ""ColumnName"": ""fk_column"",
        ""CSharpType"": ""int"",
        ""ReferencesTable"": ""other_table"",
        ""ReferencesColumn"": ""id"",
        ""ConstraintName"": ""fk_constraint_name""
      }
    ]
  }
]
```

## Type Mapping (PostgreSQL to C#)

| PostgreSQL | C# Type |
|------------|---------|
| integer, serial | int |
| bigint, bigserial | long |
| smallint | short |
| text, varchar, character varying | string |
| boolean | bool |
| timestamp, timestamp without time zone | DateTime |
| timestamptz, timestamp with time zone | DateTimeOffset |
| date | DateTime |
| time | TimeSpan |
| real | float |
| double precision | double |
| numeric, decimal | decimal |
| uuid | Guid |
| json, jsonb | string |
| bytea | byte[] |
| array types (e.g., integer[]) | corresponding[] (e.g., int[]) |

## Naming Conventions

- **TableName**: Keep original snake_case (e.g., ""user_profiles"")
- **EntityName**: Convert to PascalCase (e.g., ""UserProfiles"")
- **Column.Name**: Keep original (e.g., ""created_at"")

## Rules

1. **Primary Keys**: Mark column with `IsPrimaryKey: true` if:
   - Has `PRIMARY KEY` constraint
   - Is part of `CONSTRAINT ... PRIMARY KEY (col)`

2. **Foreign Keys**: Mark column with `IsForeignKey: true` AND add to `ForeignKeys` array if:
   - Has `REFERENCES other_table(column)`
   - Is part of `CONSTRAINT ... FOREIGN KEY (col) REFERENCES ...`
   - Is part of `ALTER TABLE ... ADD CONSTRAINT ... FOREIGN KEY ...`

3. **Nullable**: `IsNullable: false` only if column has `NOT NULL` constraint

4. **Ignore**: Skip comments (--), GRANT statements, CREATE INDEX, etc.

## Example

Input SQL:
```sql
CREATE TABLE users (
    id SERIAL PRIMARY KEY,
    email VARCHAR(255) NOT NULL,
    created_at TIMESTAMP DEFAULT NOW()
);

CREATE TABLE orders (
    id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES users(id),
    total NUMERIC(10,2)
);
```

Output JSON:
```json
[
  {
    ""TableName"": ""users"",
    ""EntityName"": ""Users"",
    ""Columns"": [
      {""Name"": ""id"", ""CSharpType"": ""int"", ""IsPrimaryKey"": true, ""IsForeignKey"": false, ""IsNullable"": false},
      {""Name"": ""email"", ""CSharpType"": ""string"", ""IsPrimaryKey"": false, ""IsForeignKey"": false, ""IsNullable"": false},
      {""Name"": ""created_at"", ""CSharpType"": ""DateTime"", ""IsPrimaryKey"": false, ""IsForeignKey"": false, ""IsNullable"": true}
    ],
    ""ForeignKeys"": []
  },
  {
    ""TableName"": ""orders"",
    ""EntityName"": ""Orders"",
    ""Columns"": [
      {""Name"": ""id"", ""CSharpType"": ""int"", ""IsPrimaryKey"": true, ""IsForeignKey"": false, ""IsNullable"": false},
      {""Name"": ""user_id"", ""CSharpType"": ""int"", ""IsPrimaryKey"": false, ""IsForeignKey"": true, ""IsNullable"": false},
      {""Name"": ""total"", ""CSharpType"": ""decimal"", ""IsPrimaryKey"": false, ""IsForeignKey"": false, ""IsNullable"": true}
    ],
    ""ForeignKeys"": [
      {""ColumnName"": ""user_id"", ""CSharpType"": ""int"", ""ReferencesTable"": ""users"", ""ReferencesColumn"": ""id"", ""ConstraintName"": null}
    ]
  }
]
```

## After Parsing

After you parse the SQL, call the `save_schema` tool with the JSON result to save it.
Then use `generate_files` to generate code from the schema.
";

        if (!string.IsNullOrWhiteSpace(sql))
        {
            prompt += $@"

---

## SQL to Parse

```sql
{sql}
```

Parse this SQL and return ONLY the JSON array (no markdown, no explanation).
";
        }

        return prompt;
    }
}
