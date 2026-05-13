using System.Text;

namespace McpServerOfflineLlm;

/// <summary>
/// File-based logger for MCP tool invocations. Writes to logs folder.
/// </summary>
public sealed class McpFileLogger : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly object _lock = new();

    public McpFileLogger()
    {
        var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logDir);
        var logPath = Path.Combine(logDir, $"mcp-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        var fs = new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(fs, Encoding.UTF8) { AutoFlush = true };
    }

    public void LogToolCall(string toolName, string args, string result)
    {
        WriteEntry("TOOL", $"{toolName}: {args} → {result}");
    }

    public void LogInfo(string message) => WriteEntry("INFO", message);
    public void LogRequest(string method, string endpoint, string body) => WriteEntry("REQUEST", $"{method} {endpoint}\n{body}");
    public void LogResponse(string endpoint, int statusCode, string body) => WriteEntry("RESPONSE", $"{endpoint} → {statusCode}\n{(body.Length > 2000 ? body[..2000] + "... [truncated]" : body)}");
    public void LogError(string message, Exception? ex = null)
    {
        WriteEntry("ERROR", message);
        if (ex != null) WriteEntry("EXCEPTION", ex.ToString());
    }

    private void WriteEntry(string level, string content)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {content}";
        lock (_lock)
        {
            _writer.WriteLine(line);
        }
        // Also to stderr (stdout is used by MCP stdio protocol)
        try { Console.Error.WriteLine(line); } catch { /* ignore */ }
    }

    public void Dispose() => _writer.Dispose();
}
