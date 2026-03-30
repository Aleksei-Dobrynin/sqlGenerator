using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SQLFileGenerator.Core.Services;

namespace SQLFileGenerator.Mcp.Server;

/// <summary>
/// MCP Tools for SQL parsing and code generation.
/// Token-optimized: returns only metadata, not full file contents.
/// </summary>
[McpServerToolType]
public class SqlGeneratorTools
{
    private readonly ISqlParsingService _parsingService;
    private readonly ICodeGenerationService _codeGenService;
    private readonly ILogger<SqlGeneratorTools>? _logger;

    public SqlGeneratorTools(
        ISqlParsingService parsingService,
        ICodeGenerationService codeGenService,
        ILogger<SqlGeneratorTools>? logger = null)
    {
        _parsingService = parsingService;
        _codeGenService = codeGenService;
        _logger = logger;
    }

    [McpServerTool(Name = "parse_sql")]
    [Description("Parses PostgreSQL CREATE TABLE script and returns table metadata. " +
        "Returns compact JSON with table names, column counts, and foreign keys. " +
        "Token-optimized: does not return full column details.")]
    public async Task<string> ParseSql(
        [Description("Absolute path to SQL file containing CREATE TABLE statements")]
        string sql_file_path,
        [Description("Use regex parser (true) or LLM parser (false). Default: true")]
        bool use_regex = true,
        CancellationToken cancellationToken = default)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(sql_file_path))
            return JsonSerializer.Serialize(new { error = "sql_file_path is required" });

        if (!File.Exists(sql_file_path))
            return JsonSerializer.Serialize(new { error = $"SQL file not found: {sql_file_path}" });

        _logger?.LogInformation("Parsing SQL: {Path}, Parser: {Parser}", sql_file_path, use_regex ? "regex" : "llm");

        var result = await _parsingService.ParseSqlFromFileAsync(sql_file_path, use_regex, cancellationToken);

        if (!result.Success)
            return JsonSerializer.Serialize(new { error = result.ErrorMessage ?? "Failed to parse SQL" });

        // Compact response (token optimization)
        var output = new
        {
            tables = result.Tables.Select(t => new
            {
                table_name = t.TableName,
                entity_name = t.EntityName,
                column_count = t.Columns.Count,
                foreign_key_count = t.ForeignKeys.Count,
                primary_key = t.Columns.FirstOrDefault(c => c.IsPrimaryKey)?.Name
            }),
            metadata = new
            {
                table_count = result.TableCount,
                parser_used = result.ParserUsed,
                sql_file = sql_file_path
            }
        };

        return JsonSerializer.Serialize(output);
    }

    [McpServerTool(Name = "generate_code")]
    [Description("Generates code files from SQL script using Scriban templates. " +
        "Returns output path and file count (NOT file contents for token optimization). " +
        "Use Read tool to inspect generated files if needed.")]
    public async Task<string> GenerateCode(
        [Description("Absolute path to SQL file")]
        string sql_file_path,
        [Description("Absolute path to output directory")]
        string output_dir,
        [Description("Template profile name (e.g., 'default', 'clean-arch'). Default: 'default'")]
        string profile = "default",
        [Description("Use regex parser instead of LLM. Default: true")]
        bool use_regex = true,
        CancellationToken cancellationToken = default)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(sql_file_path))
            return JsonSerializer.Serialize(new { error = "sql_file_path is required" });

        if (string.IsNullOrWhiteSpace(output_dir))
            return JsonSerializer.Serialize(new { error = "output_dir is required" });

        if (!File.Exists(sql_file_path))
            return JsonSerializer.Serialize(new { error = $"SQL file not found: {sql_file_path}" });

        _logger?.LogInformation("Generating: SQL={Sql}, Profile={Profile}, Output={Output}",
            sql_file_path, profile, output_dir);

        var result = await _codeGenService.GenerateFromSqlFileAsync(
            sql_file_path, profile, output_dir, use_regex, cancellationToken);

        if (!result.Success)
            return JsonSerializer.Serialize(new { error = result.ErrorMessage ?? "Code generation failed" });

        // Compact response (NO file contents)
        var output = new
        {
            output_directory = result.OutputDirectory,
            files_generated = result.FilesGenerated,
            tables = result.TableNames,
            profile = result.Profile
        };

        return JsonSerializer.Serialize(output);
    }

    [McpServerTool(Name = "list_profiles")]
    [Description("Lists available template profiles with metadata. " +
        "Returns array of profiles with name, description, and file count. " +
        "Use before generate_code to discover available templates.")]
    public async Task<string> ListProfiles(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Listing template profiles");

        var profiles = await _codeGenService.GetAvailableProfilesAsync();

        var output = new
        {
            profiles = profiles.Select(p => new
            {
                name = p.Name,
                description = p.Description,
                file_count = p.FileCount,
                directory_count = p.DirectoryCount
            })
        };

        return JsonSerializer.Serialize(output);
    }
}
