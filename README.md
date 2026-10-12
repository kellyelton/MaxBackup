# MaxBackup

A simple, set-it-and-forget-it backup tool for Windows.

MaxBackup is a one-way, additive backup utility. It copies new and changed files from your source folders to your destination, but never deletes anything from the destination. This makes it safe for archival backups where you want to preserve file history even if you delete the original.

**How it decides what to back up:** MaxBackup compares the last modified time of each file. If the source file is newer (or doesn't exist in the destination), it gets copied. Files that haven't changed are skipped.

## Features

- Runs quietly in the background as a Windows service
- Backs up to local drives, network shares, or Azure Blob Storage
- Uses glob patterns to include or exclude specific files
- Supports per-folder `.maxbackupignore` files for repository-specific exclusions
- Checks for changes every 10 seconds
- Multi-user support (each Windows user can have their own backup jobs)

## Requirements

- Windows 10/11 (64-bit)
- Administrator access (for initial installation only)

## Installation

Install using Chocolatey:

```
choco install KellyElton.Max.Backup
```

Or using winget:

```
winget install KellyElton.Max.Backup
```

After installation, the `max` command will be available in your terminal.

### Upgrades and uninstalling

By default, uninstalling preserves service registration and logs in
`%ProgramData%\MaxBackup`. To remove either during an explicit uninstall, run
from an administrator terminal using this version's MSI:

```powershell
msiexec /x "Max.Installer.Package.msi" REMOVECONFIG=1 REMOVELOGS=1
```

Both flags default to `0` and can be used independently. `REMOVECONFIG=1` removes
only the service's `config.json` (all users' registrations and service settings).
`REMOVELOGS=1` removes only `service*.log` files from the service's `logs` folder.
Neither flag removes user job definitions, personal backup logs, source files,
or backed-up files. Cleanup is disabled during upgrades, installs, and repairs.

**Upgrading from 0.2.32 or earlier:** the old version's uninstaller can delete that
folder before the new installer runs. Save a copy outside the folder before
upgrading if you need to retain its registration or logs. After upgrading, check
`max status` and run `max register` if needed. User job definitions in
`%USERPROFILE%\maxbackupconfig.json` are stored separately and are preserved.

## Quick Start

### 1. Create a backup job

Back up your Documents folder to an external drive:

```
max jobs create "My Documents" "C:\Users\YourName\Documents" "D:\Backups\Documents" --include "**"
```

### 2. Register with the service

```
max register
```

That's it. Your files will start backing up automatically.

## Configuration Examples

### Back up everything in a folder

```
max jobs create "Photos" "C:\Users\YourName\Pictures" "D:\Backups\Pictures" --include "**"
```

### Back up only specific file types

Back up only Word and Excel files:

```
max jobs create "Work Docs" "C:\Work" "D:\Backups\Work" --include "**/*.docx" --include "**/*.xlsx"
```

### Back up with exclusions

Back up everything except temporary files:

```
max jobs create "Projects" "C:\Projects" "D:\Backups\Projects" --include "**" --exclude "**/node_modules/**" --exclude "**/bin/**" --exclude "**/*.tmp"
```

### Back up specific files from your home folder

Use `~` as shorthand for your user profile folder:

```
max jobs create "Configs" "~" "D:\Backups\HomeConfigs" --include ".gitconfig" --include ".ssh/**" --include "Documents/PowerShell/**"
```

### Exclude files within an individual repository

If your job backs up all of `F:\Source`, create `F:\Source\MyApp\.maxbackupignore`:

```text
# Dependencies at any depth in this repository
node_modules/

# Build output immediately inside this repository
/dist/

# Generated assets at this specific path
assets/generated/

# Temporary files at any depth
*.tmp
```

These rules apply to `MyApp` and its subfolders. Other repositories keep their own
backup selection. Put an ignore file in a subfolder to add exclusions there;
parent rules still apply. Files above the job's source folder are not consulted.

Use one pattern per line, `/` for paths, `*` within a path segment, and `**` across
directory levels. Matching is case-insensitive. Blank lines and full-line `#`
comments are allowed; surrounding whitespace is trimmed.

| Pattern | Excludes |
|---------|----------|
| `node_modules/` | Directories named `node_modules` anywhere below this ignore file |
| `/dist/` | The `dist` directory directly beside this ignore file |
| `assets/generated/` | That exact subtree relative to this ignore file |
| `*.tmp` | Matching names at any depth |
| `**/generated/*.tmp` | Temporary files directly inside any `generated` directory |

A trailing `/` means directories only. Without it, a pattern can match files or
directories. Matching directories are skipped before their contents are scanned.
Existing job include/exclude filters still apply, and exclusions always win.

This is an exclusion-only format: `!` re-inclusion rules, backslash paths,
drive-qualified paths, and `..` traversal are rejected with a logged error.
It does not read `.gitignore` or support the complete Git ignore syntax.

Rules are reread on the next backup pass for both local and cloud jobs. The ignore
file itself is eligible for backup unless excluded. An invalid or unreadable
ignore file stops transfers for that job pass and logs the cause. Previously
backed-up files remain in the destination when you add an exclusion.

## CLI Reference

### Managing Jobs

| Command | Description |
|---------|-------------|
| `max jobs list` | Show all configured backup jobs |
| `max jobs create <name> <source> <dest>` | Create a new backup job |
| `max jobs modify <name> --source <path>` | Change a job's settings |
| `max jobs delete <name>` | Remove a backup job |

### Registration

| Command | Description |
|---------|-------------|
| `max register` | Start backing up (registers you with the service) |
| `max unregister` | Stop backing up |
| `max status` | Check if backups are running |

### Monitoring

| Command | Description |
|---------|-------------|
| `max watch` | Watch the service log in real-time |
| `max watch --user` | Watch your personal backup log |
| `max watch --tail 50` | Show last 50 lines before watching |
| `max service status` | Check if the Windows service is running |

Press `Ctrl+C` to stop watching logs.

## File Locations

| What | Where |
|------|-------|
| Your config file | `%USERPROFILE%\maxbackupconfig.json` |
| Your backup logs | `%USERPROFILE%\.max\logs\backup.log` |
| Service logs | `%ProgramData%\MaxBackup\logs\` |

## Configuration File

Your backup jobs are stored in `maxbackupconfig.json` in your user folder. You can edit this file directly if you prefer:

```json
{
  "Backup": {
    "Jobs": [
      {
        "Name": "My Documents",
        "Source": "C:\\Users\\YourName\\Documents",
        "Destination": "D:\\Backups\\Documents",
        "Include": ["**"],
        "Exclude": []
      }
    ]
  }
}
```

After editing the config file, changes are picked up automatically (no restart needed).

## Troubleshooting

**Backups not running?**
1. Check if the service is running: `max service status`
2. Check if you're registered: `max status`
3. Look at the logs: `max watch --user`

**Files not being backed up?**
- Make sure your include patterns match the files (use `**` to match everything)
- Check for exclude patterns that might be filtering them out
- Check `.maxbackupignore` files in the source folder and the file's parent folders
- Check the backup log for an invalid or unreadable `.maxbackupignore` that stopped the job pass
- Files that are open/locked by other programs will be skipped and retried later

**Permission errors?**
- The service runs under your user account, so it can only access files you have permission to read
- Network drives need to be accessible from your user session

## License

[Mozilla Public License 2.0](LICENSE)
