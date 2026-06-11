# Template Versioning & Project Binding — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Дать генератору извлекать шаблоны пресета на закреплённой git-версии (`<preset>-v<major>.<minor>`) по манифесту `.sqlgen.json` из репо проекта-потребителя, чтобы догенерировать код под старую версию архитектуры.

**Architecture:** Новый модуль `TemplateVersioning/` в основном проекте: модель манифеста (`SqlGenConfig`), модель in-tree маркера (`PresetInfo`), валидатор тега (`PresetTag`), обёртка git (`GitRunner`) и резолвер (`TemplateRefResolver` + `TemplateWorktree : IDisposable`), извлекающий `templates/<preset>` на ref через `git worktree add --detach` во временную папку. CLI (`Program.cs`) и MCP (`SqlGeneratorTools.cs`) переиспользуют резолвер: оба получают `templatesDir`, указывающий на материализованное дерево, и удаляют worktree после генерации.

**Tech Stack:** .NET 9.0, C# (nullable enable, implicit usings), `System.Text.Json`, `System.Diagnostics.Process` (git CLI), Scriban (существующий генератор), xUnit (новый тест-проект — см. «Решения к подтверждению»).

---

## Объём тестов — ТОНКИЙ СРЕЗ (выбрано 2026-06-11)

В репо нет тест-проекта; верификация исторически ручная (scratch-папки `verify_regex/`, `test_output/`).
Оператор выбрал **тонкий срез**: автотесты только там, где есть реальная логика/риск; остальное — сквозной ручной прогон.

**ПЕРЕОПРЕДЕЛЕНИЕ объёма тестов для исполнителя (override TDD-шагов ниже):**

| Задача | Что писать | Тесты |
|--------|-----------|-------|
| Task 0 | xUnit-проект `SqlGenerator.Tests` | **нужен** (хостит 2 тест-файла ниже) |
| Task 1 `SqlGenConfig` | реализация + build + commit | **БЕЗ тестов** — пропустить Step 1–2 (падающий тест) и Step 4 (прогон); верифицируется сквозным прогоном `--from-project` (Task 6) |
| **Task 2 `PresetTag`** | реализация + **юнит-тест** | **ПИСАТЬ** (полный TDD-цикл как в задаче) — чистая логика, краевые случаи |
| Task 3 `PresetInfo` | реализация + build + commit | **БЕЗ тестов** — пропустить тест-шаги |
| Task 4 `GitRunner` | реализация + build + commit | **БЕЗ тестов** — обёртка над `Process`; exercised транзитивно в Task 5 |
| **Task 5 `TemplateRefResolver`** | реализация + **интеграционный тест** | **ПИСАТЬ** (полный цикл) — ядро фичи: извлечение старой версии на теге + cleanup worktree |
| Task 6/7 CLI/MCP | реализация + сборка | **ручной сквозной прогон** на тестовом теге (шаги в задачах) |

Итог: тест-файлы только `PresetTagTests.cs` (Task 2) и `TemplateRefResolverTests.cs` (Task 5). В Task 1/3/4 шаги «написать падающий тест / прогнать тест» **игнорировать**, оставив реализацию, `dotnet build` и commit.

**Прочие допущения (не блокируют дизайн — залочен в спеке):**

1. **Папка `TemplateVersioning/`** в основном проекте (по аналогии с `LlmParser/`). Альтернатива — плоско в корне.
2. **Резолв корня репо генератора** — `git rev-parse --show-toplevel` от `Directory.GetCurrentDirectory()`. Допущение: генератор запускается из своего репо.

---

## File Structure

| Файл | Ответственность |
|------|-----------------|
| `TemplateVersioning/SqlGenConfig.cs` | модель `.sqlgen.json` (`Preset`, `GeneratorRef`, `GeneratorRepo?`, `LayerMap?`) + загрузчик `SqlGenConfigLoader` с валидацией обяз. полей |
| `TemplateVersioning/PresetInfo.cs` | модель `preset.json` (`Name`, `Version`) + `TryRead(presetDir, out info)` |
| `TemplateVersioning/PresetTag.cs` | валидация/парсинг имени тега `<preset>-v<major>.<minor>` |
| `TemplateVersioning/GitRunner.cs` | запуск `git` через `Process`, возврат `(ExitCode, StdOut, StdErr)` |
| `TemplateVersioning/TemplateRefResolver.cs` | `FindRepoRoot`, `Materialize` → `TemplateWorktree`; класс `TemplateWorktree : IDisposable` с `TemplatesDir` и удалением worktree в `Dispose` |
| `Program.cs` (modify) | CLI-флаги `--from-project`, `--templates-ref`; материализация worktree + dispose вокруг генерации |
| `SqlGenerator.Mcp/Tools/SqlGeneratorTools.cs` (modify) | параметры `fromProject?`, `templatesRef?` в `GenerateFiles`/`QuickGenerate` |
| `templates/$table$/preset.json` | in-tree маркер версии для дефолтного пресета (rollout) |
| `SqlGenerator.Tests/*` | xUnit-тесты модуля версионирования |

