<#
.SYNOPSIS
    Fast 1-Click MSIX Packaging Script for Battery Indicator.
.DESCRIPTION
    Compiles the application with .NET 9 and packages it directly into
    a production-ready Microsoft Store MSIX package using MakeAppx.
#>

param(
    [string]$OutputPath = "Packaging\BatteryIndicatorForWindows.msix"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  Battery Indicator for Windows - MSIX Build " -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

# 1. Locate makeappx.exe from local NuGet cache or Windows Kits
$makeAppx = Get-ChildItem -Path "$env:USERPROFILE\.nuget\packages\microsoft.windows.sdk.buildtools" -Filter "makeappx.exe" -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match "x64" } |
    Select-Object -ExpandProperty FullName -First 1

if (-not $makeAppx -or -not (Test-Path $makeAppx)) {
    $makeAppx = Get-ChildItem -Path "C:\Program Files (x86)\Windows Kits\10\bin" -Filter "makeappx.exe" -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "x64" } |
        Select-Object -ExpandProperty FullName -First 1
}

if (-not $makeAppx) {
    Write-Error "Could not locate makeappx.exe. Ensure Microsoft.Windows.SDK.BuildTools is installed."
    exit 1
}

Write-Host "[1/4] Found MakeAppx: $makeAppx" -ForegroundColor Green

# 2. Publish Release
Write-Host "[2/4] Publishing Release binaries with .NET 9..." -ForegroundColor Yellow
$dotnetRoot = if (Test-Path "$env:USERPROFILE\.dotnet\dotnet.exe") { "$env:USERPROFILE\.dotnet" } else { "C:\Program Files\dotnet" }
$env:DOTNET_ROOT = $dotnetRoot
$env:PATH = "$dotnetRoot;" + $env:PATH

& "$dotnetRoot\dotnet.exe" publish "$root\BatteryIndicator.csproj" -c Release -r win-x64 --self-contained false -o "$root\publish" | Out-Null

# 3. Prepare Staging
Write-Host "[3/4] Preparing MSIX staging layout..." -ForegroundColor Yellow
$staging = Join-Path $PSScriptRoot "msix_staging"
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Path $staging -Force | Out-Null

Copy-Item "$root\publish\*" $staging -Recurse -Force
Copy-Item "$PSScriptRoot\Package.appxmanifest" "$staging\AppxManifest.xml" -Force
Copy-Item "$PSScriptRoot\StoreAssets" "$staging\StoreAssets" -Recurse -Force

# 4. Pack into MSIX
$finalMsix = if ([System.IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $root $OutputPath }
if (Test-Path $finalMsix) { Remove-Item $finalMsix -Force }

Write-Host "[4/4] Packing into $finalMsix..." -ForegroundColor Yellow
& $makeAppx pack /d $staging /p $finalMsix /nv /o | Out-Null

# Cleanup staging
Remove-Item $staging -Recurse -Force

$fileInfo = Get-Item $finalMsix
$sizeMb = [Math]::Round($fileInfo.Length / 1MB, 2)

Write-Host ""
Write-Host "=============================================" -ForegroundColor Green
Write-Host " SUCCESS: MSIX Package Generated!" -ForegroundColor Green
Write-Host " Output: $finalMsix" -ForegroundColor White
Write-Host " Size  : $sizeMb MB" -ForegroundColor White
Write-Host " Ready to upload to Microsoft Partner Center!" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Green
