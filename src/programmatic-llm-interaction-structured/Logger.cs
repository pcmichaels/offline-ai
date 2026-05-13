using System.Diagnostics;
using System.Text;

namespace ProgrammaticLlmInteractionStructured;

/// <summary>
/// Logs to a file and spawns a separate console window that tails the log in real-time.
/// </summary>
public sealed class Logger : IDisposable
{
    private readonly string _logPath;
    private readonly FileStream _fileStream;
    private readonly StreamWriter _writer;
    private readonly object _lock = new();
    private static bool _tailWindowSpawned;

    public Logger(string? logDirectory = null)
    {
        logDirectory ??= Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logDirectory);

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        _logPath = Path.Combine(logDirectory, $"demo-{timestamp}.log");
        _fileStream = new FileStream(_logPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(_fileStream, Encoding.UTF8) { AutoFlush = true };

        SpawnTailWindow(_logPath);
    }

    private static void SpawnTailWindow(string logPath)
    {
        if (_tailWindowSpawned) return;
        lock (typeof(Logger))
        {
            if (_tailWindowSpawned) return;
            _tailWindowSpawned = true;
        }

        try
        {
            var escapedPath = logPath.Replace("'", "''");
            var scriptContent = $"Get-Content -Path '{escapedPath}' -Wait -Tail 50";
            var tempScript = Path.Combine(Path.GetTempPath(), $"tail-log-{Environment.ProcessId}.ps1");
            File.WriteAllText(tempScript, scriptContent);

            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoExit -ExecutionPolicy Bypass -File \"{tempScript}\"",
                UseShellExecute = true,
                CreateNoWindow = false
            });
        }
        catch
        {
            // Non-fatal: main app continues, logging to file only
        }
    }

    public void LogInfo(string message)
    {
        WriteEntry("INFO", message);
    }

    public void LogRequest(string method, string endpoint, string body)
    {
        WriteEntry("REQUEST", $"{method} {endpoint}");
        WriteEntry("BODY", body);
    }

    public void LogResponse(string endpoint, int statusCode, string body)
    {
        WriteEntry("RESPONSE", $"{endpoint} → {statusCode}");
        WriteEntry("BODY", body.Length > 2000 ? body[..2000] + "... [truncated]" : body);
    }

    public void LogError(string message, Exception? ex = null)
    {
        WriteEntry("ERROR", message);
        if (ex != null)
            WriteEntry("EXCEPTION", ex.ToString());
    }

    private void WriteEntry(string level, string content)
    {
        lock (_lock)
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {content}";
            _writer.WriteLine(line);
        }
    }

    public void Dispose() => _writer.Dispose();
}
