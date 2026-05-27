# План: Исправление шаблонов clean-arch

## Контекст

Шаблоны `templates/clean-arch/` должны соответствовать бойлерплейту `D:\Repos\boilerplate_v1`. После рефакторинга бойлерплейта изменились импорт-пути фронтенда (теперь всё через `@ui-kit`), а в генераторе добавилась поддержка виртуальных FK. Аудит выявил **3 категории проблем** в 27 шаблонах.

---

## Категория 1: Фронтенд — неверные импорт-пути (КРИТИЧНО)

Бойлерплейт использует `@ui-kit` с композитными компонентами (`SelectField`, `SwitchField`, `DataTable`, `FormDialog`, `ListPageHeader`, `SearchBar`). Шаблоны импортируют напрямую из `@mui/material` и `@components/` — это сломанные пути.

### 1.1 `Frontend/$table$AddEditView/base.tsx.sbn`
**Проблемы:**
- `import CustomTextField from '@components/TextField'` → нет такого пути
- `import DateField from '@components/DateField'` → нет такого пути
- `import Grid from '@mui/material/Grid'` → должен быть из `@ui-kit`
- Raw MUI `FormControl/Select/MenuItem` → должен быть `SelectField` из `@ui-kit`
- Raw MUI `FormControlLabel/Switch` → должен быть `SwitchField` из `@ui-kit`

**Исправления:**
- Заменить блок импортов на единый из `@ui-kit`:
  ```tsx
  import { Grid, TextField, SelectField, DateField, SwitchField } from '@ui-kit';
  ```
- Переписать FK-рендеринг с raw `Select` на `SelectField` (props: `name`, `label`, `value`, `options`, `onChange`, `emptyLabel?`, `error?`, `dataCy`)
- Переписать bool-рендеринг с raw `Switch` на `SwitchField`
- Заменить `CustomTextField` → `TextField` по всему файлу

### 1.2 `Frontend/$table$AddEditView/index.tsx.sbn`
- `import { Box, Button, Paper, Typography } from '@mui/material'` → `import { Box, MuiButton, Paper, Typography } from '@ui-kit'`
- Заменить `Button` → `MuiButton` в JSX (cancel/save кнопки)

### 1.3 `Frontend/$table$AddEditView/popupForm.tsx.sbn`
- Raw MUI `Dialog/DialogTitle/DialogContent/DialogActions/Button/CircularProgress` → `FormDialog` из `@ui-kit`
- Переписать JSX-обёртку на `<FormDialog open onClose title onSave isSaving ...>`

### 1.4 `Frontend/$table$ListView/index.tsx.sbn`
- Raw MUI `Table/TableBody/TableCell/TableHead/TableRow/TablePagination` → `DataTable` из `@ui-kit`
- Raw MUI layout → `ListPageHeader`, `SearchBar` из `@ui-kit`
- Raw icon imports → из `@ui-kit`
- FK фильтры: raw `Select` → `SelectField` из `@ui-kit`

### 1.5 `Frontend/$table$AddEditView/mtmTabs.tsx.sbn`
- `import { Box, Tab, Tabs } from '@mui/material'` → `import { Box, Tab, Tabs } from '@ui-kit'`

### 1.6 Файлы без проблем с путями (проверено):
- `valid.ts.sbn` — `@/helpers/ValidationHelper` ✅ (совпадает с бойлерплейтом)
- `store.ts.sbn` (AddEdit) — `@constants/DataModels/Organization/CompanyTypes` для `IRefOption` ✅
- Все `Api/*.ts.sbn` — `@api/https`, `@constants/DataModels/...` ✅

---

## Категория 2: Виртуальные FK не обрабатываются (ВАЖНО)

Генератор передаёт в шаблоны `virtual_foreign_keys` и `virtual_all_tables`. Часть шаблонов их игнорирует — колонки виртуальных FK (`is_foreign_key=false` на ColumnSchema) не получают Select-дропдауны, DisplayName-свойства, валидацию.

**Шаблоны, где virtual FK УЖЕ работают:** ✅
- `$table$ListFilter.cs.sbn`, `I$table$Repository.cs.sbn`, `$table$Repository.cs.sbn`, `mtmTabs.tsx.sbn`

