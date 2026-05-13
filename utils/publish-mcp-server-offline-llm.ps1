#!/usr/bin/env pwsh
# publish-mcp-server-offline-llm.ps1 - Publishes the MCP server as a single-file self-contained exe
# Output: src/mcp-server-offline-llm/publish/McpServerOfflineLlm.exe (framework embedded in exe)
# Use the exe path in mcp.json for LM Studio / Cursor (no dotnet required).

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
$projectDir = Join-Path (Join-Path $repoRoot "src") "mcp-server-offline-llm"
$projectPath = Join-Path (Join-Path $projectDir "src") "McpServerOfflineLlm.csproj"
$publishDir = Join-Path $projectDir "publish"

if (-not (Test-Path $projectPath)) {
    Write-Error "Project not found: $projectPath"
}

if (Test-Path $publishDir) {
    Write-Host "Flushing publish folder..." -ForegroundColor Yellow
    Remove-Item -Path $publishDir -Recurse -Force
}

Write-Host "Publishing mcp-server-offline-llm..." -ForegroundColor Cyan
dotnet publish $projectPath -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $publishDir

$exePath = Join-Path $publishDir "McpServerOfflineLlm.exe"
Write-Host "Published: $exePath" -ForegroundColor Green
Write-Host "Use in mcp.json: `"command`": `"$exePath`", `"args`": []" -ForegroundColor Gray
