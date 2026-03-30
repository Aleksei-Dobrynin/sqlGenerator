using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SQLFileGenerator.Core.Services;
using SQLFileGenerator.LlmParser;
using SQLFileGenerator.Mcp.Server;

// MCP Server entry point - uses STDIO transport
var builder = Host.CreateApplicationBuilder(args);

// CRITICAL: Log to stderr only (stdout is for JSON-RPC messages)
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Load configuration
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.mcp.json", optional: true)
    .AddJsonFile("appsettings.json", optional: true);

// Register LLM configuration
builder.Services.Configure<LlmConfiguration>(
    builder.Configuration.GetSection("LlmParser"));

// Register Core services
builder.Services.AddSingleton<ISqlParsingService, SqlParsingService>();
builder.Services.AddSingleton<ICodeGenerationService>(sp =>
{
    var parsingService = sp.GetRequiredService<ISqlParsingService>();
    var logger = sp.GetService<ILogger<CodeGenerationService>>();
    var templatesPath = builder.Configuration["TemplatesPath"] ?? "templates";
    return new CodeGenerationService(parsingService, logger, templatesPath);
});

// Register MCP Server with STDIO transport
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "SQLFileGenerator",
            Version = "1.0.0"
        };
    })
    .WithStdioServerTransport()
    .WithTools<SqlGeneratorTools>();

try
{
    await builder.Build().RunAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Fatal error: {ex.Message}");
    Console.Error.WriteLine(ex.StackTrace);
    Environment.Exit(1);
}
