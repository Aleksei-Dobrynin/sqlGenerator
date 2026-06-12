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
