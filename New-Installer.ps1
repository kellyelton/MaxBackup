<#
.SYNOPSIS
    Builds and publishes MaxBackup application and creates the installer package.

.DESCRIPTION
    This script automates the build, publish, and installation process for MaxBackup.
    It publishes the CLI and Service applications, optionally runs tests, builds the WiX installer,
    and can optionally run the installer.

.PARAMETER Version
    The version number to use for the build. If not specified, defaults to "1.0.0".

.PARAMETER Configuration
    The build configuration. Default is "Release".

.PARAMETER SkipTests
    Skip running the integration tests.

.PARAMETER RunInstaller
    After building the installer, run it automatically.

.PARAMETER SkipPublish
    Skip the publish step (useful if you only want to build the installer from already published binaries).

.EXAMPLE
    .\New-Installer.ps1
    Builds and publishes the application with default version 1.0.0

.EXAMPLE
    .\New-Installer.ps1 -Version "2.1.0" -RunInstaller
    Builds with version 2.1.0 and runs the installer after completion

.EXAMPLE
    .\New-Installer.ps1 -SkipTests -RunInstaller
    Builds without running tests and runs the installer
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$Version = "1.0.0",

    [Parameter()]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [Parameter()]
    [switch]$SkipTests,

    [Parameter()]
    [switch]$RunInstaller,

    [Parameter()]
    [switch]$SkipPublish
)

# Set error action preference
$ErrorActionPreference = "Stop"

# Get the script directory
$ScriptRoot = $PSScriptRoot
if (-not $ScriptRoot) {
    $ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
}

# Define paths
$CliProject = Join-Path $ScriptRoot "Max\Max.csproj"
$ServiceProject = Join-Path $ScriptRoot "MaxBackup.ServiceApp\MaxBackup.ServiceApp.csproj"
$TestProject = Join-Path $ScriptRoot "Max.IntegrationTests\Max.IntegrationTests.csproj"
$InstallerProject = Join-Path $ScriptRoot "Max.Installer.Package\Max.Installer.Package.wixproj"
$CliPublishPath = Join-Path $ScriptRoot "Max\bin\publish\win"
$ServicePublishPath = Join-Path $ScriptRoot "MaxBackup.ServiceApp\bin\publish\win"
$InstallerPath = Join-Path $ScriptRoot "Max.Installer.Package\bin\x64\$Configuration\en-US\Max.Installer.Package.msi"

function Write-ColorOutput {
    param(
        [Parameter(Mandatory)]
        [string]$Message,
        
        [Parameter()]
        [ConsoleColor]$ForegroundColor = [ConsoleColor]::White,
        
        [Parameter()]
        [switch]$NoNewline
    )
    
    $params = @{
        Object = $Message
        ForegroundColor = $ForegroundColor
    }
    
    if ($NoNewline) {
        $params.NoNewline = $true
    }
    
    Write-Host @params
}

function Write-Step {
    param([string]$Message)
    Write-ColorOutput "`n===> $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-ColorOutput "✓ $Message" -ForegroundColor Green
}

function Write-Error {
    param([string]$Message)
    Write-ColorOutput "✗ $Message" -ForegroundColor Red
}

function Write-Info {
    param([string]$Message)
    Write-ColorOutput "ℹ $Message" -ForegroundColor Yellow
}

