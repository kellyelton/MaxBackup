$packageName = 'KellyElton.Max.Backup'
$fileType = 'msi'
$url = "https://github.com/kellyelton/MaxBackup/releases/download/$($env:ChocolateyPackageVersion)/Max.Installer.Package.msi"
$silentArgs = '/qn /norestart'
$validExitCodes = @(0, 3010, 1641)

# Checksum is injected by CI workflow at build time
$checksum = '__CHECKSUM_PLACEHOLDER__'
$checksumType = 'sha256'

Install-ChocolateyPackage -PackageName $packageName `
                          -FileType $fileType `
                          -Url $url `
                          -SilentArgs $silentArgs `
                          -ValidExitCodes $validExitCodes `
                          -Checksum $checksum `
                          -ChecksumType $checksumType
