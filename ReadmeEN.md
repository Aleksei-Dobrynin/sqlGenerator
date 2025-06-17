# SQL-to-Entity Generator

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)  
[![.NET](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/)

A tool for automatic code generation based on PostgreSQL table creation SQL scripts.

## 📋 Table of Contents

- [Project Description](#-project-description)
- [Features](#-features)
- [Project Architecture](#-project-architecture)
- [Parameters and Configuration](#-parameters-and-configuration)
- [Data Types](#-data-types)
- [Usage Examples](#-usage-examples)
- [Installation and Execution](#-installation-and-execution)
- [Dependencies](#-dependencies)
- [Future Development](#-future-development)
- [License](#-license)

## 📝 Project Description

SQL-to-Entity Generator is a tool for automatic code generation based on PostgreSQL table creation SQL scripts. The project allows you to automatically create entity classes, repositories, interfaces, and other code files necessary for interacting with the database based on the SQL schema.

The tool uses the Scriban templating engine to generate files based on customizable templates, providing high flexibility and adaptability to various architectural approaches.

## ✨ Features

- 🔍 **Parsing PostgreSQL SQL scripts** to extract tables, columns, and relationships.
- 🔄 **Automatic conversion of PostgreSQL data types** to C# types.
- 📄 **Code generation** based on customizable Scriban templates.
- 🔑 **Support for primary and foreign keys.**
- 📁 **Preservation of template directory structure** in output files.
- 🔤 **Conversion of names to PascalCase** to comply with C# standards.
- 📋 **Flexible output filename formation.**
- 🔒 **Processing PRIMARY KEY and FOREIGN KEY constraints** both within `CREATE TABLE` and through `ALTER TABLE`.
- 🌐 **Support for React component generation** with MUI and other frontend frameworks.

## 🏗️ Project Architecture

The project is divided into the following main modules:

### 🔍 SQL Script Parser (Parser.cs)

This module is responsible for:
- Parsing SQL `CREATE TABLE` commands using regular expressions.
- Extracting information about tables, including names, columns, and data types.
- Identifying primary and foreign keys (both in `CREATE TABLE` and through `ALTER TABLE`).
- Converting PostgreSQL types to corresponding C# types.

**Key methods:**
- `ParsePostgresCreateTableScript`: Parses PostgreSQL SQL script and extracts table structure.
- `MapPostgresToCSharpType`: Maps PostgreSQL data types to C# types.
- `ToPascalCase`: Converts snake_case strings to PascalCase.

**Implementation details:**
- Uses various regular expressions to parse different SQL constructs.
- Processes primary keys declared both in columns and through separate constraints.
- Processes foreign keys declared both in columns and through separate `ALTER TABLE` commands.
- Represents results as structured objects for further processing.

### 📊 Data Structures (Structures.cs)

Defines classes for storing database schema information:
- `TableSchema`: Information about a table, its columns, and relationships.
- `ColumnSchema`: Information about a table column, its type, and keys.
- `ForeignKeyInfo`: Information about foreign keys and relationships between tables.

**Implementation details:**
- Each structure contains all necessary data for generating corresponding code.
- `ColumnSchema` includes flags for identifying primary and foreign keys, as well as NULL value allowance.
- `ForeignKeyInfo` stores information about relationships between tables, including constraint name.

### 📄 File Generator (Generator.cs)

Responsible for generating files based on Scriban templates:
- Processing all templates in a specified directory and its subdirectories.
- Converting table and template names to output file names.
- Forming a context for the templating engine with data about tables, columns, and relationships.
- Rendering templates and saving results.

**Key methods:**
- `GenerateOtherFiles`: Processes all templates for each table in the database schema.
- `ProcessTemplatesDirectory`: Processes a directory of templates for a specific table.
- `MapType`: Maps SQL types to C# types for use in templates.

**Implementation details:**
- Preserves the directory structure of templates in output files.
- Supports special markers in template names (e.g., `$table$`) for replacement with entity name.
- Provides templates with access to `map_type` and `to_pascal_case` functions.
- Processes templates with `.sbn` extension specially, removing this extension in output files.

### 🔤 String Utility Methods (StringExtensions)

A set of methods for converting strings between various naming styles:
- `ToPascalCase`: Converts a string to PascalCase.
- `ToCamelCase`: Converts a string to camelCase.
- `ToSnakeCase`: Converts a string to snake_case.

**Implementation details:**
- Methods consider various separators (spaces, hyphens, underscores).
- Implemented as extension methods for ease of use.

### 🚀 Main Module (Program.cs)

Coordinates the operation of all application components:
- Determining paths to SQL script, templates, and output files.
- Reading and parsing SQL script.
- Generating files based on templates for each table.

**Implementation details:**
- Provides the ability to specify the path to the output directory via command-line arguments or interactively.
- Checks for the existence of necessary files and directories before starting work.
- Processes and logs possible errors.

## ⚙️ Parameters and Configuration

### 📂 File Paths

- `sqlScriptPath`: Path to the SQL script with `CREATE TABLE` commands (default: `"sql/script.sql"`).
- `templatesDir`: Directory with Scriban templates (default: `"templates"`).
- `resultDir`: Directory for saving generated files (specified by the user, default: `"result"`).

### 📝 Scriban Templates

The Scriban template engine syntax is used with access to the following parameters:
- `entity_name`: Entity name in PascalCase.
- `table_name`: Table name in the database.
- `columns`: List of table columns.
- `foreign_keys`: List of foreign keys.
- `primary_key`: Information about the primary key.

Helper functions are also available:
- `map_type`: For converting SQL types to C# types.
- `to_pascal_case`: For converting strings to PascalCase.

### 📋 Filename Formatting

For templates with the `.sbn` extension, the output filename is formed according to the template:

{EntityName}{TemplateName}{Extension}

For example, for the `user` table and the `Repository.cs.sbn` template, a `UserRepository.cs` file will be created.

It's possible to replace the `$table$` marker in the template name with the entity name in PascalCase.

## 🔄 Data Types

### 📊 Converting PostgreSQL Types to C#

The system automatically converts PostgreSQL data types to corresponding C# types:

| PostgreSQL Type                                           | C# Type   |
|-----------------------------------------------------------|-----------|
| `integer`, `serial`                                        | `int`     |
| `bigint`, `bigserial`                                      | `long`    |
| `text`, `varchar`, `character varying`                    | `string`  |
| `boolean`                                                | `bool`    |
| `timestamp`, `timestamp without time zone`, `date`       | `DateTime`|
| `real`                                                   | `float`   |
| `double precision`                                       | `double`  |
| `numeric`, `decimal`                                       | `decimal` |
| **other types**                                          | `object`  |

## 🔍 Usage Examples

### 📄 Example SQL Script

```sql
-- auto-generated definition
create table application_status
(
    id               serial not null
        constraint application_status_pkey
            primary key,
    name             text,
    description      text,
    code             text,
    created_at       timestamp,
    updated_at       timestamp,
    created_by       integer,
    updated_by       integer,
    name_kg          text,
    status_color     text,
    description_kg   text,
    text_color       text,
    background_color text,
    type_id INT REFERENCES contact_types(id),
);

CREATE TABLE contacts (
    id INT PRIMARY KEY,
    type_id INT REFERENCES contact_types(id),
    value VARCHAR(255) NOT NULL
);
```

### 📝 Example Template (for Repository Interface)

```csharp
using Application.Models;
using Domain.Entities;

namespace Application.Repositories
{
    public interface I{{ entity_name }}Repository : BaseRepository
    {
        // Basic CRUD
        Task<List<{{ entity_name }}>> GetAll();
        Task<PaginatedList<{{ entity_name }}>> GetPaginated(int pageSize, int pageNumber);
        Task<int> Add({{ entity_name }} domain);
        Task Update({{ entity_name }} domain);
        Task<{{ entity_name }}> GetOne(int id);
        Task Delete(int id);

        // Methods for FK
        {{- for fk in foreign_keys }}
        Task<List<{{ entity_name }}>> GetBy{{ fk.column_name | to_pascal_case }}({{ fk.csharp_type }} {{ fk.column_name }});
        {{- end }}
    }
}
```

### 🔧 Example Result (Generated Interface)

```csharp
using Application.Models;
using Domain.Entities;

namespace Application.Repositories
{
    public interface IApplicationStatusRepository : BaseRepository
    {
        // Basic CRUD
        Task<List<ApplicationStatus>> GetAll();
        Task<PaginatedList<ApplicationStatus>> GetPaginated(int pageSize, int pageNumber);
        Task<int> Add(ApplicationStatus domain);
        Task Update(ApplicationStatus domain);
        Task<ApplicationStatus> GetOne(int id);
        Task Delete(int id);

        // Methods for FK
        Task<List<ApplicationStatus>> GetByTypeId(int type_id);
    }
}
```

## 🚀 Installation and Execution

1. **Clone the repository:**

   ```bash
   git clone <repository_url>
   ```

2. **Prepare an SQL script with CREATE TABLE commands in the sql/script.sql folder.**

3. **Create necessary templates in the templates folder.**

4. **Run the application:**
   ```bash
   dotnet run
   ```
   Or specifying the output directory:
   ```bash
   dotnet run -- <output_directory>
   ```

Generated files will be placed in the specified folder or in the default result folder.

## 📦 Dependencies

- .NET 9.0
- Scriban (templating library)

## 🔮 Future Development

- 🌐 Improved support for various SQL dialects.
- 📚 Expansion of the standard template set for various architectural approaches.
- ⚙️ Adding a configuration file for flexible type mapping and generation parameter settings.
- 🔍 Support for additional SQL constructs (indexes, triggers, etc.).
- 🔤 Implementation of more complex naming and transformation rules for various architectural styles.
- 🌟 Support for more frontend frameworks and component libraries.
- 🛠️ Integration with ORM frameworks like Entity Framework or Dapper.

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.