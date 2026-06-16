# Отчёт о дефектах шаблонов sql-generator

> **Источник:** реестр дефектов шаблонов (пресеты `default`/`clean-arch`), найденных при кодогенерации
> в проекте-потребителе **bga-cabinet** (`D:\Repos\new_bga_cabinet`, сессии переноса
> `CODEGEN_TRANSPLANT_PLAN.md` там же). Перенесён сюда (в репо генератора), чтобы при работе над
> утилитой дефекты были сразу видны. Заполняется агентом-потребителем; правки шаблонов/кода генератора —
> компетенция автора утилиты (после ревью). Дублируется в вики `projects/bga-cabinet/notes/sql-generator-template-defects`.
>
> Начато: 2026-06-08. Перенесён в репо генератора: 2026-06-16.

## Легенда серьёзности
- 🔴 ломает сборку / рантайм → требует обхода при transplant
- 🟡 семантический баг (компилится, но ведёт себя неверно)
- 🟢 косметика / стиль

---

## ✅ Статус исправления — пресет `default` v2.0 (2026-06-16, автор утилиты)

Починено в шаблонах/движке генератора, тег **`default-v2.0`**. Сведено к актуальному набору для
текущего `default` (часть старых дефектов реестра в нём уже не воспроизводилась).

| Дефект | Класс | Статус | Что сделано |
|--------|-------|--------|-------------|
| D-01 / D-02(docs) | 🟡/🔴 | ✅ | entity больше НЕ наследует `BaseLogDomain`; snake-поля как есть (нет дубля Pascal/snake). |
| D-09 | 🔴 | ✅ | repo зовёт `FillLogDataHelper` только при наличии audit-колонок (`has_audit` guard). |
| D-22 | 🔴 | ✅ | entity/dto не добавляют `?`, если `CSharpType` уже оканчивается на `?` (нет `Guid??`). |
| D-10 / D-16 / D-3(docs) | 🟡 | ✅ | DTO уважает `IsNullable` как entity (NOT NULL → `T`, nullable → `T?`, nullable string → `string?`); контроллерный маппинг entity↔dto компилируется без `.GetValueOrDefault()`. |
| D-23 | 🔴 | ✅ | имя/тип PK берутся из схемы (`primary_key`), не хардкод `id`/`int`: repo/usecase/dto/controller/irepo (проверено на `revoked_tokens.jti UUID`). |
| D-08 / D-4(docs) | 🔴 | ✅ | новый helper `quote_ident`; весь SQL в repo — verbatim с квотированными идентификаторами (`""User""`, `""id""`). Schema-префикс `public.` НЕ навязывается. |
| D-07 | 🟢 | ✅ | опечатка `GetPagniated` → `GetPaginated` (usecase + controller). |
| D-02 | 🔴 | ✅ | пробелы в `UPDATE SET` корректны (перепроверено после рефакторинга SQL). |
| D-5(docs) | 🔴 | ✅ | regex-парсер предупреждает (`warnings[]` + лог), если распарсено меньше таблиц, чем операторов `CREATE TABLE`. |
| D-12 / F-1 | 🔴 | ✅ | Grid → MUI v7 `size={ { xs, md } }` (без `item`). |
| D-15 / F-2 | 🟡 | ✅ | audit-поля в `constants/*.ts` — опциональны. |
| D-11 | 🟡 | ✅ | `api/*/index.ts` передаёт `orderBy`/`orderType` в URL пагинации (store уже передавал). |
| D-20 | 🟡 | ✅ | числовые не-FK (`decimal`/`double`/`float`/`short`) инициализируются `0` и коэрсятся `Number()`. |
| D-19 | 🟡 | ✅ | два FK на одну таблицу: импорт-алиас/лоадер/`_list` ключуются по колонке (`loadStatusList`/`loadOldStatusList`). |
| D-13 / D-14 | 🟢 | ✅ | убраны неиспользуемые импорты в `base.tsx`; `mtmTabs.tsx` без child-таблиц — минимальный компонент. |
| D-21 / F-3 | 🔴/🟡 | ✅ | генерация каркаса i18n `public/locales/en/{label,message,common}.json` (post-pass движка, humanized-заглушки, идемпотентно). |
| D-17 / D-18 | 🔴 | ☑️ н/в | в текущем `default` «Public<Entity>» не воспроизводится (импорт из `api/<X>`). Закрыто как не актуальное. |
| D-06 | 🟢 | ⏸️ скоуп | роутинг оставлен как есть (проектно-специфичная конвенция). |
| D-03 / D-04 / D-05 | 🔴 | ⏸️ OT-1 | контракт «ambient infra» (`Infrastructure.Data.Models`, `UserSessionHelper`, `IUserRepository`/DI-цикл) — отдельное арх-решение, вынесено из этой версии. |

