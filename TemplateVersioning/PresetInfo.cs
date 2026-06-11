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
