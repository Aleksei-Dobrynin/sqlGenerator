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
