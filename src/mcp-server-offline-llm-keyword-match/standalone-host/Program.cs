using System.Text.Json;
using Microsoft.Extensions.Configuration;
using McpServerOfflineLlm;
using ModelContextProtocol.Client;

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

Console.WriteLine("Standalone MCP + LM Studio Demo");
Console.WriteLine("================================");
Console.WriteLine("This demo uses only the MCP server and LM Studio (no Cursor).");
Console.WriteLine("Ensure LM Studio is running with a model loaded and the server started.");
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
var fileToolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "create_file", "append_to_file" };
var fileTools = tools.Where(t => fileToolNames.Contains(t.Name)).Select(t => new
{
    type = "function",
    function = new { name = t.Name, description = t.Description, parameters = t.JsonSchema }
}).Cast<object>().ToList();

Console.WriteLine($"Connected to MCP server. File tools: {string.Join(", ", tools.Where(t => fileToolNames.Contains(t.Name)).Select(t => t.Name))}");
Console.WriteLine($"Prompt: {prompt}");
Console.WriteLine();

var messages = new List<object>();
var userInput = prompt;
Console.WriteLine($"You: {userInput}");

// Direct file commands: bypass LLM when model doesn't support tool calling (e.g. gemma-3-4b)
var (matched, directResult) = await TryDirectFileCommandAsync(mcpClient, userInput);
if (matched)
{
    Console.WriteLine($"  {directResult}");
}
else
{
    messages.Add(new { role = "user", content = userInput });
    var useFileTools = HasFileIntent(userInput);

    if (!useFileTools)
    {
        try
        {
            var content = await lmStudio.ChatAsync(messages);
            Console.WriteLine($"Assistant: {content}");
        }
        catch (System.Net.Http.HttpRequestException ex) when (ex.InnerException?.Message?.Contains("refused") == true || ex.Message.Contains("refused"))
        {
            Console.WriteLine("Error: Cannot connect to LM Studio at localhost:1234. Ensure LM Studio is running with a model loaded and the local server started.");
        }
    }
    else
    {
        var fileSystemMessage = "When the user asks to create or modify a file, you MUST call create_file or append_to_file. Never claim to have done it without calling the tool.";
        var messagesWithSystem = new List<object> { new { role = "system", content = fileSystemMessage } };
        messagesWithSystem.AddRange(messages);

        var maxRounds = 10;
        try
        {
            for (var round = 0; round < maxRounds; round++)
            {
                var result = await lmStudio.ChatWithToolsAsync(messagesWithSystem, fileTools);

                if (result.ToolCalls is { Count: > 0 })
                {
                    messagesWithSystem.Add(new
                    {
                        role = "assistant",
                        content = (string?)null,
                        tool_calls = result.ToolCalls.Select(tc => new
                        {
                            id = tc.Id,
                            type = "function",
                            function = new { name = tc.Name, arguments = tc.Arguments }
                        }).ToArray()
                    });
                    messages.Add(new
                    {
                        role = "assistant",
                        content = (string?)null,
                        tool_calls = result.ToolCalls.Select(tc => new
                        {
                            id = tc.Id,
                            type = "function",
                            function = new { name = tc.Name, arguments = tc.Arguments }
                        }).ToArray()
                    });

                    foreach (var tc in result.ToolCalls)
                    {
                        Console.WriteLine($"  [Tool] {tc.Name}({tc.Arguments})");
                        try
                        {
                            var argsDict = ParseToolArguments(tc.Arguments);
                            var toolResult = await mcpClient.CallToolAsync(tc.Name, argsDict);
                            var resultText = GetToolResultText(toolResult);
                            Console.WriteLine($"  → {resultText}");

                            var toolMsg = new { role = "tool", tool_call_id = tc.Id, content = resultText };
                            messages.Add(toolMsg);
                            messagesWithSystem.Add(toolMsg);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  → Error: {ex.Message}");
                            var errMsg = new { role = "tool", tool_call_id = tc.Id, content = $"[Error: {ex.Message}]" };
                            messages.Add(errMsg);
                            messagesWithSystem.Add(errMsg);
                        }
                    }
                    continue;
                }

                if (!string.IsNullOrEmpty(result.Content))
                {
                    Console.WriteLine($"Assistant: {result.Content}");
                    messages.Add(new { role = "assistant", content = result.Content });
                }

                break;
            }
        }
        catch (System.Net.Http.HttpRequestException ex) when (ex.InnerException?.Message?.Contains("refused") == true || ex.Message.Contains("refused"))
        {
            Console.WriteLine("Error: Cannot connect to LM Studio at localhost:1234. Ensure LM Studio is running with a model loaded and the local server started.");
        }
    }
}

Console.WriteLine("Goodbye.");
return 0;

