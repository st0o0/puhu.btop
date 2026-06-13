#!/usr/bin/env pwsh
# Build the Puhu.Btop plugin, deploy it into the local Puhu plugin folder,
# and launch the Puhu host so you can test the plugin end-to-end.
#
# Usage:
#   ./test-local.ps1                # build (Debug) + deploy + run host
#   ./test-local.ps1 -Configuration Release
#   ./test-local.ps1 -NoRun         # just build + deploy, don't launch the host
#   ./test-local.ps1 -NoBuild       # skip build, just deploy existing dll + run

[CmdletBinding()]
param(
    [string]$Configuration = "Debug",
    [switch]$NoRun,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot

$pluginProject = Join-Path $repoRoot "src/Puhu.Btop/Puhu.Btop.csproj"
$hostProject   = Join-Path $repoRoot "lib/puhu/src/Puhu/Puhu.csproj"
$pluginsDir    = Join-Path $HOME ".servus/plugins"

# 1. Build the plugin
if (-not $NoBuild) {
    Write-Host "==> Building Puhu.Btop ($Configuration)..." -ForegroundColor Cyan
    dotnet build $pluginProject -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "Plugin build failed." }
}

# 2. Deploy only Puhu.Btop.dll — the host supplies all shared deps
#    (Akka, R3, Termina, ...) via the Puhu.Plugin SDK it references.
$builtDll = Join-Path $repoRoot "src/Puhu.Btop/bin/$Configuration/net10.0/Puhu.Btop.dll"
if (-not (Test-Path $builtDll)) { throw "Built plugin not found at $builtDll" }

if (-not (Test-Path $pluginsDir)) { New-Item -ItemType Directory -Force -Path $pluginsDir | Out-Null }

Write-Host "==> Deploying Puhu.Btop.dll -> $pluginsDir" -ForegroundColor Cyan
Copy-Item $builtDll (Join-Path $pluginsDir "Puhu.Btop.dll") -Force
$builtPdb = [System.IO.Path]::ChangeExtension($builtDll, ".pdb")
if (Test-Path $builtPdb) { Copy-Item $builtPdb (Join-Path $pluginsDir "Puhu.Btop.pdb") -Force }

# 3. Launch the host
if ($NoRun) {
    Write-Host "==> Done. Plugin deployed; host not started (-NoRun)." -ForegroundColor Green
    return
}

Write-Host "==> Launching Puhu host (Ctrl+C / quit key to exit)..." -ForegroundColor Cyan
dotnet run --project $hostProject -c $Configuration
