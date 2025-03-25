# SQL-to-Entity Generator

## Описание проекта

SQL-to-Entity Generator — это инструмент для автоматической генерации кода на основе SQL-скриптов создания таблиц PostgreSQL. Проект позволяет по SQL-схеме базы данных автоматически создавать классы сущностей, репозитории, интерфейсы и другие файлы кода, необходимые для взаимодействия с базой данных.

Инструмент использует шаблонизатор Scriban для генерации файлов на основе настраиваемых шаблонов, что обеспечивает высокую гибкость и возможность адаптации к различным архитектурным подходам.

## Функциональные возможности

- Парсинг SQL-скриптов PostgreSQL с выделением таблиц, колонок и связей
- Автоматическое преобразование типов данных PostgreSQL в типы C#
- Генерация кода на основе настраиваемых шаблонов Scriban
- Поддержка первичных и внешних ключей
- Сохранение структуры директорий шаблонов в результате
- Преобразование имен в PascalCase для соответствия C# стандартам
- Гибкое формирование имен выходных файлов

## Архитектура проекта

Проект разделен на следующие основные модули:

### Парсер SQL-скриптов (`SqlParser.cs`)

Модуль отвечает за:
- Парсинг SQL CREATE TABLE команд с использованием регулярных выражений
- Извлечение информации о таблицах, включая имена, колонки, типы данных
- Определение первичных и внешних ключей
- Преобразование типов PostgreSQL в соответствующие типы C#

```csharp
public static List<TableSchema> ParsePostgresCreateTableScript(string sqlScript)
```

### Структуры данных (`Structures.cs`)

Определяет классы для хранения информации о схеме базы данных:
- `TableSchema` - информация о таблице, ее колонках и связях
- `ColumnSchema` - информация о колонке таблицы, ее типе и ключах
- `ForeignKeyInfo` - информация о внешних ключах и связях между таблицами

### Генератор файлов (`Generator.cs`)

Отвечает за генерацию файлов на основе шаблонов Scriban:
- Обработка всех шаблонов в указанной директории и ее поддиректориях
- Преобразование имен таблиц и шаблонов в имена выходных файлов
- Формирование контекста для шаблонизатора с данными о таблицах, колонках и связях
- Рендеринг шаблонов и сохранение результатов

```csharp
public static void ProcessTemplatesDirectory(string templatesDir, string resultDir, TableSchema table)
```

### Основной модуль (`Program.cs`)

Координирует работу всех компонентов приложения:
1. Определение путей к SQL-скрипту, шаблонам и выходным файлам
2. Чтение и парсинг SQL-скрипта
3. Генерация файлов на основе шаблонов для каждой таблицы

## Параметры и конфигурация

### Пути к файлам
- `sqlScriptPath` - путь к SQL-скрипту с CREATE TABLE командами
- `templatesDir` - директория с шаблонами Scriban
- `resultDir` - директория для сохранения сгенерированных файлов

### Шаблоны Scriban
Используется синтаксис шаблонизатора Scriban с доступом к следующим параметрам:
- `entity_name` - имя сущности в PascalCase
- `table_name` - имя таблицы в базе данных
- `columns` - список колонок таблицы
- `foreign_keys` - список внешних ключей
- `primary_key` - информация о первичном ключе

Также доступны вспомогательные функции:
- `map_type` - для преобразования типов SQL в типы C#
- `to_pascal_case` - для преобразования строк в PascalCase

### Форматирование имен файлов
Для шаблонов с расширением `.sbn` имя выходного файла формируется по шаблону:
`{EntityName}{TemplateName}{Extension}`

Например, для таблицы `user` и шаблона `Repository.cs.sbn` будет создан файл `UserRepository.cs`.

## Примеры использования

### Пример SQL-скрипта
```sql
CREATE TABLE application_status (
    id integer NOT NULL PRIMARY KEY,
    type_id integer REFERENCES status_types (id),
    status_value varchar
);
```

### Пример шаблона (для интерфейса репозитория)
using Application.Models;
using Domain.Entities;
namespace Application.Repositories
{
public interface I{{ entity_name }}Repository : BaseRepository
{
// Базовые CRUD
Task<List<{{ entity_name }}>> GetAll();
Task<PaginatedList<{{ entity_name }}>> GetPaginated(int pageSize, int pageNumber);
Task<int> Add({{ entity_name }} domain);
Task Update({{ entity_name }} domain);
Task<{{ entity_name }}> GetOne(int id);
Task Delete(int id);
// Методы для FK
{{~ for fk in foreign_keys ~}}
Task<List<{{ entity_name }}>> GetBy{{ fk.ColumnName | to_pascal_case }}(object {{ fk.ColumnName }});
{{~ end ~}}
}
}

### Пример результата (сгенерированного интерфейса)
```csharp
using Application.Models;
using Domain.Entities;

namespace Application.Repositories
{
    public interface IApplicationStatusRepository : BaseRepository
    {
        // Базовые CRUD
        Task<List<ApplicationStatus>> GetAll();
        Task<PaginatedList<ApplicationStatus>> GetPaginated(int pageSize, int pageNumber);
        Task<int> Add(ApplicationStatus domain);
        Task Update(ApplicationStatus domain);
        Task<ApplicationStatus> GetOne(int id);
        Task Delete(int id);

        // Методы для FK
        Task<List<ApplicationStatus>> GetByTypeId(object type_id);
    }
}
```

## Установка и запуск

1. Клонируйте репозиторий
2. Подготовьте SQL-скрипт с CREATE TABLE командами в папке `sql/script.sql`
3. Создайте необходимые шаблоны в папке `templates`
4. Запустите приложение:

```bash
dotnet run
```

Сгенерированные файлы будут размещены в папке `result`.

## Зависимости

- .NET 9.0
- Scriban (библиотека шаблонизации)

## Дальнейшее развитие

- Улучшение поддержки различных диалектов SQL
- Расширение набора стандартных шаблонов для различных архитектурных подходов
- Добавление конфигурационного файла для гибкой настройки маппинга типов и параметров генерации
- Поддержка дополнительных SQL-конструкций (индексы, триггеры и т.д.)



