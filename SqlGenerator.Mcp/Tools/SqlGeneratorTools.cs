using ModelContextProtocol.Server;
using SQLFileGenerator;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SqlGenerator.Mcp.Tools;

[McpServerToolType]
public class SqlGeneratorTools
{
    /// <summary>
    /// Parses SQL using regex (fast, for simple DDL)
    /// </summary>
    [McpServerTool]
    [Description("Parse PostgreSQL CREATE TABLE SQL file using regex parser. Fast but works only for simple DDL. For complex SQL with comments, constraints, or non-standard syntax - parse it yourself using the 'sql_parsing_instructions' prompt and call 'save_schema' with results.")]
    public ParseSqlResult ParseSql(
        [Description("Path to .sql file containing CREATE TABLE statements")]
        string sqlFilePath,
        [Description("Output path for schema JSON file. Default: 'schema.json'")]
        string outputPath = "schema.json",
        [Description("Include virtual foreign keys inferred from naming conventions (e.g. user_id -> users.id). Default: true")]
        bool includeVirtualFks = true)
    {
        try
        {
            var fullSqlPath = Path.GetFullPath(sqlFilePath);
            if (!File.Exists(fullSqlPath))
            {
                return new ParseSqlResult
                {
                    Success = false,
                    Error = $"SQL file not found: {fullSqlPath}"
                };
            }

            var sql = File.ReadAllText(fullSqlPath);
            var tables = SqlParser.ParsePostgresCreateTableScript(sql);

            if (includeVirtualFks)
                VirtualForeignKeyResolver.ResolveVirtualForeignKeys(tables);

            if (tables.Count == 0)
            {
                return new ParseSqlResult
                {
                    Success = false,
                    Error = "No tables found in SQL. Try parsing complex SQL yourself using 'sql_parsing_instructions' prompt."
                };
            }

            // Save schema to file
            var fullPath = Path.GetFullPath(outputPath);
            var json = JsonSerializer.Serialize(tables, JsonOptions);
            File.WriteAllText(fullPath, json);

            return new ParseSqlResult
            {
                Success = true,
                TableCount = tables.Count,
                TableNames = tables.Select(t => t.EntityName).ToArray(),
                SchemaFile = fullPath
            };
        }
        catch (Exception ex)
        {
            return new ParseSqlResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Saves schema parsed by the agent
    /// </summary>
    [McpServerTool]
    [Description("Validate and save table schema from a JSON file. Use this after parsing complex SQL yourself following 'sql_parsing_instructions' prompt. Write JSON to a file first, then pass the file path.")]
    public SaveSchemaResult SaveSchema(
        [Description("Path to JSON file containing array of table schemas")]
        string schemaFilePath,
        [Description("Output path for validated schema JSON file. Default: 'schema.json'")]
        string outputPath = "schema.json")
    {
        try
        {
            var fullSchemaPath = Path.GetFullPath(schemaFilePath);
            if (!File.Exists(fullSchemaPath))
            {
                return new SaveSchemaResult
                {
                    Success = false,
                    Error = $"Schema file not found: {fullSchemaPath}"
                };
            }

            // Validate JSON
            var schemaJson = File.ReadAllText(fullSchemaPath);
            var tables = JsonSerializer.Deserialize<List<TableSchema>>(schemaJson, JsonOptions);

            if (tables == null || tables.Count == 0)
            {
                return new SaveSchemaResult
                {
                    Success = false,
                    Error = "Empty or invalid schema"
                };
            }

            // Re-serialize with proper formatting and save
            var fullPath = Path.GetFullPath(outputPath);
            var json = JsonSerializer.Serialize(tables, JsonOptions);
            File.WriteAllText(fullPath, json);

            return new SaveSchemaResult
            {
                Success = true,
                TableCount = tables.Count,
                TableNames = tables.Select(t => t.EntityName).ToArray(),
                SchemaFile = fullPath
            };
        }
        catch (JsonException ex)
        {
            return new SaveSchemaResult
            {
                Success = false,
                Error = $"Invalid JSON: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new SaveSchemaResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Lists available template presets
    /// </summary>
    [McpServerTool]
    [Description("List available template presets from the templates directory. Returns preset names and which one is the default.")]
    public ListPresetsResult ListPresets(
        [Description("Templates base directory. Default: 'templates'")]
        string templatesDir = "templates")
    {
        try
        {
            var templatesPath = Path.GetFullPath(templatesDir);
            if (!Directory.Exists(templatesPath))
                return new ListPresetsResult { Success = false, Error = $"Templates directory not found: {templatesPath}" };

            if (Directory.Exists(Path.Combine(templatesPath, "$table$")))
                return new ListPresetsResult { Success = true, PresetMode = false, Message = "Templates are in legacy format (no presets). Use templatesDir as-is." };

            var presets = Directory.GetDirectories(templatesPath)
                .Select(Path.GetFileName)
                .Where(name => name != null)
                .ToList()!;

            if (presets.Count == 0)
                return new ListPresetsResult { Success = false, Error = "No presets found in templates directory." };

            return new ListPresetsResult
            {
                Success = true,
                PresetMode = true,
                Presets = presets.ToArray()
            };
        }
        catch (Exception ex)
        {
            return new ListPresetsResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Generates files from schema file
    /// </summary>
    [McpServerTool]
    [Description("Generate code files from a schema JSON file using templates. First parse SQL with 'parse_sql' or 'save_schema', then call this. Use 'list_presets' to see available template presets.")]
    public GenerateFilesResult GenerateFiles(
        [Description("Path to schema JSON file created by parse_sql or save_schema")]
        string schemaFile,
        [Description("Output directory for generated files")]
        string outputDir,
        [Description("Templates directory. Default: 'templates'")]
        string templatesDir = "templates",
        [Description("Template preset name (e.g. 'default'). If not specified and presets exist, returns error. Call 'list_presets' first.")]
        string presetName = "",
        [Description("Include virtual foreign keys inferred from naming conventions (e.g. user_id -> users.id). Default: true")]
        bool includeVirtualFks = true)
    {
        try
        {
            var schemaPath = Path.GetFullPath(schemaFile);
            if (!File.Exists(schemaPath))
                return Fail($"Schema file not found: {schemaPath}");

            var templatesPath = ResolveTemplatesPath(templatesDir, presetName, out string? resolveError);
            if (resolveError != null)
                return Fail(resolveError);

            var json = File.ReadAllText(schemaPath);
            var tables = JsonSerializer.Deserialize<List<TableSchema>>(json, JsonOptions);

            if (tables == null || tables.Count == 0)
                return Fail("No tables in schema file");

            if (includeVirtualFks)
                VirtualForeignKeyResolver.ResolveVirtualForeignKeys(tables);

            var outputPath = Path.GetFullPath(outputDir);
            Directory.CreateDirectory(outputPath);

            var fileCountBefore = Directory.GetFiles(outputPath, "*", SearchOption.AllDirectories).Length;

            FileGenerator.GenerateOtherFiles(tables, templatesPath, outputPath, includeVirtualFks);

            var fileCountAfter = Directory.GetFiles(outputPath, "*", SearchOption.AllDirectories).Length;

            return new GenerateFilesResult
            {
                Success = true,
                OutputDir = outputPath,
                TableCount = tables.Count,
                FileCount = fileCountAfter - fileCountBefore
            };
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }

    /// <summary>
    /// Quick generation: parse SQL + generate files in one call
    /// </summary>
    [McpServerTool]
    [Description("Quick generation: read SQL file, parse with regex, and generate files in one call. For simple DDL only. Use 'list_presets' to see available template presets.")]
    public GenerateFilesResult QuickGenerate(
        [Description("Path to .sql file with CREATE TABLE statements")]
        string sqlFilePath,
        [Description("Output directory for generated files")]
        string outputDir,
        [Description("Templates directory. Default: 'templates'")]
        string templatesDir = "templates",
        [Description("Template preset name (e.g. 'default'). If not specified and presets exist, returns error. Call 'list_presets' first.")]
        string presetName = "",
        [Description("Include virtual foreign keys inferred from naming conventions (e.g. user_id -> users.id). Default: true")]
        bool includeVirtualFks = true)
    {
        try
        {
            var fullSqlPath = Path.GetFullPath(sqlFilePath);
            if (!File.Exists(fullSqlPath))
                return Fail($"SQL file not found: {fullSqlPath}");

            var sql = File.ReadAllText(fullSqlPath);
            if (string.IsNullOrWhiteSpace(sql))
                return Fail("SQL file is empty");

            var templatesPath = ResolveTemplatesPath(templatesDir, presetName, out string? resolveError);
            if (resolveError != null)
                return Fail(resolveError);

            var tables = SqlParser.ParsePostgresCreateTableScript(sql);

            if (tables.Count == 0)
                return Fail("No tables found. For complex SQL, parse it yourself and use save_schema + generate_files.");

            if (includeVirtualFks)
                VirtualForeignKeyResolver.ResolveVirtualForeignKeys(tables);

            var outputPath = Path.GetFullPath(outputDir);
            Directory.CreateDirectory(outputPath);

            var fileCountBefore = Directory.GetFiles(outputPath, "*", SearchOption.AllDirectories).Length;

            FileGenerator.GenerateOtherFiles(tables, templatesPath, outputPath, includeVirtualFks);

            var fileCountAfter = Directory.GetFiles(outputPath, "*", SearchOption.AllDirectories).Length;

            return new GenerateFilesResult
            {
                Success = true,
                OutputDir = outputPath,
                TableCount = tables.Count,
                FileCount = fileCountAfter - fileCountBefore
            };
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }

    private static GenerateFilesResult Fail(string error) =>
        new() { Success = false, Error = error };

    private static string ResolveTemplatesPath(string templatesDir, string presetName, out string? error)
    {
        error = null;
        var templatesPath = Path.GetFullPath(templatesDir);

        if (!Directory.Exists(templatesPath))
        {
            error = $"Templates directory not found: {templatesPath}";
            return templatesPath;
        }

        if (!string.IsNullOrEmpty(presetName))
        {
            var presetPath = Path.Combine(templatesPath, presetName);
            if (!Directory.Exists(presetPath))
            {
                error = $"Preset '{presetName}' not found. Call 'list_presets' to see available presets.";
                return templatesPath;
            }
            return presetPath;
        }

        if (Directory.Exists(Path.Combine(templatesPath, "$table$")))
            return templatesPath;

        var presets = Directory.GetDirectories(templatesPath)
            .Select(Path.GetFileName)
            .Where(name => name != null)
            .ToList()!;

        if (presets.Count == 1)
            return Path.Combine(templatesPath, presets[0]);

        error = presets.Count > 1
            ? $"Multiple presets found ({string.Join(", ", presets)}). Specify 'presetName' parameter. Call 'list_presets' to see available presets."
            : "No presets found in templates directory.";
        return templatesPath;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
}

#region Result DTOs

public class ParseSqlResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("tableCount")]
    public int TableCount { get; set; }

    [JsonPropertyName("tables")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? TableNames { get; set; }

    [JsonPropertyName("schemaFile")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SchemaFile { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; set; }
}

public class SaveSchemaResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("tableCount")]
    public int TableCount { get; set; }

    [JsonPropertyName("tables")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? TableNames { get; set; }

    [JsonPropertyName("schemaFile")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SchemaFile { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; set; }
}

public class ListPresetsResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("presetMode")]
    public bool PresetMode { get; set; }

    [JsonPropertyName("presets")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Presets { get; set; }

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; set; }
}

public class GenerateFilesResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("outputDir")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OutputDir { get; set; }

    [JsonPropertyName("tableCount")]
    public int TableCount { get; set; }

    [JsonPropertyName("fileCount")]
    public int FileCount { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; set; }
}

#endregion
