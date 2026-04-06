using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SqlGenerator.Mcp.Prompts;
using SqlGenerator.Mcp.Tools;

var builder = Host.CreateApplicationBuilder(args);

// Logging to stderr (MCP requirement for stdio transport)
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Register MCP server with tools and prompts
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<SqlGeneratorTools>()
    .WithPrompts<SqlParsingPrompts>();

await builder.Build().RunAsync();
