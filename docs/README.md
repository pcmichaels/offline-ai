# Offline LLMs – Talk Examples

Example code for a talk on **offline LLMs**, written in **.NET**. These demos show how to interact with local models (via [LM Studio](https://lmstudio.ai/)) without cloud APIs.

## Projects

| Project | Description |
|---------|-------------|
| **Programmatic LLM Interaction** | Send a configurable prompt to an LLM and print the response. Message is set in `appsettings.json`. |
| **Programmatic LLM Interaction (Structured)** | Same pattern, but always prompts for greatest-band info as JSON (`BandName`, `DateFormed`, `Members`). No CLI modes. |
| **MCP Server (LLM Intent)** | MCP server with **LLM-based intent routing** for file operations. The model classifies user intent (create_file, append_to_file, none) via structured JSON |
| **MCP Server (Keyword Match)** | MCP server with **keyword-based filtering** to decide when to call CreateFile/AppendToFile |

## How to Run

All scripts live in `utils/`. Run from the repository root.

### Prerequisites

1. **LM Studio** – [Download](https://lmstudio.ai/), install, download a model, and start the local server (default: `http://localhost:1234`).
2. **.NET 10 SDK** – [Download](https://dotnet.microsoft.com/download).

### Programmatic LLM Interaction (Structured JSON)

```powershell
.\utils\run-demo-programmatic-llm-interaction-structured.ps1
```

Asks LM Studio for the “greatest band” (record sales, then ticket sales) and prints a JSON object with `BandName`, `DateFormed`, and `Members`. Prompt and system role are in `src/programmatic-llm-interaction-structured/appsettings.json`. No script parameters.

### Programmatic LLM Interaction

```powershell
.\utils\run-demo-programmatic-llm-interaction.ps1
```

Sends a pre-defined message to LM Studio and prints the response. Configure the message in `appsettings.json` under `Prompt:Message`.

Options:
- `--prompt` — Use `Prompt:Message` from config (default).
- `--return-value-only` — Asks "Which is the greatest band?" and outputs `1` if the answer is Motorhead, `0` otherwise. Minimal output, useful for scripting.

### MCP Server (LLM Intent Routing)

```powershell
# Full demo (StandaloneHost runs configured prompt once, no interaction)
.\utils\run-demo-mcp-server-offline-llm.ps1 --standalone

# MCP server only (for Cursor/VS Code integration)
.\utils\run-demo-mcp-server-offline-llm.ps1
```

Uses the LLM to classify intent. If `create_file` or `append_to_file` with confidence ≥ 0.8, calls MCP tools; otherwise standard chat. The prompt is configured in `Demo:Prompt` in `appsettings.json`.

### MCP Server (Keyword Match)

```powershell
# Full demo (runs configured prompt once, no interaction)
.\utils\run-demo-mcp-server-offline-llm-keyword-match.ps1 --standalone

# MCP server only
.\utils\run-demo-mcp-server-offline-llm-keyword-match.ps1
```

Keyword-based detection for file operations. The prompt is configured in `Demo:Prompt` in `appsettings.json`.

## Project Structure

```
offline-ai/
├── docs/
│   ├── README.md
│   └── mcp-server-offline-llm-config.json
├── utils/
│   ├── publish-mcp-server-offline-llm.ps1
│   ├── run-demo-programmatic-llm-interaction-structured.ps1
│   ├── run-demo-programmatic-llm-interaction.ps1
│   ├── run-demo-mcp-server-offline-llm.ps1
│   └── run-demo-mcp-server-offline-llm-keyword-match.ps1
└── src/
    ├── programmatic-llm-interaction-structured/
    │   ├── Program.cs
    │   ├── LmStudioClient.cs
    │   ├── Logger.cs
    │   └── ProgrammaticLlmInteractionStructured.csproj
    ├── programmatic-llm-interaction/
    │   ├── Program.cs
    │   ├── LmStudioClient.cs
    │   ├── Logger.cs
    │   └── ProgrammaticLlmInteraction.csproj
    ├── mcp-server-offline-llm/
    │   ├── src/              # MCP server (CreateFile, AppendToFile, ChatWithLlm, CompleteWithLlm)
    │   ├── standalone-host/  # Chat UI with LLM intent routing
    │   └── publish/          # After publish: McpServerOfflineLlm.exe
    └── mcp-server-offline-llm-keyword-match/
        ├── src/
        └── standalone-host/
```

## Configuration

Configuration is in `appsettings.json` within each project:

```json
{
  "LmStudio": {
    "BaseUrl": "http://localhost:1234/v1/",
    "Model": "google/gemma-3-4b"
  },
  "Prompt": {
    "SystemRoles": [
      { "use": true, "value": "You are a helpful assistant. Keep responses concise." },
      { "use": false, "value": "You are a music expert. Be opinionated." }
    ],
    "Message": "Say hello in one sentence."
  }
}
```

- **BaseUrl:** LM Studio API endpoint. Default `http://localhost:1234/v1/`.
- **Model:** Model ID. Set to `null` to auto-detect from LM Studio; or a string to use a specific model.
- **Prompt:Message:** (Programmatic LLM only) The message to send to the LLM.
- **Prompt:SystemRoles:** (Programmatic LLM only) Array of `{ "use": true|false, "value": "..." }`. Set `use: true` on one entry to enable it; toggle to switch roles. Omit or use empty array for no system role.

Environment variables `LmStudio__BaseUrl` and `LmStudio__Model` override the config.

### Paths for MCP projects

- **Programmatic LLM (structured):** `src/programmatic-llm-interaction-structured/appsettings.json`
- **Programmatic LLM:** `src/programmatic-llm-interaction/appsettings.json`
- **MCP Server:** `src/mcp-server-offline-llm/src/appsettings.json`
- **Standalone host:** `src/mcp-server-offline-llm/standalone-host/appsettings.json` (or `...-keyword-match/...`). Set `Demo:Prompt` for the non-interactive standalone demo prompt.

## Using MCP Server with LM Studio

1. **Publish the MCP server** (once, or after code changes):
   ```powershell
   .\utils\publish-mcp-server-offline-llm.ps1
   ```
   This produces `src/mcp-server-offline-llm/publish/McpServerOfflineLlm.exe` (self-contained, no .NET install needed).

2. **Configure LM Studio:** Copy the contents of `docs/mcp-server-offline-llm-config.json` into LM Studio's `mcp.json` (Program tab → Install → Edit mcp.json). Replace `REPLACE_WITH_YOUR_REPO_PATH` with the full path to this repository (e.g. `C:/Users/pcmic/source/repos/offline-ai`). Use forward slashes. LM Studio will spawn the exe when needed.

**Note:** LM Studio's chat may require a model that supports function/tool calling (e.g. Llama 3.1 Tool-Use, Qwen 2.5) for MCP tools to be invoked. If the model responds in chat but does not create files, try a tool-capable model or use the **standalone demo** (`.\utils\run-demo-mcp-server-offline-llm.ps1 --standalone`), which routes your prompt through the LLM and MCP tools.

## Using MCP Server with Cursor / VS Code

Add to your MCP config (Cursor MCP settings, VS Code `.vscode/mcp.json`, etc.):

```json
{
  "mcpServers": {
    "mcp-server-offline-llm": {
      "command": "dotnet",
      "args": ["run", "--project", "C:\\path\\to\\offline-ai\\src\\mcp-server-offline-llm\\src\\McpServerOfflineLlm.csproj"]
    }
  }
}
```

Replace the path with the full path to your repository.

## Logging

- **Programmatic LLM:** Logs to `src/programmatic-llm-interaction/bin/Debug/net10.0/logs/demo-*.log`
- **Programmatic LLM (structured):** Logs to `src/programmatic-llm-interaction-structured/bin/Debug/net10.0/logs/demo-*.log`
- **MCP Server:** Logs to `src/mcp-server-offline-llm/src/bin/Debug/net10.0/logs/mcp-*.log`

## License

MIT
