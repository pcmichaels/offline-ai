# Offline LLMs – Talk Examples

Example code for a talk on **offline LLMs**, written in **.NET**. These demos show how to interact with local models (via [LM Studio](https://lmstudio.ai/)) without cloud APIs.

## Demos

| Demo | Description |
|------|-------------|
| [**Programmatic LLM Interaction**](programmatic-llm-interaction/docs/README.md) | Call, prompt, and consume responses from an LLM directly from .NET code |
| **MCP Server** *(coming soon)* | MCP server connecting Cursor to a local LLM |

## Quick Start

1. Install [LM Studio](https://lmstudio.ai/), download a model, and start the local server.
2. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download).
3. Run the programmatic demo:

   ```powershell
   cd programmatic-llm-interaction
   .\utils\Run-Demo.ps1
   ```

## Configuration

Edit `programmatic-llm-interaction/src/appsettings.json` to set your LM Studio endpoint and model (or leave `Model` null to auto-detect).

## License

MIT
