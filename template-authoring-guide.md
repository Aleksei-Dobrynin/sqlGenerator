# Template Authoring Guide

Инструкция для AI-агента по созданию новых шаблонов (presets) для SQL-to-Entity генератора.

## 1. Архитектура системы шаблонов

### Что такое preset

Preset -- это набор шаблонов в подпапке `templates/<preset-name>/`. Каждый preset генерирует полный набор файлов для одной сущности: entity, DTO, repository, controller, UI-компоненты и т.д.

### Структура каталогов

```
templates/
└── <preset-name>/
    └── $table$/
        ├── <layer1>/
        │   └── $table$<Suffix>.<ext>.sbn
        ├── <layer2>/
        │   └── <Prefix>$table$<Suffix>.<ext>.sbn
        └── $table$<ViewName>/
            ├── index.tsx.sbn
            └── store.tsx.sbn
```

### Правила именования

| Элемент | Правило | Пример |
|---------|---------|--------|
| Директория preset-а | любое имя | `templates/clean-arch/` |
| `$table$` в пути | Заменяется на `EntityName` (PascalCase) | `$table$/` → `UserProfile/` |
| `$table$` в имени файла | Заменяется на `EntityName` | `$table$Controller.cs.sbn` → `UserProfileController.cs` |
| Расширение `.sbn` | Scriban-шаблон; расширение удаляется на выходе | `store.tsx.sbn` → `store.tsx` |
| Файлы без `.sbn` | Копируются as-is, без обработки | `config.json` → `config.json` |

**Важно:** `$table$` можно использовать несколько раз в одном пути: `$table$/$table$ListView/` → `UserProfile/UserProfileListView/`.

---

## 2. Доступные переменные

### Основные переменные

| Переменная | Тип | Описание |
|---|---|---|
| `entity_name` | `string` | PascalCase имя сущности (`UserProfile`) |
| `table_name` | `string` | Оригинальное имя таблицы из SQL (`user_profiles`) |
| `columns` | `array` | Все колонки таблицы, включая системные |
| `editable_columns` | `array` | Колонки без системных полей |
| `foreign_keys` | `array` | Внешние ключи таблицы |
| `primary_key` | `object` или `null` | Первичный ключ (null если не найден) |
| `all_tables` | `array` | Все таблицы схемы (для cross-table логики) |
| `virtual_foreign_keys` | `array` | Виртуальные FK, выведенные из именования колонок (включены по умолчанию, отключение: `--no-virtual-fks`) |
| `virtual_all_tables` | `array` | Все таблицы с объединёнными real + virtual FK |

### Системные колонки (исключены из `editable_columns`)

```
id, created_at, updated_at, created_by, updated_by
```

### Структура объекта Column

Каждый объект в `columns` и `editable_columns` имеет два набора ключей (оба доступны):

```
PascalCase          snake_case
─────────────       ──────────────
Name                name              # имя колонки (PascalCase)
CSharpType          csharp_type       # C# тип: string, int, long, bool, DateTime, decimal, ...
IsPrimaryKey        is_primary_key    # true/false
IsForeignKey        is_foreign_key    # true/false
IsNullable          is_nullable       # true/false
IsSystem            is_system         # true/false (системная колонка)
```

### Структура объекта ForeignKey

```
column_name         # имя FK-колонки в текущей таблице (organization_id)
csharp_type         # C# тип FK (обычно int)
references_table    # имя целевой таблицы (organizations)
references_column   # имя целевой колонки (id)
constraint_name     # имя constraint из БД
```

### Структура объекта PrimaryKey

```
Name / name             # имя PK-колонки
CSharpType / csharp_type # тип PK
```

### Структура объекта в `all_tables`

```
entity_name     # PascalCase имя сущности
table_name      # оригинальное имя таблицы
foreign_keys    # массив FK этой таблицы
```

---

## 3. Функции-фильтры

### Пользовательские (определены в генераторе)

| Фильтр | Вход | Выход | Использование |
|--------|------|-------|---------------|
| `to_pascal_case` | `user_profile` | `UserProfile` | `{{ name \| to_pascal_case }}` |
| `to_camel_case` | `user_profile` | `userProfile` | `{{ name \| to_camel_case }}` |
| `to_snake_case` | `UserProfile` | `user_profile` | `{{ name \| to_snake_case }}` |
| `remove_id_suffix` | `organization_id` | `organization` | `{{ fk.column_name \| remove_id_suffix }}` |

