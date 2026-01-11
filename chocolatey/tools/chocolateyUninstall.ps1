$packageName = 'KellyElton.Max.Backup'
$installerType = 'msi'

# The MSI has a static product code or we can just let Chocolatey handle it if it's in the registry
# Since we are using an MSI, Chocolatey's automatic uninstaller should handle it if enabled.
# However, providing a script is safer.

Uninstall-ChocolateyPackage -PackageName $packageName `
                            -FileType $installerType `
                            -SilentArgs "/qn /norestart"
