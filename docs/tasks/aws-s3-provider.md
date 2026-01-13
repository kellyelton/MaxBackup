# AWS S3 Storage Provider

## Summary

Implement AWS S3 as a storage provider, following the same pattern as Azure Blob Storage.

## Context

With the provider abstraction (`IStorageProvider`) in place, adding AWS S3 support follows the established pattern. A design document already exists at `docs/AWSS3Provider.md`.

## Files to Create

- `MaxBackup.ServiceApp/Providers/AWSS3StorageProvider.cs` - S3 implementation

## Files to Modify

- `MaxBackup.Shared/ProviderConfig.cs` - Add `AWSS3ProviderConfig`
- `Max/ProviderCommand.cs` - Add S3 to provider type selection
- `Max/JobsCommand.cs` - Add S3 provider config type

## NuGet Packages

- `AWSSDK.S3` - AWS SDK for S3

## Configuration

```json
{
  "Type": "aws-s3",
  "Name": "my-s3-backup",
  "BucketName": "maxbackup-prod",
  "Region": "us-east-1",
  "AccessKeyId": "AKIA...",
  "SecretAccessKey": "...",
  "Prefix": "maxbackup/mypc"
}
```

## Acceptance Criteria

- [ ] `max provider add` supports AWS S3 with interactive TUI
- [ ] Non-interactive `max provider add --type aws-s3 ...` works
- [ ] S3 provider passes connectivity test
- [ ] Backup jobs can target S3 provider
- [ ] Metadata stored correctly (`mb-orig-size`, `mb-orig-mtime-utc`)
- [ ] Change detection uses HEAD Object for per-file comparison

## Technical Notes

Key differences from Azure:
- S3 LIST does not return custom metadata (requires HEAD per file or manifest)
- S3 metadata keys are case-insensitive, use hyphens not underscores
- Multipart upload needed for files > 5GB
- ETag behavior differs for multipart uploads

## Dependencies

Depends on: [backup-executor-provider-integration](backup-executor-provider-integration.md)

## Related Files

- [AWSS3Provider.md](../AWSS3Provider.md) - Design document