### Встроенные Scriban (часто используемые)

| Фильтр | Описание | Пример |
|--------|----------|--------|
| `string.replace` | Замена подстроки | `{{ name \| string.replace "_id" "" }}` |
| `string.downcase` | В нижний регистр | `{{ name \| string.downcase }}` |
| `string.upcase` | В верхний регистр | `{{ name \| string.upcase }}` |
| `array.add` | Добавить элемент в массив | `items = items \| array.add item` |
| `array.size` | Размер массива | `{{ items \| array.size }}` |

### Цепочки фильтров

Фильтры можно комбинировать:

```scriban
{{ fk.column_name | string.replace "_id" "" | to_snake_case }}
{{ fk.references_table | to_pascal_case }}
{{ fk.column_name | to_pascal_case | string.replace "_id" "" }}
```

---

## 4. Синтаксис Scriban

### Интерполяция

```scriban
{{ entity_name }}
{{ entity_name | to_camel_case }}
```

### Условия

```scriban
{{~ if column.is_foreign_key ~}}
  ...FK logic...
{{~ else if column.csharp_type == "bool" ~}}
  ...bool logic...
{{~ else if column.csharp_type == "DateTime" ~}}
  ...date logic...
{{~ else ~}}
  ...default...
{{~ end ~}}
```

### Циклы

```scriban
{{~ for column in editable_columns ~}}
  {{ column.name }}: {{ column.csharp_type }}
{{~ end ~}}
```

### Последний элемент в цикле (для разделителей)

```scriban
{{~ for column in columns ~}}
  {{ column.Name }}{{~ if !for.last ~}}, {{~ end ~}}
{{~ end ~}}
```

### Присвоение переменных

```scriban
{{~ my_list = [] ~}}
{{~ has_feature = false ~}}
{{~ for item in source ~}}
  {{~ if condition ~}}
    {{~ has_feature = true ~}}
    {{~ my_list = my_list | array.add item ~}}
  {{~ end ~}}
{{~ end ~}}
```

### Whitespace control

`~` у скобок удаляет пробелы/переносы строк с соответствующей стороны:

```scriban
{{~ expression ~}}    # убрать пробелы с обеих сторон
{{ expression ~}}     # убрать только справа
{{~ expression }}     # убрать только слева
{{ expression }}      # сохранить пробелы
```

**Правило:** используй `~` почти всегда в управляющих конструкциях (`if`, `for`, `end`), чтобы избежать лишних пустых строк в выходном файле.

---

## 5. Паттерны шаблонов

### 5.1. Type-aware rendering (маппинг по типу)

Выбор компонента, валидации или инициализации на основе `csharp_type`:

```scriban
{{~ if column.csharp_type == "string" ~}}
  ...строковая логика...
{{~ else if column.csharp_type == "int" || column.csharp_type == "long" ~}}
  ...числовая логика...
{{~ else if column.csharp_type == "bool" ~}}
  ...булевая логика...
{{~ else if column.csharp_type == "DateTime" ~}}
  ...дата...
{{~ else if column.csharp_type == "decimal" ~}}
  ...десятичные...
{{~ else ~}}
  ...fallback...
{{~ end ~}}
```

**Типичный маппинг:**

| csharp_type | UI компонент | Валидация (Yup) | Инициализация |
|-------------|-------------|-----------------|---------------|
| `string` | TextField | `yup.string()` | `""` |
| `int`, `long` | TextField (number) | `yup.number()` | `0` |
| `bool` | Checkbox | `yup.boolean()` | `false` |
| `DateTime` | DateField | `yup.date()` | `null` |
| `decimal` | TextField (number) | `yup.number()` | `0` |

### 5.2. FK-aware rendering

Поля с `is_foreign_key == true` обрабатываются особо:

```scriban
{{~ if column.is_foreign_key ~}}
  {/* Lookup / Select для FK */}
  <LookUp
    value={store.{{ column.name }}}
    data={store.{{ column.name | string.replace "_id" "" | to_snake_case }}_list}
    ...
  />
{{~ end ~}}
```

**Паттерн именования FK-ресурсов:**

| Исходное | Преобразование | Результат |
|----------|---------------|-----------|
| `organization_id` | `string.replace "_id" ""` | `organization` |
| список для LookUp | `+ "_list"` | `organization_list` |
| метод загрузки | `load + to_pascal_case` | `loadOrganizationList()` |
| API-вызов | `get + to_snake_case + "s"` | `get_organizations()` |