---

## Task 0: Тест-проект (CONFIRM FIRST — см. «Решения к подтверждению»)

**Files:**
- Create: `SqlGenerator.Tests/SqlGenerator.Tests.csproj`
- Modify: `SQLFileGenerator.sln`

- [ ] **Step 1: Создать проект и подключить к solution**

Run (PowerShell, из корня репо):
```powershell
dotnet new xunit -n SqlGenerator.Tests -o SqlGenerator.Tests
dotnet sln SQLFileGenerator.sln add SqlGenerator.Tests/SqlGenerator.Tests.csproj
dotnet add SqlGenerator.Tests/SqlGenerator.Tests.csproj reference SQLFileGenerator.csproj
```

- [ ] **Step 2: Прогнать пустой проект**

Run: `dotnet test SqlGenerator.Tests/SqlGenerator.Tests.csproj`
Expected: PASS (0 тестов или 1 шаблонный), сборка зелёная.

- [ ] **Step 3: Commit**

```powershell
git add SqlGenerator.Tests SQLFileGenerator.sln
git commit -m "test: add xUnit test project SqlGenerator.Tests"
```

---

## Task 1: Модель и загрузчик манифеста `.sqlgen.json`

**Files:**
- Create: `TemplateVersioning/SqlGenConfig.cs`
- Test: `SqlGenerator.Tests/SqlGenConfigTests.cs`

- [ ] **Step 1: Написать падающий тест**

`SqlGenerator.Tests/SqlGenConfigTests.cs`:
```csharp
using System.IO;
using SQLFileGenerator;
using Xunit;

namespace SqlGenerator.Tests;

public class SqlGenConfigTests
{
    private static string WriteTempManifest(string json)
    {
        var dir = Path.Combine(Path.GetTempPath(), "sqlgen-test-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, SqlGenConfigLoader.FileName), json);
        return dir;
    }

    [Fact]
    public void Load_ParsesAllFields()
    {
        var dir = WriteTempManifest(@"{
            ""preset"": ""default"",
            ""generatorRef"": ""default-v1.0"",
            ""generatorRepo"": ""https://example/repo.git"",
            ""layerMap"": { ""entity"": ""Domain"", ""repo"": ""Infrastructure"" }
        }");

        var cfg = SqlGenConfigLoader.Load(dir);

        Assert.Equal("default", cfg.Preset);
        Assert.Equal("default-v1.0", cfg.GeneratorRef);
        Assert.Equal("https://example/repo.git", cfg.GeneratorRepo);
        Assert.NotNull(cfg.LayerMap);
        Assert.Equal("Domain", cfg.LayerMap!["entity"]);
    }

    [Fact]
    public void Load_MinimalManifest_OptionalFieldsNull()
    {
        var dir = WriteTempManifest(@"{ ""preset"": ""default"", ""generatorRef"": ""default-v1.0"" }");

        var cfg = SqlGenConfigLoader.Load(dir);

        Assert.Null(cfg.GeneratorRepo);
        Assert.Null(cfg.LayerMap);
    }

    [Fact]
    public void Load_MissingPreset_Throws()
    {
        var dir = WriteTempManifest(@"{ ""generatorRef"": ""default-v1.0"" }");
        Assert.Throws<InvalidDataException>(() => SqlGenConfigLoader.Load(dir));
    }

    [Fact]
    public void Load_FileAbsent_Throws()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sqlgen-empty-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Assert.Throws<FileNotFoundException>(() => SqlGenConfigLoader.Load(dir));
    }
}
```

- [ ] **Step 2: Прогнать — убедиться, что падает**

Run: `dotnet test SqlGenerator.Tests/SqlGenerator.Tests.csproj --filter SqlGenConfigTests`
Expected: FAIL — `SqlGenConfig`/`SqlGenConfigLoader` не существуют (ошибка компиляции).

- [ ] **Step 3: Реализовать модель и загрузчик**

`TemplateVersioning/SqlGenConfig.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SQLFileGenerator;

/// <summary>
/// Привязка проекта-потребителя к версии пресета генератора (.sqlgen.json в корне репо проекта).
/// </summary>
public class SqlGenConfig
{
    [JsonPropertyName("preset")]
    public string Preset { get; set; } = "";

    [JsonPropertyName("generatorRef")]
    public string GeneratorRef { get; set; } = "";

    [JsonPropertyName("generatorRepo")]
    public string? GeneratorRepo { get; set; }

    [JsonPropertyName("layerMap")]
    public Dictionary<string, string>? LayerMap { get; set; }
}

/// <summary>
/// Загрузка и валидация .sqlgen.json из директории проекта.
/// </summary>
public static class SqlGenConfigLoader
{
    public const string FileName = ".sqlgen.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static SqlGenConfig Load(string projectDir)
    {
        var path = Path.Combine(projectDir, FileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"{FileName} not found in {projectDir}", path);

        var json = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<SqlGenConfig>(json, Options)
            ?? throw new InvalidDataException($"{FileName} deserialized to null");

        if (string.IsNullOrWhiteSpace(config.Preset))
            throw new InvalidDataException($"{FileName}: 'preset' is required");
        if (string.IsNullOrWhiteSpace(config.GeneratorRef))
            throw new InvalidDataException($"{FileName}: 'generatorRef' is required");

        return config;
    }
}
```

