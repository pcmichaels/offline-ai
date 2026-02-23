using Microsoft.Extensions.Configuration;
using ProgrammaticLlmInteraction;

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

var baseUrl = config["LmStudio:BaseUrl"]?.Trim();
var configModel = config["LmStudio:Model"]?.Trim();
var displayBaseUrl = string.IsNullOrEmpty(baseUrl) ? "http://localhost:1234/v1/" : baseUrl;

Console.WriteLine("Programmatic LLM Interaction (LM Studio)");
Console.WriteLine("=======================================\n");
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

while (true)
{
    Console.WriteLine("Choose an example:");
    Console.WriteLine("  1. Chat completion (single response)");
    Console.WriteLine("  2. Text completion (legacy endpoint)");
    Console.WriteLine("  3. Streaming chat (token-by-token)");
    Console.WriteLine("  4. Exit");
    Console.Write("\nEnter choice (1-4): ");

    var input = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(input))
    {
        Console.WriteLine("Invalid input.\n");
        continue;
    }

    switch (input)
    {
        case "1":
            await RunChatAsync(client);
            break;
        case "2":
            await RunCompletionAsync(client);
            break;
        case "3":
            await RunStreamingAsync(client);
            break;
        case "4":
            Console.WriteLine("Goodbye!");
            return;
        default:
            Console.WriteLine("Invalid choice.\n");
            break;
    }
}

static async Task RunChatAsync(LmStudioClient client)
{
    Console.Write("\nEnter your message: ");
    var message = Console.ReadLine();
    if (string.IsNullOrEmpty(message)) { Console.WriteLine("No message entered.\n"); return; }

    try
    {
        Console.WriteLine("\nResponse:");
        var response = await client.ChatAsync(message);
        Console.WriteLine(response);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        Console.WriteLine("Make sure LM Studio is running with the local server enabled.");
    }

    Console.WriteLine();
}

static async Task RunCompletionAsync(LmStudioClient client)
{
    Console.Write("\nEnter your prompt: ");
    var prompt = Console.ReadLine();
    if (string.IsNullOrEmpty(prompt)) { Console.WriteLine("No prompt entered.\n"); return; }

    try
    {
        Console.WriteLine("\nCompletion:");
        var result = await client.CompleteAsync(prompt);
        Console.WriteLine(result);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        Console.WriteLine("Make sure LM Studio is running with the local server enabled.");
    }

    Console.WriteLine();
}

static async Task RunStreamingAsync(LmStudioClient client)
{
    Console.Write("\nEnter your message: ");
    var message = Console.ReadLine();
    if (string.IsNullOrEmpty(message)) { Console.WriteLine("No message entered.\n"); return; }

    try
    {
        Console.WriteLine("\nStreaming response:");
        await foreach (var token in client.StreamChatAsync(message))
        {
            Console.Write(token);
        }
        Console.WriteLine();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        Console.WriteLine("Make sure LM Studio is running with the local server enabled.");
    }

    Console.WriteLine();
}
