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

5. **Virtual Foreign Keys**: Populate `VirtualForeignKeys` array for columns that imply relationships by naming convention but have NO explicit REFERENCES:
   - Naming patterns: `*_id` suffix (e.g. `user_id`), `id_*` prefix (e.g. `id_user`), `id*` prefix without separator (e.g. `iduser`)
   - Skip columns already in `ForeignKeys` (explicit FK)
   - Skip primary key columns
   - Strip the id part to get candidate table name, then match against other parsed table names (try exact, plural +s, +es, y->ies)
   - Use `ReferencesColumn: ""id""`, `ConstraintName: null`
   - If no matching table found, do NOT add a virtual FK

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

CREATE TABLE reviews (
    id SERIAL PRIMARY KEY,
    order_id INTEGER NOT NULL,
    reviewer_id INTEGER,
    rating INTEGER NOT NULL
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
    ""ForeignKeys"": [],
    ""VirtualForeignKeys"": []
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
    ],
    ""VirtualForeignKeys"": []
  },
  {
    ""TableName"": ""reviews"",
    ""EntityName"": ""Reviews"",
    ""Columns"": [
      {""Name"": ""id"", ""CSharpType"": ""int"", ""IsPrimaryKey"": true, ""IsForeignKey"": false, ""IsNullable"": false},
      {""Name"": ""order_id"", ""CSharpType"": ""int"", ""IsPrimaryKey"": false, ""IsForeignKey"": false, ""IsNullable"": false},
      {""Name"": ""reviewer_id"", ""CSharpType"": ""int"", ""IsPrimaryKey"": false, ""IsForeignKey"": false, ""IsNullable"": true},
      {""Name"": ""rating"", ""CSharpType"": ""int"", ""IsPrimaryKey"": false, ""IsForeignKey"": false, ""IsNullable"": false}
    ],
    ""ForeignKeys"": [],
    ""VirtualForeignKeys"": [
      {""ColumnName"": ""order_id"", ""CSharpType"": ""int"", ""ReferencesTable"": ""orders"", ""ReferencesColumn"": ""id"", ""ConstraintName"": null},
      {""ColumnName"": ""reviewer_id"", ""CSharpType"": ""int"", ""ReferencesTable"": ""users"", ""ReferencesColumn"": ""id"", ""ConstraintName"": null}
    ]
  }
]
```

## After Parsing

After you parse the SQL, write the JSON result to a file (e.g. `schema.json`), then call the `save_schema` tool with the file path to validate and save it.
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
