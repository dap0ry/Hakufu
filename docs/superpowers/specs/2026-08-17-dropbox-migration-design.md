# Replace Google Drive backup/sync with Dropbox

Date: 2026-08-17
Status: Approved, pending implementation plan

## Problem

Hakufu's cloud backup/sync (desktop app uploads manga to a cloud folder;
web/mobile PWA reads from it) is built on Google Drive today, across two
repos:

- **HakufuWeb** (backend, `api/auth/google/[action].js` +
  `api/drive/[action].js` + `lib/google.js`): OAuth flow, refresh-token
  storage in Postgres (`google_connections` table), access-token minting
  for clients.
- **HakufuWeb** (web/mobile client, `webapp.js` + `reader.js` +
  `settings.js` + `account.js`): call `googleapis.com/drive/v3/*`
  directly with a short-lived access token obtained from the backend.
  `mangas.drive_file_id` stores the Drive file ID per manga.
- **Hakufu** (WPF desktop, `IGoogleDriveService`/`GoogleDriveService`,
  used by `BackupViewModel`/`SyncViewModel`): uploads/downloads manga
  files, and does folder lookup-or-create (Drive addresses files/folders
  by opaque ID, so every collection folder needs a "find by name under
  parent, else create" round-trip).

This already uses Drive's narrowest scope (`drive.file` — the app can
only see files it created, not the user's whole Drive), which avoids
Google's expensive security-review tier. But going from "a few friends
testing" to wider distribution still means either staying capped at
~100 OAuth test users, or completing Google's lighter app-verification
step (still needs a privacy policy URL, homepage, justification — an
ongoing maintenance surface). The user wants to swap the provider
entirely rather than deal with that.

## Goal

Replace Google Drive with Dropbox as the backup/sync provider, using
Dropbox's **App folder** access type: Dropbox auto-creates a sandboxed
folder inside the user's Dropbox that only this app can see. Unlike
Drive, App-folder-scoped Dropbox apps **never require a review**,
regardless of user count — this is the entire reason for the switch, so
it's a hard requirement, not a preference.

Per user decision, this is a clean-cut replacement, not a dual-provider
system: no one outside the developer has Drive connected today, so no
migration path or transition period is needed. Existing
`google_connections` rows and `mangas.drive_file_id` values are simply
abandoned.

## Chosen approach: Dropbox App-folder, path-addressed

Alternatives considered:

- **Dropbox "Full Dropbox" access** — lets the user pick any folder,
  but requires Dropbox's app review once past a small user cap, which
  defeats the entire purpose of switching providers. Rejected.
- **Support both Drive and Dropbox side by side** — explicitly rejected
  by the user in favor of a full replacement; doubles the surface
  (two OAuth flows, two token tables, two upload/download code paths in
  three places) for no current benefit.

Dropbox's App-folder API addresses everything by **path** relative to
the app's sandbox root (e.g. `/One Piece/vol-01.cbz`), not by opaque
IDs with a separate parent-folder graph like Drive. Uploading to a path
auto-creates any missing intermediate folders. This removes the
find-or-create-folder round-trip entirely (`FindOrCreateFolderAsync` /
`FindOrCreateBackupFolderAsync` in `GoogleDriveService` have no Dropbox
equivalent — callers just build a path string), and removes the need to
separately store/query a file name to recover the extension on restore
(`GetFileNameAsync`) since the path *is* the filename.

## Design

### 1. Backend (HakufuWeb)

**`lib/dropbox.js`** (replaces `lib/google.js`) — same shape:
`buildConsentUrl(state)`, `exchangeCodeForTokens(code)`,
`refreshAccessToken(refreshToken)`, `revokeToken(token)`. Dropbox's
OAuth 2 endpoints (`https://www.dropbox.com/oauth2/authorize`,
`https://api.dropboxapi.com/oauth2/token`) and refresh-token flow
(`token_access_type=offline` on the auth URL, same
`grant_type=refresh_token` shape on token refresh) are structurally
identical to Google's, so this is close to a 1:1 port. Env vars:
`DROPBOX_APP_KEY`, `DROPBOX_APP_SECRET`, `DROPBOX_REDIRECT_URI`
(replacing `GOOGLE_CLIENT_ID`/`GOOGLE_CLIENT_SECRET`/
`GOOGLE_REDIRECT_URI`).

**`api/auth/dropbox/[action].js`** (replaces `api/auth/google/[action].js`,
moved not copied) — `start` + `callback` actions, identical structure to
today: `callback` resolves the `state` link_code to a username, exchanges
the code, and upserts into `dropbox_connections`.

**`api/dropbox/[action].js`** (replaces `api/drive/[action].js`) —
`status` + `token` + `disconnect` + `link-start` actions, same structure.

**Database migration** (`lib/db.js` schema + one-off migration run
against the live Neon instance):
```sql
ALTER TABLE google_connections RENAME TO dropbox_connections;
ALTER TABLE mangas RENAME COLUMN drive_file_id TO dropbox_path;
```
`link_codes` is provider-agnostic already (just `code`/`username`/
`expires_at`) — no change needed. Since there is no current production
data to preserve, this is a straight rename; no backfill logic.

### 2. Web / mobile client (HakufuWeb)

Every direct `googleapis.com/drive/v3/*` call in `webapp.js`, `reader.js`,
and `settings.js` is replaced with the Dropbox equivalent:

| Drive call | Dropbox equivalent |
|---|---|
| `GET .../files/{id}?alt=media` (download) | `POST content.dropboxapi.com/2/files/download` with `Dropbox-API-Arg: {"path": "..."}` header |
| resumable upload session | `POST content.dropboxapi.com/2/files/upload` (small files) with `Dropbox-API-Arg` header carrying `path`/`mode`/etc. |
| `files?q=...` (find by name/parent) | not needed — path is the address |
| `files/{id}?fields=name` | not needed — path already contains the name |

`api.driveToken()` → `api.dropboxToken()` (client `api.js` wrapper
renamed to match the new backend route). `manga.drive_file_id` →
`manga.dropbox_path` everywhere it's read (`webapp.js`, `reader.js`,
`settings.js`). User-facing copy ("Conectar Google Drive", "Toca para
descargar", status labels, the "Sin respaldar en Drive" messages) updated
to say Dropbox.

### 3. Desktop (Hakufu, WPF)

**`IDropboxService`/`DropboxService`** (replaces
`IGoogleDriveService`/`GoogleDriveService`) in `Services/`:
- `IsConnectedAsync`, `StartConnectFlowAsync`, `DisconnectAsync`,
  `GetAccessTokenAsync` — unchanged shape, just repointed at
  `dropbox/*` backend routes instead of `drive/*`.
- `UploadFileAsync(accessToken, path, localFilePath, progress, ct)` —
  path replaces `parentFolderId` + `fileName`; no more
  `FindOrCreateFolderAsync`/`FindOrCreateBackupFolderAsync` (deleted,
  no replacement needed).
- `DownloadFileAsync(accessToken, path, destinationPath, progress, ct)`
  — `fileId` param becomes `path`.
- `GetFileNameAsync` — deleted (path already has the name; callers that
  used this to recover the extension on restore now just read it from
  the path directly).

`BackupViewModel`/`SyncViewModel`/`AccountView` (or wherever "Google
Drive" appears in bindings/copy) updated to say Dropbox and to build
paths (`$"/{collectionName}/{fileName}"`) instead of resolving folder
IDs.

### 4. Other references to update

- Legal terms in `HelpViewModel.cs` (§6, added in v0.9.6) currently name
  Google Drive explicitly for the data-processing disclosure — reword
  for Dropbox.
- `MEMORY.md` / `project_hakufu.md` memory file — update once shipped,
  per the project's own convention of keeping that file accurate.

### Error handling

Unchanged shape from today: a failed token refresh surfaces as "not
connected, reconnect" in both the web client and
`GoogleDriveService.GetAccessTokenAsync`'s existing
`InvalidOperationException` pattern (renamed, same behavior). Upload/
download failures keep the existing progress-reporting +
`EnsureSuccessStatusCode`/try-catch patterns already in place — Dropbox's
HTTP error shape (JSON body with an `error_summary` field) gets the same
treatment `google.js` gives Google's `error_description`/`error` fields.

### Testing

Neither repo has an automated test suite; verification follows the
pattern already used throughout this project: `node --check` on changed
JS files, `dotnet build` on the WPF app, and a manual smoke test of
connect → upload → disconnect → reconnect once real Dropbox credentials
exist.

## Manual steps required from the user (not automatable)

1. Create the Dropbox app in the
   [App Console](https://www.dropbox.com/developers/apps) — Scoped
   access, **App folder** permission type, `files.content.write` +
   `files.content.read` scopes. Register the redirect URI
   (same domain as the current `GOOGLE_REDIRECT_URI`, path
   `/api/auth/dropbox/callback`).
2. Set `DROPBOX_APP_KEY`, `DROPBOX_APP_SECRET`, `DROPBOX_REDIRECT_URI`
   as Vercel environment variables for HakufuWeb.
3. Run the two-line SQL migration above against the production Neon
   database (via Neon's SQL console, or hand it to the assistant if DB
   credentials get added locally).

## Risks / open questions

- Dropbox's small-file `upload` endpoint has a 150 MB limit; large
  manga PDFs could exceed it. Mitigation: use the
  `upload_session/start` + `/append_v2` + `/finish` chunked API instead
  of the simple upload endpoint from the start, rather than adding it
  later as a fix — same chunking shape `GoogleDriveService`'s resumable
  upload already handles, so no new concept for whoever implements it.
- Until step 1–2 above are done, none of the new code can be exercised
  end-to-end (`lib/dropbox.js` will throw its "missing config" error,
  mirroring `lib/google.js`'s existing `requireConfig()` behavior) —
  implementation can proceed and be verified structurally
  (`node --check`, `dotnet build`) ahead of that, but the connect flow
  itself needs real credentials to test.
