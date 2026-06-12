# sql-generator MCP Server — Agent Specification

## Server Identity

- **Name:** `sql-generator`
- **Transport:** stdio
- **Command:** `dotnet run --project <path>/SqlGenerator.Mcp`

## Token Economy Principle

All tools accept ONLY file paths, never file content. The MCP server reads/writes files directly on disk. Agents must:

- Pass file paths to tools, not file content
- Not read files into context solely to pass them to a tool
- Use file copy/move operations instead of loading content into context
- Write intermediate results (e.g. parsed JSON) to disk, then pass the path

This design supports scaffolding entire applications (potentially thousands of files) without context window degradation.

## Tools

### ParseSql

Parse a PostgreSQL SQL file using regex. Fast, works for simple DDL.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `sqlFilePath` | yes | Path to `.sql` file with `CREATE TABLE` statements |
| `outputPath` | no | Output path for schema JSON (default: `schema.json`) |
| `includeVirtualFks` | no | Include virtual foreign keys inferred from naming conventions (default: `true`) |

**Returns:** `{ success, tableCount, tables[], schemaFile }`

**Limitations:** Regex parser. Tolerates SQL line/block comments, multi-word types (`timestamp with time zone`, `double precision`, `character varying`), quoted/schema-qualified identifiers (`"User"`, `public."User"`), and primary keys declared inline, table-level (`PRIMARY KEY (...)`) or via `ALTER TABLE ... ADD ... PRIMARY KEY`. May still fail on advanced DDL: partitioning, INHERITS, exclusion constraints, expression indexes. For complex SQL, use the agent-parsing workflow instead.

### SaveSchema

Validate and save a schema JSON file parsed by the agent.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `schemaFilePath` | yes | Path to JSON file with table schema array |
| `outputPath` | no | Output path for validated schema (default: `schema.json`) |

**Returns:** `{ success, tableCount, tables[], schemaFile, warnings[]? }`