> Деталь по `quote_ident`: `public.`-префикс намеренно НЕ добавлен (исходные шаблоны его не имели;
> навязывание схемы сломало бы потребителей не в `public`). Квотируется только идентификатор.

---

## D-01 🟡 Конфликт audit-полей: entity наследует BaseLogDomain И дублирует snake_case поля

**Где:** `*/entity/*.cs` (шаблон entity).
**Симптом:** сген-сущность `public class User : BaseLogDomain` объявляет собственные
`id/created_at/updated_at/created_by/updated_by` (snake_case), при этом наследует
`Id/CreatedAt/UpdatedAt/CreatedBy/UpdatedBy` (PascalCase) из `BaseLogDomain`.
**Последствия:**
- C# регистрозависим → НЕ ошибка компиляции (`id` ≠ `Id`), сущность несёт по 2 набора audit-полей.
- Dapper (`MatchNamesWithUnderscores = true`) при `SELECT *` имеет неоднозначность маппинга
  колонки `created_at` → и в `created_at`, и в `CreatedAt`.
- `FillLogDataHelper` пишет PascalCase (`CreatedBy`), а INSERT/UPDATE SQL использует snake_case
  (`@created_by` из `domain.created_by`) → **аудит фактически не персистится** (created_by=NULL).
**Обход при transplant:** для компиляции — ничего (компилится). Для корректного аудита — batch-постобработка
(убрать дубли из entity ИЛИ переписать FillLogDataHelper под snake).
**🔴 РАНТАЙМ-ПОСЛЕДСТВИЕ (выявлено в Фазе 6):** Dapper для параметра `@id` в `UPDATE ... WHERE id = @id`
привязывался к унаследованному Pascal `Id`=0 вместо своего snake `id` → UPDATE затрагивал 0 строк → 500 "Not found".
**✅ РЕШЕНО:** убрано наследование `: BaseLogDomain` у всех сген-сущностей (perl-постобработка) +
`FillLogDataHelper` переписан на рефлексию по snake-полям (`backend/Infrastructure/FillLogData/FillLogDataHelper.cs`).
Бонус: аудит теперь реально персистится (проверено: `updated_by=1`).

## D-02 🔴 UPDATE SQL без пробелов: `SET<col>` и `<col>WHERE`

**Где:** `*/repo/*.cs`, метод `Update` (старые генерации в `db/_codegen/out/`).
**Симптом:** `UPDATE public.user SETcreated_at = ... is_seen_tourWHERE id = @id` — слиплись `SET`+первая
колонка и последняя колонка+`WHERE`.
**Последствия:** рантайм-ошибка SQL.
**Статус:** ✅ **ПОДТВЕРЖДЕНО ПОЧИНЕНО** в обновлённых шаблонах (перегенерация 2026-06-08):
`UPDATE public."user" SET created_at = @created_at, ... WHERE id = @id` — пробелы корректны.

## D-08 🔴 Кавычечные идентификаторы рвут C# verbatim-строку (нет удвоения `"`)

**Где:** `*/repo/*.cs` для таблиц/колонок с кавычками (`"user"`, `"Language"`, `"S_DocumentTemplate"`,
`"S_DocumentTemplateTranslation"`, колонка `"idLanguage"`).
**Симптом:** генератор вставляет имя как есть в `@"..."`:
`var sql = @"UPDATE public."user" SET ...";` — внутри verbatim-строки `"` обязан быть удвоен (`""`),
иначе строка завершается на `public."`, а `user` становится синтаксической ошибкой C#.
**Последствия:** не компилируется (CS) во всех репозиториях 4 кавычечных сущностей.
**Обход (Фаза 4):** batch-постобработка — удвоить `"` внутри SQL-литералов этих репозиториев
(`public."user"` → `public.""user""`). Затрагивает критичную User (нужна для MTM-демо usercompany).
**Первопричина:** шаблон не экранирует кавычки PG-идентификаторов под C# verbatim. Альтернатива на будущее —
интерполированные/обычные строки или хелпер квотинга в шаблоне.

## D-03 🔴 `using Infrastructure.Data.Models;` без содержимого namespace

**Где:** `*/repo/*.cs` (все репозитории импортируют namespace).
**Симптом:** генератор НЕ эмитит типы в `Infrastructure.Data.Models`, но импортирует его → `using`
не резолвится (CS0246), часто при этом неиспользуемый.
**Обход:** заведён placeholder-namespace `Infrastructure/Data/Models/_ModelsNamespaceMarker.cs`.

