<#
.SYNOPSIS
    SOrtus 主构建脚本（规范 12.1）
.DESCRIPTION
    还原依赖并构建整个解决方案。产物输出到 artifacts/。
    注意：SOrtus.slnx 只定义 Any CPU 平台；目标架构由发布时的 -r win-x64 决定，
    因此这里不传 -p:Platform（传 Release|x64 会因解决方案无此配置而直接失败）。
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Artifacts = Join-Path $RepoRoot "artifacts"

Write-Host "==> Restoring SOrtus ($Configuration)" -ForegroundColor Cyan
dotnet restore "$RepoRoot/SOrtus.slnx"

Write-Host "==> Building SOrtus ($Configuration)" -ForegroundColor Cyan
dotnet build "$RepoRoot/SOrtus.slnx" -c $Configuration `
    -p:PublishReadyToRun=false `
    --no-restore

if ($LASTEXITCODE -ne 0) {
    throw "dotnet build 失败（退出码 $LASTEXITCODE）。"
}

if (-not (Test-Path $Artifacts)) { New-Item -ItemType Directory -Path $Artifacts | Out-Null }
Write-Host "==> Build complete. Artifacts dir: $Artifacts" -ForegroundColor Green