- [ ] **Step 4: Прогнать — убедиться, что проходит**

Run: `dotnet test SqlGenerator.Tests/SqlGenerator.Tests.csproj --filter SqlGenConfigTests`
Expected: PASS (4 теста).

**Manual fallback (если без тест-проекта):** написать `.sqlgen.json` вручную, запустить генератор с `--from-project` (после Task 6) и проверить, что preset/ref считаны; невалидный манифест даёт внятную ошибку.

- [ ] **Step 5: Commit**

```powershell
git add TemplateVersioning/SqlGenConfig.cs SqlGenerator.Tests/SqlGenConfigTests.cs
git commit -m "feat(versioning): .sqlgen.json model + loader with validation"
```

---

## Task 2: Валидатор/парсер тега `<preset>-v<major>.<minor>`

**Files:**
- Create: `TemplateVersioning/PresetTag.cs`
- Test: `SqlGenerator.Tests/PresetTagTests.cs`

- [ ] **Step 1: Написать падающий тест**

`SqlGenerator.Tests/PresetTagTests.cs`:
```csharp
using SQLFileGenerator;
using Xunit;

namespace SqlGenerator.Tests;

public class PresetTagTests
{
    [Theory]
    [InlineData("default-v1.0", true)]
    [InlineData("clean-arch-v1.0", true)]
    [InlineData("default-v2.11", true)]
    [InlineData("default", false)]
    [InlineData("default-v1", false)]
    [InlineData("default-1.0", false)]
    [InlineData("Default-v1.0", false)]
    public void IsValid_MatchesSchema(string tag, bool expected)
    {
        Assert.Equal(expected, PresetTag.IsValid(tag));
    }

    [Fact]
    public void TryParse_SplitsPresetAndVersion()
    {
        Assert.True(PresetTag.TryParse("clean-arch-v2.5", out var preset, out var major, out var minor));
        Assert.Equal("clean-arch", preset);
        Assert.Equal(2, major);
        Assert.Equal(5, minor);
    }
}
```

- [ ] **Step 2: Прогнать — убедиться, что падает**

Run: `dotnet test SqlGenerator.Tests/SqlGenerator.Tests.csproj --filter PresetTagTests`
Expected: FAIL — `PresetTag` не существует.

- [ ] **Step 3: Реализовать**

`TemplateVersioning/PresetTag.cs`:
```csharp
using System.Text.RegularExpressions;

namespace SQLFileGenerator;

/// <summary>
/// Имя git-тега версии пресета: &lt;preset&gt;-v&lt;major&gt;.&lt;minor&gt; (preset допускает дефисы: clean-arch-v1.0).
/// </summary>
public static class PresetTag
{
    private static readonly Regex Pattern = new(
        @"^(?<preset>[a-z0-9][a-z0-9-]*)-v(?<major>\d+)\.(?<minor>\d+)$",
        RegexOptions.Compiled);

    public static bool IsValid(string tag) =>
        !string.IsNullOrEmpty(tag) && Pattern.IsMatch(tag);

    public static bool TryParse(string tag, out string preset, out int major, out int minor)
    {
        preset = "";
        major = 0;
        minor = 0;
        if (string.IsNullOrEmpty(tag)) return false;

        var m = Pattern.Match(tag);
        if (!m.Success) return false;

        preset = m.Groups["preset"].Value;
        major = int.Parse(m.Groups["major"].Value);
        minor = int.Parse(m.Groups["minor"].Value);
        return true;
    }
}
```

- [ ] **Step 4: Прогнать — убедиться, что проходит**

Run: `dotnet test SqlGenerator.Tests/SqlGenerator.Tests.csproj --filter PresetTagTests`
Expected: PASS (8 кейсов).

**Manual fallback:** не применимо — чистая логика, проверяется только тестом или REPL.

- [ ] **Step 5: Commit**

```powershell
git add TemplateVersioning/PresetTag.cs SqlGenerator.Tests/PresetTagTests.cs
git commit -m "feat(versioning): preset tag name validation/parsing"
```

---

## Task 3: Модель и чтение in-tree `preset.json`

**Files:**
- Create: `TemplateVersioning/PresetInfo.cs`
- Test: `SqlGenerator.Tests/PresetInfoTests.cs`

- [ ] **Step 1: Написать падающий тест**

