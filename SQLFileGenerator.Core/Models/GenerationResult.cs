namespace SQLFileGenerator.Core.Models;

/// <summary>
/// Result of code generation
/// </summary>
public class GenerationResult
{
    /// <summary>
    /// Path to output directory with generated files
    /// </summary>
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Number of files generated
    /// </summary>
    public int FilesGenerated { get; set; }

    /// <summary>
    /// List of table names processed
    /// </summary>
    public List<string> TableNames { get; set; } = new();

    /// <summary>
    /// Whether generation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if generation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Template profile used
    /// </summary>
    public string Profile { get; set; } = string.Empty;

    /// <summary>
    /// List of generated files (relative paths from output directory)
    /// </summary>
    public List<string> GeneratedFiles { get; set; } = new();
}

/// <summary>
/// Metadata about a template profile
/// </summary>
public class ProfileMetadata
{
    /// <summary>
    /// Profile name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Profile description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Number of template files in profile
    /// </summary>
    public int FileCount { get; set; }

    /// <summary>
    /// Number of directories in profile
    /// </summary>
    public int DirectoryCount { get; set; }

    /// <summary>
    /// Path to profile directory
    /// </summary>
    public string Path { get; set; } = string.Empty;
}

/// <summary>
/// Structure of a template profile
/// </summary>
public class ProfileStructure
{
    /// <summary>
    /// Profile name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// List of template files in profile
    /// </summary>
    public List<string> Files { get; set; } = new();

    /// <summary>
    /// List of directories in profile
    /// </summary>
    public List<string> Directories { get; set; } = new();

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