### 5.3. Master-detail (parent-child)

Поиск дочерних таблиц через `all_tables`:

```scriban
{{~
  child_tables = []
  for other_table in all_tables
    for fk in other_table.foreign_keys
      if fk.references_table == table_name
        child_tables = child_tables | array.add other_table
      end
    end
  end
~}}
{{~ if child_tables.size > 0 ~}}
  // У этой таблицы есть дочерние записи
  {{~ for child in child_tables ~}}
    // Дочерняя таблица: {{ child.entity_name }}
  {{~ end ~}}
{{~ end ~}}
```

### 5.4. Detect audit fields

Проверка наличия audit-полей для условного наследования:

```scriban
{{~
  has_audit_fields = false
  for column in columns
    if column.Name == "created_at" || column.Name == "updated_at" || column.Name == "created_by" || column.Name == "updated_by"
      has_audit_fields = true
      break
    end
  end
~}}
{{~ if has_audit_fields ~}}
  public class {{ entity_name }} : BaseLogDomain
{{~ else ~}}
  public class {{ entity_name }}
{{~ end ~}}
```

### 5.5. Фильтрация системных колонок вручную

Если нужен контроль тоньше, чем `editable_columns`:

```scriban
{{~ for column in columns ~}}
{{~ if column.name != "id" && column.name != "created_at" && column.name != "updated_at" && column.name != "created_by" && column.name != "updated_by" ~}}
  ...
{{~ end ~}}
{{~ end ~}}
```

### 5.6. Nullable types

```scriban
{{~ if column.IsNullable && !column.IsPrimaryKey && column.CSharpType != "string" ~}}
  public {{ column.CSharpType }}? {{ column.Name }} { get; set; }
{{~ else ~}}
  public {{ column.CSharpType }} {{ column.Name }} { get; set; }
{{~ end ~}}
```

### 5.7. Генерация SQL в шаблонах

INSERT с перечислением колонок через `for.last`:

```scriban
INSERT INTO {{ table_name }}(
  {{~ for column in columns ~}}
  {{~ if column.Name != "id" ~}}
    {{ column.Name }}{{~ if !for.last ~}}, {{~ end ~}}
  {{~ end ~}}
  {{~ end ~}}
)
VALUES (
  {{~ for column in columns ~}}
  {{~ if column.Name != "id" ~}}
    @{{ column.Name }}{{~ if !for.last ~}}, {{~ end ~}}
  {{~ end ~}}
  {{~ end ~}}
)
```

### 5.8. Генерация методов для каждого FK

```scriban
{{~ for fk in foreign_keys ~}}
  public async Task<List<{{ entity_name }}>> GetBy{{ fk.column_name | to_pascal_case | string.replace "_id" "" }}({{ fk.csharp_type }} {{ fk.column_name }})
  {
    var sql = "SELECT * FROM {{ table_name }} WHERE {{ fk.column_name }} = @{{ fk.column_name }}";
    ...
  }
{{~ end ~}}
```

### 5.9. Virtual FK usage

Виртуальные FK включены по умолчанию (отключение: `--no-virtual-fks`). Структура идентична `foreign_keys`:

```scriban
{{~ for vfk in virtual_foreign_keys ~}}
  // Виртуальная связь: {{ vfk.column_name }} → {{ vfk.references_table }}.{{ vfk.references_column }}
{{~ end ~}}
```

Для обратного поиска (virtual master-detail) через `virtual_all_tables`:

```scriban
{{~
  virtual_child_tables = []
  for t in virtual_all_tables
    if t.table_name != table_name
      for fk in t.foreign_keys
        if fk.references_table == table_name
          virtual_child_tables = virtual_child_tables | array.add t
          break
        end
      end
    end
  end
~}}
```

### 5.10. Self-referencing FK detection

Определение, является ли FK ссылкой на parent-форму (для вложенных CRUD):

```scriban
{{~ for fk in foreign_keys ~}}
{{~ if fk.column_name == (table_name | to_snake_case) + "_id" ~}}
  // Это self-reference или parent FK
{{~ else ~}}
  // Это FK-справочник, нужен LookUp
{{~ end ~}}
{{~ end ~}}
```

---

## 6. Алгоритм создания нового preset-а

### Шаг 1. Анализ проекта-образца

Изучи архитектуру проекта, для которого создаются шаблоны:

1. Определи слои/layers (entity, repository, service/usecase, controller, DTO, UI views)
2. Найди файлы, которые повторяются для каждой сущности
3. Выяви паттерны именования (PascalCase, camelCase, snake_case)
4. Определи используемые фреймворки и библиотеки

### Шаг 2. Выбор образцовых файлов

Для каждого типа повторяющегося файла выбери один конкретный пример. Бери сущность средней сложности -- с FK, nullable полями, но без уникальных особенностей.

### Шаг 3. Проектирование структуры каталогов

Создай дерево каталогов с `$table$` плейсхолдерами. Структура должна отражать целевую архитектуру проекта:

```
templates/<preset-name>/
└── $table$/
    ├── <layer1>/<files>.sbn
    ├── <layer2>/<files>.sbn
    └── ...
```

### Шаг 4. Преобразование кода в шаблоны

Для каждого файла-образца:

1. **Заменить имя сущности** на `{{ entity_name }}` (PascalCase) или `{{ entity_name | to_camel_case }}` (camelCase)
2. **Заменить имя таблицы** на `{{ table_name }}`
3. **Заменить перечисление полей** на цикл:
   - Все поля → `{{~ for col in columns ~}}`
   - Только редактируемые → `{{~ for col in editable_columns ~}}`
4. **Добавить type-mapping** для типо-зависимой логики (UI компоненты, валидация, инициализация)
5. **Обработать FK** — условная логика для `is_foreign_key`
6. **Обработать nullable** — условная логика для `is_nullable`
7. **Обработать PK** — исключить из INSERT, использовать в WHERE
8. **Добавить master-detail** если нужно (через `all_tables`)

### Шаг 5. Проверка

Мысленно прогони шаблон на 3 разных таблицах:
- Простая таблица (3-4 поля, без FK)
- Таблица с FK (2-3 внешних ключа)
- Таблица с nullable полями и разными типами

Убедись, что:
- Нет хардкода имён сущностей или таблиц
- Разделители (запятые) корректны с `for.last`
- Whitespace control (`~`) не ломает форматирование
- Системные колонки корректно фильтруются
- Nullable типы обрабатываются

---

## 7. Правила качества

### Обязательно

- Использовать `editable_columns` для форм ввода и валидации
- Использовать `columns` для entity-классов, SQL-запросов, полного маппинга
- Проверять `is_nullable` при генерации типов (`Type?`) и валидации
- Проверять `is_foreign_key` для LookUp/Select компонентов
- Использовать `for.last` для разделителей между элементами
- Применять `~` в управляющих конструкциях для чистого вывода
- Всё через переменные: никакого хардкода имён таблиц/сущностей

### Запрещено

- Хардкодить имена таблиц или сущностей в шаблонах
- Предполагать наличие FK или конкретных колонок -- всегда проверять через циклы/условия
- Использовать `columns` вместо `editable_columns` для пользовательских форм
- Забывать обработку `null` для `primary_key` (может быть `null`)
- Игнорировать `is_nullable` при генерации типов

---

## 8. Пример: минимальный preset

```
templates/minimal/
└── $table$/
    ├── $table$.cs.sbn              # Entity class
    ├── I$table$Repository.cs.sbn   # Repository interface
    └── $table$Repository.cs.sbn    # Repository implementation
```

**`$table$.cs.sbn`:**

```scriban
namespace Domain.Entities
{
    public class {{ entity_name }}
    {
{{~ for column in columns ~}}
        {{~ if column.IsNullable && !column.IsPrimaryKey && column.CSharpType != "string" ~}}
        public {{ column.CSharpType }}? {{ column.Name }} { get; set; }
        {{~ else ~}}
        public {{ column.CSharpType }} {{ column.Name }} { get; set; }
        {{~ end ~}}
{{~ end ~}}
    }
}
```

**`I$table$Repository.cs.sbn`:**

```scriban
using Domain.Entities;

namespace Application.Repositories
{
    public interface I{{ entity_name }}Repository
    {
        Task<List<{{ entity_name }}>> GetAll();
        Task<{{ entity_name }}> GetOneByID(int id);
        Task<int> Add({{ entity_name }} entity);
        Task Update({{ entity_name }} entity);
        Task Delete(int id);
{{~ for fk in foreign_keys ~}}
        Task<List<{{ entity_name }}>> GetBy{{ fk.column_name | to_pascal_case | string.replace "_id" "" }}({{ fk.csharp_type }} {{ fk.column_name }});
{{~ end ~}}
    }
}
```