# Main script execution
try {
    Write-ColorOutput @"

╔═══════════════════════════════════════════════════════════╗
║         MaxBackup Installer Build Script                 ║
╚═══════════════════════════════════════════════════════════╝

"@ -ForegroundColor Magenta

    Write-Info "Configuration: $Configuration"
    Write-Info "Version: $Version"
    Write-Info "Skip Tests: $SkipTests"
    Write-Info "Run Installer: $RunInstaller"
    Write-Info "Skip Publish: $SkipPublish"

    # Check if WiX is installed
    Write-Step "Checking WiX installation"
    $wixInstalled = $null -ne (Get-Command wix -ErrorAction SilentlyContinue)
    if (-not $wixInstalled) {
        Write-Info "WiX toolset not found. Installing..."
        dotnet tool install --global wix
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to install WiX toolset"
        }
        Write-Success "WiX toolset installed"
    } else {
        Write-Success "WiX toolset is already installed"
    }

    # Publish CLI
    if (-not $SkipPublish) {
        Write-Step "Publishing CLI application"
        $cliArgs = @(
            "publish"
            $CliProject
            "-c", $Configuration
            "-o", $CliPublishPath
            "-p:Platform=x64"
            "/p:AssemblyVersion=$Version"
            "/p:Version=$Version"
        )
        
        & dotnet @cliArgs
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to publish CLI application"
        }
        Write-Success "CLI published to $CliPublishPath"

        # Publish Service
        Write-Step "Publishing Service application"
        $serviceArgs = @(
            "publish"
            $ServiceProject
            "-c", $Configuration
            "-o", $ServicePublishPath
            "-p:Platform=x64"
            "/p:AssemblyVersion=$Version"
            "/p:Version=$Version"
        )
        
        & dotnet @serviceArgs
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to publish Service application"
        }
        Write-Success "Service published to $ServicePublishPath"
    } else {
        Write-Info "Skipping publish step"
    }

    # Verify CLI executable exists
    $cliExePath = Join-Path $CliPublishPath "max.exe"
    if (-not (Test-Path $cliExePath)) {
        throw "CLI executable not found at: $cliExePath"
    }
    Write-Success "CLI executable verified at: $cliExePath"

    # Run tests
    if (-not $SkipTests) {
        Write-Step "Running integration tests"
        
        # Set environment variable for tests
        $env:MAX_CLI_PATH = $cliExePath
        
        $testArgs = @(
            "test"
            $TestProject
            "--configuration", $Configuration
            "--logger", "trx;LogFileName=test-results.trx"
        )
        
        & dotnet @testArgs
        if ($LASTEXITCODE -ne 0) {
            throw "Tests failed"
        }
        Write-Success "All tests passed"
    } else {
        Write-Info "Skipping tests"
    }

    # Build installer
    Write-Step "Building WiX installer"
    $installerArgs = @(
        "build"
        $InstallerProject
        "-c", $Configuration
        "-p:Platform=x64"
        "/p:AssemblyVersion=$Version"
        "/p:Version=$Version"
    )
    
    & dotnet @installerArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build installer"
    }
    Write-Success "Installer built successfully"

    # Check if installer exists
    if (-not (Test-Path $InstallerPath)) {
        throw "Installer not found at expected path: $InstallerPath"
    }

    Write-ColorOutput "`n╔═══════════════════════════════════════════════════════════╗" -ForegroundColor Green
    Write-ColorOutput "║              Build Completed Successfully!               ║" -ForegroundColor Green
    Write-ColorOutput "╚═══════════════════════════════════════════════════════════╝`n" -ForegroundColor Green
    
    Write-Success "Installer location: $InstallerPath"
    
    $installerSize = (Get-Item $InstallerPath).Length / 1MB
    Write-Info "Installer size: $([math]::Round($installerSize, 2)) MB"

    # Run installer if requested
    if ($RunInstaller) {
        Write-Step "Launching installer"
        Start-Process -FilePath $InstallerPath -Wait
        Write-Success "Installer completed"
        
        # Refresh PATH environment variable to pick up changes from the installer
        Write-Step "Refreshing environment variables"
        $env:PATH = [System.Environment]::GetEnvironmentVariable("Path", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path", "User")
        Write-Success "PATH environment variable refreshed"
        Write-Info "Note: Other PowerShell sessions will need to be restarted to see PATH changes"
    } else {
        Write-Info "To install, run: Start-Process '$InstallerPath'"
    }

} catch {
    Write-Error "Build failed: $_"
    Write-ColorOutput $_.ScriptStackTrace -ForegroundColor Red
    exit 1
}
