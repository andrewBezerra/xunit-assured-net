# Run-RemoteTests.ps1
# PowerShell script to run XUnitAssured remote tests with environment configuration

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("local", "staging", "prod")]
    [string]$Environment = "local",
    
    [Parameter(Mandatory=$false)]
    [string]$ApiUrl = "",
    
    [Parameter(Mandatory=$false)]
    [string]$ApiToken = "",
    
    [Parameter(Mandatory=$false)]
    [string]$Filter = "",
    
    [Parameter(Mandatory=$false)]
    [switch]$Verbose
)

Write-Host "🚀 XUnitAssured Remote Tests Runner" -ForegroundColor Cyan
Write-Host "Environment: $Environment" -ForegroundColor Yellow

# Set environment variable for test environment
$env:TEST_ENV = $Environment

# Set API URL if provided
if ($ApiUrl) {
    switch ($Environment) {
        "local" { $env:REMOTE_API_URL = $ApiUrl }
        "staging" { $env:STAGING_API_URL = $ApiUrl }
        "prod" { $env:PROD_API_URL = $ApiUrl }
    }
    Write-Host "API URL: $ApiUrl" -ForegroundColor Green
}

# Set API token if provided
if ($ApiToken) {
    switch ($Environment) {
        "staging" { $env:STAGING_API_TOKEN = $ApiToken }
        "prod" { $env:PROD_API_TOKEN = $ApiToken }
    }
    Write-Host "✅ API Token configured" -ForegroundColor Green
}

# Build test filter
$filterArg = ""
if ($Filter) {
    $filterArg = "--filter `"$Filter`""
    Write-Host "Test Filter: $Filter" -ForegroundColor Yellow
}

# Build verbosity argument
$verbosityArg = if ($Verbose) { "-v detailed" } else { "" }

# Navigate to test project directory
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptPath

Write-Host "`n📋 Running tests..." -ForegroundColor Cyan

# Run tests
$command = "dotnet test $verbosityArg $filterArg --logger `"console;verbosity=normal`""
Write-Host "Command: $command" -ForegroundColor DarkGray

Invoke-Expression $command

# Check exit code
if ($LASTEXITCODE -eq 0) {
    Write-Host "`n✅ All tests passed!" -ForegroundColor Green
} else {
    Write-Host "`n❌ Some tests failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}

# Cleanup environment variables
Remove-Item Env:TEST_ENV -ErrorAction SilentlyContinue
Remove-Item Env:REMOTE_API_URL -ErrorAction SilentlyContinue
Remove-Item Env:STAGING_API_URL -ErrorAction SilentlyContinue
Remove-Item Env:PROD_API_URL -ErrorAction SilentlyContinue
Remove-Item Env:STAGING_API_TOKEN -ErrorAction SilentlyContinue
Remove-Item Env:PROD_API_TOKEN -ErrorAction SilentlyContinue

<#
.SYNOPSIS
    Runs XUnitAssured remote API tests with environment configuration

.DESCRIPTION
    This script configures environment variables and runs remote API tests
    against different environments (local, staging, production)

.PARAMETER Environment
    Target environment: local, staging, or prod (default: local)

.PARAMETER ApiUrl
    Override API URL for the specified environment

.PARAMETER ApiToken
    API authentication token (for staging/prod)

.PARAMETER Filter
    Test filter expression (e.g., "Category=Integration")

.PARAMETER Verbose
    Enable verbose test output

.EXAMPLE
    .\Run-RemoteTests.ps1
    Run tests against local environment with default configuration

.EXAMPLE
    .\Run-RemoteTests.ps1 -Environment staging -ApiUrl "https://api.staging.com"
    Run tests against staging environment with custom URL

.EXAMPLE
    .\Run-RemoteTests.ps1 -Environment staging -Filter "Category=Integration"
    Run only integration tests against staging

.EXAMPLE
    .\Run-RemoteTests.ps1 -Environment prod -ApiToken "xyz..." -Verbose
    Run tests against production with authentication and verbose output

.EXAMPLE
    .\Run-RemoteTests.ps1 -Filter "Authentication=Bearer"
    Run only Bearer authentication tests

.NOTES
    For production testing, use with caution and ensure read-only operations!
#>
