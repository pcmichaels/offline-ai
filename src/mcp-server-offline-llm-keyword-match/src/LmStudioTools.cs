using System.ComponentModel;
using ModelContextProtocol.Server;

namespace McpServerOfflineLlm;

/// <summary>
/// MCP tools that call LM Studio's local LLM via chat and completion endpoints.
/// </summary>
[McpServerToolType]
public static class LmStudioTools
{
    [McpServerTool, Description("Send a message to the local LLM (LM Studio) and get a chat-style response. Requires LM Studio to be running with a model loaded and the local server started.")]
    public static async Task<string> ChatWithLlm(
        LmStudioClient lmStudio,
        McpFileLogger logger,
        [Description("The message or prompt to send to the LLM")]
        string message)
    {
        try
        {
            var response = await lmStudio.ChatAsync(message);
            logger.LogToolCall("ChatWithLlm", $"{{\"messageLength\":{message.Length}}}", $"Response length: {response.Length} chars");
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError("ChatWithLlm failed", ex);
            return $"[Error: {ex.Message}]";
        }
    }

    [McpServerTool, Description("Send a text prompt to the local LLM for completion (continue/autocomplete style). Requires LM Studio to be running with a model loaded and the local server started.")]
    public static async Task<string> CompleteWithLlm(
        LmStudioClient lmStudio,
        McpFileLogger logger,
        [Description("The prompt to complete")]
        string prompt)
    {
        try
        {
            var response = await lmStudio.CompleteAsync(prompt);
            logger.LogToolCall("CompleteWithLlm", $"{{\"promptLength\":{prompt.Length}}}", $"Response length: {response.Length} chars");
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError("CompleteWithLlm failed", ex);
            return $"[Error: {ex.Message}]";
        }
    }
}
