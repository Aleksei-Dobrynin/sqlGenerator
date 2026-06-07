# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## LLMWiki — shared knowledge vault

Personal Obsidian-based knowledge vault at **`$OBSIDIAN_VAULT`** (= `D:\Practice\LLMWiki`). Holds architectural context for this and sibling projects.

### When to read (before non-trivial work)

- `$OBSIDIAN_VAULT/projects/sql-generator/index.md` — MOC: stack, parser variants (regex/LLM), template system overview
- `$OBSIDIAN_VAULT/projects/sql-generator/architecture.md` — компоненты, потоки, template helpers
- `$OBSIDIAN_VAULT/projects/sql-generator/decisions/` — ADR'ы (ADR-0001 dual-parsing strategy, ADR-0002 virtual FK inference, ADR-0003 MCP token economy)
- `$OBSIDIAN_VAULT/projects/sql-generator/runbooks/` — quick-start

### Related projects in the vault

Сейчас sql-generator изолированный — кросс-проектных связей нет. Если появится потребитель (например, saas-boilerplate начнёт генерить entities через MCP), отметить здесь и в `projects/sql-generator/index.md`.

### When to write (back to the vault) — follow the agent playbooks

The vault has explicit, step-by-step procedures. **Read the relevant playbook before writing — don't improvise.**

- `$OBSIDIAN_VAULT/_meta/triggers.md` — **start here.** Flat map: "this happened in code → do that in the wiki (or nothing)". Explicit DO and DO NOT tables. If a trigger isn't on the map, default to NOT writing.
- `$OBSIDIAN_VAULT/_meta/add-adr.md` — full procedure for an Architectural Decision (per-project numbering, supersede flow, four-section structure, link-back into project MOC).
- `$OBSIDIAN_VAULT/_meta/add-knowledge.md` — full procedure for a reusable knowledge note (`status: draft` always on first write, `confidence` scale, cross-project vs project-specific test — most learnings belong in `projects/sql-generator/notes/`, not in shared `knowledge/`).
- `$OBSIDIAN_VAULT/AGENTS.md` — canonical rules: append-only files, supersede-not-delete, protected `<!-- STABLE -->` / `<!-- HUMAN -->` blocks, no cosmetic edits, log every operation to `_meta/log.md`.
- `$OBSIDIAN_VAULT/_meta/conventions.md` + `_meta/taxonomy.md` — formatting rules and permitted tags (never invent tags).

**Two universal protections** (apply on every write):

1. If your edit would touch a `<!-- STABLE -->` or `<!-- HUMAN -->` block — **stop**, add a `## Pending review` section to that file with a one-liner, hand off to human.
2. Every content-changing wiki write ends with one line in `_meta/log.md`. No exceptions.

Default to **no wiki write** when ambiguous — the daily-summary worker captures session activity passively. A missed note is recoverable; a noisy wiki is not.

### Auto-logging (no action required)

Claude Code хуки и OpenCode plugin в этом репо (`.claude/settings.local.json`, `.opencode/plugins/obsidian-logger.js` — оба gitignored) сами пишут sessions/daily-logs в вальт. Делать ничего не нужно.

## Build and Run

```bash
# Build the project
dotnet build

# Run with regex parser (default)
dotnet run
dotnet run -- <output_directory>

# Run with LLM parser
dotnet run -- --use-llm
dotnet run -- --use-llm --output ./generated

# Full options
dotnet run -- --use-llm --config appsettings.json --sql sql/script.sql --output result

# Virtual foreign keys are enabled by default. To disable:
dotnet run -- --no-virtual-fks
dotnet run -- --no-virtual-fks --output ./generated
```

## Project Overview

SQL-to-Entity Generator is a .NET 9.0 CLI tool that parses PostgreSQL `CREATE TABLE` scripts and generates code files using Scriban templates. It's designed for scaffolding backend (C# entities, repositories, controllers) and frontend (React/MUI components) code from database schemas.

## Architecture

### Core Components

