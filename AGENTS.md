# Offline LLMs – Talk Examples

This repository contains example code for a talk on **offline LLMs**. All code is written in **.NET**.

## Scenario Overview

### 1. MCP Server for an Offline LLM

Example demonstrating how to build an MCP (Model Context Protocol) server that connects Cursor (or other MCP clients) to a local/offline large language model. This enables AI assistants to interact with models running entirely on-device without cloud APIs.

- **Location:** `mcp-server-offline-llm/`

### 2. Programmatically Interact with an LLM

Example showing how to call, prompt, and consume responses from an LLM directly from .NET code—for use in scripts, tooling, or custom applications that need LLM capabilities without MCP.

- **Location:** `programmatic-llm-interaction/`

## Demo Folder Structure

Each demo subdirectory uses the following structure:

```
<demo-name>/
├── src/
├── utils/
├── docs/
└── test/
```

## Script Requirements

Scripts (e.g. PowerShell, shell) must be **idempotent** and safe to re-run:

- **Idempotent:** Re-running a script must produce the same outcome as the first run. No action should fail or corrupt state if the script is run multiple times.
- **Restore directory:** When the script completes (success or failure), return to the caller's original working directory. Use `Push-Location`/`Pop-Location` (PowerShell) or equivalent.
- **Safe re-run:** Avoid operations that cannot be safely repeated (e.g. creating resources that already exist without checking, overwriting without confirmation). Prefer checks before mutating state.

## Logging

All demos must include a **logger** that:

- Logs API requests and responses (e.g. LM Studio calls) for debugging and demos
- Writes to a file so output persists
- Optionally displays in a separate console window for real-time visibility during demos

## Technical Constraints

- **Language:** .NET (C#)
- **Scope:** Offline/local LLMs only (no cloud APIs)
