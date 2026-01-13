# Cloud Backup Target: AWS S3 (Selective Upload)

## Goal
Add an AWS S3 target to a one-way backup tool.

Behavior:
- Initial bulk upload.
- After that, detect changes cheaply and upload only changed or missing files.
- Preserve directory structure.
- No content hashing required.
- Future: support restore (download).

## Pick the AWS service
Use **Amazon S3**.

Avoid for your workflow:
- Glacier classes (they are archive-oriented and come with operational constraints like minimum storage durations and slower restore paths).

## Bucket layout
### Bucket
- One private bucket, example: `maxbackup-prod`.

### Object key naming (preserve structure)
Encode the local relative path into the S3 object key.

Rules:
- Use `/`.
- Keys are case-sensitive. Decide if you want case-folding for Windows sources.

Recommended format:
- `{backupRootPrefix}/{machineId}/{volumeOrRoot}/{relativePath}`

Example:
- `maxbackup/PC01/C/Users/KE/Documents/taxes/2024.pdf`

## Metadata schema (critical)
Do not use S3 `LastModified` as your source timestamp. It is upload time.

Store these **custom metadata** on upload:
- `mb-orig-mtime-utc`: original file last-write time UTC (ISO8601 or ticks)
- `mb-orig-size`: original file size bytes
- `mb-source`: optional

Notes:
- S3 user metadata is returned by HEAD Object.
- Keys are stored without case sensitivity but typically returned lowercase by SDKs. Treat them case-insensitively.

## Change detection strategies
Unlike Azure, S3 LIST does not include your custom metadata, only basic fields like Size and LastModified.

You have two realistic modes:

### Mode A: Per-file HEAD (simple, usually fine)
For each local file:
1. HEAD the object.
2. Compare `mb-orig-size` and `mb-orig-mtime-utc`.
3. Upload only if missing or different.

This matches your “lots of HTTP checks” requirement.

### Mode B: Manifest index object (fewer HEADs at scale)
If you expect millions of files and frequent runs:
1. Maintain a manifest object (JSON/CSV) in S3 containing:
   - key -> orig size, orig mtime
2. Download manifest once per run.
3. Compare locally, upload changed files.
4. Update manifest at end.

This drastically reduces request count but adds complexity and consistency concerns.

## Comparison rule
A file is “same” if:
- `remote.mb-orig-size == local.length` AND
- `remote.mb-orig-mtime-utc == local.lastWriteTimeUtc` (same representation)

Otherwise upload and set metadata.

## Upload rules
### Overwrite behavior
When remote differs:
- PUT the same key to overwrite.

Optional safety:
- Enable S3 versioning to preserve prior versions even if you overwrite.

## Prevent accidental overwrites
Use conditional requests where possible:
- If-Match with ETag only works reliably if you understand ETag behavior.
- ETag is not guaranteed to be MD5 for multipart uploads.

Practical approach:
- If you care about detecting external modification, store your own “generation id” in metadata and check it.
- Otherwise accept overwrite and rely on bucket versioning for safety.

## Optional safety features (strongly recommended)
### Enable bucket versioning
- Supports future restore and rollback.
- Lets you keep “overwrite” semantics while retaining history.

### Enable lifecycle rules
- Move old objects to cheaper storage if you want, but keep restore requirements in mind.

### Enable default encryption (SSE-S3 or SSE-KMS)
- Most accounts already do this. Make it explicit.

## Authentication options
### For server/service: IAM role (best)
- Run on AWS infra or anywhere you can assume a role.
- Grant least privilege:
  - `s3:ListBucket` on bucket with prefix condition
  - `s3:GetObject` for HEAD/GET
  - `s3:PutObject` for upload
  - Optional `s3:GetObjectVersion` if versioning restore later

### For user-configured target: access keys (last resort)
- If you must, store securely and rotate.
- Prefer role assumption or short-lived credentials (STS).

## AWS SDK APIs you will use (conceptual)
Objects:
- S3 client

Core operations:
- LIST: `ListObjectsV2` with Prefix (optional for discovery or delete-mirror modes)
- HEAD: `HeadObject` to read metadata for a specific key
- PUT: `PutObject` (small files) or multipart upload (large files)

## Suggested algorithm (Mode A, per-file HEAD)
    s3 = new AmazonS3Client(credentials, region)

    foreach localFile in EnumerateFiles(root):
        key = MakeKey(prefix, machineId, volume, localFile.RelativePath)
        localSize = localFile.Length
        localMtime = localFile.LastWriteTimeUtc

        remote = TryHeadObject(bucket, key)
        if remote == null:
            PutObject(bucket, key, localFile, localSize, localMtime)
            continue

        remoteSize = ParseLong(remote.Metadata["mb-orig-size"])
        remoteMtime = ParseTime(remote.Metadata["mb-orig-mtime-utc"])

        if remoteSize == localSize && remoteMtime == localMtime:
            continue

        PutObject(bucket, key, localFile, localSize, localMtime)

## Upload implementation details
### Small files
Use single PUT.

### Large files
Use multipart upload.
- You do not need hashes for change detection, but multipart is needed for throughput and size limits.
- Remember: multipart ETag behavior is not a stable content hash.

### Metadata on upload
Set:
- `mb-orig-mtime-utc`
- `mb-orig-size`

### Content-Type
Optional.

### Integrity
Rely on TLS and S3 integrity checks.
Optionally compute a hash only during upload verification, not for every compare.

## Request volume control
Per-file HEAD is the main cost driver.
Ways to reduce without manifest:
- Only check files that are candidates:
  - Compare against local persisted state (last run snapshot).
  - If local mtime and size unchanged since last successful upload, skip remote check.
- Add rate limiting and concurrency:
  - Cap concurrent HEADs and PUTs.
  - Use exponential backoff on throttling.

## Dealing with deletes (one-way)
Your stated model is copy-only. Decide:
- Do nothing when local is deleted.
- Optional future: add tombstones or delete-mirror mode.

If you later want “mirror deletes”, implement retention rules and delete logic carefully.

## Restore planning (future)
- Keys encode the full path.
- Metadata includes original timestamps.
On download:
- Map key back to local path.
- Set last-write time from `mb-orig-mtime-utc`.

If versioning enabled:
- Restore can select latest or specific version id.

## Minimal configuration you need in your app
- Bucket name
- Region
- Prefix (backup root prefix)
- Machine ID and root mapping strategy
- Credential mode (role assumption vs keys)
- Concurrency limits
- Optional: require bucket versioning, encryption policy

## Testing checklist
- Metadata correctness on PUT and returned on HEAD.
- Path normalization round-trip.
- Large file multipart behavior.
- Compare logic with:
  - same size different mtime
  - same mtime different size
  - file replaced but mtime preserved
- Permission failures and retry behavior.
- Resume after interruption (idempotency).
