# Velopack installer & auto-update migration

Date: 2026-08-16
Status: Approved, pending implementation plan

## Problem

The only distributable Hakufu currently ships is a raw `dotnet publish`
zip attached to GitHub releases (e.g. `Hakufu-0.8.1-x64.zip`, 105 MB,
self-contained). A user has to download it, extract it themselves into
whatever folder they choose, and find dozens of loose files (the .NET
runtime, `pdfium.dll`, `updater.exe`, satellite resources, …) next to
`Hakufu.exe`. There is no Start Menu entry, no desktop shortcut, no
uninstall entry — none of this is created because nothing ever *installs*
the app.

An Inno Setup script (`Installer/Hakufu.iss`) and build script
(`Build-Installer.ps1`) already exist and produce a proper wizard-based
`Setup.exe`, but they are unused — the last several releases (v0.5.9
through v0.8.1) all shipped the raw zip instead. Inno Setup itself isn't
even installed on the dev machine.

Separately, the app has a working **in-app auto-update** mechanism
(`UpdateService` + `updater.exe`) that polls the GitHub Releases API,
downloads the release zip asset, and re-launches a small updater
process that extracts it over the install directory. This part works
today and is not the source of the user's complaint — but it's
custom-built, zip-based plumbing that a proper packaging tool replaces
more robustly (delta updates, atomic apply, no manual process-juggling).

## Goal

Ship a "double-click and it just works" first-run install experience
(no wizard pages, no admin/UAC prompt, no picking a folder, no
loose-files zip) **and** make subsequent updates fully automatic and
silent, the way Discord/Slack/VS Code behave — without abandoning the
in-app visibility the current Update screen gives the user.

## Chosen approach: Velopack

[Velopack](https://velopack.io) (the maintained successor to
Squirrel.Windows) replaces **both** pieces at once: the installer *and*
the auto-update engine. This is a bigger change than "just polish the
Inno Setup script" — it touches `IUpdateService`, `UpdateViewModel`, the
`Updater` project, and the release process — but it directly produces
the single polished exe + silent background updates being asked for,
using a tool purpose-built for exactly this, rather than reimplementing
delta-update/atomic-apply logic by hand on top of Inno Setup.

Alternatives considered and rejected for this iteration:

- **Polish the existing Inno Setup script** (per-user install dir, no
  admin, branded wizard) — lower risk, reuses working code, but still
  leaves the current custom zip-based updater in place, and doesn't
  reach the "fully silent/automatic" bar the user asked for. Rejected
  in favor of Velopack per explicit user choice.
- **Custom bootstrapper WPF exe** — full control, but reinvents delta
  updates and atomic apply from scratch. Not worth it when a
  purpose-built library exists.

## Components removed

- `Installer/` (`Hakufu.iss`) — Inno Setup script, unused.
- `Build-Installer.ps1` — Inno Setup build script.
- `Updater/` project (`Updater.csproj`, `Program.cs`, `app.manifest`) —
  the custom zip-extracting relauncher. Removed from `Hakufu.slnx`.
- The zip-download/extract code path in `UpdateService.DownloadAndInstallAsync`.

## Components added

- NuGet package `Velopack` referenced from `Hakufu.csproj`.
- `vpk` CLI, installed as a local dotnet tool (`dotnet tool install
  --local vpk`) — build-time only, not a runtime dependency of the app.
- `Build-Release.ps1` — replaces `Build-Installer.ps1` (see below).

## Install & first run

`App.xaml.cs` → `OnStartup` gets `VelopackApp.Build().Run()` as its
**first** statement (required by Velopack: it intercepts
install/update/uninstall hook invocations before the rest of the app
initializes — this is what wires up the Start Menu shortcut on first
install and runs cleanup on uninstall).

Result: a single `Setup.exe` a user downloads and double-clicks.
Installs to `%LocalAppData%\Hakufu` — no admin rights required, no UAC
prompt, no directory-picker page, no component/wizard pages at all. The
app launches itself at the end, same as today's Inno Setup flow.

