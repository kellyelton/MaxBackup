# Cloud Backup Target: Azure Blob Storage (Selective Upload)

## Goal
Add an Azure Blob Storage target to a one-way backup tool.

Behavior:
- Initial bulk upload.
- After that, detect changes cheaply and upload only changed or missing files.
- Preserve directory structure.
- No content hashing required.
- Future: support restore (download).

## Pick the Azure service
Use **Azure Blob Storage (GPv2 Storage Account, Block Blobs)**.

Avoid:
- Azure Archive tier for data you might want to restore quickly.
- ADLS Gen2 unless you need ACLs or filesystem semantics.

## Storage layout
### Container
- Single private container, example: `backups`.

### Blob naming (preserve structure)
Encode the local relative path into the blob name.

Rules:
- Use forward slashes `/`.
- Normalize case only if you intentionally want case-insensitive behavior. Windows is case-insensitive but blobs are not.
- Escape or normalize characters that are invalid/awkward for your tool (keep it simple, blobs allow most characters but you should avoid control chars).

Recommended format:
- `{backupRootPrefix } / { machineId }/ { volumeOrRoot }/ { relativePath }`

Example:
- `maxbackup/PC01/C/Users/KE/Documents/taxes/2024.pdf`

### Metadata schema (critical)
Relying on blob `Last-Modified` is misleading because it reflects upload time.

Write these **custom metadata** on upload:
- `mb_orig_mtime_utc`: original file last-write time in UTC, ISO8601 or ticks
- `mb_orig_size`: original file size in bytes
- `mb_source`: optional, machine or job id
- `mb_version`: optional, your app version

Constraints:
- Azure metadata is string key/value. Keep keys lowercase and short.
- Store mtime in UTC to avoid DST/timezone drift.

## Change detection strategy
Two workable modes.

### Mode A: LIST-first index (recommended)
Azure can return blob metadata in listing, so you can avoid per-file property calls.

1. List blobs under your prefix and build an in-memory map:
- key: blob name
- value: `(mb_orig_mtime_utc, mb_orig_size)` and blob properties
2. Walk the local filesystem.
3. For each local file, compute blob name and compare:
- If blob missing, upload.
- Else compare `size` and `mtime` from your metadata.
4. Upload only if different.

### Mode B: Per-file HEAD/properties
For each local file:
- Call GetProperties on the target blob.
- Compare metadata and decide.
This is simpler but higher request count on large trees.

## Upload rules
A local file is “same” if:
- `remote.mb_orig_size == local.length` AND
- `remote.mb_orig_mtime_utc == local.lastWriteTimeUtc` (same representation)

Otherwise upload and set metadata to local values.

## Prevent accidental overwrites
Use conditional requests:
- If you treat “remote changed externally” as an error, use an ETag precondition.
- If you do not care, skip this.

Practical approach:
- When remote exists and matches, do nothing.
- When remote exists and differs, overwrite (or write a new version if you enable blob versioning).

## Optional safety features (strongly recommended)
### Enable blob versioning (storage account)
- Helps recover from mistakes and supports future restore scenarios.
- Your tool can still behave “overwrite” while Azure retains prior versions.

### Enable soft delete
- Protects against accidental deletions.

## Authentication options
Pick one per deployment scenario.

### Best for server/service: Microsoft Entra ID (Managed Identity or service principal)
- Use Azure AD auth, no account keys.
- Grant least privilege:
- Storage Blob Data Contributor (write)
- Storage Blob Data Reader (read/list)
- Or narrower via container-level RBAC.

### Best for user-configured target: SAS token
- Generate a SAS scoped to:
- Container
- Permissions: `r` (read), `l` (list), `w` (write), `c` (create)
- Expiry reasonable for your use case
- Store SAS securely.

Avoid:
- Storage account shared key in end-user apps unless you control the environment.

## Azure SDK APIs you will use (conceptual)
Objects:
- `BlobServiceClient`
- `BlobContainerClient`
- `BlobClient`

Core operations:
- List: `GetBlobs(prefix=..., traits=Metadata)`
    - Properties (if doing Mode B): `GetProperties()`
    - Upload: `Upload(...)` with overwrite true/false and request conditions
- Set metadata: included at upload or set after

## Suggested algorithm (pseudocode, C#-ish)
container = new BlobContainerClient(connectionOrCredential, containerName)

// 1) Build remote index via listing (Mode A)
remoteIndex = Dictionary<string, RemoteEntry>()
foreach blobItem in container.GetBlobs(traits: Metadata, prefix: prefix):
remoteIndex[blobItem.Name] = Parse(blobItem.Metadata, blobItem.Properties.ContentLength)

// 2) Scan local files
foreach localFile in EnumerateFiles(root):
blobName = MakeBlobName(prefix, machineId, volume, localFile.RelativePath)
localSize = localFile.Length
localMtime = localFile.LastWriteTimeUtc

if !remoteIndex.TryGetValue(blobName, out remote):
Upload(localFile, blobName, localSize, localMtime)
continue

if remote.OrigSize == localSize && remote.OrigMtimeUtc == localMtime:
continue

Upload(localFile, blobName, localSize, localMtime)

## Upload implementation details
### Use block blobs (default)
Azure SDK handles chunking for large files.
Set parallelism reasonably. Default is often fine. Tune:
- Max concurrency
- Block size for large files

### Metadata set on upload
Always set:
- `mb_orig_mtime_utc`
- `mb_orig_size`

### Content-Type
Optional, but nice:
- Set based on extension.
- Not required for backups.

### Integrity
Even if you skip hashing for change detection, consider:
- Rely on TLS + Azure storage integrity
- Optionally compute a hash only during upload for verification, not for every compare

## Request volume control
If you have many files, avoid per-file GetProperties.
Use LIST-first index when possible.
Batch work:
- Process in directory chunks.
- Cache remote index to disk with an “index generation time” to reduce listing if you run very frequently.

## Dealing with deletes (one-way)
Your stated model is copy-only. Decide:
- Do nothing when local is deleted.
- Optional future: support “tombstone” markers.

If you later want “mirror deletes”, implement a retention policy and a delete mode.

## Restore planning (future)
Store enough information to reconstruct:
- Blob name encodes full path.
- Metadata includes original timestamps.
On download:
- Map blob name back to local path.
- Set last-write time from `mb_orig_mtime_utc`.

If you enable versioning:
- Restore can choose latest or a specific version.

## Minimal configuration you need in your app
- Storage account endpoint or connection string OR credential type (AAD vs SAS)
- Container name
- Prefix (backup root prefix)
- Machine ID and root mapping strategy (C:, D:, etc.)
- Concurrency limits
- Optional: enable/require versioning

## Testing checklist
- Upload and metadata correctness.
- Path normalization round-trip.
- Large file upload.
- High file count performance (listing vs per-file properties).
- Compare logic with:
- same size different mtime
- same mtime different size
- file replaced but mtime preserved (rare, but possible)
- Permission failures and retry behavior.
- Resume after interruption (idempotency).