**Подход к исправлению:** В начале каждого шаблона строить набор имён VFK-колонок:
```scriban
{{~ vfk_col_names = []
  for vfk in virtual_foreign_keys
    vfk_col_names = vfk_col_names | array.add (vfk.column_name | to_pascal_case)
  end ~}}
```
Затем в циклах по колонкам добавлять проверку `(vfk_col_names | array.contains col.name)` параллельно с `col.is_foreign_key`, либо добавлять отдельные циклы по `virtual_foreign_keys`.

### 2.1 Backend — DTOs
| Файл | Проблема |
|---|---|
| `$table$DetailDto.cs.sbn` | `DisplayName` генерируется только для `col.is_foreign_key` — VFK-колонки не получают DisplayName |
| `$table$ListDto.cs.sbn` | То же самое |

**Исправление:** После цикла по `dto_columns` добавить блок для виртуальных FK:
```scriban
{{~ for vfk in virtual_foreign_keys ~}}
    public string? {{ vfk.column_name | to_pascal_case | string.replace "Id" "" }}DisplayName { get; set; }
{{~ end ~}}
```

### 2.2 Backend — Domain
| Файл | Проблема |
|---|---|
| `Domain/$table$.cs.sbn` | `ValidateInvariants()` проверяет `> 0` только для `col.is_foreign_key` |

**Исправление:** Добавить цикл по `virtual_foreign_keys` с поиском соответствующей колонки для проверки nullable.

### 2.3 Backend — Validators
| Файл | Проблема |
|---|---|
| `Create$table$Validator.cs.sbn` | `GreaterThan(0)` только для explicit FK |
| `Update$table$Validator.cs.sbn` | То же самое |

**Исправление:** Добавить цикл по `virtual_foreign_keys` после основного цикла, с проверкой nullable для правила `GreaterThan(0)`.

### 2.4 Frontend — Types
| Файл | Проблема |
|---|---|
| `Types/$table$Types.ts.sbn` | `IListDto` не содержит `DisplayName` для VFK-колонок |

**Исправление:** Добавить VFK DisplayName поля в `IListDto`.

### 2.5 Frontend — Формы и стор
| Файл | Проблема |
|---|---|
| `base.tsx.sbn` | VFK-колонки рендерятся как текстовые поля, а не Select |
| `store.ts.sbn` (AddEdit) | Нет `Refs[]` массивов, нет загрузки refs для VFK |
| `valid.ts.sbn` | Нет `checkId` валидации для VFK-колонок |

**Исправление:** Использовать `vfk_col_names` набор для расширения условий, либо добавить отдельные циклы по `virtual_foreign_keys` после explicit FK блоков.

### 2.6 Frontend — ListView
| Файл | Проблема |
|---|---|
| `ListView/index.tsx.sbn` | Нет фильтр-дропдаунов для VFK |
| `ListView/store.ts.sbn` | Нет filter state / refs / set*Filter методов для VFK |
| `Api/useGet$table$s.ts.sbn` | Нет VFK params в API-вызове |

**Исправление:** Добавить циклы по `virtual_foreign_keys` параллельно с explicit FK циклами.

---

## Категория 3: Отсутствуют Lookup-методы (ВАЖНО)

Бойлерплейт имеет `Lookup` эндпоинты для подгрузки справочных данных в дропдауны. Шаблоны их не генерируют.

### 3.1 Новый файл: `Backend/DTOs/$table$LookupDto.cs.sbn`
```csharp
namespace Template_v1.Application.DTOs;

public class {{ entity_name }}LookupDto
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}
```

### 3.2 `$table$Controller.cs.sbn` — добавить эндпоинт
```csharp
[HttpGet("lookup")]
[RequirePermission("{{ route_name }}.read")]
public async Task<IActionResult> Lookup([FromQuery] string? search)
    => Ok(await _service.Lookup(search, TenantId));
```

### 3.3 `I$table$Service.cs.sbn` — добавить метод
```csharp
Task<IEnumerable<{{ entity_name }}LookupDto>> Lookup(string? search, int tenantId);
```

### 3.4 `$table$Service.cs.sbn` — добавить реализацию
Стандартный паттерн: `BeginAsync` → `_repo.Lookup(search, tenantId, _uow)` → `RollbackAsync`.

### 3.5 `I$table$Repository.cs.sbn` — добавить метод
```csharp
Task<IEnumerable<{{ entity_name }}LookupDto>> Lookup(string? search, int tenantId, IUnitOfWork uow);
```

### 3.6 `$table$Repository.cs.sbn` — добавить реализацию
SQL с `WHERE tenant_id = @TenantId` + опциональный `ILIKE` поиск + `LIMIT 50`. TODO для поля DisplayName.