Trade-off carried over unchanged from today: the exe isn't
code-signed, so SmartScreen will still show an "unknown publisher"
warning on first run. Explicitly accepted (see brainstorming answers) —
not in scope for this migration.

## Auto-update behavior

`IUpdateService` is reimplemented on top of Velopack's `UpdateManager`
with a `GithubSource` pointed at `dap0ry/Hakufu`. Exact API surface
(`CheckForUpdatesAsync`, `DownloadUpdatesAsync`,
`ApplyUpdatesAndRestart`/equivalent) will be confirmed against current
Velopack docs during implementation, since the library's API can shift
between versions — the contract below is what matters, not the literal
method names:

- On app start, check for and download an available update **in the
  background**, with no visible UI and no user action required.
- Never force a restart while the app is in use (a user mid-chapter
  should not get yanked out of their reading session).
- Once a downloaded update is ready to apply, surface it unobtrusively:
  `UpdateViewModel` shows a "Restart to update" affordance instead of
  the current "Download" button. Clicking it applies the update and
  restarts. If the user never clicks it, the update applies the next
  time the app is naturally closed and reopened.
- `UpdateViewModel`'s existing states (`IsChecking`, `IsUpdateAvailable`,
  `IsUpToDate`, `HasError`, `StatusMessage`) are kept but re-purposed to
  reflect this background flow instead of a manual
  check → download → apply sequence. `IsDownloading`/`DownloadProgress`
  may become unused or repurposed for the background download's
  progress, decided during implementation.
- Changelog text keeps being fetched from the GitHub Releases API
  (`GitHubRelease` model, already in place) purely for display — this
  is independent of how the update itself is delivered/applied.

## Release / build process

`Build-Release.ps1` (replaces `Build-Installer.ps1`):

1. `dotnet publish` — self-contained, win-x64, **no**
   `PublishSingleFile` (Velopack manages the whole output folder as a
   unit; single-file would fight that).
2. `vpk pack` — packages the publish folder into a Velopack release:
   produces `Setup.exe`, a full `nupkg`, and delta packages relative to
   the previous published release (Velopack fetches the previous
   release's manifest from the GitHub source to compute deltas).
3. Upload the produced assets to a GitHub release via `gh release
   create`/`gh release upload`, run manually — same manual-trigger
   workflow as today, no GitHub Actions in this iteration (explicitly
   deferred per brainstorming answers).

## Migration for existing (pre-Velopack) users

Users on v0.8.1 and earlier installed by manually unzipping into an
arbitrary folder — there is no installed-app manifest for a Velopack
`Setup.exe` to detect and take over. No migration code is written for
this. Instead:

- The next release (v0.9.0) ships as the first Velopack build.
- Its GitHub release notes explicitly say this is the **last manual
  step**: download and run `Setup.exe` once. Every release after that
  updates automatically in the background as described above.
- The existing in-app "Update available" flow (which already just
  opens the GitHub releases page today) is what carries a v0.8.1 user
  to that page to get v0.9.0 — no code change needed for that part.

## Testing / verification

- Local build of `Setup.exe` via `Build-Release.ps1`; verify a clean-VM
  or clean-user install: no admin prompt, shortcut created, app
  launches.
- Publish a v(N) release, then a v(N+1) release, and confirm a running
  v(N) install picks up v(N+1) in the background and offers/applies the
  restart-to-update affordance without any zip/manual step.
- Confirm uninstall (via Windows "Apps" settings) removes the app but
  preserves `%APPDATA%\Hakufu` user data, matching today's Inno Setup
  behavior.

## Out of scope

- Code signing / SmartScreen removal.
- GitHub Actions / CI-driven releases.
- Any change to the mobile app (separate, explicitly deferred by the
  user to a later conversation).
- Automatic migration/detection of pre-Velopack manual installs.
