# Runbooks

Пошаговые процедуры для работы с `sql-generator`.

| Runbook | Назначение |
|---------|------------|
| [`batch-transplant-workflow.md`](batch-transplant-workflow.md) | **Предпочтительный** режим работы агента: генерация партии кода через MCP + встраивание в проект (compiler-as-oracle, 5 фаз). |

Связанные документы:
- [`../agent-spec.md`](../agent-spec.md) / [`../agent-spec-ru.md`](../agent-spec-ru.md) — спецификация MCP-инструментов и сценариев.
- [`../docs/batch/`](../docs/batch/) — детали фаз 3–5 batch-transplant (диагностика, классы ошибок, runtime-проверка).
