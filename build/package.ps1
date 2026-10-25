<#
.SYNOPSIS
    SOrtus 便携版打包脚本（规范 12.2 / 12.3）
.DESCRIPTION
    产出自包含的便携版（win-x64 zip），仅面向 Windows x64。
    安装包（Setup.exe）不由本脚本生成：它由工作区 EXE/ 目录下的
    SOrtus.iss 配合 Publish-Release.ps1 产出，那条链路才负责自动更新所需的资产名与 SHA256。
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Version = "",
    [switch]$SingleFile
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Artifacts = Join-Path $RepoRoot "artifacts"
$PublishDir = Join-Path $Artifacts "publish"

# 版本号单一来源：build/version.props。此处禁止硬编码，
# 否则便携包文件名会与程序集版本、安装包版本三者不一致。
if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$versionProps = Get-Content (Join-Path $RepoRoot "build/version.props") -Raw
    $Version = $versionProps.Project.PropertyGroup.Version
}

$singleFileArg = if ($SingleFile) { "true" } else { "false" }

Write-Host "==> Publishing self-contained (win-x64, version $Version)..." -ForegroundColor Cyan
dotnet publish "$RepoRoot/src/SOrtus/SOrtus.csproj" -c $Configuration `
    -r win-x64 `
    -p:PublishSelfContained=true `
    -p:PublishSingleFile=$singleFileArg `
    -p:Platform=x64 `
    -o "$PublishDir"

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish 失败（退出码 $LASTEXITCODE）。"
}

# 便携版：直接压缩 publish 目录
$zip = Join-Path $Artifacts "SOrtus-$Version-win-x64-portable.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$PublishDir/*" -DestinationPath $zip -Force
Write-Host "==> Portable package: $zip" -ForegroundColor Green
Write-Host "==> 安装包请使用工作区 EXE/ 下的 SOrtus.iss 与 Publish-Release.ps1" -ForegroundColor Yellow