## D-04 🔴 `UserSessionHelper` вызывается, но не генерируется

**Где:** `*/repo/*.cs`, методы `Add`/`Update`:
`await UserSessionHelper.SetCurrentUserAsync(_userRepository, _dbConnection, _dbTransaction)`.
**Симптом:** класс `UserSessionHelper` шаблоном не создаётся.
**Обход:** написан вручную (реш.№3: резолв юзера из JWT-claim `user_id` через `IHttpContextAccessor`;
переданные DB-параметры в нашей реализации игнорируются).

## D-05 🟡 Каждый repo-конструктор требует `IUserRepository` (для UserSessionHelper)

**Где:** `*/repo/*.cs` — `public XRepository(IDbConnection dbConnection, IUserRepository userRepository)`.
**Симптом:** даже `UserRepository` зависит от `IUserRepository` (саморекурсия); все репо требуют сген
`IUserRepository` для вызова `UserSessionHelper`.
**Последствия:** усложняет проводку в `UnitOfWork`/DI; риск циклической зависимости.
**Обход:** наша `UserSessionHelper` параметр игнорирует.
**🔴 РАНТАЙМ-ПОСЛЕДСТВИЕ (Фаза 6):** регистрация репозиториев в DI (`AddScoped<IXRepository,XRepository>()` из
integration-сниппетов) вызывала **циклическую зависимость** `IUserRepository(UserRepository) -> IUserRepository`
→ падение валидации DI на старте.
**✅ РЕШЕНО:** репозитории НЕ регистрируем в DI (их создаёт `UnitOfWork` через `new`); в DI только use-case'ы
(`GeneratedModulesRegistration.cs`). Use-case'ы зависят только от `IUnitOfWork`.

## D-06 🟢 Контроллер без версионирования и нестандартный роут

**Где:** `*/controller/*.cs` — `[Route("[controller]")]`, экшены `[Route("GetAll")]` и т.п.
**Симптом:** не совпадает с конвенцией scaffold `api/v{version:apiVersion}/...`.
**Последствия:** косметика/консистентность API; компилируется.

## D-09 🔴 Repo зовёт FillLogDataHelper для сущностей без audit-полей (не наследующих BaseLogDomain)

**Где:** `*/repo/*.cs` метод `Add`/`Update` для таблиц БЕЗ audit-колонок (created_at/...): 8 сущностей —
ApplicationPaidInvoice, ApplicationPayerRequisite, CommonSetting, DocumentTemplate, FileSign,
PayerRequisite, UserCheck, Usercompany.
**Симптом:** entity-шаблон НЕ добавляет `: BaseLogDomain` (нет audit-колонок), но repo всё равно зовёт
`FillLogDataHelper.FillLogDataCreate(model, userId)` (требует `BaseLogDomain`) → CS1503.
**Обход (Фаза 4):** добавлен `: BaseLogDomain` этим 8 сущностям (фантомные audit-поля, не персистятся —
INSERT не включает audit-колонки). Альтернатива: убрать вызов FillLogData из их repo.

## D-10 🔴 Контроллер маппит all-nullable DTO → entity с non-nullable value-полями

**Где:** `*/controller/*.cs` Create/Update: `field = requestDto.field` где DTO-поле `int?`/`DateTime?`/`bool?`/`double?`,
а entity-поле (NOT NULL колонка) — `int`/`DateTime`/... → CS0266 (122 ошибки).
**Нюанс:** в `UpdateXRequest` поле `id` НЕ nullable (`public int id`), остальные value-поля nullable —
несимметрично.
**Обход (Фаза 4):** для NOT NULL value-полей в контроллерах добавлен `.GetValueOrDefault()`
(скрипт `fix_controllers_nullable.py` по validated-схеме); для `id` (уже non-null в DTO) — не добавлять.

## D-11 🟡 ListView store шлёт сортировку, api её не передаёт на сервер

**Где:** `*/<Entity>ListView/store.tsx` + `api/<Entity>/index.ts`. Store зовёт пагинацию с
`orderBy/orderType`, но api-функция их в URL не подставляет (UI шлёт `sortingMode="server"`).
**Последствия:** серверная сортировка мёртвая (компилируется, но не работает).

## D-12 🟡 Сген-вьюхи используют устаревший `Grid item xs/md` (несовместимо с MUI v7 Grid v2)

**Где:** `*/<Entity>AddEditView/base.tsx`, `index.tsx`. В MUI v7 у `Grid` удалён API `item/xs/md` → TS2769.
**Обход (Фаза 5):** импорт `@mui/material/GridLegacy`. Кандидат на обновление шаблона под `size={{ md, xs }}`.

