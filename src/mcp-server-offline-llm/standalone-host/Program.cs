using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using McpServerOfflineLlm;
using ModelContextProtocol.Client;

// --- Configuration ---
var baseDir = AppContext.BaseDirectory;
var config = new ConfigurationBuilder()
    .SetBasePath(baseDir)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var baseUrl = config["LmStudio:BaseUrl"]?.Trim() ?? "http://localhost:1234/v1/";
var model = config["LmStudio:Model"]?.Trim();
var configModel = string.IsNullOrWhiteSpace(model) ? null : model;
var prompt = config["Demo:Prompt"]?.Trim() ?? "Output the text \"hello world\" to a file in c:\\tmp called hello.txt";

var mcpProjectPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "src", "McpServerOfflineLlm.csproj"));
if (!File.Exists(mcpProjectPath))
{
    Console.WriteLine($"Error: MCP server project not found at {mcpProjectPath}");
    return 1;
}

Console.WriteLine("Standalone MCP + LM Studio Demo (LLM Intent Routing)");
Console.WriteLine("===================================================");
Console.WriteLine("Uses LLM to classify file intent—no keyword matching.");
Console.WriteLine("Ensure LM Studio is running with a model loaded.");
Console.WriteLine($"LM Studio: {baseUrl}");
Console.WriteLine();

using var lmStudio = new LmStudioClient(baseUrl, configModel, null);

var transport = new StdioClientTransport(new StdioClientTransportOptions
{
    Command = "dotnet",
    Arguments = ["run", "--project", mcpProjectPath],
    Name = "offline-ai-mcp"
});

await using var mcpClient = await McpClient.CreateAsync(transport);
var tools = await mcpClient.ListToolsAsync();
Console.WriteLine($"Connected to MCP server. Tools: {string.Join(", ", tools.Select(t => t.Name))}");
Console.WriteLine($"Prompt: {prompt}");
Console.WriteLine();

var messages = new List<object>();
const double ConfidenceThreshold = 0.8;

// --- System prompt for intent classification ---
const string ClassificationSystemPrompt = """
You are a strict JSON classifier. You MUST respond with ONLY valid JSON—no markdown, no explanation, no extra text.

Given the user's message and optional conversation context, classify whether they intend a file operation.

Output EXACTLY this JSON schema:
{
  "intent": "create_file" | "append_to_file" | "none",
  "confidence": 0.0 to 1.0,
  "reason": "brief explanation",
  "path": "full file path if detectable, else null",
  "content": "content to write/append if detectable, else null"
}

Rules:
- intent "create_file": user wants to create or overwrite a new file
- intent "append_to_file": user wants to add content to an existing file
- intent "none": user is NOT clearly requesting a file operation—general chat, questions, or ambiguous
- confidence: 0.0-1.0. Use >= 0.8 only when the file intent is clear and path/content are extractable
- path: extract the file path (e.g. C:\\tmp\\hello.txt). Use null if not specified
- content: extract what should be written/appended. For "append today's date", use current date. Use null if not specified

Examples:

User: "Hello, how are you?"
{"intent":"none","confidence":0.0,"reason":"General greeting, no file operation","path":null,"content":null}

User: "Create a file at C:\\temp\\notes.txt with content Hello World"
{"intent":"create_file","confidence":0.95,"reason":"Explicit create with path and content","path":"C:\\temp\\notes.txt","content":"Hello World"}

User: "Append today's date to C:\\tmp\\log.txt"
{"intent":"append_to_file","confidence":0.9,"reason":"Append to file with path","path":"C:\\tmp\\log.txt","content":"2025-02-24"}

User: "Save that to C:\\tmp\\sonnet.txt" (after assistant sent a poem)
{"intent":"create_file","confidence":0.85,"reason":"Save prior content to file","path":"C:\\tmp\\sonnet.txt","content":"[copy the full poem/content from the assistant's last message]"}

User: "What is the capital of France?"
{"intent":"none","confidence":0.0,"reason":"General question","path":null,"content":null}
""";

