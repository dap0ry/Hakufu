# Velopack Installer & Auto-Update Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the unused Inno Setup installer and the custom zip-based auto-updater with Velopack, so Hakufu ships as a single double-click `Setup.exe` (no admin prompt, no wizard pages) and updates itself silently in the background.

**Architecture:** `VelopackApp.Build().Run()` hooks install/update/uninstall lifecycle events at app startup. `UpdateService` wraps Velopack's `UpdateManager` (pointed at the `dap0ry/Hakufu` GitHub repo) to check/download updates in the background; a new `Build-Release.ps1` script packages the published app with the `vpk` CLI and publishes the GitHub release. The GitHub-API-based changelog fetch (`FetchLatestReleaseAsync`) is kept as-is — it's just display text and is decoupled from how the update is actually delivered.

**Tech Stack:** .NET 10 / WPF, Velopack NuGet package + `vpk` CLI (dotnet local tool), PowerShell, GitHub CLI (`gh`).

**Spec:** `docs/superpowers/specs/2026-08-16-velopack-installer-design.md`

## Global Constraints

- No automated test suite exists anywhere in this repo. Verification for
  every task is `dotnet build Hakufu.csproj` succeeding, plus the manual
  check described in that task — there is no `dotnet test` step anywhere
  in this plan, and that is expected, not a gap.
- Target framework stays `net10.0-windows`; strict MVVM with manual DI
  composed in `App.xaml.cs` (per `CLAUDE.md`) — don't introduce a
  container.
- `StackPanel.Spacing` is not available in WPF — use `Margin`.
- User-facing strings, code comments, and commit messages are in
  Spanish, matching the rest of the codebase.
- All `DynamicResource`/`StaticResource` XAML keys referenced (e.g.
  `PrimaryButton`, `BoolToVisibility`) already exist globally — do not
  redeclare them locally.
- Existing constructor signatures for ViewModels not touched by this
  plan (e.g. `HomeViewModel`) must keep compiling unchanged — `HomeViewModel`
  keeps taking `IUpdateService updateService` in its constructor even
  though it doesn't call anything on it (pre-existing, out of scope).
- Do not run `gh release create` / actually publish a GitHub release as
  part of any task's verification — that's a real, outward-facing,
  hard-to-reverse production action. Every task verifies with local
  builds only; the final real release is a manual step the user runs
  themselves when ready (called out explicitly in Task 6).

---

### Task 1: Remove Inno Setup / custom updater, add Velopack

**Files:**
- Delete: `Installer/Hakufu.iss`
- Delete: `Updater/Updater.csproj`, `Updater/Program.cs`, `Updater/app.manifest`
- Modify: `Hakufu.csproj`
- Create: `.config/dotnet-tools.json` (via `dotnet new tool-manifest`)

