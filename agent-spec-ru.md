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
| `includeVirtualFks` | нет | Включить виртуальные FK из конвенций именования (по умолчанию: `true`) |

**Возвращает:** `{ success, tableCount, tables[], schemaFile }`

**Ограничения:** Regex-парсер. Корректно обрабатывает SQL-комментарии (`-- ...`, `/* ... */`), multi-word типы (`timestamp with time zone`, `double precision`, `character varying`), quoted/schema-qualified идентификаторы (`"User"`, `public."User"`) и первичные ключи, объявленные inline, table-level (`PRIMARY KEY (...)`) или через `ALTER TABLE ... ADD ... PRIMARY KEY`. Всё ещё ограничен в продвинутых DDL: партиционирование, INHERITS, exclusion constraints, expression-индексы. Для сложного SQL используйте workflow с парсингом агентом.

### SaveSchema

Валидация и сохранение JSON-файла схемы, распарсенной агентом.

| Параметр | Обязательный | Описание |
|----------|-------------|----------|
| `schemaFilePath` | да | Путь к JSON файлу с массивом таблиц |
| `outputPath` | нет | Путь для валидированной схемы (по умолчанию: `schema.json`) |

**Возвращает:** `{ success, tableCount, tables[], schemaFile, warnings[]? }`

**Валидация:** запускается `LlmResponseValidator` на исходном JSON. Возвращает `success: false` с описанием ошибки при критических проблемах (пустой TableName, нет PK, дублирующиеся имена колонок, FK-колонка отсутствует в таблице, невалидный C#-тип). Нефатальные замечания (EntityName не в PascalCase, нестандартный тип, FK ссылается вне result set) попадают в `warnings[]`.

### GenerateFiles

Генерация файлов кода из JSON-схемы по шаблонам.

| Параметр | Обязательный | Описание |
|----------|-------------|----------|
| `schemaFile` | да | Путь к JSON файлу схемы |
| `outputDir` | да | Директория для сгенерированных файлов |
| `templatesDir` | нет | Директория шаблонов (по умолчанию: `templates`) |
| `presetName` | нет | Имя пресета шаблонов (вызовите `ListPresets` сначала) |
| `includeVirtualFks` | нет | Включить виртуальные FK из конвенций именования (по умолчанию: `true`) |

**Возвращает:** `{ success, outputDir, tableCount, fileCount }`

### QuickGenerate

Парсинг SQL файла + генерация файлов одним вызовом. Только для простого DDL.

| Параметр | Обязательный | Описание |
|----------|-------------|----------|
| `sqlFilePath` | да | Путь к `.sql` файлу |
| `outputDir` | да | Директория вывода |
| `templatesDir` | нет | Директория шаблонов (по умолчанию: `templates`) |
| `presetName` | нет | Имя пресета шаблонов |
| `includeVirtualFks` | нет | Включить виртуальные FK из конвенций именования (по умолчанию: `true`) |

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

### Batch-transplant (предпочтительный режим для full-module scaffolding)

Для генерации целого модуля / множества таблиц с встраиванием в реальный проект — это
**рекомендуемый по умолчанию** режим. Агент работает с утилитой **как с MCP-сервером** и **парсит
схему сам** (сценарий «агент парсит» выше), а не через встроенные парсеры: regex-парсер спотыкается
на боевых pg_dump (quoted-идентификаторы, table-level / `ALTER` PRIMARY KEY), поэтому нетривиальный
DDL разбирает агент.

Пять фаз (компилятор/тулчейн как оракул ошибок):

```
[предусловие: инфра-базис заведён в проекте]
1. Generate (batch)    агент парсит -> SaveSchema -> GenerateFiles в scratch (не в дерево проекта)
2. Bulk transplant     одна скриптовая операция: папка_генератора -> слой проекта
3. Diagnostics oracle  один прогон build + typecheck/build + lint -> ПОЛНЫЙ список ошибок
4. Batch-fix           чинить классами, каскадные -> частные; rebuild до зелёного
5. Verify              CRUD / runtime к живой базе
```

Полный пошаговый гайд (источник истины): [`runbooks/batch-transplant-workflow.md`](runbooks/batch-transplant-workflow.md).
Детали фаз 3–5: [`docs/batch/`](docs/batch/).

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

**Примечание:** `VirtualForeignKeys` содержит связи, выведенные из конвенций именования колонок (`*_id`, `id_*`, `id*`), когда нет явного `REFERENCES`. Заполняется при `includeVirtualFks: true`.

**Идемпотентность:** `VirtualForeignKeyResolver` идемпотентен. Если агент уже заполнил `VirtualForeignKeys` по инструкции из `sql_parsing_instructions`, резолвер только дополняет недостающие записи (например, cross-chunk случаи для LLM-парсера) и не создаёт дубликаты. Шаблоны полагаются на уникальность в этом массиве — дубликаты сгенерируют дублирующиеся свойства `DisplayName` и сломают компиляцию.

**Системные поля:** SaaS-шаблоны (пресет clean-arch) ожидают конкретные имена колонок с семантическим значением. Всегда сохраняйте их:

| Колонка | Назначение |
|---------|------------|
| `tenant_id` (int, NOT NULL) | Multi-tenancy. Исключается из create/update DTO. |
| `is_active` (bool, NOT NULL) | Флаг активности. Активирует методы `Activate/Deactivate()` на сущности. |
| `is_deleted` (bool, NOT NULL) | Флаг soft-delete. Активирует `ISoftDeletable` + метод `Delete()`. |
| `created_at`, `updated_at` (timestamptz, NOT NULL) | Audit timestamps. Должны быть `DateTimeOffset`, не `DateTime`. |
| `created_by`, `updated_by` (int, nullable) | Audit user IDs. |
| `deleted_at` (timestamptz), `deleted_by` (int) | Audit soft-delete. Опционально, парные с `is_deleted`. |

Агент должен включать эти поля в массив `Columns` с корректными типами/nullability, если они есть в SQL. Шаблоны определяют их по имени и включают audit/soft-delete/tenant-логику только при их наличии.

## Маппинг типов (PostgreSQL -> C#)

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
