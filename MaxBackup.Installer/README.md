# MaxBackup Installer

This WiX installer project creates an MSI package for installing the MaxBackup Windows Service.

## Security

This project uses **WixToolset.Sdk version 4.0.5** to address security vulnerability GHSA-rf39-3f98-xr7r (CVE-2024-29187).

### Vulnerability Details
- **Affected versions**: WixToolset.Sdk < 4.0.5
- **Severity**: High (CVSS 7.3)
- **Issue**: Binary hijack vulnerability when installers run as SYSTEM
- **Fix**: Upgraded to WixToolset.Sdk 4.0.5

### Service Account Configuration
By default, the service runs under the NT AUTHORITY\LocalService account (principle of least privilege). You can specify a different account during installation:

```bash
msiexec /i MaxBackupInstaller.msi SERVICEACCOUNT="DOMAIN\Username" SERVICEPASSWORD="password"
```

## Building the Installer

To build the installer MSI:

```bash
dotnet build MaxBackup.Installer/MaxBackup.Installer.wixproj -c Release
```

The output MSI will be located in the `bin/Release` directory.

## Requirements

- WiX Toolset v4.0.5 or higher
- .NET SDK 6.0 or higher
