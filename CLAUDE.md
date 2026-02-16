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

# Full options
dotnet run -- --use-llm --config appsettings.json --sql sql/script.sql --output result
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

- Type mapping defined in `SqlParser.MapPostgresToCSharpType()` (Parser.cs:209)
- String case conversions in `StringExtensions` class (Generator.cs:13-87)
- System columns (excluded from `editable_columns`): id, created_at, updated_at, created_by, updated_by (Generator.cs:266)
- FK parsing handles both inline `REFERENCES` in columns and separate `ALTER TABLE ADD CONSTRAINT` statements