`SqlGenerator.Tests/PresetInfoTests.cs`:
```csharp
using System.IO;
using SQLFileGenerator;
using Xunit;

namespace SqlGenerator.Tests;

public class PresetInfoTests
{
    [Fact]
    public void TryRead_PresentFile_ReturnsInfo()
    {
        var dir = Path.Combine(Path.GetTempPath(), "preset-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, PresetInfo.FileName),
            @"{ ""name"": ""default"", ""version"": ""1.0"" }");

        Assert.True(PresetInfo.TryRead(dir, out var info));
        Assert.Equal("default", info!.Name);
        Assert.Equal("1.0", info.Version);
    }

    [Fact]
    public void TryRead_Absent_ReturnsFalse()
    {
        var dir = Path.Combine(Path.GetTempPath(), "preset-none-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        Assert.False(PresetInfo.TryRead(dir, out var info));
        Assert.Null(info);
    }
}
```

- [ ] **Step 2: Прогнать — убедиться, что падает**

Run: `dotnet test SqlGenerator.Tests/SqlGenerator.Tests.csproj --filter PresetInfoTests`
Expected: FAIL — `PresetInfo` не существует.

- [ ] **Step 3: Реализовать**

`TemplateVersioning/PresetInfo.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SQLFileGenerator;

/// <summary>
/// In-tree маркер версии пресета: templates/&lt;preset&gt;/preset.json. Зеркалит версию git-тега.
/// </summary>
public class PresetInfo
{
    public const string FileName = "preset.json";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static bool TryRead(string presetDir, out PresetInfo? info)
    {
        info = null;
        var path = Path.Combine(presetDir, FileName);
        if (!File.Exists(path)) return false;

        try
        {
            info = JsonSerializer.Deserialize<PresetInfo>(File.ReadAllText(path), Options);
            return info != null;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
```

- [ ] **Step 4: Прогнать — убедиться, что проходит**

Run: `dotnet test SqlGenerator.Tests/SqlGenerator.Tests.csproj --filter PresetInfoTests`
Expected: PASS (2 теста).

- [ ] **Step 5: Commit**

```powershell
git add TemplateVersioning/PresetInfo.cs SqlGenerator.Tests/PresetInfoTests.cs
git commit -m "feat(versioning): preset.json in-tree marker model + reader"
```

---

## Task 4: Обёртка git (`GitRunner`)

**Files:**
- Create: `TemplateVersioning/GitRunner.cs`
- Test: `SqlGenerator.Tests/GitRunnerTests.cs`

- [ ] **Step 1: Написать падающий тест**

`SqlGenerator.Tests/GitRunnerTests.cs`:
```csharp
using SQLFileGenerator;
using Xunit;

namespace SqlGenerator.Tests;

public class GitRunnerTests
{
    [Fact]
    public void Run_GitVersion_ExitsZero()
    {
        var result = GitRunner.Run(System.IO.Path.GetTempPath(), "--version");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("git version", result.StdOut);
    }

    [Fact]
    public void Run_UnknownCommand_NonZeroExit()
    {
        var result = GitRunner.Run(System.IO.Path.GetTempPath(), "definitely-not-a-git-command");
        Assert.NotEqual(0, result.ExitCode);
    }
}
```

- [ ] **Step 2: Прогнать — убедиться, что падает**

Run: `dotnet test SqlGenerator.Tests/SqlGenerator.Tests.csproj --filter GitRunnerTests`
Expected: FAIL — `GitRunner` не существует.

- [ ] **Step 3: Реализовать**

`TemplateVersioning/GitRunner.cs`:
```csharp
using System.Diagnostics;

namespace SQLFileGenerator;

/// <summary>
/// Тонкая обёртка над git CLI. Аргументы передаются через ArgumentList (без ручного экранирования).
/// </summary>
public static class GitRunner
{
    public record GitResult(int ExitCode, string StdOut, string StdErr);

    public static GitResult Run(string workingDir, params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git process");

        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();

        return new GitResult(p.ExitCode, stdout, stderr);
    }
}
```

- [ ] **Step 4: Прогнать — убедиться, что проходит**

Run: `dotnet test SqlGenerator.Tests/SqlGenerator.Tests.csproj --filter GitRunnerTests`
Expected: PASS (2 теста). Требует `git` в PATH.

- [ ] **Step 5: Commit**

```powershell
git add TemplateVersioning/GitRunner.cs SqlGenerator.Tests/GitRunnerTests.cs
git commit -m "feat(versioning): git CLI runner wrapper"
```

---

## Task 5: Резолвер `TemplateRefResolver` + `TemplateWorktree`

**Files:**
- Create: `TemplateVersioning/TemplateRefResolver.cs`
- Test: `SqlGenerator.Tests/TemplateRefResolverTests.cs`

- [ ] **Step 1: Написать падающий интеграционный тест**

Тест строит реальный временный git-репо с `templates/default/preset.json`, тегирует `default-v1.0`, затем резолвит на ref и проверяет, что worktree содержит дерево пресета; после Dispose worktree удалён.

