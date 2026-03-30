# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

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

# List available template profiles
dotnet run -- --list-profiles

# Run with specific profile (default profile: "default")
dotnet run -- --profile clean-arch --output ./generated

# Full options
dotnet run -- --use-llm --profile default --config appsettings.json --sql sql/script.sql --output result
```

## Project Overview

SQL-to-Entity Generator is a .NET 9.0 CLI tool that parses PostgreSQL `CREATE TABLE` scripts and generates code files using Scriban templates. It's designed for scaffolding backend (C# entities, repositories, controllers) and frontend (React/MUI components) code from database schemas.

## Architecture

### Core Components

- **Program.cs** - Entry point. Reads SQL from `sql/script.sql`, invokes parser, then generator.
- **Parser.cs** (`SqlParser`) - Parses PostgreSQL DDL using regex. Extracts tables, columns, primary/foreign keys from `CREATE TABLE` and `ALTER TABLE` statements. Maps PostgreSQL types to C# types.
- **Generator.cs** (`FileGenerator`) - Processes Scriban templates. Walks `templates/` directory, replaces `$table$` placeholder in paths/filenames with entity name, renders `.sbn` templates with table schema context.
- **Structures.cs** - Data models: `TableSchema`, `ColumnSchema`, `ForeignKeyInfo`

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

Templates are organized by profiles in `templates/{profile}/`:
- Each profile is a subdirectory containing a complete set of templates
- Use `--profile {name}` to select a profile (default: "default")
- Use `--list-profiles` to see available profiles
- Directories starting with `_` (e.g., `_shared`) are excluded from profile list

Template processing:
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

Helper functions: `map_type`, `to_pascal_case`, `to_camel_case`, `to_snake_case`, `remove_id_suffix`

### File Layout

```
sql/script.sql     # Input: PostgreSQL CREATE TABLE statements
templates/         # Template profiles directory
├── default/       # Default profile (ships with generator)
│   └── $table$/   # Templates with $table$ placeholders
├── clean-arch/    # Clean Architecture profile (example)
└── _shared/       # Shared templates (excluded from --list-profiles)
result/            # Default output directory
appsettings.json   # LLM parser configuration
LlmParser/         # LLM parser module
```

## Key Implementation Details

- Type mapping defined in `SqlParser.MapPostgresToCSharpType()` (Parser.cs:209)
- String case conversions in `StringExtensions` class (Generator.cs:13-87)
- System columns (excluded from `editable_columns`): id, created_at, updated_at, created_by, updated_by (Generator.cs:266)
- FK parsing handles both inline `REFERENCES` in columns and separate `ALTER TABLE ADD CONSTRAINT` statements

## Claude Code Skills

### /generate-template

Skill for generating Scriban templates from example code files. Use this when you have working code for a table and want to create templates for generating similar code for other tables.

```bash
# Basic usage
/generate-template Entity.cs Controller.cs --sql sql/users.sql --output templates/my-profile

# With inline SQL
/generate-template UserProfile.cs --sql "CREATE TABLE user_profile (id SERIAL, name VARCHAR)" --output templates/custom

# Full component set
/generate-template Entity.cs Repository.cs ListView/index.tsx --sql sql/orders.sql --output templates/clean-arch
```

**Skill location:** `.claude/skills/generate-template/`

The skill includes:
- `SKILL.md` - Main instructions
- `template-api.md` - Complete API reference
- `examples/` - Working template examples

## MCP Server Mode

SQLFileGenerator includes an MCP (Model Context Protocol) server for AI agent integration.

### Overview

The MCP server allows AI agents (like Claude Desktop) to:
- Parse SQL scripts and get structured table information
- Generate code files from SQL using template profiles
- List available template profiles

**Token Optimization:** All tools return only metadata and file paths, not file contents.

### Build and Run

```bash
# Build the MCP server
dotnet build SQLFileGenerator.Mcp -c Release

# Publish
dotnet publish SQLFileGenerator.Mcp -c Release -o publish/mcp

# Run (from project root for templates access)
./publish/mcp/SQLFileGenerator.Mcp.exe
```

### Claude Desktop Configuration

Edit `%APPDATA%\Claude\claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "sql-file-generator": {
      "command": "D:\\Practice\\SQLFileGenerator\\publish\\mcp\\SQLFileGenerator.Mcp.exe",
      "args": [],
      "cwd": "D:\\Practice\\SQLFileGenerator"
    }
  }
}
```

**Important:** Set `cwd` to project root so templates are accessible.

### Available Tools

#### 1. parse_sql

Parses PostgreSQL CREATE TABLE scripts and returns table metadata.

**Input:**
- `sql_file_path` (string, required) - Absolute path to SQL file
- `use_regex` (bool, default: true) - Use regex or LLM parser

**Output:**
```json
{
  "tables": [
    {
      "table_name": "users",
      "entity_name": "User",
      "column_count": 5,
      "foreign_key_count": 0,
      "primary_key": "id"
    }
  ],
  "metadata": {
    "table_count": 1,
    "parser_used": "regex",
    "sql_file": "path/to/file.sql"
  }
}
```

#### 2. generate_code

Generates code files from SQL using Scriban templates.

**Input:**
- `sql_file_path` (string, required) - Absolute path to SQL file
- `output_dir` (string, required) - Output directory path
- `profile` (string, default: "default") - Template profile name
- `use_regex` (bool, default: true) - Parser mode

**Output:**
```json
{
  "output_directory": "D:\\output",
  "files_generated": 15,
  "tables": ["User", "Order"],
  "profile": "default"
}
```

**Note:** Returns only paths, not file contents. Use Read tool to inspect generated files.

#### 3. list_profiles

Lists available template profiles.

**Input:** (none)

**Output:**
```json
{
  "profiles": [
    {
      "name": "default",
      "description": "Default profile with full stack templates",
      "file_count": 15,
      "directory_count": 10
    }
  ]
}
```

### MCP Server Configuration

Edit `appsettings.mcp.json` for LLM parser settings:

```json
{
  "LlmParser": {
    "ApiUrl": "http://localhost:11434/v1",
    "ModelName": "llama3.2",
    "MaxTablesPerChunk": 5
  }
}
```

### Troubleshooting

**MCP server not showing in Claude Desktop:**
- Verify path in config.json is correct
- Ensure `cwd` points to project root
- Restart Claude Desktop completely

**Empty profile list:**
- Server must run from directory containing `templates/` folder
- Check `cwd` in Claude Desktop config

**Tools failing:**
- Use absolute paths for `sql_file_path` and `output_dir`
- Check appsettings.mcp.json for LLM parser config
