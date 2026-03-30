namespace SQLFileGenerator.Core.Models;

/// <summary>
/// Result of parsing SQL script
/// </summary>
public class ParseResult
{
    /// <summary>
    /// List of parsed tables
    /// </summary>
    public List<TableSchema> Tables { get; set; } = new();

    /// <summary>
    /// Parser used (regex or llm)
    /// </summary>
    public string ParserUsed { get; set; } = string.Empty;

    /// <summary>
    /// Whether parsing was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if parsing failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of tables parsed
    /// </summary>
    public int TableCount => Tables?.Count ?? 0;

    /// <summary>
    /// Additional metadata (execution time, warnings, etc.)
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
