# Дефекты пресета `default` и парсера — отчёт потребителя (bga-cabinet, 2026-06-16)

> **Контекст.** Найдено при генерации модуля «Работа с документами» проекта bga-cabinet
> (12 таблиц, пресет `default`, запуск через MCP: `save_schema` → `generate_files`).
> Потребитель шаблоны/утилиту НЕ правит — обходит на своей стороне; этот отчёт для
> владельца утилиты, чтобы починить в **самом генераторе/шаблонах**.
> Зеркальный (consumer-side) лог с обходами: `LLMWiki/projects/bga-cabinet/notes/sql-generator-template-defects.md` (#3–#5).

Среда: .NET 9, Dapper, PostgreSQL. Сущности наследуют доменный `BaseLogDomain` (Pascal-аудит),
аудит в репозиториях заполняется хелпером по snake-полям.

> **✅ ИСПРАВЛЕНО автором утилиты в пресете `default` v2.0 (тег `default-v2.0`, 2026-06-16):**
> D-2 (снято `: BaseLogDomain`), D-3 (DTO уважает `IsNullable`), D-4 (helper `quote_ident` + verbatim SQL
> с квотированными идентификаторами; `public.`-префикс не навязывается), D-5 (warning при тихой
> потере таблиц), F-1 (Grid `size={ { } }` MUI v7), F-2 (audit опциональны в `constants/*`),
> F-3 (генерация каркаса i18n `public/locales/en/*`). D-1 (пробелы UPDATE) перепроверено — корректно.
> Полная карта статусов: `SQL_GENERATOR_TEMPLATE_DEFECTS.md` → раздел «Статус исправления v2.0».

---

## D-1 (проверка) — пробелы в UPDATE-SQL: НЕ воспроизводится (кандидат в «исправлено»)

Ранее (2026-06-05) сообщалось: `UPDATE ... SET<col>` и `= @<col>WHERE` без пробелов.
В текущем шаблоне `*Repository.cs` сген корректен:
```csharp
var sql = @"UPDATE s_document_template SET name = @name, ... background_color = @background_color WHERE id = @id";
```
**Действие владельцу:** подтвердить, что фикс внесён, и закрыть прежний дефект.

---

## D-2 — Сущность одновременно наследует `BaseLogDomain` И дублирует snake-поля `id`/audit

**Где:** шаблон entity (`*/entity/<Entity>.cs`).

**Симптом (сген):**
```csharp
public class SDocumentTemplate : BaseLogDomain   // BaseLogDomain даёт Pascal Id, CreatedAt, ...
{
    public int id { get; set; }                  // ...и тут же snake-дубликат
    public DateTime? created_at { get; set; }
    ...
}
```
**Корень.** Когда в схеме присутствуют колонки `id`/`created_at`/`created_by`/`updated_at`/`updated_by`,
шаблон эмитит их как snake-свойства И добавляет `: BaseLogDomain` (Pascal-аналоги). Возникает пара
`Id` (Pascal, из базы) + `id` (snake). Dapper при резолве параметра `@id` в `UPDATE ... WHERE id = @id`
видит два кандидата и связывает с `Id` (= 0 по умолчанию) → **UPDATE затрагивает 0 строк** (тихий баг рантайма).

**Репро:** любая таблица с `id` + audit-колонками, пресет `default`.

**Предлагаемая правка утилиты (одно из):**
1. Не эмитить `: BaseLogDomain`, если audit/`id`-колонки уже выводятся как поля сущности; **или**
2. при наследовании `BaseLogDomain` — НЕ эмитить дублирующие snake-`id`/audit-поля (и тогда репозиторий
   должен мапить snake-колонки на Pascal-свойства: `created_at = @CreatedAt`, `WHERE id = @Id`);
3. сделать поведение управляемым флагом пресета (`baseClass` / `emitAuditFields`).

**Обход потребителя:** batch-удаление ` : BaseLogDomain` из сгена.

---

## D-3 — DTO делает все поля nullable, контроллер не компилируется (CS0266)

**Где:** шаблоны `*/dto/<Entity>dto.cs` (Create/Update) + `*/controller/<Entity>Controller.cs`.

**Симптом.** DTO эмитит каждое поле как nullable, даже для `NOT NULL`-колонок:
```csharp
public class CreateSDocumentTemplateRequest { public int? id_document_type { get; set; } ... }
```
а сущность уважает `NOT NULL` (`public int id_document_type`). В контроллере:
```csharp
var request = new SDocumentTemplate { id_document_type = requestDto.id_document_type, ... };
// error CS0266: Cannot implicitly convert 'int?' to 'int'
```
**Корень.** DTO-шаблон не учитывает `IsNullable` колонки (делает всё `T?`), а entity-шаблон — учитывает.
Рассинхрон типов на присваивании `entity = dto`.

**Масштаб:** 22 ошибки компиляции на модуле из 12 таблиц (9 `NOT NULL` FK).

**Предлагаемая правка утилиты:** DTO должен уважать `IsNullable` так же, как entity (тот же маппинг
типов), **или** контроллер должен кастовать/проверять (`.Value`/валидация) для non-nullable полей.
Консистентность entity↔dto — предпочтительнее.

**Обход потребителя:** batch-перевод `NOT NULL` FK-полей сущности в `T?`.

---

## D-4 — Имя таблицы в SQL без кавычек + смешанные строковые литералы (нельзя задать регистрозависимое имя)

**Где:** шаблон `*/repo/<Entity>Repository.cs`.

**Симптом.** Имя таблицы подставляется «как есть», без кавычек и без schema-префикса:
```csharp
var sql = "SELECT * FROM s_document_template WHERE id = @Id LIMIT 1";   // обычный литерал
var sql = @"INSERT INTO s_document_template(...) VALUES (...)";          // verbatim литерал
```
Для регистрозависимого имени (`"S_DocumentTemplate"`) это даёт рассинхрон: PostgreSQL свернёт
unquoted-идентификатор в lowercase → `relation "s_documenttemplate" does not exist` в рантайме.
Попытка обойти, задав `TableName` уже в кавычках, **ломает компиляцию**, потому что шаблон вставляет
значение в оба типа литералов без экранирования:
```csharp
var sql = "SELECT * FROM "S_DocumentTemplate"";        // обычный: неэкранированные кавычки -> CS
var sql = @"INSERT INTO "S_DocumentTemplate"(...)";     // verbatim: требуется "" -> тоже CS
```
**Корень.** (а) идентификаторы таблиц не квотируются; (б) смешение обычных и verbatim строк не даёт
единой стратегии экранирования.

**Предлагаемая правка утилиты:**
1. Квотировать идентификаторы единообразно: `public.""{TableName}""` во **всех** SQL и использовать
   verbatim-строки везде (как было в более ранней версии шаблона — там встречалось `public.""Language""`); **или**
2. флаг пресета «quote identifiers» (вкл/выкл) для проектов с регистрозависимыми именами.

**Обход потребителя:** физ. имена таблиц заданы lowercase snake_case (тогда unquoted-SQL корректен).
Раньше не всплывало — все таблицы прежнего модуля уже были lowercase.

---

## D-5 — `parse_sql` (regex) тихо пропускает таблицы с кавычечными именами

**Где:** regex-парсер DDL (`Parser.cs` / MCP `parse_sql`).

**Симптом.** На файле с 12 `CREATE TABLE`, где 10 имён в кавычках (`"S_DocumentTemplate"`, `"Language"`…)
и 2 без, парсер вернул **только 2** таблицы (`document_metadata`, `org_structure_templates`) — кавычечные
**молча пропущены**, без ошибки/предупреждения.
```
parse_sql(...) -> {"success":true,"tableCount":2,"tables":["DocumentMetadata","OrgStructureTemplates"]}
```
**Риск.** Тихая потеря данных: `success:true` создаёт ложное впечатление полного разбора.

**Предлагаемая правка утилиты:**
1. Поддержать кавычечные/регистрозависимые идентификаторы таблиц и колонок в regex-парсере; **или**
2. как минимум — **предупреждать** о пропущенных `CREATE TABLE` (число найденных vs число операторов в файле),
   чтобы потребитель видел расхождение.

**Обход потребителя:** schema JSON авторён вручную и подан через `save_schema` (минуя regex-парсер).

---

## FRONTEND-дефекты пресета `default` (перенос фронта bga-cabinet, 2026-06-16)

> Найдено при переносе сген-фронта (api/constants/*View, 12 сущностей) в целевой проект:
> React 19, MUI **7**, Vite, MobX, i18next. Зеркальный consumer-лог: вики bga-cabinet (#3-5 + frontend).
> Контракт сген-фронта ↔ сген-бэка **совпадает** (snake_case, `/Create`,`/GetAll`,`/GetOneById?id=`) — это плюс, не дефект.

### F-1 — Сген-формы используют устаревший Grid API (MUI v5/v6), ломаются на MUI v7

**Где:** шаблоны `*/AddEditView/{base,index}.tsx`, `*/ListView` — везде `<Grid container>` и `<Grid item xs={12} md={6}>`.

**Симптом.** На MUI v7 у `Grid` больше нет пропов `item`/`xs`/`md` (новый Grid использует `size={{ md: 6 }}`,
без `item`). Компиляция падает: **TS2769 «No overload matches this call» — 101 ошибка** на модуле из 12 сущностей.

**Предлагаемая правка утилиты (одно из):**
1. Эмитить новый Grid API MUI v7: `<Grid size={{ xs: 12, md: 6 }}>` без `item`; **или**
2. импортировать legacy-вариант: `import { GridLegacy as Grid } from '@mui/material'`; **или**
3. сделать целевую версию MUI параметром пресета.

**Обход потребителя:** batch-замена импорта на `GridLegacy as Grid` (поведение старого API сохранено, usaги не трогаются).

### F-2 — Тип `constants/<Entity>.ts` требует audit-поля, а сген-store их не передаёт

**Где:** шаблоны `constants/<Entity>.ts` (тип) и `*/AddEditView/store.tsx`.

**Симптом.** Тип объявляет `created_at: string; created_by: number; updated_at: string; updated_by: number;`
как **обязательные**, но сген-store в `create/update` собирает объект без них:
```ts
// store.tsx
const data = { id, name, svg_path, used_tables };  // нет audit-полей
createCustomSvgIcon(data);  // TS2345: missing created_at, created_by, updated_at, updated_by
```
**TS2345 — 24 ошибки** на модуле. Несогласованность тип↔store внутри одного пресета.

**Предлагаемая правка утилиты:** audit-поля в TS-типе сделать опциональными (`created_at?: string` …),
т.к. их проставляет бэк (FillLogDataHelper), фронт их не шлёт; **или** store должен включать их в payload.

**Обход потребителя:** batch — audit-поля в `constants/*.ts` → optional.

### F-3 — i18n-словари не генерируются (повтор backend-дефекта #2, frontend-сторона)

Сген-формы массово ссылаются на `translate("label:<View>.<field>")` / `t("message:…")` / `t("common:…")`,
но JSON-словари (`public/locales/<lng>/{label,message,common}.json`) пресетом НЕ создаются → i18next рендерит
сами ключи. На модуле документов: **172 label + ~6 message + 3 common** ключа без словарей.
**Правка для утилиты:** см. backend-дефект #2 — генерировать каркас словарей в пресете.
**Обход потребителя:** скрипт-постпроцессор `frontend/scripts/generate-locales.mjs` (сканирует src, строит каркас).

## Резюме для приоритезации

| # | Класс | Серьёзность | Тип |
|---|-------|-------------|-----|
| D-2 | entity: BaseLogDomain + дубль snake-id | высокая (тихий рантайм-баг UPDATE) | шаблон (backend) |
| D-3 | dto nullable vs entity NOT NULL | высокая (не компилируется) | шаблон (backend) |
| D-5 | parse_sql молча теряет кавычечные таблицы | высокая (тихая потеря данных) | парсер |
| F-1 | Grid item/xs/md (MUI v5/v6) на MUI v7 | высокая (не компилируется, 101 ошибка) | шаблон (frontend) |
| F-2 | constants-тип требует audit, store не шлёт | высокая (не компилируется, 24 ошибки) | шаблон (frontend) |
| D-4 | имена таблиц без кавычек / смешанные литералы | средняя (обходится lowercase) | шаблон (backend) |
| F-3 | i18n-словари не генерируются | средняя (формы кажут ключи) | шаблон (frontend, = #2) |
| D-1 | пробелы в UPDATE | — (похоже исправлено) | проверка |

Все воспроизводятся на пресете `default`. Артефакты генерации потребителя:
`D:\Repos\new_bga_cabinet-docmodule\db\_codegen\out` (срез 2026-06-16).
