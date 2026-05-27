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

Note: the example below demonstrates SaaS-style entities with the system fields that templates expect:
`tenant_id` (multi-tenancy), `is_active`/`is_deleted` (soft-delete flags), and audit columns
`created_at`/`updated_at`/`created_by`/`updated_by`. Always preserve these fields and their nullability/type
exactly as in the SQL — templates rely on them for generating audit, soft-delete, and tenant-isolation logic.
`TIMESTAMPTZ`/`TIMESTAMP WITH TIME ZONE` must map to `DateTimeOffset` (not `DateTime`).

Input SQL:
```sql
CREATE TABLE user_accounts (
    id SERIAL PRIMARY KEY,
    tenant_id INTEGER NOT NULL,
    email VARCHAR(255) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE departments (
    id SERIAL PRIMARY KEY,
    tenant_id INTEGER NOT NULL,
    name VARCHAR(255) NOT NULL,
    parent_id INTEGER REFERENCES departments(id),
    head_user_account_id INTEGER,  -- virtual FK: matches user_accounts by naming convention
    is_active BOOLEAN NOT NULL DEFAULT true,
    is_deleted BOOLEAN NOT NULL DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by INTEGER,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by INTEGER
);
```

Output JSON:
```json
[
  {
    ""TableName"": ""user_accounts"",
    ""EntityName"": ""UserAccounts"",
    ""Columns"": [
      {""Name"": ""id"", ""CSharpType"": ""int"", ""IsPrimaryKey"": true, ""IsForeignKey"": false, ""IsNullable"": false},
      {""Name"": ""tenant_id"", ""CSharpType"": ""int"", ""IsPrimaryKey"": false, ""IsForeignKey"": false, ""IsNullable"": false},
      {""Name"": ""email"", ""CSharpType"": ""string"", ""IsPrimaryKey"": false, ""IsForeignKey"": false, ""IsNullable"": false},
      {""Name"": ""is_active"", ""CSharpType"": ""bool"", ""IsPrimaryKey"": false, ""IsForeignKey"": false, ""IsNullable"": false},
      {""Name"": ""created_at"", ""CSharpType"": ""DateTimeOffset"", ""IsPrimaryKey"": false, ""IsForeignKey"": false, ""IsNullable"": false},
      {""Name"": ""updated_at"", ""CSharpType"": ""DateTimeOffset"", ""IsPrimaryKey"": false, ""IsForeignKey"": false, ""IsNullable"": false}
    ],
    ""ForeignKeys"": [],
    ""VirtualForeignKeys"": []
  },
  {
    ""TableName"": ""departments"",
    ""EntityName"": ""Departments"",
    ""Columns"": [
      {""Name"": ""id"", ""CSharpType"": ""int"", ""IsPrimaryKey"": true, ""IsForeignKey"": false, ""IsNullable"": false},
      {""Name"": ""tenant_id"", ""CSharpType"": ""int"", ""IsPrimaryKey"": false, ""IsForeignKey"": false, ""IsNullable"": false},
      {""Name"": ""name"", ""CSharpType"": ""string"", ""IsPrimaryKey"": false, ""IsForeignKey"": false, ""IsNullable"": false},
      {""Name"": ""parent_id"", ""CSharpType"": ""int"", ""IsPrimaryKey"": false, ""IsForeignKey"": true, ""IsNullable"": true},
      {""Name"": ""head_user_account_id"", ""CSharpType"": ""int"", ""IsPrimaryKey"": false, ""IsForeignKey"": false, ""IsNullable"": true},
      {""Name"": ""is_active"", ""CSharpType"": ""bool"", ""IsPrimaryKey"": false, ""IsForeignKey"": false, ""IsNullable"": false},
      {""Name"": ""is_deleted"", ""CSharpType"": ""bool"", ""IsPrimaryKey"": false, ""IsForeignKey"": false, ""IsNullable"": false},
      {""Name"": ""created_at"", ""CSharpType"": ""DateTimeOffset"", ""IsPrimaryKey"": false, ""IsForeignKey"": false, ""IsNullable"": false},
      {""Name"": ""created_by"", ""CSharpType"": ""int"", ""IsPrimaryKey"": false, ""IsForeignKey"": false, ""IsNullable"": true},
      {""Name"": ""updated_at"", ""CSharpType"": ""DateTimeOffset"", ""IsPrimaryKey"": false, ""IsForeignKey"": false, ""IsNullable"": false},
      {""Name"": ""updated_by"", ""CSharpType"": ""int"", ""IsPrimaryKey"": false, ""IsForeignKey"": false, ""IsNullable"": true}
    ],
    ""ForeignKeys"": [
      {""ColumnName"": ""parent_id"", ""CSharpType"": ""int"", ""ReferencesTable"": ""departments"", ""ReferencesColumn"": ""id"", ""ConstraintName"": null}
    ],
    ""VirtualForeignKeys"": [
      {""ColumnName"": ""head_user_account_id"", ""CSharpType"": ""int"", ""ReferencesTable"": ""user_accounts"", ""ReferencesColumn"": ""id"", ""ConstraintName"": null}
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
