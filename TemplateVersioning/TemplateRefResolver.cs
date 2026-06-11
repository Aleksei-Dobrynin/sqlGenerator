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
