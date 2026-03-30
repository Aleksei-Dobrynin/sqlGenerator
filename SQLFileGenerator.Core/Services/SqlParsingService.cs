using SQLFileGenerator.Core.Models;
using SQLFileGenerator.Core.Exceptions;
using SQLFileGenerator.LlmParser;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace SQLFileGenerator.Core.Services;

/// <summary>
/// Service for parsing SQL scripts using regex or LLM
/// </summary>
public class SqlParsingService : ISqlParsingService
{
    private readonly LlmConfiguration? _llmConfig;
    private readonly ILogger<SqlParsingService>? _logger;

    public SqlParsingService(
        IOptions<LlmConfiguration>? llmConfig = null,
        ILogger<SqlParsingService>? logger = null)
    {
        _llmConfig = llmConfig?.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ParseResult> ParseSqlFromFileAsync(
        string sqlFilePath,
        bool useRegex = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation("Parsing SQL file: {FilePath}, Parser: {Parser}",
                sqlFilePath, useRegex ? "regex" : "llm");

            // Validate file path
            if (string.IsNullOrWhiteSpace(sqlFilePath))
            {
                throw new ArgumentException("SQL file path cannot be empty", nameof(sqlFilePath));
            }

            if (!File.Exists(sqlFilePath))
            {
                throw new FileNotFoundException($"SQL file not found: {sqlFilePath}", sqlFilePath);
            }

            // Read SQL content
            var sqlScript = await File.ReadAllTextAsync(sqlFilePath, cancellationToken);

            if (string.IsNullOrWhiteSpace(sqlScript))
            {
                return new ParseResult
                {
                    Success = false,
                    ErrorMessage = "SQL file is empty",
                    ParserUsed = useRegex ? "regex" : "llm"
                };
            }

            // Parse SQL
            return await ParseSqlFromStringAsync(sqlScript, useRegex, cancellationToken);
        }
        catch (Exception ex) when (ex is not SqlParsingException)
        {
            _logger?.LogError(ex, "Error parsing SQL file: {FilePath}", sqlFilePath);

            return new ParseResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ParserUsed = useRegex ? "regex" : "llm",
                Metadata = new Dictionary<string, object>
                {
                    ["ExceptionType"] = ex.GetType().Name,
                    ["SqlFilePath"] = sqlFilePath
                }
            };
        }
    }

    /// <inheritdoc />
    public async Task<ParseResult> ParseSqlFromStringAsync(
        string sqlScript,
        bool useRegex = true,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger?.LogDebug("Parsing SQL script (length: {Length} chars), Parser: {Parser}",
                sqlScript.Length, useRegex ? "regex" : "llm");

            List<TableSchema> tables;
            string parserUsed;

            if (useRegex)
            {
                // Use regex parser (synchronous)
                tables = SqlParser.ParsePostgresCreateTableScript(sqlScript);
                parserUsed = "regex";
            }
            else
            {
                // Use LLM parser (asynchronous)
                if (_llmConfig == null)
                {
                    throw new SqlParsingException(
                        "LLM configuration is not provided. Cannot use LLM parser.");
                }

                using var llmService = new LlmParserService(_llmConfig);
                tables = await llmService.ParseSqlAsync(sqlScript, cancellationToken);
                parserUsed = "llm";
            }

            var executionTime = DateTime.UtcNow - startTime;

            _logger?.LogInformation("Successfully parsed {TableCount} tables in {ExecutionTime}ms",
                tables.Count, executionTime.TotalMilliseconds);

            return new ParseResult
            {
                Tables = tables,
                ParserUsed = parserUsed,
                Success = true,
                Metadata = new Dictionary<string, object>
                {
                    ["ExecutionTimeMs"] = executionTime.TotalMilliseconds,
                    ["ScriptLength"] = sqlScript.Length
                }
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error parsing SQL script with {Parser} parser",
                useRegex ? "regex" : "llm");

            var parsingException = new SqlParsingException(
                $"Failed to parse SQL script: {ex.Message}", ex)
            {
                ParserType = useRegex ? "regex" : "llm"
            };

            return new ParseResult
            {
                Success = false,
                ErrorMessage = parsingException.Message,
                ParserUsed = useRegex ? "regex" : "llm",
                Metadata = new Dictionary<string, object>
                {
                    ["ExceptionType"] = ex.GetType().Name,
                    ["ExecutionTimeMs"] = (DateTime.UtcNow - startTime).TotalMilliseconds
                }
            };
        }
    }
}
