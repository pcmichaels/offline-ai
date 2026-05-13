using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace McpServerOfflineLlm;

/// <summary>
/// Client for LM Studio's OpenAI-compatible local API.
/// </summary>
public sealed class LmStudioClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly string? _modelOverride;
    private readonly McpFileLogger? _logger;
    private string? _resolvedModel;
    private readonly SemaphoreSlim _modelResolveLock = new(1, 1);
    private const string DefaultBaseUrl = "http://localhost:1234/v1/";

    public LmStudioClient(string? baseUrl = null, string? model = null, McpFileLogger? logger = null)
    {
        var baseUri = EnsureTrailingSlash(baseUrl ?? DefaultBaseUrl);
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUri),
            Timeout = TimeSpan.FromMinutes(5)
        };
        _modelOverride = model;
        _logger = logger;
    }

    private async Task<string> ResolveModelAsync(CancellationToken ct)
    {
        if (_modelOverride != null)
            return _modelOverride;

        if (_resolvedModel != null)
            return _resolvedModel;

        await _modelResolveLock.WaitAsync(ct);
        try
        {
            if (_resolvedModel != null)
                return _resolvedModel;

            try
            {
                _logger?.LogInfo("Fetching models from GET /v1/models");
                using var modelsResponse = await _http.GetAsync("models", ct);
                var modelsRaw = await modelsResponse.Content.ReadAsStringAsync(ct);
                _logger?.LogResponse("GET /v1/models", (int)modelsResponse.StatusCode, modelsRaw);

                var modelsResp = JsonSerializer.Deserialize<ModelsListResponse>(modelsRaw, JsonOptions);
                var firstModel = modelsResp?.Data?.FirstOrDefault()?.Id;
                if (!string.IsNullOrEmpty(firstModel))
                {
                    _resolvedModel = firstModel;
                    _logger?.LogInfo($"Resolved model: {_resolvedModel}");
                    return _resolvedModel;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("Models endpoint failed, using 'local-model'", ex);
            }

            _resolvedModel = "local-model";
            return _resolvedModel;
        }
        finally
        {
            _modelResolveLock.Release();
        }
    }

    public Task<string> ChatAsync(string userMessage, CancellationToken ct = default) =>
        ChatAsync(new[] { new { role = "user", content = userMessage } }.Cast<object>().ToList(), ct);

    /// <summary>Standard chat without tools. Use for normal conversation.</summary>
    public async Task<string> ChatAsync(IReadOnlyList<object> messages, CancellationToken ct = default)
    {
        var model = await ResolveModelAsync(ct);
        var request = new { model, messages, temperature = 0.7, max_tokens = 512 };

        var requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = false });
        _logger?.LogRequest("POST", "/v1/chat/completions", requestJson);

        using var response = await _http.PostAsJsonAsync("chat/completions", request, ct);
        var raw = await response.Content.ReadAsStringAsync(ct);
        _logger?.LogResponse("/v1/chat/completions", (int)response.StatusCode, raw);

        response.EnsureSuccessStatusCode();
        var result = JsonSerializer.Deserialize<ChatCompletionResponse>(raw, JsonOptions);
        var content = result?.Choices?[0].Message?.Content;
        if (!string.IsNullOrEmpty(content))
            return content;

        return string.IsNullOrWhiteSpace(raw)
            ? "[No content in response. Ensure a model is loaded in LM Studio and the server is started.]"
            : $"[Unexpected response format. Raw (first 300 chars): {raw[..Math.Min(300, raw.Length)]}...]";
    }

    public async Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
    {
        var model = await ResolveModelAsync(ct);
        var request = new { model, prompt, temperature = 0.7, max_tokens = 256 };

        var requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = false });
        _logger?.LogRequest("POST", "/v1/completions", requestJson);

        using var response = await _http.PostAsJsonAsync("completions", request, ct);
        var raw = await response.Content.ReadAsStringAsync(ct);
        _logger?.LogResponse("/v1/completions", (int)response.StatusCode, raw);

        response.EnsureSuccessStatusCode();
        var result = JsonSerializer.Deserialize<CompletionResponse>(raw, JsonOptions);
        var text = result?.Choices?[0].Text;
        if (!string.IsNullOrEmpty(text))
            return text;

        return string.IsNullOrWhiteSpace(raw)
            ? "[No content in response. Ensure a model is loaded in LM Studio and the server is started.]"
            : $"[Unexpected response format. Raw (first 300 chars): {raw[..Math.Min(300, raw.Length)]}...]";
    }

    /// <summary>
    /// Chat with tool-calling support. Returns content and any tool_calls from the model.
    /// </summary>
    public async Task<ChatWithToolsResult> ChatWithToolsAsync(
        IReadOnlyList<object> messages,
        IReadOnlyList<object> tools,
        CancellationToken ct = default)
    {
        var model = await ResolveModelAsync(ct);
        var request = new Dictionary<string, object>
        {
            ["model"] = model,
            ["messages"] = messages,
            ["tools"] = tools,
            ["tool_choice"] = "auto",
            ["temperature"] = 0.7,
            ["max_tokens"] = 512
        };

        var requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = false });
        _logger?.LogRequest("POST", "/v1/chat/completions", requestJson);

        using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync("chat/completions", content, ct);
        var raw = await response.Content.ReadAsStringAsync(ct);
        _logger?.LogResponse("/v1/chat/completions", (int)response.StatusCode, raw);

        response.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(raw);
        var choice = doc.RootElement.GetProperty("choices")[0];
        var msg = choice.GetProperty("message");

        var result = new ChatWithToolsResult();
        if (msg.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
            result.Content = c.GetString();

        if (msg.TryGetProperty("tool_calls", out var tc) && tc.ValueKind == JsonValueKind.Array)
        {
            var toolCalls = new List<ToolCall>();
            foreach (var t in tc.EnumerateArray())
            {
                    toolCalls.Add(new ToolCall
                    {
                        Id = t.TryGetProperty("id", out var id) ? id.GetString() ?? Guid.NewGuid().ToString("n") : Guid.NewGuid().ToString("n"),
                        Name = t.GetProperty("function").GetProperty("name").GetString() ?? "",
                        Arguments = t.GetProperty("function").TryGetProperty("arguments", out var args) ? args.GetString() ?? "{}" : "{}"
                    });
            }
            result.ToolCalls = toolCalls;
        }

        return result;
    }

    public void Dispose() => _http.Dispose();

    private static string EnsureTrailingSlash(string url) =>
        string.IsNullOrEmpty(url) || url.EndsWith('/') ? url : url + "/";

    private sealed class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public Choice[]? Choices { get; set; }
    }

    private sealed class CompletionResponse
    {
        [JsonPropertyName("choices")]
        public CompletionChoice[]? Choices { get; set; }
    }

    private sealed class Choice
    {
        [JsonPropertyName("message")]
        public Message? Message { get; set; }
    }

    private sealed class CompletionChoice
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private sealed class Message
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private sealed class ModelsListResponse
    {
        [JsonPropertyName("data")]
        public ModelEntry[]? Data { get; set; }
    }

    private sealed class ModelEntry
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }
}

public sealed class ChatWithToolsResult
{
    public string? Content { get; set; }
    public IReadOnlyList<ToolCall>? ToolCalls { get; set; }
}

public sealed class ToolCall
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Arguments { get; set; } = "{}";
}