**Validation:** runs `LlmResponseValidator` over the input JSON. Returns `success: false` with an error summary on critical issues (empty TableName, missing PK, duplicate column names, FK column not present in table, invalid C# type). Non-blocking issues (non-PascalCase EntityName, non-standard type, FK referencing table outside the result set) are surfaced in `warnings[]`.

### GenerateFiles

Generate code files from a schema JSON using templates.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `schemaFile` | yes | Path to schema JSON file |
| `outputDir` | yes | Output directory for generated files |
| `templatesDir` | no | Templates directory (default: `templates`) |
| `presetName` | no | Template preset name (call `ListPresets` first) |
| `includeVirtualFks` | no | Include virtual foreign keys inferred from naming conventions (default: `true`) |

**Returns:** `{ success, outputDir, tableCount, fileCount }`

### QuickGenerate

Parse SQL file + generate files in one call. For simple DDL only.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `sqlFilePath` | yes | Path to `.sql` file |
| `outputDir` | yes | Output directory |
| `templatesDir` | no | Templates directory (default: `templates`) |
| `presetName` | no | Template preset name |
| `includeVirtualFks` | no | Include virtual foreign keys inferred from naming conventions (default: `true`) |

**Returns:** `{ success, outputDir, tableCount, fileCount }`

### ListPresets

List available template presets.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `templatesDir` | no | Templates base directory (default: `templates`) |

**Returns:** `{ success, presetMode, presets[] }`

## Prompts

### sql_parsing_instructions

Returns detailed instructions for the agent to parse complex SQL into the schema JSON format. Includes type mapping table and output format specification.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `sql` | no | SQL to include in the prompt for parsing |

## Workflows

### Simple DDL (one step)

```
Agent -> QuickGenerate(sqlFilePath, outputDir, presetName)
```

### Simple DDL (two steps)

```
Agent -> ParseSql(sqlFilePath) -> GenerateFiles(schemaFile, outputDir, presetName)
```

### Complex DDL (agent parses)

```
1. Agent gets prompt: sql_parsing_instructions(sql)
2. Agent reads SQL file and parses it following the instructions
3. Agent writes JSON result to a file on disk
4. Agent -> SaveSchema(schemaFilePath, outputPath)
5. Agent -> GenerateFiles(schemaFile, outputDir, presetName)
```

### Batch-transplant mode (preferred for full-module scaffolding)

For full-module / multi-table scaffolding into a real project, this is the **recommended default**.
The agent drives the utility **as an MCP server** and **parses the schema itself** (the agent-parsing
flow above) rather than relying on the built-in parsers — the regex parser stumbles on real pg_dumps
(quoted identifiers, table-level / `ALTER` primary keys), so for non-trivial DDL the agent parses.

Five phases (compiler/toolchain as the error oracle):

```
[precondition: infra basis present in the project]
1. Generate (batch)    agent parses -> SaveSchema -> GenerateFiles into scratch (not the project tree)
2. Bulk transplant     one scripted move: generator-folder -> project layer
3. Diagnostics oracle  one build + typecheck/build + lint pass -> FULL error list
4. Batch-fix           fix per error class, cascading -> specific; rebuild until green
5. Verify              CRUD / runtime against a live DB
```

Full step-by-step guide (source of truth): [`runbooks/batch-transplant-workflow.md`](runbooks/batch-transplant-workflow.md).
Phase 3–5 details: [`docs/batch/`](docs/batch/).

## Schema JSON Format

```json
[
  {
    "TableName": "org.users",
    "EntityName": "Users",
    "Columns": [
      {
        "Name": "id",
        "CSharpType": "int",
        "IsPrimaryKey": true,
        "IsForeignKey": false,
        "IsNullable": false
      }
    ],
    "ForeignKeys": [
      {
        "ColumnName": "role_id",
        "CSharpType": "int",
        "ReferencesTable": "org.roles",
        "ReferencesColumn": "id",
        "ConstraintName": null
      }
    ],
    "VirtualForeignKeys": [
      {
        "ColumnName": "department_id",
        "CSharpType": "int",
        "ReferencesTable": "org.departments",
        "ReferencesColumn": "id",
        "ConstraintName": null
      }
    ]
  }
]
```

**Note:** `VirtualForeignKeys` contains relationships inferred from column naming conventions (`*_id`, `id_*`, `id*` patterns) when no explicit `REFERENCES` exists. Populated when `includeVirtualFks` is `true`.

**Idempotency:** `VirtualForeignKeyResolver` is idempotent. If the agent has already populated `VirtualForeignKeys` following the `sql_parsing_instructions` prompt, the resolver only fills in missing entries (e.g. cross-chunk cases for the LLM parser) and never produces duplicates. Templates rely on uniqueness in this array — duplicates would generate duplicate `DisplayName` properties and fail compilation.

**System fields:** SaaS templates (clean-arch preset) expect specific column names with semantic meaning. Always preserve them:

| Column | Purpose |
|--------|---------|
| `tenant_id` (int, NOT NULL) | Multi-tenancy isolation. Excluded from create/update DTOs. |
| `is_active` (bool, NOT NULL) | Activation flag. Enables `Activate/Deactivate()` methods on entity. |
| `is_deleted` (bool, NOT NULL) | Soft-delete flag. Enables `ISoftDeletable` + `Delete()` method. |
| `created_at`, `updated_at` (timestamptz, NOT NULL) | Audit timestamps. Must be `DateTimeOffset`, not `DateTime`. |
| `created_by`, `updated_by` (int, nullable) | Audit user IDs. |
| `deleted_at` (timestamptz), `deleted_by` (int) | Soft-delete audit. Optional, paired with `is_deleted`. |

The agent must include these in `Columns` array with correct types/nullability if they exist in SQL. Templates detect them by name and gate audit/soft-delete/tenant logic on their presence.

## Type Mapping (PostgreSQL to C#)

| PostgreSQL | C# |
|------------|-----|
| integer, int, int4 | int |
| bigint, int8 | long |
| smallint, int2 | short |
| serial | int |
| bigserial | long |
| smallserial | short |
| boolean, bool | bool |
| varchar, character varying, text, char, character | string |
| decimal, numeric, money | decimal |
| real, float4 | float |
| double precision, float8 | double |
| date, timestamp, timestamp without time zone | DateTime |
| timestamptz, timestamp with time zone | DateTimeOffset |
| time, time without time zone | TimeSpan |
| uuid | Guid |
| json, jsonb | string |
| bytea | byte[] |

## Claude Desktop Configuration

```json
{
  "mcpServers": {
    "sql-generator": {
      "command": "dotnet",
      "args": ["run", "--project", "D:\\Practice\\sqlGenerator\\SqlGenerator.Mcp"]
    }
  }
}
```