### 3.7 Новый файл: `Frontend/Api/useGet$table$Lookup.ts.sbn`
```typescript
export const get{{ entity_name }}Lookup = async (search?: string): Promise<I{{ entity_name }}LookupDto[]> => { ... };
```

### 3.8 `Types/$table$Types.ts.sbn` — добавить интерфейс
```typescript
export interface I{{ entity_name }}LookupDto {
  id: number;
  displayName: string;
}
```

---

## Порядок реализации

### Фаза 1: Импорт-пути фронтенда (5 файлов)
1. `base.tsx.sbn` — самый сложный, полная переработка импортов и FK/bool рендеринга
2. `index.tsx.sbn` (AddEdit) — замена импортов + `Button` → `MuiButton`
3. `popupForm.tsx.sbn` — замена на `FormDialog`
4. `ListView/index.tsx.sbn` — замена на `DataTable`/`ListPageHeader`/`SearchBar`
5. `mtmTabs.tsx.sbn` — замена источника импорта

### Фаза 2: Виртуальные FK (12 файлов)
6. Backend DTOs: `DetailDto`, `ListDto`
7. Backend Domain: `$table$.cs`
8. Backend Validators: `Create`, `Update`
9. Frontend Types: `$table$Types.ts`
10. Frontend store: `store.ts` (AddEdit) + `store.ts` (ListView)
11. Frontend form: `base.tsx` (VFK рендеринг) + `valid.ts`
12. Frontend API: `useGet$table$s.ts`
13. Frontend ListView: `index.tsx` (VFK фильтры)

### Фаза 3: Lookup-методы (8 файлов, 2 новых)
14. Создать `$table$LookupDto.cs.sbn`
15. Обновить `I$table$Repository`, `$table$Repository`
16. Обновить `I$table$Service`, `$table$Service`
17. Обновить `$table$Controller`
18. Создать `useGet$table$Lookup.ts.sbn`
19. Обновить `$table$Types.ts`

---

## Верификация

1. `dotnet build` — проект должен компилироваться
2. Тестовый прогон генерации на `sql/script.sql`:
   ```bash
   dotnet run -- --output ./test_output
   ```
3. Проверить сгенерированные файлы в `test_output/`:
   - Все фронтенд-импорты используют `@ui-kit`, нет `@components/` или прямых `@mui/material`
   - VFK-колонки имеют DisplayName в DTOs, SelectField в формах, фильтры в ListView
   - Lookup DTO/Controller/Service/Repository сгенерированы
4. Удалить `test_output/` после проверки

## Критичные файлы для модификации
- `templates/clean-arch/$table$/Frontend/$table$AddEditView/base.tsx.sbn`
- `templates/clean-arch/$table$/Frontend/$table$ListView/index.tsx.sbn`
- `templates/clean-arch/$table$/Frontend/$table$AddEditView/popupForm.tsx.sbn`
- `templates/clean-arch/$table$/Frontend/$table$AddEditView/store.ts.sbn`
- `templates/clean-arch/$table$/Frontend/$table$ListView/store.ts.sbn`
- `templates/clean-arch/$table$/Backend/DTOs/$table$DetailDto.cs.sbn`
- `templates/clean-arch/$table$/Backend/DTOs/$table$ListDto.cs.sbn`
- `templates/clean-arch/$table$/Backend/Controllers/$table$Controller.cs.sbn`
- `templates/clean-arch/$table$/Backend/Interfaces/I$table$Service.cs.sbn`
- `templates/clean-arch/$table$/Backend/Interfaces/I$table$Repository.cs.sbn`
- `templates/clean-arch/$table$/Backend/Services/$table$Service.cs.sbn`
- `templates/clean-arch/$table$/Backend/Repositories/$table$Repository.cs.sbn`
- `templates/clean-arch/$table$/Backend/Domain/$table$.cs.sbn`
- `templates/clean-arch/$table$/Backend/Validators/Create$table$Validator.cs.sbn`
- `templates/clean-arch/$table$/Backend/Validators/Update$table$Validator.cs.sbn`
- `templates/clean-arch/$table$/Frontend/Types/$table$Types.ts.sbn`
- `templates/clean-arch/$table$/Frontend/$table$AddEditView/valid.ts.sbn`
- `templates/clean-arch/$table$/Frontend/Api/useGet$table$s.ts.sbn`
- `templates/clean-arch/$table$/Frontend/$table$AddEditView/mtmTabs.tsx.sbn`