**Interfaces:**
- Consumes: nothing from earlier tasks (first task).
- Produces: a `Velopack` PackageReference and a local `vpk` dotnet tool
  available to later tasks (Task 5's build script calls `dotnet tool run
  vpk`).

- [ ] **Step 1: Delete the Inno Setup installer and the custom Updater project**

```bash
git rm -r Installer
git rm -r Updater
```

- [ ] **Step 2: Remove the Updater build hook and stale excludes from `Hakufu.csproj`**

Current `Hakufu.csproj` has this exclude line:

```xml
<DefaultItemExcludes>$(DefaultItemExcludes);Updater\**;Installer\**</DefaultItemExcludes>
```

Change it to just:

```xml
<DefaultItemExcludes>$(DefaultItemExcludes)</DefaultItemExcludes>
```

(Or remove the `<DefaultItemExcludes>` line entirely — both directories
no longer exist so there's nothing left to exclude.)

Then delete the entire `PublishUpdater` target block at the bottom of
the file (it compiles `Updater\Updater.csproj`, which no longer
exists):

```xml
  <!--
    AfterPublish: compila updater.exe y lo copia a la carpeta de publicación.
    Se ejecuta automáticamente al publicar desde Visual Studio o con dotnet publish.
    Solo se activa cuando PublishDir está definido (es decir, en publish, no en build normal).
  -->
  <Target Name="PublishUpdater" AfterTargets="Publish" Condition="'$(PublishDir)' != ''">
    <Message Text="[AfterPublish] Compilando updater.exe..." Importance="High" />
    <Exec Command="dotnet publish &quot;$(MSBuildThisFileDirectory)Updater\Updater.csproj&quot; -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o &quot;$(MSBuildThisFileDirectory)obj\updater-temp&quot; --verbosity quiet" />
    <Copy SourceFiles="$(MSBuildThisFileDirectory)obj\updater-temp\updater.exe" DestinationFolder="$(PublishDir)" OverwriteReadOnlyFiles="true" />
    <RemoveDir Directories="$(MSBuildThisFileDirectory)obj\updater-temp" />
    <Message Text="[AfterPublish] updater.exe copiado a $(PublishDir)" Importance="High" />
  </Target>
```

Delete the whole `<Target Name="PublishUpdater" ...> ... </Target>` block.

- [ ] **Step 3: Bump the app version to 0.9.0 (first Velopack release)**

In `Hakufu.csproj`, change:

```xml
    <Version>0.5.4</Version>
    <AssemblyVersion>0.5.4.0</AssemblyVersion>
    <FileVersion>0.5.4.0</FileVersion>
```

to:

```xml
    <Version>0.9.0</Version>
    <AssemblyVersion>0.9.0.0</AssemblyVersion>
    <FileVersion>0.9.0.0</FileVersion>
```

- [ ] **Step 4: Add the Velopack package reference**

```bash
dotnet add Hakufu.csproj package Velopack
```

This resolves and pins the current latest stable version in the
`<ItemGroup>` alongside `Docnet.Core`, `Microsoft.Web.WebView2`, etc. —
don't hand-type a version number.

- [ ] **Step 5: Create a local dotnet tool manifest and install `vpk`**

```bash
dotnet new tool-manifest
dotnet tool install --local vpk
```

This creates `.config/dotnet-tools.json`, committed to the repo so
`Build-Release.ps1` (Task 5) can run `dotnet tool run vpk` without
requiring a global install. Verify it was created:

```bash
cat .config/dotnet-tools.json
```

Expected: JSON containing a `"vpk"` entry under `"tools"`.

- [ ] **Step 6: Verify the project still builds**

```bash
dotnet build Hakufu.csproj
```

Expected: `Build succeeded.` — no reference to `Updater\Updater.csproj`
anywhere, no missing-file errors.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "chore: quitar Inno Setup/Updater, añadir Velopack + vpk"
```

---

### Task 2: Hook `VelopackApp` into startup

**Files:**
- Modify: `App.xaml.cs`

**Interfaces:**
- Consumes: `Velopack.VelopackApp` (from the package added in Task 1).
- Produces: nothing new consumed by later tasks directly, but this must
  run before any Velopack API is used (Task 3's `UpdateManager`), so it
  has to land first in execution order within `App`.

- [ ] **Step 1: Add the `using` and a constructor that runs `VelopackApp.Build().Run()` first**

`App.xaml.cs` currently has no explicit constructor — only `OnStartup`
and `OnExit`. Add one, and it must be the very first thing that runs in
the app (Velopack's own requirement: it needs to intercept
install/update/uninstall invocations before anything else touches the
filesystem or UI):

```csharp
using System.Windows;
using Hakufu.Data;
using Hakufu.MVVM.Model;
using Hakufu.MVVM.ViewModel;
using Hakufu.Services;
using Velopack;

namespace Hakufu;

public partial class App : Application
{
    private IDataRepository? _repo;
    private DateTime _sessionStart;

    public App()
    {
        // Debe ejecutarse antes que cualquier otra cosa: gestiona los
        // hooks de instalar/actualizar/desinstalar de Velopack (crea el
        // acceso directo del menú Inicio en la primera instalación,
        // limpia versiones antiguas, etc.).
        VelopackApp.Build().Run();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        _sessionStart = DateTime.Now;
        base.OnStartup(e);
        ...
```

(Leave the rest of `OnStartup` and `OnExit` unchanged for now — the
background update kick-off is added in Task 4 once `UpdateService`
actually implements it.)

- [ ] **Step 2: Verify the app still builds and launches**

```bash
dotnet build Hakufu.csproj
dotnet run --project Hakufu.csproj
```

Expected: builds clean, the app window opens normally. `VelopackApp.Build().Run()`
is a documented no-op when the process isn't running from a
Velopack-installed context (e.g. `dotnet run`), so this must not throw
or change any visible behavior yet.

- [ ] **Step 3: Commit**

```bash
git add App.xaml.cs
git commit -m "feat: inicializar VelopackApp en el arranque"
```

---

### Task 3: Reimplement `IUpdateService` / `UpdateService` on Velopack

**Files:**
- Modify: `Services/IUpdateService.cs`
- Modify: `Services/UpdateService.cs`

**Interfaces:**
- Consumes: `Velopack.UpdateManager`, `Velopack.UpdateInfo`,
  `Velopack.Sources.GithubSource` (from the package added in Task 1).
- Produces (used by Task 4's `UpdateViewModel` and `App.xaml.cs`):
  - `Task CheckForUpdatesInBackgroundAsync()`
  - `bool IsUpdateReadyToApply { get; }`
  - `void ApplyUpdateAndRestart()`
  - `Version GetCurrentVersion()` and `Task<GitHubRelease?> FetchLatestReleaseAsync()`
    kept with their existing signatures, unchanged behavior.

- [ ] **Step 1: Rewrite `IUpdateService`**

Replace the full contents of `Services/IUpdateService.cs`:

```csharp
using Hakufu.MVVM.Model;

namespace Hakufu.Services;

public interface IUpdateService
{
    Version GetCurrentVersion();

    /// Se usa solo para mostrar el changelog en pantalla — independiente
    /// de cómo se descarga/aplica la actualización real (eso lo gestiona
    /// Velopack por debajo).
    Task<GitHubRelease?> FetchLatestReleaseAsync();

    /// Comprueba y descarga una actualización en segundo plano, sin
    /// avisar ni interrumpir al usuario. No lanza excepción si falla
    /// (sin red, no instalado vía Velopack, etc.) — falla en silencio.
    Task CheckForUpdatesInBackgroundAsync();

    /// True una vez hay una actualización descargada lista para aplicar.
    bool IsUpdateReadyToApply { get; }

    /// Aplica la actualización pendiente y reinicia la app. No hace
    /// nada si no hay ninguna actualización lista.
    void ApplyUpdateAndRestart();
}
```

- [ ] **Step 2: Rewrite `UpdateService`**

Replace the full contents of `Services/UpdateService.cs`:

```csharp
using System.Reflection;
using System.Text.Json;
using Hakufu.MVVM.Model;
using Velopack;
using Velopack.Sources;

namespace Hakufu.Services;

public class UpdateService : IUpdateService
{
    private const string ApiUrl  = "https://api.github.com/repos/dap0ry/Hakufu/releases/latest";
    private const string RepoUrl = "https://github.com/dap0ry/Hakufu";
    private const string UserAgent = "HakufuApp";

    private static readonly HttpClient _http = new();

    private readonly UpdateManager _mgr = new(new GithubSource(RepoUrl, null, false));
    private UpdateInfo? _pendingUpdate;

    static UpdateService()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    public Version GetCurrentVersion()
        => Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 1, 0);

    public async Task<GitHubRelease?> FetchLatestReleaseAsync()
    {
        var json = await _http.GetStringAsync(ApiUrl);
        return JsonSerializer.Deserialize<GitHubRelease>(json);
    }

    public bool IsUpdateReadyToApply => _pendingUpdate is not null;

    public async Task CheckForUpdatesInBackgroundAsync()
    {
        // Fuera de una instalación gestionada por Velopack (p. ej. `dotnet run`
        // en desarrollo) no hay nada que comprobar.
        if (!_mgr.IsInstalled)
            return;

        try
        {
            var updateInfo = await _mgr.CheckForUpdatesAsync();
            if (updateInfo is null)
                return;

            await _mgr.DownloadUpdatesAsync(updateInfo);
            _pendingUpdate = updateInfo;
        }
        catch
        {
            // Silencioso a propósito: una comprobación fallida en segundo
            // plano nunca debe interrumpir ni bloquear la app. El
            // changelog manual (FetchLatestReleaseAsync) sigue disponible
            // como vía alternativa para que el usuario vea si hay algo nuevo.
        }
    }

    public void ApplyUpdateAndRestart()
    {
        if (_pendingUpdate is null)
            return;

        _mgr.ApplyUpdatesAndRestart(_pendingUpdate);
    }
}
```

> **Nota para quien implemente:** este código asume la superficie de API
> actual de Velopack (`UpdateManager.IsInstalled`,
> `CheckForUpdatesAsync()`, `DownloadUpdatesAsync(UpdateInfo)`,
> `ApplyUpdatesAndRestart(UpdateInfo)`, `GithubSource(repoUrl, token,
> prerelease)`). Si `dotnet build` falla por un nombre distinto en la
> versión de paquete resuelta en la Tarea 1, abre el paquete instalado
> en `~/.nuget/packages/velopack/<version>/lib/` (o el IntelliSense de
> tu editor) para confirmar el nombre exacto y ajusta la llamada — la
> forma (comprobar → descargar → guardar referencia → aplicar) no
> cambia, solo los nombres literales podrían diferir entre versiones.

- [ ] **Step 3: Verify build**

```bash
dotnet build Hakufu.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 4: Manual smoke test — background check is a safe no-op outside an install**

```bash
dotnet run --project Hakufu.csproj
```

Navigate to the Updates screen in the running app. Expected: no crash,
no exception dialog (the `OnStartup` try/catch would show one if
anything threw). At this point in the plan `CheckForUpdatesInBackgroundAsync`
isn't wired to run automatically yet (that's Task 4) — this step is
only confirming the service class itself compiles and instantiates
cleanly via `App.xaml.cs`'s existing `new UpdateService()` call.

- [ ] **Step 5: Commit**

```bash
git add Services/IUpdateService.cs Services/UpdateService.cs
git commit -m "feat: UpdateService sobre Velopack en vez de zip+updater.exe"
```

---

### Task 4: Wire background check + "Restart to update" UI

**Files:**
- Modify: `App.xaml.cs`
- Modify: `MVVM/ViewModel/UpdateViewModel.cs`
- Modify: `MVVM/View/UpdateWindow.xaml`

**Interfaces:**
- Consumes: `IUpdateService.CheckForUpdatesInBackgroundAsync()`,
  `IUpdateService.IsUpdateReadyToApply`, `IUpdateService.ApplyUpdateAndRestart()`
  (all produced by Task 3).
- Produces: `UpdateViewModel.IsRestartReady` (bool property) and
  `UpdateViewModel.RestartCommand` (`RelayCommand`), bound from XAML.

- [ ] **Step 1: Kick off the background check at startup**

In `App.xaml.cs`, in `OnStartup`, right after `window.Show();`, add:

```csharp
            window.Show();

            // Comprobar/descargar actualizaciones en segundo plano, sin
            // bloquear el arranque ni avisar al usuario.
            _ = updateService.CheckForUpdatesInBackgroundAsync();
```

- [ ] **Step 2: Add `IsRestartReady` and `RestartCommand` to `UpdateViewModel`**

Current constructor and field list (`MVVM/ViewModel/UpdateViewModel.cs`):

```csharp
    private bool   _isDownloading     = false;
    private bool   _hasError          = false;
    private string _statusMessage     = "Comprobando actualizaciones…";
```

Add a new backing field right after `_isDownloading`:

```csharp
    private bool   _isDownloading     = false;
    private bool   _isRestartReady    = false;
    private bool   _hasError          = false;
    private string _statusMessage     = "Comprobando actualizaciones…";
```

Add the property next to the other bool properties (after `IsDownloading`'s
property block):

```csharp
    public bool IsRestartReady
    {
        get => _isRestartReady;
        private set => SetProperty(ref _isRestartReady, value);
    }
```

In the constructor, read the service's current state once (it may
already have a downloaded update ready from a previous session's
background check):

```csharp
    public UpdateViewModel(IUpdateService svc, INavigationService nav)
    {
        _svc = svc;
        _nav = nav;
        var v = svc.GetCurrentVersion();
        CurrentVersion  = $"v{v.Major}.{v.Minor}.{v.Build}";
        IsRestartReady  = svc.IsUpdateReadyToApply;
        _ = CheckAsync();
    }
```

In `CheckAsync()`, re-read it alongside the existing GitHub-changelog
check so "Comprobar de nuevo" also refreshes this state — add this line
at the very start of the `try` block (before `var release = ...`):

```csharp
        try
        {
            IsRestartReady = _svc.IsUpdateReadyToApply;

            var release = await _svc.FetchLatestReleaseAsync();
```

Add the command next to the other `RelayCommand` properties at the
bottom of the class:

```csharp
    public RelayCommand RestartCommand => new(() => _svc.ApplyUpdateAndRestart());
```

- [ ] **Step 3: Add the "Restart to update" button to `UpdateWindow.xaml`**

In the `Buttons` `StackPanel` (the one containing `DownloadCommand` and
`CheckAgainCommand`), add a new button as the first child, so it's the
primary action when present:

```xml
                <!-- Buttons -->
                <StackPanel Orientation="Horizontal"
                            HorizontalAlignment="Center"
                            Margin="0,32,0,0">
                    <Button Style="{StaticResource PrimaryButton}"
                            Content="Reiniciar y actualizar"
                            Padding="24,11"
                            Margin="0,0,10,0"
                            Command="{Binding RestartCommand}"
                            Visibility="{Binding IsRestartReady,
                                Converter={StaticResource BoolToVisibility}}"/>
                    <Button Style="{StaticResource PrimaryButton}"
                            Content="Ver en GitHub"
                            Padding="24,11"
                            Command="{Binding DownloadCommand}"
                            Visibility="{Binding IsUpdateAvailable,
                                Converter={StaticResource BoolToVisibility}}"/>
                    <Button Style="{StaticResource GhostButton}"
                            Content="Comprobar de nuevo"
                            Padding="24,11"
                            Command="{Binding CheckAgainCommand}"
                            Visibility="{Binding IsUpdateAvailable,
                                Converter={StaticResource InverseBoolToVisibility}}"/>
                </StackPanel>
```

- [ ] **Step 4: Verify build and manual UI check**

```bash
dotnet build Hakufu.csproj
dotnet run --project Hakufu.csproj
```

Navigate Home → Actualizaciones. Expected: screen renders as before
(no "Restart to update" button visible, since `IsUpdateReadyToApply`
is `false` outside an installed context — there's nothing to restart
into). No crash, no exception dialog.

- [ ] **Step 5: Commit**

```bash
git add App.xaml.cs MVVM/ViewModel/UpdateViewModel.cs MVVM/View/UpdateWindow.xaml
git commit -m "feat: comprobación de updates en segundo plano + botón reiniciar y actualizar"
```

---

### Task 5: `Build-Release.ps1`

**Files:**
- Delete: `Build-Installer.ps1`
- Create: `Build-Release.ps1`

**Interfaces:**
- Consumes: `.config/dotnet-tools.json` (Task 1, for `dotnet tool run vpk`),
  the bumped `<Version>` in `Hakufu.csproj` (Task 1).
- Produces: `Releases/Setup.exe` + delta/full `.nupkg` packages, used
  manually by the user in Task 6 to actually publish a GitHub release
  (not run automatically by this plan — see Global Constraints).

- [ ] **Step 1: Delete the old Inno Setup build script**

```bash
git rm Build-Installer.ps1
```

- [ ] **Step 2: Create `Build-Release.ps1`**

```powershell
#Requires -Version 5.1
# ============================================================================
#  Build-Release.ps1 — Publica Hakufu y lo empaqueta con Velopack (vpk)
#
#  Uso:
#    .\Build-Release.ps1 -Version "0.9.0"
#
#  Salida:
#    Releases\Setup.exe            ← instalador de un solo exe
#    Releases\Hakufu-<version>-full.nupkg
#    Releases\RELEASES
#
#  Este script NO publica el release en GitHub — eso es un paso manual
#  y deliberado (ver el bloque final que imprime el comando exacto).
# ============================================================================

param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = "Stop"
$root        = $PSScriptRoot
$publishDir  = Join-Path $root "publish"
$releasesDir = Join-Path $root "Releases"

function Write-Step([int]$n, [int]$total, [string]$msg) {
    Write-Host ""
    Write-Host "[$n/$total] $msg" -ForegroundColor Cyan
}

# ── 1. Publicar la app ────────────────────────────────────────────────────
Write-Step 1 3 "Publicando Hakufu (self-contained x64 Release)..."

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

dotnet publish "$root\Hakufu.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $publishDir

if ($LASTEXITCODE -ne 0) { throw "dotnet publish falló (código $LASTEXITCODE)." }

Remove-Item (Join-Path $publishDir "*.pdb") -ErrorAction SilentlyContinue

Write-Host "   Publicación completada." -ForegroundColor Green

# ── 2. Empaquetar con vpk ───────────────────────────────────────────────────
Write-Step 2 3 "Empaquetando con vpk..."

if (Test-Path $releasesDir) { Remove-Item $releasesDir -Recurse -Force }

dotnet tool run vpk pack `
    --packId "Hakufu" `
    --packVersion $Version `
    --packDir $publishDir `
    --mainExe "Hakufu.exe" `
    --icon "$root\HakufuLogo.ico" `
    --outputDir $releasesDir

if ($LASTEXITCODE -ne 0) { throw "vpk pack falló (código $LASTEXITCODE)." }

Write-Host "   Paquete generado en Releases\." -ForegroundColor Green

# ── Resumen ──────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host "  Paquete Velopack listo — Releases\Setup.exe" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Para publicar el release en GitHub (paso manual):"
Write-Host ""
Write-Host "    gh release create v$Version (Get-ChildItem Releases -File).FullName ``" -ForegroundColor Yellow
Write-Host "      --title `"v$Version`" --generate-notes" -ForegroundColor Yellow
Write-Host ""
```

- [ ] **Step 3: Verify the packaging step works locally (no GitHub publish)**

```bash
pwsh -File Build-Release.ps1 -Version 0.9.0
```

Expected: `dotnet publish` succeeds, `vpk pack` succeeds, and
`Releases\Setup.exe` exists afterward:

```bash
ls Releases/Setup.exe
```

Do **not** run the `gh release create` command it prints — that's the
real publish step, left for the user to run deliberately (Task 6).

- [ ] **Step 4: Commit**

```bash
git add Build-Release.ps1
git commit -m "build: Build-Release.ps1 con vpk en lugar de Inno Setup"
```

---

### Task 6: Docs, `.gitignore`, and the manual release cutover

**Files:**
- Modify: `.gitignore`
- Modify: `CLAUDE.md`

**Interfaces:**
- Consumes: nothing new.
- Produces: nothing consumed by other tasks (last task).

- [ ] **Step 1: Ignore the Velopack packaging output**

In `.gitignore`, under the `# Build output` section, add `Releases/`
next to the existing `publish/`/`output/` entries:

```
# Build output
bin/
obj/
publish/
output/
Releases/
```

Leave `.config/dotnet-tools.json` untracked-by-nothing (i.e. don't add
it to `.gitignore`) — it must stay committed so `dotnet tool restore`
works for anyone building the release.

- [ ] **Step 2: Update `CLAUDE.md`'s command list**

Current block in `CLAUDE.md`:

```
## Commands

```bash
dotnet build Hakufu.csproj         # compile
dotnet run --project Hakufu.csproj # launch app
dotnet restore                     # restore NuGet packages
```

Local data is stored at `%LOCALAPPDATA%\Hakufu\data.json`; cover image cache at `%LOCALAPPDATA%\Hakufu\covers\`.
```

Replace it with:

```
## Commands

```bash
dotnet build Hakufu.csproj         # compile
dotnet run --project Hakufu.csproj # launch app
dotnet restore                     # restore NuGet packages
dotnet tool restore                # restore vpk (Velopack CLI) as a local tool
.\Build-Release.ps1 -Version "0.9.0"   # publish + package a Velopack release → Releases\Setup.exe
```

Local data is stored at `%LOCALAPPDATA%\Hakufu\data.json`; cover image cache at `%LOCALAPPDATA%\Hakufu\covers\`.

Packaging/install uses **Velopack** (`Velopack` NuGet package + `vpk` CLI) — a single
`Setup.exe` installs per-user (no admin) to `%LocalAppData%\Hakufu`, and the app
checks/downloads updates from GitHub Releases in the background at startup
(`Services/UpdateService.cs`), applying them on next restart. See
`docs/superpowers/specs/2026-08-16-velopack-installer-design.md` for the full
design.
```

- [ ] **Step 3: Commit**

```bash
git add .gitignore CLAUDE.md
git commit -m "docs: documentar build/release con Velopack"
```

- [ ] **Step 4: Manual, deliberate step — actually publish v0.9.0 (not automated by this plan)**

This step is for you (not to be run unattended as part of implementation):
once every earlier task is done and reviewed, run:

```bash
pwsh -File Build-Release.ps1 -Version 0.9.0
gh release create v0.9.0 (Get-ChildItem Releases -File).FullName `
  --title "v0.9.0" `
  --notes "Última actualización manual: instala este Setup.exe una vez. A partir de aquí, Hakufu se actualiza solo en segundo plano — no hará falta volver a descargar nada a mano."
```

Confirm on github.com/dap0ry/Hakufu/releases that `v0.9.0` has a
`Setup.exe` asset (plus the `.nupkg`/`RELEASES` files Velopack needs for
future delta updates) instead of a raw zip.

---

## Self-Review Notes

- **Spec coverage:** install/first-run (Task 2, 4), background silent
  update + non-intrusive restart affordance (Task 3, 4), Inno
  Setup/Updater removal (Task 1), release/build process (Task 5),
  migration messaging for existing users (Task 6 Step 4's release
  notes), changelog decoupling (Task 3 keeps `FetchLatestReleaseAsync`
  unchanged) — all covered. Code signing, CI/GitHub Actions, and mobile
  are explicitly out of scope per the spec and untouched here.
- **Placeholder scan:** no TBDs; the one area of genuine external
  uncertainty (exact Velopack API member names for the version NuGet
  resolves at implementation time) is called out explicitly in Task 3
  with concrete fallback instructions, not left blank.
- **Type consistency:** `IUpdateService.IsUpdateReadyToApply` (Task 3)
  matches `UpdateViewModel.IsRestartReady`'s source (Task 4) and
  `RestartCommand` → `ApplyUpdateAndRestart()` names match across
  Tasks 3–4. `CheckForUpdatesInBackgroundAsync()` name matches between
  its definition (Task 3) and both call sites (Task 4 Step 1 and
  Step 2's constructor-time read of `IsUpdateReadyToApply`).
