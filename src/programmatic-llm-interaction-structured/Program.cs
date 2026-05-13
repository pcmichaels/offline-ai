using Microsoft.Extensions.Configuration;
using ProgrammaticLlmInteractionStructured;

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

var baseUrl = config["LmStudio:BaseUrl"]?.Trim();
var configModel = config["LmStudio:Model"]?.Trim();
var message = config["Prompt:Message"]?.Trim();
var systemRole = config["Prompt:SystemRole"]?.Trim();

if (string.IsNullOrEmpty(message))
{
    Console.WriteLine("Error: Prompt:Message is required in appsettings.json.");
    return 1;
}

var displayBaseUrl = string.IsNullOrEmpty(baseUrl) ? "http://localhost:1234/v1/" : baseUrl;

Console.WriteLine("Programmatic LLM Interaction — Structured JSON (LM Studio)");
Console.WriteLine("=========================================================\n");
Console.WriteLine("Ensure LM Studio is running with a model loaded and the local server started.");
Console.WriteLine($"Endpoint: {displayBaseUrl}");
if (!string.IsNullOrEmpty(configModel))
    Console.WriteLine($"Model: {configModel}");
Console.WriteLine();

using var logger = new Logger();
using var client = new LmStudioClient(baseUrl: baseUrl, model: string.IsNullOrWhiteSpace(configModel) ? null : configModel, logger: logger);
try
{
    var model = await client.GetModelIdAsync();
    Console.WriteLine($"Using model: {model}\n");
}
catch
{
    Console.WriteLine("Could not fetch models from LM Studio; using 'local-model'. Ensure the server is running.\n");
}

try
{
    var response = await client.ChatAsync(message, string.IsNullOrEmpty(systemRole) ? null : systemRole);
    Console.WriteLine(response);
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine("Make sure LM Studio is running with the local server enabled.");
    return 1;
}

return 0;
