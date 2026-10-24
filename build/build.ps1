<#
.SYNOPSIS
    SOrtus 主构建脚本（规范 12.1）
.DESCRIPTION
    还原依赖并构建整个解决方案。产物输出到 artifacts/。
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Artifacts = Join-Path $RepoRoot "artifacts"

Write-Host "==> Restoring SOrtus ($Configuration|$Platform)" -ForegroundColor Cyan
dotnet restore "$RepoRoot/SOrtus.sln"

Write-Host "==> Building SOrtus ($Configuration|$Platform)" -ForegroundColor Cyan
dotnet build "$RepoRoot/SOrtus.sln" -c $Configuration -p:Platform=$Platform `
    -p:PublishReadyToRun=false `
    --no-restore

if (-not (Test-Path $Artifacts)) { New-Item -ItemType Directory -Path $Artifacts | Out-Null }
Write-Host "==> Build complete. Artifacts dir: $Artifacts" -ForegroundColor Green
