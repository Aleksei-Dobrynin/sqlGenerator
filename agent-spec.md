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

**Limitations:** Regex parser. May fail on complex DDL with advanced constraints, comments inside column definitions, or non-standard syntax. For complex SQL, use the agent-parsing workflow instead.

### SaveSchema

Validate and save a schema JSON file parsed by the agent.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `schemaFilePath` | yes | Path to JSON file with table schema array |
| `outputPath` | no | Output path for validated schema (default: `schema.json`) |

**Returns:** `{ success, tableCount, tables[], schemaFile }`

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

## Type Mapping (PostgreSQL to C#)

| PostgreSQL | C# |
|------------|-----|
| integer, int, int4 | int |
| bigint, int8 | long |
| smallint, int2 | short |
| serial | int |
| bigserial | long |
| boolean, bool | bool |
| varchar, character varying, text, char | string |
| decimal, numeric, money | decimal |
| real, float4 | float |
| double precision, float8 | double |
| date, timestamp, timestamptz, timestamp with time zone | DateTime |
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