## D-13 🟢 Мусорные неиспользуемые импорты в `base.tsx`

**Где:** `*/<Entity>AddEditView/base.tsx` — Button, Container, Typography, Stack, LookUp, InfoIcon,
MainStore, useState, useNavigate, useLocation импортируются, но не используются.
**Последствия:** проходит только при `noUnusedLocals: false`; при строгом tsconfig — падение сборки.

## D-14 🟢 `mtmTabs.tsx` для сущности без MTM генерит пустой компонент с неиспользуемыми хелперами

**Где:** `*/<Entity>AddEditView/mtmTabs.tsx` — возвращает `null`, но объявляет неиспользуемые
value/setValue/handleChange/a11yProps/CustomTabPanel. Аналогично D-13 — спасает нестрогий tsconfig.

## D-15 🟡 TS-тип сущности требует audit-поля, форма их не заполняет

**Где:** `constants/<Entity>.ts` — created_at/updated_at/created_by/updated_by как required, но
форма/стор не заполняют → пришлось ослаблять параметры api до `Partial<>`. Шаблон: сделать audit-поля
опциональными в типе либо отдельный create/update-DTO.

## D-16 🟡 DTO с non-nullable string-полями → ложная ASP.NET-валидация «field is required»

**Где:** `*/dto/*.cs` (`CreateXRequest`/`UpdateXRequest`) — поля nullable-колонок объявлены как `string`
(non-nullable reference type). При включённом nullable-контексте ASP.NET ModelBinding трактует их как `[Required]`.
**Симптом (Фаза 6):** POST `/Customer/Create` с пустым телом → 400 с требованием 14 полей
(pin/okpo/ugns/nomer/address/director/last_name/first_name/reg_number/postal_code/second_name/passport_*/...),
хотя в БД эти колонки NULLABLE. Аналогично `/User/Create` (pin/guid/email/second_name/password_hash/password_salt).
**Последствия:** нельзя создать запись без заполнения всех string-полей, даже опциональных.
**Кандидат на фикс шаблона:** string-поля nullable-колонок генерить как `string?` в DTO.

## D-17 🔴 FK-lookup ссылается на несуществующий `api/Public<Entity>` / `getPublic<Entity>s`

**Где:** `*/<Entity>AddEditView/store.tsx` (и base.tsx) сущностей с FK. Для подгрузки списков связанных
сущностей store импортирует `getPublic<X>s` из `api/Public<X>` и зовёт `loadPublic<X>List` — таких
API-модулей генератор НЕ создаёт (есть только `api/<X>` с `get<X>s`).
**Последствия:** не резолвится импорт → не компилируется.
**Обход (перенос фронта):** снять инфикс `Public` → маппинг на реально сгенерированный API той же сущности
(`api/<X>`, `get<X>s`, `load<X>List`). Детерминированно, эндпоинты существуют.

## D-18 🔴 Незаменённый плейсхолдер: кавычки и неверный кейс в TS-идентификаторах FK-lookup

**Где:** FK-lookup на кавычечные/case-sensitive сущности — генерит `getPublic"user"s`, `api/Public"language"`,
`"sDocumenttemplate"` (буквальные кавычки + ломаный кейс) → невалидный TS.
**Обход:** заменить на корректные PascalCase-сущности (User/Language/SDocumentTemplate) до снятия `Public` (D-17).

## D-19 🟡 Два FK на одну таблицу → дублирующиеся импорты и одноимённые лоадеры

**Где:** напр. ApplicationStatusHistory (status_id + old_status_id → оба на ApplicationStatus). Store получает
дублирующийся импорт и два метода `loadApplicationStatusList` (коллизия имён).
**Обход:** убрать дубль импорта, второй лоадер переименовать (`loadOldApplicationStatusList`, грузит `old_status_list`).

## D-20 🟡 Числовые НЕ-FK поля хранятся как string и не приводятся к number

**Где:** `*/<Entity>AddEditView/store.tsx` — деньги/координаты (`total_sum`, `sum`, `summa`, `x_coord`, `y_coord`)
инициализированы `""`, но в `constants/<Entity>.ts` типизированы `number`; в data-объект идут без приведения
(генератор коэрсит только FK `*_id` через `- 0`).
**Обход:** обернуть в `Number(this.<field>)` перед отправкой.

## D-07 🟢 Опечатка `GetPagniated` (вместо `GetPaginated`)

