[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$komac = Join-Path ([System.IO.Path]::GetTempPath()) 'maxbackup-komac-2.16.0.exe'
Invoke-WebRequest 'https://github.com/russellbanks/Komac/releases/download/v2.16.0/komac-2.16.0-x86_64-pc-windows-msvc.exe' -OutFile $komac
if ((Get-FileHash $komac -Algorithm SHA256).Hash -ne 'BDC45BAF028F750DA7519CC8A5F2EAB5DFEF46DFE88650EEB0D0A9BF51446E4F') {
    throw 'Komac download checksum mismatch.'
}

$releaseUrl = "https://github.com/kellyelton/MaxBackup/releases"
$arguments = @(
    'update', 'KellyElton.Max.Backup', '--version', $Version,
    '--urls', "$releaseUrl/download/$Version/Max.Installer.Package.msi",
    '--release-notes-url', "$releaseUrl/tag/$Version"
)
if ($DryRun) { $arguments += @('--dry-run', '--skip-pr-check') }
else { $arguments += '--submit' }

# update creates its package branch from upstream; syncing the fork first is
# redundant and can require workflow permissions beyond the public_repo token.
& $komac @arguments
if ($LASTEXITCODE -ne 0) { throw "WinGet submission failed (Komac exit $LASTEXITCODE)." }
