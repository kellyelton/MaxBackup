# Integrate IStorageProvider into BackupExecutor

## Summary

Modify `BackupExecutor.cs` to detect when a backup job uses a cloud provider and route file uploads through `IStorageProvider` instead of local file copy.

## Context

The Azure Blob Storage provider infrastructure is complete:
- `IStorageProvider` interface defined in `MaxBackup.ServiceApp/Providers/`
- `AzureBlobStorageProvider` implementation ready
- `BackupJobConfig.Provider` field added
- CLI commands for provider management working

However, `BackupExecutor.RunBackupJobAsync()` currently only supports local file copy. It needs to be updated to:
1. Check if `config.Provider` is set
2. Load the provider configuration from the service config
3. Create an `IStorageProvider` instance
4. Use `IStorageProvider.UploadFileAsync()` instead of `File.Copy()`

## Files to Modify

- `MaxBackup.ServiceApp/BackupExecutor.cs` - Main backup logic
- `MaxBackup.ServiceApp/UserBackupWorker.cs` - May need to pass provider configs

## Acceptance Criteria

- [ ] Jobs with a `Provider` set upload files to the cloud provider
- [ ] Jobs without a `Provider` continue to use local file copy (existing behavior)
- [ ] Upload uses metadata-based change detection (compare size + mtime from blob metadata)
- [ ] Errors during cloud upload are logged and don't crash the service
- [ ] Progress is logged during large uploads

## Technical Notes

Reference the design doc at `docs/AzureBlobStorageProvider.md` for:
- Metadata schema (`mb_orig_size`, `mb_orig_mtime_utc`)
- Change detection strategy (LIST-first index recommended for efficiency)
- Blob naming conventions

## Related Files

- [IStorageProvider.cs](../MaxBackup.ServiceApp/Providers/IStorageProvider.cs)
- [AzureBlobStorageProvider.cs](../MaxBackup.ServiceApp/Providers/AzureBlobStorageProvider.cs)
- [AzureBlobStorageProvider.md](../AzureBlobStorageProvider.md)
