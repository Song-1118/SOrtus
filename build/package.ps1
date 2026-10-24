<#
.SYNOPSIS
    SOrtus 打包脚本（规范 12.2 / 12.3）
.DESCRIPTION
    产出两种形式：自包含安装包（单文件可选）与便携版（zip）。
    仅面向 Windows x64。
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Version = "0.1.0",
    [switch]$SingleFile
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Artifacts = Join-Path $RepoRoot "artifacts"
$PublishDir = Join-Path $Artifacts "publish"

$singleFileArg = if ($SingleFile) { "true" } else { "false" }

Write-Host "==> Publishing self-contained (win-x64)..." -ForegroundColor Cyan
dotnet publish "$RepoRoot/src/SOrtus/SOrtus.csproj" -c $Configuration `
    -r win-x64 `
    -p:PublishSelfContained=true `
    -p:PublishSingleFile=$singleFileArg `
    -p:Platform=x64 `
    --no-build `
    -o "$PublishDir"

# 便携版：直接压缩 publish 目录
$zip = Join-Path $Artifacts "SOrtus-$Version-win-x64-portable.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$PublishDir/*" -DestinationPath $zip -Force
Write-Host "==> Portable package: $zip" -ForegroundColor Green
Write-Host "==> (安装包构建请结合 WiX/InnoSetup 模板，见 build/templates/)" -ForegroundColor Yellow
