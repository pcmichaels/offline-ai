using System.ComponentModel;
using ModelContextProtocol.Server;

namespace McpServerOfflineLlm;

/// <summary>
/// MCP tools for file system operations. Basic example demonstrating MCP communication.
/// </summary>
[McpServerToolType]
public static class FileTools
{
    [McpServerTool, Description("Creates or overwrites a file at the specified path with the given content.")]
    public static string CreateFile(
        McpFileLogger logger,
        [Description("Full or relative path where the file will be created (e.g. C:\\temp\\output.txt or ./myfile.txt)")]
        string path,
        [Description("Content to write into the file")]
        string content)
    {
        var resolvedPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(resolvedPath, content);
        var result = $"Successfully created file at: {resolvedPath} ({content.Length} bytes)";
        logger.LogToolCall("CreateFile", $"{{\"path\":\"{path}\",\"contentLength\":{content.Length}}}", result);
        return result;
    }

    [McpServerTool, Description("Appends content to an existing file, or creates the file if it does not exist.")]
    public static string AppendToFile(
        McpFileLogger logger,
        [Description("Full or relative path to the file")]
        string path,
        [Description("Content to append to the file")]
        string content)
    {
        var resolvedPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.AppendAllText(resolvedPath, content);
        var result = $"Appended {content.Length} bytes to: {resolvedPath}";
        logger.LogToolCall("AppendToFile", $"{{\"path\":\"{path}\",\"contentLength\":{content.Length}}}", result);
        return result;
    }
}
