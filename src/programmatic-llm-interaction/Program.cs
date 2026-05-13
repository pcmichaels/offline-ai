using Microsoft.Extensions.Configuration;
using ProgrammaticLlmInteraction;

var argsList = args is { Length: > 0 } ? [.. args] : Array.Empty<string>();
var usePromptMode = argsList.Contains("--prompt");
var useReturnValueOnly = argsList.Contains("--return-value-only");

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

var baseUrl = config["LmStudio:BaseUrl"]?.Trim();
var configModel = config["LmStudio:Model"]?.Trim();

string message;
string? systemRole;

if (useReturnValueOnly)
{
    // Ask yes/no: 1 = Motorhead is the answer, 0 = any other band. Avoids "1" being read as "#1 band".
    //message = "Is the greatest band of all time Motorhead? Reply with only 1 for yes or 0 for no.";
    message = "Is the greatest band of all time The Beatles? Reply with only 1 for yes or 0 for no.";
    systemRole = "Reply with a single digit only: 1 or 0.";
}
else
{
    message = config["Prompt:Message"]?.Trim() ?? "";
    systemRole = GetActiveSystemRole(config);
    if (string.IsNullOrEmpty(message))
    {
        Console.WriteLine("Error: Prompt:Message is required in appsettings.json (or use --return-value-only).");
        return 1;
    }
}

var displayBaseUrl = string.IsNullOrEmpty(baseUrl) ? "http://localhost:1234/v1/" : baseUrl;

if (!useReturnValueOnly)
{
    Console.WriteLine("Programmatic LLM Interaction (LM Studio)");
    Console.WriteLine("=======================================\n");
    Console.WriteLine("Ensure LM Studio is running with a model loaded and the local server started.");
    Console.WriteLine($"Endpoint: {displayBaseUrl}");
    if (!string.IsNullOrEmpty(configModel))
        Console.WriteLine($"Model: {configModel}");
    Console.WriteLine($"\nSending message: {message}\n");
}

using var logger = new Logger();
using var client = new LmStudioClient(baseUrl: baseUrl, model: string.IsNullOrWhiteSpace(configModel) ? null : configModel, logger: logger);

if (!useReturnValueOnly)
{
    try
    {
        var model = await client.GetModelIdAsync();
        Console.WriteLine($"Using model: {model}\n");
    }
    catch
    {
        Console.WriteLine("Could not fetch models from LM Studio; using 'local-model'. Ensure the server is running.\n");
    }
}

try
{
    var response = await client.ChatAsync(message, string.IsNullOrEmpty(systemRole) ? null : systemRole);

    if (useReturnValueOnly)
    {
        Console.WriteLine(response);
        return 0;
    }

    Console.WriteLine("Response:");
    Console.WriteLine(response);
}
catch (Exception ex)
{
    if (useReturnValueOnly)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 1;
    }
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine("Make sure LM Studio is running with the local server enabled.");
    return 1;
}

return 0;

static string? GetActiveSystemRole(IConfiguration config)
{
    var section = config.GetSection("Prompt:SystemRoles");
    if (!section.Exists()) return config["Prompt:SystemRole"]?.Trim();

    for (var i = 0; ; i++)
    {
        var use = section[$"{i}:use"];
        if (string.IsNullOrEmpty(use)) break;
        if (bool.TryParse(use, out var isUsed) && isUsed)
            return section[$"{i}:value"]?.Trim();
    }
    return null;
}
