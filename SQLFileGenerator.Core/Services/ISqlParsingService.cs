using SQLFileGenerator.Core.Models;

namespace SQLFileGenerator.Core.Services;

/// <summary>
/// Service for parsing SQL scripts
/// </summary>
public interface ISqlParsingService
{
    /// <summary>
    /// Parses SQL script from file
    /// </summary>
    /// <param name="sqlFilePath">Path to SQL file</param>
    /// <param name="useRegex">Use regex parser (true) or LLM parser (false)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Parse result with tables and metadata</returns>
    Task<ParseResult> ParseSqlFromFileAsync(
        string sqlFilePath,
        bool useRegex = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses SQL script from string
    /// </summary>
    /// <param name="sqlScript">SQL script content</param>
    /// <param name="useRegex">Use regex parser (true) or LLM parser (false)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Parse result with tables and metadata</returns>
    Task<ParseResult> ParseSqlFromStringAsync(
        string sqlScript,
        bool useRegex = true,
        CancellationToken cancellationToken = default);
}
