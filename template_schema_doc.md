# 📌 СХЕМА ШАБЛОНИЗАЦИИ SQL FILE GENERATOR
## ОБЯЗАТЕЛЬНЫЙ ДОКУМЕНТ - НЕ ИЗМЕНЯТЬ БЕЗ КРАЙНЕЙ НЕОБХОДИМОСТИ!

---

## ⚠️ КРИТИЧЕСКИ ВАЖНО
**ЭТА СХЕМА ФИКСИРОВАНА И НЕ ДОЛЖНА МЕНЯТЬСЯ!**  
Любые изменения в этой схеме ломают ВСЕ существующие шаблоны!

---

## 1. ДОСТУПНЫЕ ПЕРЕМЕННЫЕ В ШАБЛОНАХ

### 1.1 Основные переменные (всегда доступны)
```liquid
{{ entity_name }}     - Имя сущности в PascalCase (например: "EmployeeSavedFilters")
{{ table_name }}      - Имя таблицы в БД как есть (например: "employee_saved_filters")
```

### 1.2 Массив columns[] - ВСЕ колонки таблицы
Каждый элемент массива содержит ОБА варианта именования:

#### PascalCase версии (для существующих шаблонов):
```liquid
{{ column.Name }}           - имя колонки как в БД (string)
{{ column.CSharpType }}     - тип C# (string)
{{ column.IsPrimaryKey }}   - флаг первичного ключа (bool)
{{ column.IsForeignKey }}   - флаг внешнего ключа (bool) 
{{ column.IsNullable }}     - флаг nullable (bool)
{{ column.IsSystem }}       - флаг системной колонки (bool)
```

#### snake_case версии (для новых шаблонов):
```liquid
{{ column.name }}           - имя колонки как в БД (string)
{{ column.csharp_type }}    - тип C# (string)
{{ column.is_primary_key }} - флаг первичного ключа (bool)
{{ column.is_foreign_key }} - флаг внешнего ключа (bool)
{{ column.is_nullable }}    - флаг nullable (bool)
{{ column.is_system }}      - флаг системной колонки (bool)
```

### 1.3 Массив editable_columns[] - колонки БЕЗ системных полей
Исключены: `id`, `created_at`, `updated_at`, `created_by`, `updated_by`
Структура элементов такая же как в columns[]

### 1.4 Массив foreign_keys[] - информация о внешних ключах
Только snake_case версия:
```liquid
{{ fk.column_name }}        - имя колонки с FK (string)
{{ fk.csharp_type }}        - тип C# (string)
{{ fk.references_table }}   - имя связанной таблицы (string)
{{ fk.references_column }}  - имя связанной колонки (string)
{{ fk.constraint_name }}    - имя ограничения (string)
```

### 1.5 Массив virtual_foreign_keys[] — виртуальные внешние ключи
Структура идентична `foreign_keys[]`. Содержит FK, выведенные из конвенций именования колонок (`*_id`, `id_*`, `id*`).
Включён по умолчанию (пустой массив при `--no-virtual-fks`).
```liquid
{{ vfk.column_name }}        - имя колонки (string)
{{ vfk.csharp_type }}        - тип C# (string)
{{ vfk.references_table }}   - имя выведенной связанной таблицы (string)
{{ vfk.references_column }}  - имя связанной колонки, всегда "id" (string)
{{ vfk.constraint_name }}    - всегда null (нет реального ограничения)
```

### 1.6 Массив virtual_all_tables[] — все таблицы с объединёнными FK
Структура идентична `all_tables[]`. Поле `foreign_keys` содержит объединённые real + virtual FK.
Включён по умолчанию (пустой массив при `--no-virtual-fks`).
```liquid
{{ t.entity_name }}     - PascalCase имя сущности (string)
{{ t.table_name }}      - имя таблицы (string)
{{ t.foreign_keys }}    - массив объединённых real + virtual FK
```

### 1.7 Объект primary_key (может быть null)
Поддерживает ОБА варианта:
```liquid
{{ primary_key.Name }}       - имя колонки PK (PascalCase версия)
{{ primary_key.CSharpType }} - тип C# (PascalCase версия)
{{ primary_key.name }}       - имя колонки PK (snake_case версия)
{{ primary_key.csharp_type }} - тип C# (snake_case версия)
```

---

## 2. ДОСТУПНЫЕ ФУНКЦИИ-ФИЛЬТРЫ

```liquid
{{ value | to_pascal_case }}              - преобразует в PascalCase
{{ value | to_camel_case }}               - преобразует в camelCase
{{ value | to_snake_case }}               - преобразует в snake_case
{{ value | map_type }}                    - маппинг типов SQL в C#
{{ value | string.replace "old" "new" }}  - замена строк
{{ value | remove_id_suffix }}            - удаляет суффикс "_id"
```

---

## 3. СИСТЕМНЫЕ КОЛОНКИ
Эти колонки автоматически помечаются как системные:
- `id`
- `created_at`
- `updated_at`
- `created_by`
- `updated_by`

Используйте `editable_columns` чтобы их исключить из генерации.

---

## 4. ТИПЫ ДАННЫХ C# (CSharpType/csharp_type)
Возможные значения:
- `"int"` - для integer, serial
- `"long"` - для bigint, bigserial
- `"string"` - для text, varchar, character varying
- `"bool"` - для boolean
- `"DateTime"` - для timestamp, date
- `"float"` - для real
- `"double"` - для double precision
- `"decimal"` - для numeric, decimal
- `"object"` - для всех остальных

---

## 5. ПРИМЕРЫ ИСПОЛЬЗОВАНИЯ

### Пример 1: Итерация по всем колонкам (старый стиль)
```liquid
{{~ for column in columns ~}}
{{~ if column.Name != "id" ~}}
    public {{ column.CSharpType }}? {{ column.Name }} { get; set; }
{{~ end ~}}
{{~ end ~}}
```

### Пример 2: Итерация по редактируемым колонкам (новый стиль)
```liquid
{{~ for column in editable_columns ~}}
    public {{ column.csharp_type }}? {{ column.name }} { get; set; }
{{~ end ~}}
```

### Пример 3: Работа с foreign keys
```liquid
{{~ for fk in foreign_keys ~}}
    Task<List<{{ entity_name }}>> GetBy{{ fk.column_name | to_pascal_case | remove_id_suffix }}({{ fk.csharp_type }} {{ fk.column_name }});
{{~ end ~}}
```

---

## 6. ПРАВИЛА СОВМЕСТИМОСТИ

### ✅ МОЖНО:
- Использовать ЛЮБОЙ из двух стилей (PascalCase или snake_case) в columns[]
- Использовать ЛЮБОЙ из двух стилей в primary_key
- Добавлять НОВЫЕ фильтры-функции
- Создавать новые шаблоны с любым стилем

### ❌ НЕЛЬЗЯ:
- Удалять поддержку PascalCase версий
- Изменять имена существующих переменных
- Изменять структуру данных
- Удалять поддержку старого стиля

---

## 7. КРИТИЧЕСКИЕ ЗАМЕЧАНИЯ

1. **НИКОГДА** не меняйте эту схему без обновления ВСЕХ шаблонов
2. **ВСЕГДА** поддерживайте обратную совместимость
3. **ВСЕГДА** тестируйте на существующих шаблонах после любых изменений
4. При добавлении новых полей - добавляйте ОБА варианта именования

---

## ВЕРСИЯ СХЕМЫ: 2.0 (ФИНАЛЬНАЯ)
**Дата фиксации:** 2024
**Статус:** ЗАМОРОЖЕНА - изменения запрещены!