static bool HasFileIntent(string input)
{
    if (string.IsNullOrWhiteSpace(input)) return false;
    var lower = input.ToLowerInvariant();
    return (lower.Contains("create") && lower.Contains("file")) ||
           (lower.Contains("append") && lower.Contains("file")) ||
           (lower.Contains("write") && (lower.Contains("file") || lower.Contains(" to "))) ||
           (lower.Contains("output") && (lower.Contains("file") || lower.Contains("to a file"))) ||
           (lower.Contains("save") && lower.Contains("file")) ||
           lower.Contains("save to file") ||
           lower.Contains("create a file") ||
           lower.Contains("append to ") ||
           lower.Contains("to a file") ||
           lower.Contains("to file ") ||
           lower.Contains("put ") && lower.Contains(" in ") && lower.Contains("file");
}

static async Task<(bool Matched, string Result)> TryDirectFileCommandAsync(dynamic mcpClient, string input)
{
    input = input.Trim();
    if (string.IsNullOrWhiteSpace(input)) return (false, "");

    // Handle "Create... or Append..." (e.g. from docs) - run each command
    var parts = System.Text.RegularExpressions.Regex.Split(input, @"\s+or\s+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    var results = new List<string>();
    foreach (var part in parts.Select(p => p.Trim()).Where(p => p.Length > 0))
    {
        var (matched, r) = await TrySingleFileCommandAsync(mcpClient, part);
        if (matched) results.Add(r);
    }
    if (results.Count > 0) return (true, string.Join("\n  ", results));
    return (false, "");
}

static async Task<(bool Matched, string Result)> TrySingleFileCommandAsync(dynamic mcpClient, string input)
{
    try
    {
        // "Output the text "hello world" to a file in c:\tmp called hello.txt"
        var outputMatch = System.Text.RegularExpressions.Regex.Match(input,
            @"output\s+the\s+text\s+[""']?(.+?)[""']?\s+to\s+a\s+file\s+in\s+(.+?)\s+called\s+(\S+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (outputMatch.Success)
        {
            var dir = outputMatch.Groups[2].Value.Trim().Trim('"', '\'');
            var filename = outputMatch.Groups[3].Value.Trim().Trim('"', '\'');
            var path = Path.Combine(dir, filename);
            var content = outputMatch.Groups[1].Value.Trim().Trim('"', '\'');
            var toolResult = await mcpClient.CallToolAsync("create_file", new Dictionary<string, object?> { ["path"] = path, ["content"] = content });
            return (true, GetToolResultText(toolResult));
        }

        // "Create a file at C:\tmp\hello.txt with content Hello World"
        var createMatch = System.Text.RegularExpressions.Regex.Match(input,
            @"create\s+(?:a\s+)?file\s+at\s+(.+?)\s+with\s+content\s+(.+?)(?=\s+or\s+|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
        if (createMatch.Success)
        {
            var path = createMatch.Groups[1].Value.Trim().Trim('"', '\'');
            var content = createMatch.Groups[2].Value.Trim().Trim('"', '\'');
            var toolResult = await mcpClient.CallToolAsync("create_file", new Dictionary<string, object?> { ["path"] = path, ["content"] = content });
            return (true, GetToolResultText(toolResult));
        }

        // "Append today's date to C:\tmp\log.txt" or "append X to C:\tmp\log.txt"
        var appendMatch = System.Text.RegularExpressions.Regex.Match(input,
            @"append\s+(?:(?:today'?s?\s+date)|(.+?))\s+to\s+(.+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (appendMatch.Success)
        {
            var path = appendMatch.Groups[2].Value.Trim().Trim('"', '\'');
            var content = appendMatch.Groups[1].Success && !string.IsNullOrWhiteSpace(appendMatch.Groups[1].Value)
                ? appendMatch.Groups[1].Value.Trim().Trim('"', '\'')
                : DateTime.Now.ToString("yyyy-MM-dd");
            var toolResult = await mcpClient.CallToolAsync("append_to_file", new Dictionary<string, object?> { ["path"] = path, ["content"] = content });
            return (true, GetToolResultText(toolResult));
        }
    }
    catch (Exception ex)
    {
        return (true, $"Error: {ex.Message}");
    }

    return (false, "");
}


static IReadOnlyDictionary<string, object?> ParseToolArguments(string argsJson)
{
    if (string.IsNullOrWhiteSpace(argsJson) || argsJson == "{}")
        return new Dictionary<string, object?>();
    var dict = new Dictionary<string, object?>();
    using var doc = JsonDocument.Parse(argsJson);
    foreach (var prop in doc.RootElement.EnumerateObject())
        dict[prop.Name] = JsonElementToObject(prop.Value);
    return dict;
}

static object? JsonElementToObject(JsonElement el)
{
    return el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt32(out var i) ? i : el.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.Object => el.EnumerateObject().ToDictionary(p => p.Name, p => JsonElementToObject(p.Value)),
        JsonValueKind.Array => el.EnumerateArray().Select(JsonElementToObject).ToArray(),
        _ => el.GetRawText()
    };
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