**Где:** `*/usecase/*.cs` и `*/controller/*.cs` — метод пагинации назван `GetPagniated`.
**Симптом:** опечатка консистентна (usecase+controller) → компилируется, но торчит в публичном API.

## D-21 🔴 Не генерируются словари i18n → формы показывают сами ключи

**Где:** весь frontend. Сген-формы массово зовут `translate("label:<View>.<field>")`, `translate("label:<View>.entityTitle")`,
`i18n.t("message:...")`, `i18n.t("common:...")` (всего **838** label-ключей по 57 фичам), но генератор
**не эмитит** соответствующие JSON-словари `public/locales/<lng>/{label,message,common}.json`.
**Симптом:** i18next по умолчанию рендерит сам ключ при отсутствии перевода → каждая форма показывает
технические пути вместо подписей (напр. заголовок списка = `UserCheckListView.entityTitle`, колонки = `username`/`code_sent_at`).
**Усугубляющий фактор:** источника человекочитаемых подписей в схеме нет (`COMMENT ON COLUMN` = 0) —
автоперевод из метаданных БД невозможен.
**Обход (на стороне проекта):** постпроцессор `frontend/scripts/generate-locales.mjs` — сканирует `src`,
собирает все ключи переводов, строит каркас словарей `ru-RU`/`ky-KG`. Идемпотентен (не затирает существующие
переводы). Значения: известные UI-термины → русский; поля → humanized-заглушка (имя поля латиницей);
полноценный перевод и язык `ky-KG` — вручную.
**Кандидат на фикс шаблона:** добавить в пресет `default` генерацию каркаса словарей локализации
(`label.json` с ключами `<View>.<field>` + `entityTitle`, плюс `message`/`common`) на язык(и) проекта.
После этого проектный постпроцессор станет избыточным.

---

> Сессия 2026-06-16 (первичная миграция auth-таблиц `0001_registration_auth.up`, пресет `default`).
> Пере-встречены и применены те же обходы: D-01 (убрано `: BaseLogDomain`), D-05 (вырезан `IUserRepository`
> из репо, репо — только через UnitOfWork), D-12 (Grid → MUI7 `size={{ md, xs }}`), D-15 (audit-поля
> в `constants/*` опциональны), D-19 (FileSign: 2 FK→customer, разведены лоадеры). Новые дефекты:

## D-22 🔴 Двойной nullable `DateTime??`/`Guid??` когда тип схемы уже nullable

**Где:** `*/entity/*.cs`, `*/dto/*.cs` (шаблоны entity/dto).
**Симптом:** если во входной `schema.json` `CSharpType` уже содержит `?` (например, ручная правка
TIMESTAMPTZ→`DateTime?`, UUID→`Guid?`), entity/dto-шаблон **дополнительно** добавляет `?` к non-PK
nullable-колонкам → `public DateTime?? created_at` / `public Guid?? guid` → невалидный C# (CS).
**Последствия:** не компилируется во всех сущностях/DTO с такими полями.
**Обход (Фаза 1):** во входной схеме давать БАЗОВЫЙ тип без `?` (`DateTime`/`Guid`); nullable шаблон
добавит сам. PK-колонки шаблон не делает nullable (видно по `Guid jti`).
**Кандидат на фикс шаблона:** не добавлять `?`, если `CSharpType` уже оканчивается на `?`.

## D-23 🔴 PK-колонка с именем ≠ `id` (напр. `revoked_tokens.jti UUID`) ломает сген-CRUD

**Где:** `*/repo/*.cs`, `*/usecase/*.cs`, `*/dto/*.cs`, `*/controller/*.cs` таблиц, чей PK не называется `id`.
**Симптом (на `revoked_tokens`, PK=`jti UUID`):**
- repo `GetOneByID(int id)`/`Delete(int id)` → `WHERE id = @Id`, `Add` → `RETURNING id`: колонки `id` нет → рантайм-ошибка SQL;
- usecase `Create` делает `domain.id = result;` → CS1061 (у сущности нет `id`);
- DTO мапит `jti` как nullable (`Guid?`), а entity PK — non-null (`Guid`) → CS0266 в контроллере.
**Последствия:** не компилируется (usecase/controller) + рантайм-сломанный CRUD.
**Обход (Фаза 4/5):** для чисто-рантайм токен-таблиц (`revoked_tokens`/`refresh_token`/`user_check`)
сген-CRUD удалён, логика — хэндрайт-порт. Для разовой компиляции: убрать `domain.id=result`, PK→nullable в entity.
**Кандидат на фикс шаблона:** определять имя PK-колонки из схемы (`IsPrimaryKey`), а не хардкодить `id`.