// --- Single prompt (from config) ---
var userInput = prompt;
messages.Add(new { role = "user", content = userInput });
Console.WriteLine($"You: {userInput}");

    // Step 1: Classify intent via LLM (include recent context for "save that to file")
    FileIntentClassification? classification = null;
    try
    {
        var classifyMessages = new List<object> { new { role = "system", content = ClassificationSystemPrompt } };
        if (messages.Count > 1)
        {
            foreach (var m in messages.TakeLast(4))
                classifyMessages.Add(m);
        }
        else
        {
            classifyMessages.Add(new { role = "user", content = userInput });
        }
        var classificationJson = await lmStudio.ChatForClassificationAsync(classifyMessages);
        classification = FileIntentClassifier.Parse(classificationJson);
    }
    catch (HttpRequestException ex) when (ex.InnerException?.Message?.Contains("refused") == true || ex.Message.Contains("refused"))
    {
        Console.WriteLine("Error: Cannot connect to LM Studio. Ensure it is running with a model loaded.");
        messages.RemoveAt(messages.Count - 1);
        return 1;
    }

    if (classification == null)
    {
        Console.WriteLine("  [Classification] Parse failed or low confidence → standard chat");
        await RunStandardChatAsync();
    }
    else

    {
        // Log classification
        Console.WriteLine($"  [Classification] intent={classification.Intent} confidence={classification.Confidence:F2} reason={classification.Reason}");

        // Step 2: Route based on classification
        if (classification.Intent == "create_file" && classification.Confidence >= ConfidenceThreshold &&
            !string.IsNullOrWhiteSpace(classification.Path) && !string.IsNullOrWhiteSpace(classification.Content))
        {
            await ExecuteCreateFileAsync(classification.Path, classification.Content);
        }
        else if (classification.Intent == "append_to_file" && classification.Confidence >= ConfidenceThreshold &&
            !string.IsNullOrWhiteSpace(classification.Path))
        {
            var content = classification.Content ?? DateTime.Now.ToString("yyyy-MM-dd");
            await ExecuteAppendToFileAsync(classification.Path, content);
        }
        else
        {
            if (classification.Intent != "none" && classification.Confidence < ConfidenceThreshold)
                Console.WriteLine($"  [Classification] Low confidence ({classification.Confidence:F2}) → standard chat");
            if (classification.Intent == "none")
                Console.WriteLine($"  [Classification] No file intent → standard chat");
            await RunStandardChatAsync();
        }
    }

Console.WriteLine("Goodbye.");
return 0;

async Task RunStandardChatAsync()
{
    try
    {
        var content = await lmStudio.ChatAsync(messages);
        Console.WriteLine($"Assistant: {content}");
        messages.Add(new { role = "assistant", content });
    }
    catch (HttpRequestException ex) when (ex.InnerException?.Message?.Contains("refused") == true || ex.Message.Contains("refused"))
    {
        Console.WriteLine("Error: Cannot connect to LM Studio.");
        messages.RemoveAt(messages.Count - 1);
    }
}

async Task ExecuteCreateFileAsync(string path, string content)
{
    try
    {
        var result = await mcpClient.CallToolAsync("create_file", new Dictionary<string, object?> { ["path"] = path, ["content"] = content });
        var text = GetToolResultText(result);
        Console.WriteLine($"  [Tool] create_file → {text}");
        messages.Add(new { role = "assistant", content = text });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  [Tool] Error: {ex.Message}");
        messages.Add(new { role = "assistant", content = $"[Error: {ex.Message}]" });
    }
}

async Task ExecuteAppendToFileAsync(string path, string content)
{
    try
    {
        var result = await mcpClient.CallToolAsync("append_to_file", new Dictionary<string, object?> { ["path"] = path, ["content"] = content });
        var text = GetToolResultText(result);
        Console.WriteLine($"  [Tool] append_to_file → {text}");
        messages.Add(new { role = "assistant", content = text });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  [Tool] Error: {ex.Message}");
        messages.Add(new { role = "assistant", content = $"[Error: {ex.Message}]" });
    }
}

static string GetToolResultText(ModelContextProtocol.Protocol.CallToolResult result)
{
    if (result.Content == null || result.Content.Count == 0)
        return result.IsError == true ? "[Tool error]" : "";
    var first = result.Content[0];
    if (first is ModelContextProtocol.Protocol.TextContentBlock textBlock)
        return textBlock.Text ?? "";
    return JsonSerializer.Serialize(result.Content);
}

// --- Model and parser ---

/// <summary>Deserialized file intent classification from the LLM.</summary>
public sealed class FileIntentClassification
{
    [JsonPropertyName("intent")]
    public string Intent { get; set; } = "none";

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "";

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

/// <summary>Safely parses LLM JSON output into FileIntentClassification.</summary>
public static class FileIntentClassifier
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public static FileIntentClassification? Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        // Strip markdown code blocks if present
        var cleaned = json.Trim();
        if (cleaned.StartsWith("```json")) cleaned = cleaned["```json".Length..].Trim();
        else if (cleaned.StartsWith("```")) cleaned = cleaned["```".Length..].Trim();
        if (cleaned.EndsWith("```")) cleaned = cleaned[..^3].Trim();

        try
        {
            var result = JsonSerializer.Deserialize<FileIntentClassification>(cleaned, Options);
            if (result == null) return null;

            // Validate intent
            if (result.Intent is not ("create_file" or "append_to_file" or "none"))
                result.Intent = "none";

            // Clamp confidence
            result.Confidence = Math.Clamp(result.Confidence, 0, 1);
            return result;
        }
        catch
        {
            return null;
        }
    }
}
