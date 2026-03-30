using SQLFileGenerator.Core.Models;

namespace SQLFileGenerator.Core.Services;

/// <summary>
/// Service for generating code from SQL schemas
/// </summary>
public interface ICodeGenerationService
{
    /// <summary>
    /// Generates code files from SQL file
    /// </summary>
    /// <param name="sqlFilePath">Path to SQL file</param>
    /// <param name="profileName">Template profile name</param>
    /// <param name="outputDirectory">Directory for generated files</param>
    /// <param name="useRegexParser">Use regex parser instead of LLM</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generation result with output directory and metadata</returns>
    Task<GenerationResult> GenerateFromSqlFileAsync(
        string sqlFilePath,
        string profileName,
        string outputDirectory,
        bool useRegexParser = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates code files from already parsed tables
    /// </summary>
    /// <param name="tables">List of table schemas</param>
    /// <param name="profileName">Template profile name</param>
    /// <param name="outputDirectory">Directory for generated files</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generation result with output directory and metadata</returns>
    Task<GenerationResult> GenerateFromTablesAsync(
        List<TableSchema> tables,
        string profileName,
        string outputDirectory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets list of available template profiles with metadata
    /// </summary>
    /// <returns>List of profile metadata</returns>
    Task<List<ProfileMetadata>> GetAvailableProfilesAsync();

    /// <summary>
    /// Gets structure of specific template profile
    /// </summary>
    /// <param name="profileName">Profile name</param>
    /// <returns>Profile structure with files and directories</returns>
    Task<ProfileStructure> GetProfileStructureAsync(string profileName);
}
