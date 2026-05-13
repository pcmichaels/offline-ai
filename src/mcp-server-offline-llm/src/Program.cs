using McpServerOfflineLlm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(consoleOptions =>
{
    consoleOptions.LogToStandardErrorThreshold = LogLevel.Information;
});

var baseUrl = builder.Configuration["LmStudio:BaseUrl"]?.Trim();
var model = builder.Configuration["LmStudio:Model"]?.Trim();
var configModel = string.IsNullOrWhiteSpace(model) ? null : model;

builder.Services.AddSingleton<McpFileLogger>();

builder.Services.AddSingleton<LmStudioClient>(sp =>
{
    var logger = sp.GetRequiredService<McpFileLogger>();
    return new LmStudioClient(baseUrl, configModel, logger);
});

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

try
{
    Console.Error.WriteLine("[MCP] Offline-LLM server starting. Tools: create_file, append_to_file, chat_with_llm, complete_with_llm");
    Console.Error.WriteLine("[MCP] stderr = tool/request logs. If you ask to create a file but see no [TOOL] CreateFile log, LM Studio isn't invoking the tool.");
} catch { }

await builder.Build().RunAsync();
