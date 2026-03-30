using SQLFileGenerator.Core.Models;
using SQLFileGenerator.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace SQLFileGenerator.Core.Services;

/// <summary>
/// Service for generating code files from SQL schemas
/// </summary>
public class CodeGenerationService : ICodeGenerationService
{
    private readonly ISqlParsingService _parsingService;
    private readonly ILogger<CodeGenerationService>? _logger;
    private readonly string _templatesRoot;

    public CodeGenerationService(
        ISqlParsingService parsingService,
        ILogger<CodeGenerationService>? logger = null,
        string? templatesRoot = null)
    {
        _parsingService = parsingService ?? throw new ArgumentNullException(nameof(parsingService));
        _logger = logger;
        _templatesRoot = templatesRoot ?? "templates";
    }

    /// <inheritdoc />
    public async Task<GenerationResult> GenerateFromSqlFileAsync(
        string sqlFilePath,
        string profileName,
        string outputDirectory,
        bool useRegexParser = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation(
                "Generating code: SQL={SqlFile}, Profile={Profile}, Output={Output}",
                sqlFilePath, profileName, outputDirectory);

            // Validate inputs
            if (string.IsNullOrWhiteSpace(sqlFilePath))
                throw new ArgumentException("SQL file path cannot be empty", nameof(sqlFilePath));

            if (string.IsNullOrWhiteSpace(profileName))
                throw new ArgumentException("Profile name cannot be empty", nameof(profileName));

            if (string.IsNullOrWhiteSpace(outputDirectory))
                throw new ArgumentException("Output directory cannot be empty", nameof(outputDirectory));

            // Parse SQL
            var parseResult = await _parsingService.ParseSqlFromFileAsync(
                sqlFilePath, useRegexParser, cancellationToken);

            if (!parseResult.Success || parseResult.Tables == null || parseResult.Tables.Count == 0)
            {
                return new GenerationResult
                {
                    Success = false,
                    ErrorMessage = parseResult.ErrorMessage ?? "No tables found in SQL script",
                    Profile = profileName,
                    OutputDirectory = outputDirectory
                };
            }

            // Generate code from tables
            return await GenerateFromTablesAsync(
                parseResult.Tables, profileName, outputDirectory, cancellationToken);
        }
        catch (Exception ex) when (ex is not CodeGenerationException)
        {
            _logger?.LogError(ex, "Error generating code from SQL file: {SqlFile}", sqlFilePath);

            return new GenerationResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Profile = profileName,
                OutputDirectory = outputDirectory
            };
        }
    }

    /// <inheritdoc />
    public Task<GenerationResult> GenerateFromTablesAsync(
        List<TableSchema> tables,
        string profileName,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger?.LogDebug("Generating code for {TableCount} tables with profile {Profile}",
                tables.Count, profileName);

            // Validate profile
            var templatesDir = Path.Combine(_templatesRoot, profileName);
            if (!Directory.Exists(templatesDir))
            {
                throw new TemplateProfileException($"Profile '{profileName}' not found at {templatesDir}")
                {
                    ProfileName = profileName
                };
            }

            // Create output directory if needed
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
                _logger?.LogDebug("Created output directory: {OutputDir}", outputDirectory);
            }

            // Track files before generation
            var filesBefore = GetAllFilesRecursive(outputDirectory).Count;

            // Generate files using FileGenerator
            FileGenerator.GenerateOtherFiles(tables, templatesDir, outputDirectory);

            // Track files after generation
            var filesAfter = GetAllFilesRecursive(outputDirectory).Count;
            var filesGenerated = filesAfter - filesBefore;

            // Get list of generated files (relative paths)
            var generatedFiles = GetAllFilesRecursive(outputDirectory)
                .Select(f => Path.GetRelativePath(outputDirectory, f))
                .ToList();

            var executionTime = DateTime.UtcNow - startTime;

            _logger?.LogInformation(
                "Successfully generated {FileCount} files for {TableCount} tables in {ExecutionTime}ms",
                filesGenerated, tables.Count, executionTime.TotalMilliseconds);

            return Task.FromResult(new GenerationResult
            {
                Success = true,
                OutputDirectory = outputDirectory,
                FilesGenerated = filesGenerated,
                TableNames = tables.Select(t => t.EntityName).ToList(),
                Profile = profileName,
                GeneratedFiles = generatedFiles
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error generating code for {TableCount} tables", tables.Count);

            var generationException = new CodeGenerationException(
                $"Failed to generate code: {ex.Message}", ex)
            {
                ProfileName = profileName
            };

            return Task.FromResult(new GenerationResult
            {
                Success = false,
                ErrorMessage = generationException.Message,
                Profile = profileName,
                OutputDirectory = outputDirectory
            });
        }
    }

    /// <inheritdoc />
    public Task<List<ProfileMetadata>> GetAvailableProfilesAsync()
    {
        try
        {
            _logger?.LogDebug("Getting available profiles from {TemplatesRoot}", _templatesRoot);

            if (!Directory.Exists(_templatesRoot))
            {
                _logger?.LogWarning("Templates directory not found: {TemplatesRoot}", _templatesRoot);
                return Task.FromResult(new List<ProfileMetadata>());
            }

            var profiles = Directory.GetDirectories(_templatesRoot)
                .Select(dir => Path.GetFileName(dir))
                .Where(name => !string.IsNullOrEmpty(name) && !name.StartsWith("_"))
                .Select(name =>
                {
                    var profilePath = Path.Combine(_templatesRoot, name!);
                    var files = GetAllFilesRecursive(profilePath);
                    var directories = GetAllDirectoriesRecursive(profilePath);

                    return new ProfileMetadata
                    {
                        Name = name!,
                        Description = GetProfileDescription(name!),
                        FileCount = files.Count,
                        DirectoryCount = directories.Count,
                        Path = profilePath
                    };
                })
                .OrderBy(p => p.Name)
                .ToList();

            _logger?.LogInformation("Found {ProfileCount} profiles", profiles.Count);

            return Task.FromResult(profiles);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting available profiles");
            throw new TemplateProfileException("Failed to get available profiles", ex);
        }
    }

    /// <inheritdoc />
    public Task<ProfileStructure> GetProfileStructureAsync(string profileName)
    {
        try
        {
            _logger?.LogDebug("Getting structure for profile: {Profile}", profileName);

            var profilePath = Path.Combine(_templatesRoot, profileName);
            if (!Directory.Exists(profilePath))
            {
                throw new TemplateProfileException($"Profile '{profileName}' not found")
                {
                    ProfileName = profileName
                };
            }

            var files = GetAllFilesRecursive(profilePath)
                .Select(f => Path.GetRelativePath(profilePath, f))
                .ToList();

            var directories = GetAllDirectoriesRecursive(profilePath)
                .Select(d => Path.GetRelativePath(profilePath, d))
                .ToList();

            var structure = new ProfileStructure
            {
                Name = profileName,
                Files = files,
                Directories = directories,
                Metadata = new Dictionary<string, object>
                {
                    ["FileCount"] = files.Count,
                    ["DirectoryCount"] = directories.Count,
                    ["Path"] = profilePath
                }
            };

            return Task.FromResult(structure);
        }
        catch (Exception ex) when (ex is not TemplateProfileException)
        {
            _logger?.LogError(ex, "Error getting profile structure: {Profile}", profileName);
            throw new TemplateProfileException($"Failed to get profile structure for '{profileName}'", ex)
            {
                ProfileName = profileName
            };
        }
    }

    /// <summary>
    /// Gets all files recursively from directory
    /// </summary>
    private List<string> GetAllFilesRecursive(string directory)
    {
        if (!Directory.Exists(directory))
            return new List<string>();

        try
        {
            return Directory.GetFiles(directory, "*", SearchOption.AllDirectories).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// Gets all directories recursively from directory
    /// </summary>
    private List<string> GetAllDirectoriesRecursive(string directory)
    {
        if (!Directory.Exists(directory))
            return new List<string>();

        try
        {
            return Directory.GetDirectories(directory, "*", SearchOption.AllDirectories).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// Gets profile description from README or directory name
    /// </summary>
    private string? GetProfileDescription(string profileName)
    {
        // Try to read description from README.md or profile.txt
        var profilePath = Path.Combine(_templatesRoot, profileName);
        var readmePath = Path.Combine(profilePath, "README.md");
        var profileTxtPath = Path.Combine(profilePath, "profile.txt");

        if (File.Exists(readmePath))
        {
            try
            {
                var lines = File.ReadAllLines(readmePath);
                return lines.FirstOrDefault()?.TrimStart('#', ' ');
            }
            catch { }
        }

        if (File.Exists(profileTxtPath))
        {
            try
            {
                return File.ReadAllText(profileTxtPath).Trim();
            }
            catch { }
        }

        // Generate description from profile name
        return profileName switch
        {
            "default" => "Default profile with full stack templates",
            "clean-arch" => "Clean Architecture profile",
            _ => null
        };
    }
}
