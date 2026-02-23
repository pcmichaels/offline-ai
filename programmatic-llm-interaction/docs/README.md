# Programmatic LLM Interaction Demo

This demo shows how to interact with a local LLM **programmatically** from .NET code, using LM Studio's OpenAI-compatible API. You can prompt the model, receive responses, and stream tokens—all without MCP or cloud services.

## What This Demo Does

- **Chat Completion** – Send a user message and get a single assistant response (like ChatGPT-style turn-taking).
- **Text Completion** – Use the legacy `completions` endpoint to continue a prompt (useful for autocomplete or generation tasks).
- **Streaming Chat** – Stream the model’s reply token-by-token for a responsive, real-time experience.

The app runs as an interactive console: you choose an example, enter your prompt, and see the model’s output. All communication goes to your local LM Studio server over HTTP.

## Prerequisites

1. **LM Studio** – [Download LM Studio](https://lmstudio.ai/) and install it.
2. **A local model** – In LM Studio, download a model (e.g. Llama, Mistral) and load it.
3. **Local server** – In LM Studio, go to the **Local Server** tab and start the server (default: `http://localhost:1234`).
4. **.NET 10 SDK** – Required to build and run the demo.

## Usage

### Run via PowerShell

From the `programmatic-llm-interaction` folder:

```powershell
.\utils\Run-Demo.ps1
```

### Run manually

```powershell
cd programmatic-llm-interaction\src
dotnet run
```

### Configuration

Settings are read from `appsettings.json` in the `src/` folder:

```json
{
  "LmStudio": {
    "BaseUrl": "http://localhost:1234/v1/",
    "Model": null
  }
}
```

- **BaseUrl:** LM Studio API endpoint. Default `http://localhost:1234/v1/`. Use a trailing slash.
- **Model:** Model ID to use. Set to `null` (or omit) to auto-detect from LM Studio; set to a string (e.g. `"llama-3.2-3b-instruct"`) to use that model explicitly.

You can also override via environment variables: `LmStudio__BaseUrl` and `LmStudio__Model` (use `__` for the section separator).

## Logging

The demo includes a **logger** that writes all LM Studio API requests and responses to a file. On startup, a **separate console window** opens and tails the log in real-time, so you can see the raw HTTP traffic during demos.

Logs are written to `src/bin/Debug/net10.0/logs/` (or the equivalent output directory) as `demo-YYYYMMDD-HHmmss.log`.

## Project Structure

```
programmatic-llm-interaction/
├── src/
│   ├── Program.cs           # Entry point and interactive menu
│   ├── LmStudioClient.cs     # HTTP client for LM Studio's OpenAI API
│   ├── Logger.cs              # File logger with separate console tail
│   └── ProgrammaticLlmInteraction.csproj
├── utils/
│   └── Run-Demo.ps1         # Script to run the demo
└── docs/
    └── README.md            # This file
```