`SqlGenerator.Tests/TemplateRefResolverTests.cs`:
```csharp
using System.IO;
using SQLFileGenerator;
using Xunit;

namespace SqlGenerator.Tests;

public class TemplateRefResolverTests
{
    private static string InitGeneratorRepo()
    {
        var repo = Path.Combine(Path.GetTempPath(), "gen-repo-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repo);
        GitRunner.Run(repo, "init");
        GitRunner.Run(repo, "config", "user.email", "test@test");
        GitRunner.Run(repo, "config", "user.name", "test");

        var presetDir = Path.Combine(repo, "templates", "default");
        Directory.CreateDirectory(presetDir);
        File.WriteAllText(Path.Combine(presetDir, "preset.json"), @"{ ""name"": ""default"", ""version"": ""1.0"" }");
        File.WriteAllText(Path.Combine(presetDir, "marker.txt"), "v1.0 content");

        GitRunner.Run(repo, "add", ".");
        GitRunner.Run(repo, "commit", "-m", "v1.0");
        GitRunner.Run(repo, "tag", "default-v1.0");
        return repo;
    }

    [Fact]
    public void Materialize_ExtractsPresetTreeAtRef_AndCleansUp()
    {
        var repo = InitGeneratorRepo();
        // изменить пресет после тега — worktree должен отдать СТАРОЕ содержимое
        File.WriteAllText(Path.Combine(repo, "templates", "default", "marker.txt"), "v2 content");

        string worktreePath;
        using (var wt = TemplateRefResolver.Materialize(repo, "default", "default-v1.0"))
        {
            Assert.True(Directory.Exists(wt.TemplatesDir));
            Assert.EndsWith(Path.Combine("templates", "default"), wt.TemplatesDir);
            Assert.Equal("v1.0 content",
                File.ReadAllText(Path.Combine(wt.TemplatesDir, "marker.txt")));
            worktreePath = Path.GetDirectoryName(Path.GetDirectoryName(wt.TemplatesDir))!;
            Assert.True(Directory.Exists(worktreePath));
        }
        // после Dispose worktree удалён
        Assert.False(Directory.Exists(worktreePath));
    }

    [Fact]
    public void Materialize_UnknownRef_Throws()
    {
        var repo = InitGeneratorRepo();
        Assert.Throws<InvalidOperationException>(() =>
            TemplateRefResolver.Materialize(repo, "default", "default-v9.9"));
    }

    [Fact]
    public void FindRepoRoot_ReturnsToplevel()
    {
        var repo = InitGeneratorRepo();
        var sub = Path.Combine(repo, "templates", "default");
        var root = TemplateRefResolver.FindRepoRoot(sub);
        Assert.Equal(
            Path.GetFullPath(repo).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar));
    }
}
```

- [ ] **Step 2: Прогнать — убедиться, что падает**

Run: `dotnet test SqlGenerator.Tests/SqlGenerator.Tests.csproj --filter TemplateRefResolverTests`
Expected: FAIL — `TemplateRefResolver`/`TemplateWorktree` не существуют.

- [ ] **Step 3: Реализовать**

`TemplateVersioning/TemplateRefResolver.cs`:
```csharp
namespace SQLFileGenerator;

/// <summary>
/// Материализованное дерево шаблонов пресета на конкретном git-ref (через git worktree).
/// Dispose удаляет worktree.
/// </summary>
public sealed class TemplateWorktree : IDisposable
{
    private readonly string _repoDir;
    private readonly string _worktreePath;
    private bool _disposed;

    /// <summary>Путь к templates/&lt;preset&gt; внутри материализованного дерева.</summary>
    public string TemplatesDir { get; }

    internal TemplateWorktree(string repoDir, string worktreePath, string templatesDir)
    {
        _repoDir = repoDir;
        _worktreePath = worktreePath;
        TemplatesDir = templatesDir;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GitRunner.Run(_repoDir, "worktree", "remove", "--force", _worktreePath);
    }
}

/// <summary>
/// Извлекает шаблоны пресета на нужном git-ref в отдельный worktree.
/// </summary>
public static class TemplateRefResolver
{
    /// <summary>Корень git-репо генератора (toplevel) от стартовой директории.</summary>
    public static string FindRepoRoot(string startDir)
    {
        var r = GitRunner.Run(startDir, "rev-parse", "--show-toplevel");
        if (r.ExitCode != 0)
            throw new InvalidOperationException(
                $"Not inside a git repository: {startDir}. {r.StdErr.Trim()}");
        return r.StdOut.Trim();
    }

    /// <summary>
    /// Создаёт detached worktree на ref и возвращает обёртку с путём к templates/&lt;preset&gt;.
    /// Кидает, если ref неизвестен или подпапка пресета отсутствует в дереве ref.
    /// </summary>
    public static TemplateWorktree Materialize(string repoDir, string preset, string @ref)
    {
        var worktreePath = Path.Combine(
            Path.GetTempPath(),
            $"sqlgen-wt-{preset}-{Guid.NewGuid():N}");

        var add = GitRunner.Run(repoDir, "worktree", "add", "--detach", worktreePath, @ref);
        if (add.ExitCode != 0)
            throw new InvalidOperationException(
                $"git worktree add failed for ref '{@ref}': {add.StdErr.Trim()}");

        var templatesDir = Path.Combine(worktreePath, "templates", preset);
        if (!Directory.Exists(templatesDir))
        {
            GitRunner.Run(repoDir, "worktree", "remove", "--force", worktreePath);
            throw new InvalidOperationException(
                $"Preset '{preset}' not found at ref '{@ref}' (expected templates/{preset}).");
        }

        return new TemplateWorktree(repoDir, worktreePath, templatesDir);
    }
}
```

