using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProgrammaticLlmInteraction;

/// <summary>
/// Client for LM Studio's OpenAI-compatible local API.
/// </summary>
public sealed class LmStudioClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http;
    private readonly string _model;
    private const string DefaultBaseUrl = "http://localhost:1234/v1/";

    public LmStudioClient(string? baseUrl = null, string? model = null, Logger? logger = null)
    {
        var uri = string.IsNullOrEmpty(baseUrl) ? DefaultBaseUrl : (baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
        _http = new HttpClient { BaseAddress = new Uri(uri), Timeout = TimeSpan.FromMinutes(5) };
        _model = string.IsNullOrWhiteSpace(model) ? "local-model" : model;
    }

    /// <summary>
    /// Send a chat completion request.
    /// </summary>
    public async Task<string> ChatAsync(string userMessage, string? systemMessage = null, CancellationToken ct = default)
    {
        var messages = new List<object>();
        if (!string.IsNullOrWhiteSpace(systemMessage))
            messages.Add(new { role = "system", content = systemMessage });
        messages.Add(new { role = "user", content = userMessage });

        var request = new { model = _model, messages, temperature = 0.7, max_tokens = 512 };

        // --- Model interaction (demo: ~8 lines) ---
        using var response = await _http.PostAsJsonAsync("chat/completions", request, ct);
        var raw = await response.Content.ReadAsStringAsync(ct);
        response.EnsureSuccessStatusCode();

        var result = JsonSerializer.Deserialize<ChatCompletionResponse>(raw, JsonOptions);
        var content = result?.Choices?[0].Message?.Content;
        return !string.IsNullOrEmpty(content) ? content : "[No content. Ensure LM Studio has a model loaded.]";
    }

    public Task<string> GetModelIdAsync(CancellationToken ct = default) => Task.FromResult(_model);

    public void Dispose() => _http.Dispose();

    private sealed class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public Choice[]? Choices { get; set; }
    }

    private sealed class Choice
    {
        [JsonPropertyName("message")]
        public Message? Message { get; set; }
    }

    private sealed class Message
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
