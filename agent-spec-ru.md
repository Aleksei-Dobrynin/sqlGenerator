# sql-generator MCP Server — Спецификация для агентов

## Идентификация сервера

- **Имя:** `sql-generator`
- **Транспорт:** stdio
- **Команда:** `dotnet run --project <path>/SqlGenerator.Mcp`

## Принцип экономии токенов

Все инструменты принимают ТОЛЬКО пути к файлам, не содержимое. MCP-сервер читает/пишет файлы на диске напрямую. Агенты должны:

- Передавать инструментам пути к файлам, не содержимое
- Не читать файлы в контекст только для передачи в инструмент
- Использовать операции копирования/перемещения файлов вместо загрузки содержимого в контекст
- Записывать промежуточные результаты (например, распарсенный JSON) на диск, затем передавать путь

Этот дизайн поддерживает генерацию бойлерплейта целых приложений (потенциально тысячи файлов) без деградации контекстного окна.

## Инструменты

### ParseSql

Парсинг PostgreSQL SQL файла с помощью regex. Быстрый, работает для простого DDL.

| Параметр | Обязательный | Описание |
|----------|-------------|----------|
| `sqlFilePath` | да | Путь к `.sql` файлу с `CREATE TABLE` |
| `outputPath` | нет | Путь для schema JSON (по умолчанию: `schema.json`) |

**Возвращает:** `{ success, tableCount, tables[], schemaFile }`

**Ограничения:** Regex-парсер. Может не справиться со сложным DDL с комментариями внутри определений колонок или нестандартным синтаксисом. Для сложного SQL используйте workflow с парсингом агентом.

### SaveSchema

Валидация и сохранение JSON-файла схемы, распарсенной агентом.

| Параметр | Обязательный | Описание |
|----------|-------------|----------|
| `schemaFilePath` | да | Путь к JSON файлу с массивом таблиц |
| `outputPath` | нет | Путь для валидированной схемы (по умолчанию: `schema.json`) |

**Возвращает:** `{ success, tableCount, tables[], schemaFile }`

### GenerateFiles

Генерация файлов кода из JSON-схемы по шаблонам.

| Параметр | Обязательный | Описание |
|----------|-------------|----------|
| `schemaFile` | да | Путь к JSON файлу схемы |
| `outputDir` | да | Директория для сгенерированных файлов |
| `templatesDir` | нет | Директория шаблонов (по умолчанию: `templates`) |
| `presetName` | нет | Имя пресета шаблонов (вызовите `ListPresets` сначала) |

**Возвращает:** `{ success, outputDir, tableCount, fileCount }`

### QuickGenerate

Парсинг SQL файла + генерация файлов одним вызовом. Только для простого DDL.

| Параметр | Обязательный | Описание |
|----------|-------------|----------|
| `sqlFilePath` | да | Путь к `.sql` файлу |
| `outputDir` | да | Директория вывода |
| `templatesDir` | нет | Директория шаблонов (по умолчанию: `templates`) |
| `presetName` | нет | Имя пресета шаблонов |

**Возвращает:** `{ success, outputDir, tableCount, fileCount }`

### ListPresets

Список доступных пресетов шаблонов.

| Параметр | Обязательный | Описание |
|----------|-------------|----------|
| `templatesDir` | нет | Базовая директория шаблонов (по умолчанию: `templates`) |

**Возвращает:** `{ success, presetMode, presets[] }`

## Промпты

### sql_parsing_instructions

Возвращает детальные инструкции для агента по парсингу сложного SQL в формат JSON-схемы. Включает таблицу маппинга типов и спецификацию формата вывода.

| Параметр | Обязательный | Описание |
|----------|-------------|----------|
| `sql` | нет | SQL для включения в промпт для парсинга |

## Сценарии работы

### Простой DDL (один шаг)

```
Agent -> QuickGenerate(sqlFilePath, outputDir, presetName)
```

### Простой DDL (два шага)

```
Agent -> ParseSql(sqlFilePath) -> GenerateFiles(schemaFile, outputDir, presetName)
```

### Сложный DDL (агент парсит сам)

```
1. Агент получает промпт: sql_parsing_instructions(sql)
2. Агент читает SQL файл и парсит его по инструкциям
3. Агент записывает JSON результат в файл на диске
4. Agent -> SaveSchema(schemaFilePath, outputPath)
5. Agent -> GenerateFiles(schemaFile, outputDir, presetName)
```

## Формат JSON схемы

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
    ]
  }
]
```

## Маппинг типов (PostgreSQL -> C#)

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

## Конфигурация Claude Desktop

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
