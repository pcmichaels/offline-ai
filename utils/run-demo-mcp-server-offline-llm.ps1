#!/usr/bin/env pwsh
# run-demo-mcp-server-offline-llm.ps1 - MCP Server (LLM intent routing) demo
# Idempotent: safe to re-run. Restores caller's working directory on exit.
#
# Usage:
#   .\run-demo-mcp-server-offline-llm.ps1              # MCP server only (Cursor/VS Code)
#   .\run-demo-mcp-server-offline-llm.ps1 --standalone # Full demo: StandaloneHost + MCP server

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
$projectRoot = Join-Path (Join-Path $repoRoot "src") "mcp-server-offline-llm"
$standalone = $args -contains "--standalone"

if ($standalone) {
    $runDir = Join-Path $projectRoot "standalone-host"
    $csproj = Join-Path $runDir "StandaloneHost.csproj"
    if (-not (Test-Path $csproj)) { Write-Error "StandaloneHost not found: $csproj" }
    Write-Host "Running MCP + LM Studio demo (LLM intent routing)..." -ForegroundColor Cyan
    Write-Host "No Cursor required. Ensure LM Studio is running with a model loaded.`n"
} else {
    $runDir = Join-Path $projectRoot "src"
    $csproj = Join-Path $runDir "McpServerOfflineLlm.csproj"
    if (-not (Test-Path $csproj)) { Write-Error "MCP server not found: $csproj" }
    Write-Host "Running MCP Server Offline LLM..." -ForegroundColor Cyan
    Write-Host "Stdio transport. Connect via Cursor/VS Code mcp.json.`n"
}

Push-Location $runDir
try {
    dotnet run
}
finally {
    Pop-Location
}