- [ ] **Step 4: Прогнать — убедиться, что проходит**

Run: `dotnet test SqlGenerator.Tests/SqlGenerator.Tests.csproj --filter TemplateRefResolverTests`
Expected: PASS (3 теста). Требует `git` в PATH; на Windows worktree штатно.

- [ ] **Step 5: Commit**

```powershell
git add TemplateVersioning/TemplateRefResolver.cs SqlGenerator.Tests/TemplateRefResolverTests.cs
git commit -m "feat(versioning): git-worktree template ref resolver"
```

---

## Task 6: CLI-флаги `--from-project` / `--templates-ref` (Program.cs)

**Files:**
- Modify: `Program.cs:29-109` (Main) и добавить приватный хелпер

- [ ] **Step 1: Добавить хелпер резолва версии**

Вставить в класс `Program` (после `ResolveTemplatesDir`, около `Program.cs:212`):
```csharp
/// <summary>
/// Если заданы --from-project или --templates-ref — материализует шаблоны пресета на git-ref.
/// Возвращает templatesDir (внутри worktree) и worktree для последующего Dispose (или null).
/// При отсутствии флагов возвращает обычный templatesDir и null.
/// </summary>
private static string ResolveVersionedTemplatesDir(string[] args, string defaultTemplatesDir, out TemplateWorktree? worktree)
{
    worktree = null;

    var fromProject = GetArgValue(args, "--from-project");
    var templatesRef = GetArgValue(args, "--templates-ref");

    string? preset = null;
    string? @ref = null;

    if (!string.IsNullOrEmpty(fromProject))
    {
        var cfg = SqlGenConfigLoader.Load(fromProject);
        preset = cfg.Preset;
        @ref = cfg.GeneratorRef;
        Console.WriteLine($"From project: preset='{preset}', ref='{@ref}'");
    }

    if (!string.IsNullOrEmpty(templatesRef))
    {
        @ref = templatesRef;
        preset ??= GetArgValue(args, "--preset")
            ?? throw new ArgumentException("--templates-ref requires --preset (or use --from-project).");
    }

    if (@ref == null)
        return defaultTemplatesDir; // версионирование не запрошено

    var repoRoot = TemplateRefResolver.FindRepoRoot(Directory.GetCurrentDirectory());
    worktree = TemplateRefResolver.Materialize(repoRoot, preset!, @ref);
    Console.WriteLine($"Materialized templates at ref '{@ref}': {worktree.TemplatesDir}");
    return worktree.TemplatesDir;
}
```

- [ ] **Step 2: Подключить хелпер в Main**

В `Program.cs` заменить строку 38:
```csharp
                string templatesDir = ResolveTemplatesDir(args, "templates");
```
на:
```csharp
                string templatesDir = ResolveVersionedTemplatesDir(args, ResolveTemplatesDir(args, "templates"), out TemplateWorktree? versionedWorktree);
```

И обернуть генерацию в try/finally для очистки worktree. Заменить строки 95-99:
```csharp
                Directory.CreateDirectory(resultDir);
                FileGenerator.GenerateOtherFiles(tables, templatesDir, resultDir, includeVirtualFks);

                Console.WriteLine();
                Console.WriteLine($"Files successfully generated to: {resultDir}");
```
на:
```csharp
                try
                {
                    Directory.CreateDirectory(resultDir);
                    FileGenerator.GenerateOtherFiles(tables, templatesDir, resultDir, includeVirtualFks);

                    Console.WriteLine();
                    Console.WriteLine($"Files successfully generated to: {resultDir}");
                }
                finally
                {
                    versionedWorktree?.Dispose();
                }
```

Примечание: `--templates-ref`/`--from-project` уже корректно игнорируются `GetResultDirFromArgs` — он берёт первый не-флаг и не-значение известных флагов; добавить их в список «флагов со значением» в `GetResultDirFromArgs` (строки 142, 153), чтобы их значения не считались позиционным output-путём:
```csharp
                    if (arg == "--config" || arg == "--output" || arg == "--sql" || arg == "--preset" || arg == "--from-project" || arg == "--templates-ref")
```
(в обоих местах — строка 142 и строка 153).

- [ ] **Step 3: Добавить `using` и обновить doc-комментарий**

Вверху `Program.cs` namespace `SQLFileGenerator` уже не импортирован под алиасом — классы версионирования в `SQLFileGenerator`, а файл уже содержит `using SQLFileGenerator;` (строка 6). Доп. using не нужен. Обновить XML-doc `<param name="args">` (строки 22-28), добавив:
```csharp
        ///   --from-project [path] Догенерация по .sqlgen.json проекта (preset + версия пресета)
        ///   --templates-ref [ref] Извлечь шаблоны пресета на git-ref (требует --preset)
```