- **Program.cs** - Entry point. Reads SQL from `sql/script.sql`, invokes parser, then generator.
- **Parser.cs** (`SqlParser`) - Parses PostgreSQL DDL using regex. Extracts tables, columns, primary/foreign keys from `CREATE TABLE` and `ALTER TABLE` statements. Maps PostgreSQL types to C# types.
- **Generator.cs** (`FileGenerator`) - Processes Scriban templates. Walks `templates/` directory, replaces `$table$` placeholder in paths/filenames with entity name, renders `.sbn` templates with table schema context.
- **Structures.cs** - Data models: `TableSchema`, `ColumnSchema`, `ForeignKeyInfo`
- **VirtualForeignKeyResolver.cs** - Post-processor that infers virtual FK relationships from column naming conventions (`*_id`, `id_*`, `id*` patterns). Matches candidates against parsed table names with pluralization support.

### LLM Parser (`LlmParser/`)

Alternative parser using LLM (OpenAI-compatible API) for complex SQL schemas:

- **LlmParserService.cs** - Main service orchestrating chunked parsing, validation, and retry logic
- **OpenAiCompatibleClient.cs** - HTTP client for OpenAI/Ollama/etc APIs with retry and exponential backoff
- **SqlChunker.cs** - Splits large SQL into chunks by `MaxTablesPerChunk` for context management
- **LlmResponseValidator.cs** - Validates JSON responses, checks types, FK references
- **SystemPrompts.cs** - System prompts for SQL parsing instructions
- **LlmConfiguration.cs** - Config model loaded from `appsettings.json`

Configuration in `appsettings.json`:
```json
{
  "LlmParser": {
    "ApiUrl": "http://localhost:11434/v1",
    "ModelName": "llama3.2",
    "MaxTablesPerChunk": 5
  }
}
```

### Template System

Templates live in `templates/` with structure preserved in output:
- `$table$` in directory/file names is replaced with PascalCase entity name
- `.sbn` extension indicates Scriban template (removed in output)
- Non-`.sbn` files are copied as-is

Available template variables:
- `entity_name` - PascalCase entity name
- `table_name` - Original table name
- `columns` - List of column objects (supports both `Name`/PascalCase and `name`/snake_case access)
- `editable_columns` - Columns excluding system fields (id, created_at, updated_at, created_by, updated_by)
- `foreign_keys` - Foreign key relationships
- `primary_key` - Primary key column info
- `all_tables` - All parsed tables (for cross-table relationships)
- `virtual_foreign_keys` - Virtual FK relationships inferred from naming conventions (enabled by default, disable with `--no-virtual-fks`)
- `virtual_all_tables` - All tables with combined real + virtual FKs (for cross-table relationships including inferred ones)

Helper functions: `map_type`, `to_pascal_case`, `to_camel_case`, `to_snake_case`, `remove_id_suffix`

### File Layout

```
sql/script.sql     # Input: PostgreSQL CREATE TABLE statements
templates/         # Scriban templates with $table$ placeholders
result/            # Default output directory
appsettings.json   # LLM parser configuration
LlmParser/         # LLM parser module
```

## Key Implementation Details

- Type mapping defined in `SqlParser.MapPostgresToCSharpType()` (Parser.cs). Supports `timestamp with time zone → DateTimeOffset`, `time → TimeSpan`, `uuid → Guid`, `bytea → byte[]`, `json/jsonb → string`, `money → decimal`, `smallint/int2 → short`. Multi-word types склеиваются в `ParseSingleColumn` перед маппингом.
- SQL line/block-комментарии (`-- ...`, `/* ... */`) удаляются `ParsePostgresCreateTableScript` перед регексом — комментарии внутри `CREATE TABLE` не ломают парсинг колонок.
- `VirtualForeignKeyResolver` идемпотентен: если входная схема уже содержит `VirtualForeignKeys` (заполненные LLM/agent), резолвер только дополняет недостающие записи, дубликаты не создаёт.
- MCP `SaveSchema` запускает `LlmResponseValidator` на JSON; критические ошибки → `success: false`, warnings → поле `warnings[]` в результате.
- String case conversions in `StringExtensions` class (Generator.cs:13-87)
- System columns (excluded from `editable_columns`): id, created_at, updated_at, created_by, updated_by (Generator.cs:266)
- FK parsing handles both inline `REFERENCES` in columns and separate `ALTER TABLE ADD CONSTRAINT` statements
- PK parsing handles inline (`id int PRIMARY KEY`), table-level (`PRIMARY KEY (a, b)`, compound) and `ALTER TABLE ... ADD ... PRIMARY KEY (cols)`. Table-level constraint lines (`PRIMARY KEY`/`UNIQUE`/`FOREIGN KEY`/`CHECK`/`EXCLUDE`/`CONSTRAINT`) распознаются в `ParseColumns` и не превращаются в колонки.
- Quoted-идентификаторы и schema-qualified имена (`"User"`, `public."User"`) парсятся и нормализуются (`StripQuotes`) для таблиц, REFERENCES и ALTER-конструкций.

