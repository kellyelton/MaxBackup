# Cloud Backup Progress Reporting

## Summary

Add detailed upload progress logging and reporting for cloud backup operations.

## Context

When backing up to Azure Blob Storage, users need visibility into:
- Which files are being uploaded
- Upload progress for large files
- Overall job progress (X of Y files, Z% complete)
- Transfer speed and ETA

Currently local backups log progress every 30 seconds. Cloud backups should have similar or better visibility.

## Files to Modify

- `MaxBackup.ServiceApp/BackupExecutor.cs` - Add progress callbacks
- `MaxBackup.ServiceApp/Providers/AzureBlobStorageProvider.cs` - Implement progress reporting

## Acceptance Criteria

- [ ] Log message when starting upload of each file
- [ ] For files > 10MB, log progress updates during upload
- [ ] Log completion with file size and duration
- [ ] Overall job progress logged periodically (similar to local backup)
- [ ] Transfer speed displayed in human-readable format (KB/s, MB/s)

## Technical Notes

`IStorageProvider.UploadFileAsync()` already accepts `IProgress<long>? progress` parameter. This needs to be wired up to logging.

## Dependencies

Depends on: [backup-executor-provider-integration](backup-executor-provider-integration.md)