- [ ] **Step 4: Сборка + ручная верификация**

Run:
```powershell
dotnet build SQLFileGenerator.csproj
```
Expected: сборка зелёная.

Ручная проверка `--templates-ref` (в репо генератора уже должен быть тег — если нет, временно `git tag default-v1.0` на HEAD для проверки и удалить после):
```powershell
git tag default-vTEST HEAD
dotnet run -- --templates-ref default-vTEST --preset default --sql sql/test_mtm.sql --output _verify_ref
git tag -d default-vTEST
```
Expected: вывод «Materialized templates at ref…», файлы в `_verify_ref`, временный `sqlgen-wt-*` в TEMP удалён по завершении.

- [ ] **Step 5: Commit**

```powershell
git add Program.cs
git commit -m "feat(cli): --from-project / --templates-ref versioned template resolution"
```

---

## Task 7: MCP-параметры `fromProject` / `templatesRef`

**Files:**
- Modify: `SqlGenerator.Mcp/Tools/SqlGeneratorTools.cs` — `GenerateFiles` (201-253), `QuickGenerate` (260-315), `ResolveTemplatesPath` (320-357)

- [ ] **Step 1: Добавить общий резолв версии в инструментах**

Добавить приватный метод в класс `SqlGeneratorTools` (рядом с `ResolveTemplatesPath`):
```csharp
/// <summary>
/// Версионный резолв шаблонов для MCP. Если задан templatesRef или fromProject —
/// материализует worktree и возвращает его templatesDir + worktree для Dispose.
/// Иначе делегирует обычному ResolveTemplatesPath.
/// </summary>
private static string ResolveTemplatesPathVersioned(
    string templatesDir, string presetName, string templatesRef, string fromProject,
    out TemplateWorktree? worktree, out string? error)
{
    worktree = null;
    error = null;

    string? preset = string.IsNullOrEmpty(presetName) ? null : presetName;
    string? @ref = string.IsNullOrEmpty(templatesRef) ? null : templatesRef;

    if (!string.IsNullOrEmpty(fromProject))
    {
        try
        {
            var cfg = SqlGenConfigLoader.Load(fromProject);
            preset ??= cfg.Preset;
            @ref ??= cfg.GeneratorRef;
        }
        catch (Exception ex) { error = ex.Message; return templatesDir; }
    }

    if (@ref == null)
        return ResolveTemplatesPath(templatesDir, presetName, out error);

    if (string.IsNullOrEmpty(preset))
    {
        error = "templatesRef requires presetName (or use fromProject with .sqlgen.json).";
        return templatesDir;
    }

    try
    {
        var repoRoot = TemplateRefResolver.FindRepoRoot(Directory.GetCurrentDirectory());
        worktree = TemplateRefResolver.Materialize(repoRoot, preset, @ref);
        return worktree.TemplatesDir;
    }
    catch (Exception ex) { error = ex.Message; return templatesDir; }
}
```

- [ ] **Step 2: Добавить параметры и подключить в `GenerateFiles`**

Добавить параметры в сигнатуру `GenerateFiles` (после `includeVirtualFks`, строка 211):
```csharp
        [Description("Git ref of preset version to extract templates from (e.g. 'default-v1.0'). Requires presetName.")]
        string templatesRef = "",
        [Description("Path to consumer project dir with .sqlgen.json — resolves preset + version automatically.")]
        string fromProject = "")
```
Заменить строки 219-221:
```csharp
            var templatesPath = ResolveTemplatesPath(templatesDir, presetName, out string? resolveError);
            if (resolveError != null)
                return Fail(resolveError);
```
на:
```csharp
            var templatesPath = ResolveTemplatesPathVersioned(
                templatesDir, presetName, templatesRef, fromProject, out var worktree, out string? resolveError);
            if (resolveError != null)
                return Fail(resolveError);
            using var _wt = worktree;
```

- [ ] **Step 3: Те же параметры и подключение в `QuickGenerate`**

Добавить идентичные параметры `templatesRef`/`fromProject` в сигнатуру `QuickGenerate` (после строки 270) и заменить строки 282-284:
```csharp
            var templatesPath = ResolveTemplatesPath(templatesDir, presetName, out string? resolveError);
            if (resolveError != null)
                return Fail(resolveError);
```
на:
```csharp
            var templatesPath = ResolveTemplatesPathVersioned(
                templatesDir, presetName, templatesRef, fromProject, out var worktree, out string? resolveError);
            if (resolveError != null)
                return Fail(resolveError);
            using var _wt = worktree;
```

- [ ] **Step 4: Сборка**

Run:
```powershell
dotnet build SqlGenerator.Mcp/SqlGenerator.Mcp.csproj
```
Expected: сборка зелёная. (`SQLFileGenerator` уже импортирован — строка 2 `using SQLFileGenerator;`.)