## MCP Server (`SqlGenerator.Mcp/`)

MCP (Model Context Protocol) сервер для интеграции с AI-агентами (Claude Desktop, etc.).

### Build and Run MCP Server

```bash
# Build
cd SqlGenerator.Mcp
dotnet build

# Run with MCP Inspector
npx @anthropic/mcp-inspector dotnet run --project SqlGenerator.Mcp
```

### Token Economy Principle

MCP tools accept ONLY file paths, never file content. The MCP server reads/writes files directly on disk. Agents pass only paths. This minimizes context window usage and prevents quality degradation on large schemas.

See [`agent-spec.md`](agent-spec.md) for full agent integration specification.

### MCP Tools

| Tool | Params | Description |
|------|--------|-------------|
| `parse_sql` | `sqlFilePath`, `outputPath?`, `includeVirtualFks?` | Parse SQL file with regex. Returns schema file path |
| `save_schema` | `schemaFilePath`, `outputPath?` | Validate agent-parsed schema from JSON file |
| `generate_files` | `schemaFile`, `outputDir`, `templatesDir?`, `presetName?`, `includeVirtualFks?` | Generate code from schema.json file |
| `quick_generate` | `sqlFilePath`, `outputDir`, `templatesDir?`, `presetName?`, `includeVirtualFks?` | Parse SQL file + generate in one call (regex only) |
| `list_presets` | `templatesDir?` | List available template presets |

### MCP Prompts

| Prompt | Description |
|--------|-------------|
| `sql_parsing_instructions` | Instructions for agent to parse complex SQL into JSON format |

### Usage Scenarios

**Simple SQL (regex):**
```
Agent → quick_generate(sqlFilePath, outputDir, templatesDir)
```

**Complex SQL (agent parses):**
```
Agent:
  1. Get prompt: sql_parsing_instructions(sql)
  2. Parse SQL following instructions
  3. Write JSON result to file
  4. → save_schema(schemaFilePath, "schema.json")
  5. → generate_files("schema.json", outputDir, templatesDir)
```

**Full-module scaffolding into a project — batch-transplant (preferred):** generate a whole batch via
MCP, then transplant into the project using the compiler/toolchain as an error oracle (5 phases:
generate → bulk transplant → diagnostics → batch-fix → verify). The agent drives the utility as an MCP
server and parses the schema itself (not the built-in parsers). Source of truth:
[`runbooks/batch-transplant-workflow.md`](runbooks/batch-transplant-workflow.md); phase 3–5 details in
[`docs/batch/`](docs/batch/).

### Schema JSON Format

```json
[
  {
    "TableName": "users",
    "EntityName": "Users",
    "Columns": [
      {"Name": "id", "CSharpType": "int", "IsPrimaryKey": true, "IsForeignKey": false, "IsNullable": false}
    ],
    "ForeignKeys": [
      {"ColumnName": "role_id", "CSharpType": "int", "ReferencesTable": "roles", "ReferencesColumn": "id"}
    ],
    "VirtualForeignKeys": [
      {"ColumnName": "dept_id", "CSharpType": "int", "ReferencesTable": "departments", "ReferencesColumn": "id"}
    ]
  }
]
```

`VirtualForeignKeys` - inferred from naming conventions (`*_id`, `id_*`, `id*`), populated by default (disable with `--no-virtual-fks`).

### Claude Desktop Configuration

Add to `claude_desktop_config.json`:

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
