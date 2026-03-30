namespace SQLFileGenerator.Core.Exceptions;

/// <summary>
/// Base exception for SQLFileGenerator operations
/// </summary>
public class SqlGeneratorException : Exception
{
    public SqlGeneratorException(string message) : base(message) { }

    public SqlGeneratorException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// Exception thrown during SQL parsing
/// </summary>
public class SqlParsingException : SqlGeneratorException
{
    /// <summary>
    /// Path to SQL file that failed to parse
    /// </summary>
    public string? SqlFilePath { get; set; }

    /// <summary>
    /// Parser type used (regex or llm)
    /// </summary>
    public string? ParserType { get; set; }

    public SqlParsingException(string message) : base(message) { }

    public SqlParsingException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// Exception thrown during code generation
/// </summary>
public class CodeGenerationException : SqlGeneratorException
{
    /// <summary>
    /// Profile name that caused the error
    /// </summary>
    public string? ProfileName { get; set; }

    /// <summary>
    /// Template path that failed
    /// </summary>
    public string? TemplatePath { get; set; }

    public CodeGenerationException(string message) : base(message) { }

    public CodeGenerationException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// Exception thrown for template profile errors
/// </summary>
public class TemplateProfileException : SqlGeneratorException
{
    /// <summary>
    /// Profile name that caused the error
    /// </summary>
    public string? ProfileName { get; set; }

    public TemplateProfileException(string message) : base(message) { }

    public TemplateProfileException(string message, Exception innerException)
        : base(message, innerException) { }
}