- [ ] **Step 5: Ручная верификация через MCP Inspector**

Run:
```powershell
npx @anthropic/mcp-inspector dotnet run --project SqlGenerator.Mcp
```
В инспекторе вызвать `quick_generate` с `templatesRef="default-vTEST"`, `presetName="default"` (тег создать временно как в Task 6) — проверить успешную генерацию и удаление worktree.

- [ ] **Step 6: Commit**

```powershell
git add SqlGenerator.Mcp/Tools/SqlGeneratorTools.cs
git commit -m "feat(mcp): templatesRef / fromProject versioned generation params"
```

---

## Task 8: Rollout v1.0 — `preset.json` + теги + документация

**Files:**
- Create: `templates/$table$/preset.json` (дефолтный пресет)
- Create (пример): `docs/specs/examples/.sqlgen.json`
- Modify: `CLAUDE.md` (раздел про новые флаги) — опционально

> ⚠️ Тегирование (`git tag default-v1.0`) и заведение `.sqlgen.json` в реальных проектах-потребителях — **операционные действия оператора**, не часть кодовой сборки. Здесь — только артефакты в репо генератора + пример.

- [ ] **Step 1: Добавить `preset.json` в каждый пресет**

> Раскладка подтверждена (2026-06-11): пресеты — папки `templates/default/` и `templates/clean-arch/` (preset-режим активен; записи `templates\$table$\` в csproj устаревшие, в живом дереве их нет). Резолвер `templates/<preset>` (Task 5) этому соответствует.

Создать маркер в каждом пресете:
- `templates/default/preset.json` → `{ "name": "default", "version": "1.0" }`
- `templates/clean-arch/preset.json` → `{ "name": "clean-arch", "version": "1.0" }`

- [ ] **Step 2: Зарегистрировать как копируемый ресурс (если нужно)**

Если генератор копирует не-`.sbn` файлы as-is, `preset.json` попадёт в output — этого НЕ нужно. Убедиться, что `preset.json` исключён из копирования (он метаданные пресета, не шаблон). Проверить логику копирования в `Generator.cs` (`ProcessTemplatesDirectory`); при необходимости добавить исключение имени `preset.json`.

> Этот шаг требует проверки кода копирования — вынести в отдельный под-таск при исполнении, если `preset.json` иначе протекает в сгенерированный вывод.

- [ ] **Step 3: Создать пример манифеста**

`docs/specs/examples/.sqlgen.json`:
```json
{
  "preset": "default",
  "generatorRef": "default-v1.0",
  "generatorRepo": "https://github.com/Aleksei-Dobrynin/sqlGenerator.git",
  "layerMap": {
    "entity": "Domain",
    "irepo": "Application",
    "usecase": "Application",
    "dto": "Application",
    "repo": "Infrastructure",
    "controller": "WebApi"
  }
}
```

- [ ] **Step 4: Verify — полный прогон тестов + сборка обоих проектов**

Run:
```powershell
dotnet build SQLFileGenerator.sln
dotnet test SqlGenerator.Tests/SqlGenerator.Tests.csproj
```
Expected: всё зелёное.

- [ ] **Step 5: Commit**

```powershell
git add templates docs/specs/examples/.sqlgen.json
git commit -m "feat(versioning): preset.json marker + .sqlgen.json example (v1.0 rollout artifacts)"
```

- [ ] **Step 6: Операционный rollout (оператор, вне сборки)**

Чек-лист (выполняет оператор):
1. `git tag default-v1.0` (+ `clean-arch-v1.0`, если применимо), `git push --tags`.
2. В каждом проекте-потребителе: создать `.sqlgen.json` по примеру, закоммитить в репо проекта.
3. Сверить синхрон тег ↔ `preset.json.version`.

---

## Self-Review (выполнено при написании плана)

- **Spec coverage:** §2 версионирование → Task 2,8; §3 манифест → Task 1,6,7; §4 preset.json → Task 3,8; §5 резолвер/worktree → Task 4,5; §6 миграция → документируется (rollout Task 8 §6, кода не требует); §7 CLI/MCP → Task 6,7; §8 rollout → Task 8; §9 backlog → вне охвата (явно). ✅
- **Placeholders:** код приведён в каждом шаге; единственные «проверить при исполнении» помечены явно (Task 8 §1/§2 — зависят от фактической раскладки `templates/`, что честнее, чем угадывать).
- **Type consistency:** `SqlGenConfigLoader.Load`, `PresetTag.IsValid/TryParse`, `PresetInfo.TryRead`, `GitRunner.Run`, `TemplateRefResolver.FindRepoRoot/Materialize`, `TemplateWorktree.TemplatesDir/Dispose` — имена согласованы между задачами и местами вызова (Task 6/7).

## Связанное

- Спека: `docs/specs/2026-06-11-template-versioning-project-binding.md`
- Decision-log: `template-versioning-report.md`
- ADR: `LLMWiki/projects/sql-generator/decisions/ADR-0004-template-version-pinning.md`
