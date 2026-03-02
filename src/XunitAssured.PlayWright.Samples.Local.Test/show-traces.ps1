#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Opens Playwright Trace Viewer for test trace files.

.DESCRIPTION
    This script launches the Playwright Trace Viewer UI to inspect test execution traces.
    Traces provide time-travel debugging with DOM snapshots, screenshots, network requests,
    console logs, and action timelines — similar to Playwright's UI Mode.

.PARAMETER TracePath
    Path to a specific .zip trace file, or a directory containing trace files.
    Default: "test-results/traces"

.EXAMPLE
    # Open the most recent trace file
    .\show-traces.ps1

.EXAMPLE
    # Open a specific trace file
    .\show-traces.ps1 -TracePath "test-results/traces/HomePageTests_20250101_120000.zip"

.EXAMPLE
    # List all available trace files
    .\show-traces.ps1 -List

.NOTES
    Requires Playwright browsers to be installed.
    Run 'pwsh bin/Debug/net10.0/playwright.ps1 install' if needed.
#>

param(
    [string]$TracePath = "test-results/traces",
    [switch]$List,
    [switch]$Latest
)

$ErrorActionPreference = "Stop"

# Find the playwright.ps1 script
$playwrightScript = Get-ChildItem -Path "bin" -Filter "playwright.ps1" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1

if (-not $playwrightScript) {
    Write-Error "Could not find playwright.ps1. Build the project first: dotnet build"
    exit 1
}

$playwrightCmd = $playwrightScript.FullName

if ($List) {
    Write-Host "`n📂 Available trace files:" -ForegroundColor Cyan
    $traces = Get-ChildItem -Path $TracePath -Filter "*.zip" -Recurse -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending
    if ($traces.Count -eq 0) {
        Write-Host "  No trace files found in '$TracePath'." -ForegroundColor Yellow
        Write-Host "  Enable tracing in playwrightsettings.json and run tests first." -ForegroundColor Yellow
    } else {
        foreach ($trace in $traces) {
            $size = [math]::Round($trace.Length / 1KB, 1)
            Write-Host "  $($trace.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))  ${size}KB  $($trace.Name)" -ForegroundColor White
        }
    }
    Write-Host ""
    exit 0
}

# If TracePath is a directory, find trace files
if (Test-Path $TracePath -PathType Container) {
    $traces = Get-ChildItem -Path $TracePath -Filter "*.zip" -Recurse -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending

    if ($traces.Count -eq 0) {
        Write-Host "❌ No trace files found in '$TracePath'." -ForegroundColor Red
        Write-Host "   Enable tracing in playwrightsettings.json:" -ForegroundColor Yellow
        Write-Host '   { "Playwright": { "RecordTrace": true, "TraceOnFailureOnly": false } }' -ForegroundColor Gray
        exit 1
    }

    if ($Latest -or $traces.Count -eq 1) {
        $TracePath = $traces[0].FullName
        Write-Host "🔍 Opening latest trace: $($traces[0].Name)" -ForegroundColor Cyan
    } else {
        Write-Host "`n📂 Found $($traces.Count) trace file(s). Select one:" -ForegroundColor Cyan
        for ($i = 0; $i -lt [math]::Min($traces.Count, 10); $i++) {
            $size = [math]::Round($traces[$i].Length / 1KB, 1)
            Write-Host "  [$($i + 1)] $($traces[$i].LastWriteTime.ToString('HH:mm:ss'))  ${size}KB  $($traces[$i].Name)" -ForegroundColor White
        }
        Write-Host ""
        $selection = Read-Host "Enter number (or press Enter for latest)"
        if ([string]::IsNullOrEmpty($selection)) { $selection = "1" }
        $index = [int]$selection - 1
        $TracePath = $traces[$index].FullName
    }
}

if (-not (Test-Path $TracePath)) {
    Write-Error "Trace file not found: $TracePath"
    exit 1
}

Write-Host "`n🎭 Opening Playwright Trace Viewer..." -ForegroundColor Green
Write-Host "   File: $TracePath" -ForegroundColor Gray
Write-Host "   Tip: Use the timeline to travel through test actions" -ForegroundColor Gray
Write-Host ""

& pwsh $playwrightCmd show-trace $TracePath
