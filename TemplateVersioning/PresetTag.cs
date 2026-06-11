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
