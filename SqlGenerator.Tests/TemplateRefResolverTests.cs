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
