# SQL-to-Entity Generator

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)  
[![.NET](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/)

Инструмент для автоматической генерации кода на основе SQL-скриптов создания таблиц PostgreSQL.

## 📋 Оглавление

- [Описание проекта](#-описание-проекта)
- [Функциональные возможности](#-функциональные-возможности)
- [Архитектура проекта](#-архитектура-проекта)
- [MCP Server](#-mcp-server)
- [Параметры и конфигурация](#-параметры-и-конфигурация)
- [Типы данных](#-типы-данных)
- [Примеры использования](#-примеры-использования)
- [Установка и запуск](#-установка-и-запуск)
- [Зависимости](#-зависимости)
- [Дальнейшее развитие](#-дальнейшее-развитие)
- [Лицензия](#-лицензия)

## 📝 Описание проекта

SQL-to-Entity Generator — это инструмент для автоматической генерации кода на основе SQL-скриптов создания таблиц PostgreSQL. Проект позволяет по SQL-схеме базы данных автоматически создавать классы сущностей, репозитории, интерфейсы и другие файлы кода, необходимые для взаимодействия с базой данных.

Инструмент использует шаблонизатор Scriban для генерации файлов на основе настраиваемых шаблонов, что обеспечивает высокую гибкость и возможность адаптации к различным архитектурным подходам.

## ✨ Функциональные возможности

- 🔍 **Парсинг SQL-скриптов PostgreSQL** с выделением таблиц, колонок и связей.
- 🔄 **Автоматическое преобразование типов данных PostgreSQL** в типы C#.
- 📄 **Генерация кода** на основе настраиваемых шаблонов Scriban.
- 🔑 **Поддержка первичных и внешних ключей.**
- 📁 **Сохранение структуры директорий шаблонов** в результирующих файлах.
- 🔤 **Преобразование имен в PascalCase** для соответствия C# стандартам.
- 📋 **Гибкое формирование имен выходных файлов.**
- 🔒 **Обработка ограничений PRIMARY KEY и FOREIGN KEY** как внутри `CREATE TABLE`, так и через `ALTER TABLE`.
- 🌐 **Поддержка генерации React-компонентов** с использованием MUI и других фронтенд-фреймворков.
- 🔗 **Виртуальные внешние ключи** — вывод неявных связей из конвенций именования колонок (`*_id`, `id_*`, `id*`), включены по умолчанию (отключение: `--no-virtual-fks`).

## 🏗️ Архитектура проекта

Проект разделен на следующие основные модули:

### 🔍 Парсер SQL-скриптов (Parser.cs)

Модуль отвечает за:
- Парсинг SQL `CREATE TABLE` команд с использованием регулярных выражений.
- Извлечение информации о таблицах, включая имена, колонки, типы данных.
- Определение первичных и внешних ключей (как в `CREATE TABLE`, так и через `ALTER TABLE`).
- Преобразование типов PostgreSQL в соответствующие типы C#.

**Основные методы:**
- `ParsePostgresCreateTableScript`: Парсит SQL-скрипт PostgreSQL и извлекает структуру таблиц.
- `MapPostgresToCSharpType`: Преобразует типы данных PostgreSQL в типы C#.
- `ToPascalCase`: Преобразует строки в формате snake_case в формат PascalCase.

**Особенности реализации:**
- Использует различные регулярные выражения для парсинга различных конструкций SQL.
- Обрабатывает первичные ключи, объявленные как в колонке, так и через отдельное ограничение.
- Обрабатывает внешние ключи, объявленные как в колонке, так и через отдельные `ALTER TABLE` команды.
- Представляет результаты в виде структурированных объектов для дальнейшей обработки.

### 📊 Структуры данных (Structures.cs)

Определяет классы для хранения информации о схеме базы данных:
- `TableSchema` — информация о таблице, ее колонках и связях.
- `ColumnSchema` — информация о колонке таблицы, ее типе и ключах.
- `ForeignKeyInfo` — информация о внешних ключах и связях между таблицами.

**Особенности реализации:**
- Каждая структура содержит все необходимые данные для генерации соответствующего кода.
- В `ColumnSchema` включены флаги для определения первичных и внешних ключей, а также допустимости `NULL`-значений.
- В `ForeignKeyInfo` хранится информация о связях между таблицами, включая имя ограничения.

### 📄 Генератор файлов (Generator.cs)

Отвечает за генерацию файлов на основе шаблонов Scriban:
- Обработка всех шаблонов в указанной директории и ее поддиректориях.
- Преобразование имен таблиц и шаблонов в имена выходных файлов.
- Формирование контекста для шаблонизатора с данными о таблицах, колонках и связях.
- Рендеринг шаблонов и сохранение результатов.

**Основные методы:**
- `GenerateOtherFiles`: Обрабатывает все шаблоны для каждой таблицы в схеме базы данных.
- `ProcessTemplatesDirectory`: Обрабатывает директорию шаблонов для конкретной таблицы.
- `MapType`: Преобразует типы SQL в типы C# для использования в шаблонах.

**Особенности реализации:**
- Сохраняет структуру директорий шаблонов в выходных файлах.
- Поддерживает специальные маркеры в именах шаблонов (например, `$table$`) для замены на имя сущности.
- Предоставляет для шаблонов доступ к функциям `map_type` и `to_pascal_case`.
- Обрабатывает шаблоны с расширением `.sbn` особым образом, удаляя это расширение в выходных файлах.

### 🔤 Вспомогательные методы для строк (StringExtensions)

Набор методов для преобразования строк между различными стилями именования:
- `ToPascalCase` — преобразует строку в PascalCase.
- `ToCamelCase` — преобразует строку в camelCase.
- `ToSnakeCase` — преобразует строку в snake_case.

**Особенности реализации:**
- Методы учитывают различные разделители (пробелы, дефисы, подчеркивания).
- Реализованы как методы расширения для удобства использования.

### 🔌 MCP Server (SqlGenerator.Mcp/)

MCP (Model Context Protocol) сервер для интеграции с AI-агентами:
- **Program.cs** — точка входа MCP сервера с stdio транспортом.
- **Tools/SqlGeneratorTools.cs** — MCP инструменты для парсинга и генерации.
- **Prompts/SqlParsingPrompts.cs** — инструкции для агента по парсингу сложного SQL.

**Особенность:** Сложный SQL парсится самим AI-агентом, а не через внешний LLM API.

### 🚀 Основной модуль (Program.cs)

Координирует работу всех компонентов приложения:
- Определение путей к SQL-скрипту, шаблонам и выходным файлам.
- Чтение и парсинг SQL-скрипта.
- Генерация файлов на основе шаблонов для каждой таблицы.

**Особенности реализации:**
- Предоставляет возможность указать путь к выходной директории через аргументы командной строки или интерактивно.
- Проверяет наличие необходимых файлов и директорий перед началом работы.
- Обрабатывает и логирует возможные ошибки.

## 🔌 MCP Server

MCP (Model Context Protocol) сервер позволяет AI-агентам (Claude Desktop, GPT и др.) взаимодействовать с генератором напрямую.

### Сборка и запуск

```bash
# Сборка MCP сервера
cd SqlGenerator.Mcp
dotnet build

# Тестирование с MCP Inspector
npx @anthropic/mcp-inspector dotnet run --project SqlGenerator.Mcp
```

### Принцип экономии токенов

MCP-инструменты принимают ТОЛЬКО пути к файлам, не содержимое. Сервер читает/пишет файлы на диске напрямую. Агенты передают только пути. Это минимизирует расход контекстного окна и предотвращает деградацию качества на больших схемах.

Полная спецификация для интеграции агентов: [`agent-spec-ru.md`](agent-spec-ru.md)

### MCP Tools

| Инструмент | Параметры | Описание |
|------------|-----------|----------|
| `parse_sql` | `sqlFilePath`, `outputPath?` | Regex парсер SQL файла |
| `save_schema` | `schemaFilePath`, `outputPath?` | Валидация и сохранение схемы из JSON файла |
| `generate_files` | `schemaFile`, `outputDir`, `templatesDir?`, `presetName?` | Генерация кода из schema.json |
| `quick_generate` | `sqlFilePath`, `outputDir`, `templatesDir?`, `presetName?` | Парсинг SQL файла + генерация одним вызовом |
| `list_presets` | `templatesDir?` | Список доступных пресетов шаблонов |

### MCP Prompts

| Prompt | Описание |
|--------|----------|
| `sql_parsing_instructions` | Инструкции агенту для парсинга сложного SQL в JSON |

### Сценарии использования

**Простой SQL (regex парсер):**
```
User: "Сгенерируй код из этого SQL"
Agent → quick_generate(sqlFilePath, outputDir, templatesDir)
```

**Сложный SQL (агент парсит сам):**
```
User: "Распарси этот SQL с комментариями"
Agent:
  1. Запрашивает prompt: sql_parsing_instructions(sql)
  2. Парсит SQL самостоятельно по инструкциям
  3. Записывает JSON результат в файл
  4. → save_schema(schemaFilePath, "schema.json")
  5. → generate_files("schema.json", outputDir)
```

### Формат JSON схемы

```json
[
  {
    "TableName": "users",
    "EntityName": "Users",
    "Columns": [
      {"Name": "id", "CSharpType": "int", "IsPrimaryKey": true, "IsForeignKey": false, "IsNullable": false},
      {"Name": "email", "CSharpType": "string", "IsPrimaryKey": false, "IsForeignKey": false, "IsNullable": false}
    ],
    "ForeignKeys": [
      {"ColumnName": "role_id", "CSharpType": "int", "ReferencesTable": "roles", "ReferencesColumn": "id"}
    ]
  }
]
```

### Конфигурация Claude Desktop

Добавить в `claude_desktop_config.json`:

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

Или для скомпилированного бинарника:

```json
{
  "mcpServers": {
    "sql-generator": {
      "command": "D:\\Practice\\sqlGenerator\\SqlGenerator.Mcp\\bin\\Release\\net9.0\\SqlGenerator.Mcp.exe"
    }
  }
}
```

## ⚙️ Параметры и конфигурация

### 📂 Пути к файлам

- `sqlScriptPath` — путь к SQL-скрипту с `CREATE TABLE` командами (по умолчанию `"sql/script.sql"`).
- `templatesDir` — директория с шаблонами Scriban (по умолчанию `"templates"`).
- `resultDir` — директория для сохранения сгенерированных файлов (указывается пользователем, по умолчанию `"result"`).

### 📝 Шаблоны Scriban

Используется синтаксис шаблонизатора Scriban с доступом к следующим параметрам:
- `entity_name` — имя сущности в PascalCase.
- `table_name` — имя таблицы в базе данных.
- `columns` — список колонок таблицы.
- `foreign_keys` — список внешних ключей.
- `primary_key` — информация о первичном ключе.

Также доступны вспомогательные функции:
- `map_type` — для преобразования типов SQL в типы C#.
- `to_pascal_case` — для преобразования строк в PascalCase.

### 📋 Форматирование имен файлов

Для шаблонов с расширением `.sbn` имя выходного файла формируется по шаблону:

{EntityName}{TemplateName}{Extension}

Например, для таблицы `user` и шаблона `Repository.cs.sbn` будет создан файл `UserRepository.cs`.

Возможна замена маркера `$table$` в имени шаблона на имя сущности в PascalCase.

## 🔄 Типы данных

### 📊 Преобразование типов PostgreSQL в C#

Система автоматически преобразует типы данных PostgreSQL в соответствующие типы C#:

| Тип PostgreSQL                                                | Тип C#     |
|---------------------------------------------------------------|------------|
| `integer`, `serial`                                             | `int`      |
| `bigint`, `bigserial`                                           | `long`     |
| `text`, `varchar`, `character varying`                         | `string`   |
| `boolean`                                                     | `bool`     |
| `timestamp`, `timestamp without time zone`, `date`            | `DateTime` |
| `real`                                                        | `float`    |
| `double precision`                                            | `double`   |
| `numeric`, `decimal`                                            | `decimal`  |
| **другие типы**                                               | `object`   |

## 🔍 Примеры использования

### 📄 Пример SQL-скрипта

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

### 📝 Пример шаблона (для интерфейса репозитория)

```csharp
using Application.Models;
using Domain.Entities;

namespace Application.Repositories
{
    public interface I{{ entity_name }}Repository : BaseRepository
    {
        // Базовые CRUD-операции
        Task<List<{{ entity_name }}>> GetAll();
        Task<PaginatedList<{{ entity_name }}>> GetPaginated(int pageSize, int pageNumber);
        Task<int> Add({{ entity_name }} domain);
        Task Update({{ entity_name }} domain);
        Task<{{ entity_name }}> GetOne(int id);
        Task Delete(int id);

        // Методы для внешних ключей
        {{- for fk in foreign_keys }}
        Task<List<{{ entity_name }}>> GetBy{{ fk.column_name | to_pascal_case }}({{ fk.csharp_type }} {{ fk.column_name }});
        {{- end }}
    }
}
```

### 🔧 Пример результата (сгенерированного интерфейса)

```csharp
using Application.Models;
using Domain.Entities;

namespace Application.Repositories
{
    public interface IApplicationStatusRepository : BaseRepository
    {
        // Базовые CRUD-операции
        Task<List<ApplicationStatus>> GetAll();
        Task<PaginatedList<ApplicationStatus>> GetPaginated(int pageSize, int pageNumber);
        Task<int> Add(ApplicationStatus domain);
        Task Update(ApplicationStatus domain);
        Task<ApplicationStatus> GetOne(int id);
        Task Delete(int id);

        // Методы для внешних ключей
        Task<List<ApplicationStatus>> GetByTypeId(int type_id);
    }
}
```

## 🚀 Установка и запуск

1. **Клонируйте репозиторий:**

   ```bash
   git clone <repository_url>
   ```

2. **Подготовьте SQL-скрипт с CREATE TABLE командами в папке sql/script.sql.**

3. **Создайте необходимые шаблоны в папке templates.**

4. **Запустите приложение:**
   ```bash
   dotnet run
   ```
   Или с указанием выходной директории:
   ```bash
   dotnet run -- <output_directory>
   ```

Сгенерированные файлы будут размещены в указанной папке или в папке result по умолчанию.

## 📦 Зависимости

- .NET 9.0
- Scriban (библиотека шаблонизации)

## 🔮 Дальнейшее развитие

- 🌐 Улучшение поддержки различных диалектов SQL.
- 📚 Расширение набора стандартных шаблонов для различных архитектурных подходов.
- ⚙️ Добавление конфигурационного файла для гибкой настройки маппинга типов и параметров генерации.
- 🔍 Поддержка дополнительных SQL-конструкций (индексы, триггеры и т.д.).
- 🔤 Реализация более сложных правил именования и трансформации для различных архитектурных стилей.
- 🌟 Поддержка большего количества фронтенд-фреймворков и библиотек компонентов.
- 🛠️ Интеграция с ORM-фреймворками, такими как Entity Framework или Dapper.
