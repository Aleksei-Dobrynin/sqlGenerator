---
name: batch-transplant
description: >-
  Use when generating backend/frontend code from a SQL/PostgreSQL schema with the sql-generator
  utility and transplanting the result into a target project — full-module or multi-table
  scaffolding. Triggers: "сгенерировать код из SQL", "генерация модуля по схеме", "перенести
  сгенерированный код в проект", "batch transplant", "scaffold module from DDL". Drives the
  utility as an MCP server and has the agent parse the schema itself (not the built-in parsers).
---

# Batch-transplant workflow

Предпочтительный режим работы агента с `sql-generator`: сгенерировать партию кода и встроить её в
проект, используя **компилятор/тулчейн как оракул ошибок**.

**Полный пошаговый гайд (источник истины):**
[`runbooks/batch-transplant-workflow.md`](../../../runbooks/batch-transplant-workflow.md).
Детали фаз 3–5 — в [`docs/batch/`](../../../docs/batch/). Спецификация MCP-инструментов —
[`agent-spec.md`](../../../agent-spec.md) / [`agent-spec-ru.md`](../../../agent-spec-ru.md).

## Два правила

1. **Утилита = MCP-сервер.** Вызывать через MCP (`list_presets`, `save_schema`, `generate_files`, …);
   инструменты принимают только пути к файлам, не содержимое.
2. **Агент парсит схему сам** для любого нетривиального DDL (по промпту `sql_parsing_instructions`),
   а не через встроенные парсеры (`parse_sql`/regex, `--use-llm`). Встроенный regex — только для
   простого DDL / CLI.

## 5 фаз (кратко)

1. **Generate (batch)** — агент парсит SQL сам → `save_schema` → `generate_files` в scratch (не в проект).
2. **Bulk transplant** — одной скриптовой операцией по маппингу `папка_генератора → слой`.
3. **Diagnostics oracle** — один прогон `build` + `typecheck/build` + lint → полный список ошибок.
4. **Batch-fix** — чинить классами, каскадные → частные (инфра-типы → namespace/using → DI → баги
   шаблонов); build до зелёного.
5. **Verify** — CRUD/runtime к живой базе.

**Предусловие:** инфра-базис (типы, которые ожидает сгенерированный код) заведён в проекте **до** фазы 3.

**Дефекты шаблонов:** документировать для ревью, не править генератор «на лету»; повторяющийся дефект —
временно обойти batch-постобработкой на стороне проекта (фаза 4).
