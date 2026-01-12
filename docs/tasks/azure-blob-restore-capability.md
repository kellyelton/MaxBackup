# Azure Blob Storage Restore Capability

## Summary

Implement download/restore functionality from Azure Blob Storage to local filesystem.

## Context

Currently MaxBackup only supports one-way backup (local → cloud). Users need the ability to restore files from Azure Blob Storage back to their local system.

## Features

1. **Full restore** - Download all files from a backup to a local directory
2. **Selective restore** - Download specific files or folders
3. **Point-in-time restore** - If blob versioning is enabled, restore from a specific version

## CLI Commands

```bash
# Restore all files from a provider/path to local directory
max restore my-azure-backup /documents --to C:\Restored\Documents

# Restore specific file
max restore my-azure-backup /documents/taxes/2024.pdf --to C:\Restored\

# List available files (for selective restore)
max restore my-azure-backup /documents --list

# Restore from specific version (if versioning enabled)
max restore my-azure-backup /documents --version "2024-01-01" --to C:\Restored\
```

## Files to Create/Modify

- `Max/RestoreCommand.cs` - New CLI command
- `MaxBackup.ServiceApp/Providers/IStorageProvider.cs` - Add download methods
- `MaxBackup.ServiceApp/Providers/AzureBlobStorageProvider.cs` - Implement download

## Acceptance Criteria

- [ ] `max restore` command with provider, path, and destination
- [ ] Download preserves original file timestamps from metadata
- [ ] Progress reporting for large downloads
- [ ] Conflict handling (skip, overwrite, rename)
- [ ] Optional: version listing and point-in-time restore

## Dependencies

Depends on: [backup-executor-provider-integration](backup-executor-provider-integration.md)

## Related Files

- [AzureBlobStorageProvider.md](../AzureBlobStorageProvider.md) - Restore planning section
