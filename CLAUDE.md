# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

**Hakufu** is an offline manga manager/reader built with **Avalonia 11.3** on **.NET 10**, running on
Windows, macOS, Linux, iPhone and iPad. Built by Daniel Poza and friends. It manages PDF and CBR/CBZ files locally —
**no backend, no accounts, no network access** — the only exception is `Services/UpdateService`, which
checks/downloads updates from GitHub Releases (Velopack; opt-out in Ajustes → Acerca de). Don't add any other
network use: it's a product decision.
Migrated from WPF in October 2026 (spec: `docs/superpowers/specs/2026-10-01-avalonia-offline-design.md`).

## Commands

```bash
dotnet build Hakufu.csproj            # compile
dotnet run --project Hakufu.csproj    # launch app
dotnet test tests/Hakufu.Tests        # tests (xUnit + Avalonia.Headless, real Skia)
./scripts/publish.sh [rid]            # macOS/Linux self-contained publish (.app on macOS)
.\scripts\publish.ps1 [-Rid win-x64]  # Windows self-contained publish + zip
./scripts/pack-velopack.sh <rid>     # after publish.sh: Velopack installer + update feed (publish/velopack)
.\scripts\pack-velopack.ps1          # Windows: Setup.exe + feed, channel "win" (same packId/channel as 0.9.7)
bash scripts/package-ipa.sh          # macOS + `dotnet workload install ios`: unsigned publish/Hakufu-ios.ipa
```

### iOS (iPhone/iPad)

Same project, opt-in target: `-p:HakufuIos=true` switches `TargetFramework` to `net10.0-ios` (needs macOS,
Xcode and the `ios` workload — CI job `ios` does it; it can't build on Linux/Windows). Without that property
nothing changes for desktop. Spec: `docs/superpowers/specs/2026-10-08-ios-design.md`.
- iOS-only code lives in `Platforms/iOS/` (excluded from desktop); `Program.cs` and `Services/PdfDocument.Docnet.cs`
  are excluded from iOS. PDF goes through `PdfDocument` (pdfium/Docnet on desktop, CoreGraphics on iOS).
- `Platforms/iOS/IosPlatform.Configure()` sets `AppPlatform` (IsMobile, OpenFolder → Files app, OpenUrl) and
  `AppPaths` (`FixedLibraryRoot` = Documents, shown in Files as "On My iPhone → Hakufu"; `DataDirOverride` =
  Library/Application Support/Hakufu). Use `AppPlatform.IsMobile` for things that don't apply (Quit, pick folder).
- The library folder can't be changed on iOS; saves (backup .zip, profile PNG) go to Documents; picked files are
  copied to temp first. `ContainerPaths.Rebase` fixes stored paths when iOS moves the app container.
- `HAKUFU_START_SCREEN` (library/collection/reader/settings/profile) opens a screen after the first scan: only
  for CI screenshots in the simulator (`scripts/ios-simulator-shots.sh`).
- Distribution: unsigned .ipa → sideloaded with a free Apple ID (7-day signature). No App Store/TestFlight.

Releases: `git tag vX.Y.Z && git push origin vX.Y.Z` (must match `<Version>`); CI uploads portable zips + Velopack
installers/feeds; a tag with `-` is a pre-release. Velopack: packId `Hakufu`, channels `win`, `osx-arm64`, `osx-x64`,
`linux`; NuGet `Velopack` and the `vpk` tool must be the same version. macOS needs `--signAppIdentity "-"` (ad hoc).

Data folder: `AppPaths.DataDir` = `Environment.SpecialFolder.ApplicationData/Hakufu`
(`%APPDATA%\Hakufu` on Windows, `~/.config/Hakufu` on macOS/Linux), overridable with the
`HAKUFU_DATA_DIR` env var (tests rely on this). Never hardcode paths — use `Data/AppPaths.cs`.

Library folder: the manga files are **not** in the data folder. The user picks a folder
(`AppDataStore.LibraryRoot`, Ajustes → "Carpeta de la biblioteca"): each direct subfolder is a
collection and its `.cbz/.cbr/.pdf` files are the volumes (files in the root go to "Sin colección").
Hakufu only **reads** it — never copy, move or delete user files. `<datos>/biblioteca/` is where old
versions copied mangas; it is only used as the default root when `LibraryRoot` is empty, so old
installs keep their progress.

## Architecture

Strict **MVVM** with manual dependency injection in `CompositionRoot.cs` (used by `App.axaml.cs`
and by the view smoke tests).

```
Program.cs / App.axaml(.cs)  ← Avalonia bootstrap; loads data.json, builds CompositionRoot, shows MainWindow
CompositionRoot.cs           ← creates all services + the ViewModel factory for NavigationService
ViewLocator.cs               ← XxxViewModel → XxxView by naming convention (no registration needed)
MainView.axaml(.cs)          ← everything inside the window: page ContentControl, update bar, modal overlay,
                               reader lifecycle (ZenModeChanged/ReaderClosed); classes "compact" (< 700 px, iPhone)
                               and "narrow" (< 900 px = desktop MinWidth, iPad portrait). On iOS it's the single view
MainWindow.axaml(.cs)        ← desktop shell around MainView; zen mode = WindowState.FullScreen;
                               on Linux its own title bar (OwnTitleBar: minimize / full screen / close);
                               theme changes play the ink transition (ThemeService.Transition → PlayThemeTransition)
Controls/InkTransition       ← InkShadow-style ink wipe between themes: SkSL shader on OpenGL, Skia paths
                               elsewhere (SkiaSharp 2.88 aborts with SIGILL drawing SkSL on the CPU)
MVVM/
  Model/                     ← plain data classes (Manga, Collection, ReadingProgress, …)
  ViewModel/                 ← BaseViewModel, RelayCommand, AsyncRelayCommand, CommandRequery + all VMs
  View/                      ← UserControls (.axaml), one per ViewModel
Data/
  AppPaths                   ← all folder locations
  IDataRepository / JsonDataRepository  ← load/save AppDataStore to data.json
Services/
  NavigationService          ← ContentControl dispatch via Func<Type, object?, BaseViewModel>
  ThemeService               ← swaps Application.Resources.MergedDictionaries[0] + ThemeVariant (via Transition if set)
  DialogService              ← modal overlay callbacks wired into MainWindowViewModel
  LibraryService / ProfileService  ← collections, mangas, favorites, history
  LibraryScanner             ← syncs data.json with the library folder (async; keeps Ids/progress by RelativePath)
  CoverService / PageLoaderService ← PDF (Docnet/pdfium) + CBR/CBZ (SharpCompress) → Avalonia Bitmap
  BackupService              ← local .zip export/import (data.json + covers + profile, no mangas); rebases paths, keeps the local LibraryRoot
  FilePickerService          ← Avalonia StorageProvider (files, folder, save) + OpenFolder (explorer/open/xdg-open)
  ProfileService             ← profile (name, photo in DataDir/profile), reading log per day (ReadingLog) for the profile charts
  UpdateService              ← the ONLY network code: Velopack UpdateManager + GithubSource(dap0ry/Hakufu); not installed
                               (zip / dotnet run) → asks the GitHub API for releases/latest and only notifies
Assets/
  Themes/LightTheme.axaml, DarkTheme.axaml  ← all brushes; always use DynamicResource
  Styles/GlobalStyles.axaml   ← shared styles as CLASSES (Classes="primary", "ghost", "icon", "card", "caption"…)
  Styles/PixelIcons.axaml     ← pixel-art icons as ContentControl templates (Template="{StaticResource IconPixelStar}")
Converters/                  ← PathToBitmap, PathAndOpacityToImageBrush, Equality, PercentageWidth, BoolToOpacity
```

### Navigation

`NavigationService` holds a factory defined in `CompositionRoot`. `NavigateTo<T>()` / `NavigateTo<T>(param)`
creates the VM and sets `CurrentViewModel`; `MainWindowViewModel` propagates it to `CurrentView`, and
`ViewLocator` picks the view. A new screen = new `XxxViewModel` + `MVVM/View/XxxView.axaml` + one line in
the factory.

### Commands

WPF's `CommandManager` doesn't exist in Avalonia. `CommandRequery` replaces it: every
`BaseViewModel.OnPropertyChanged` and every click/key in `MainWindow` re-evaluates `CanExecute` of all
`RelayCommand`/`AsyncRelayCommand` (weak subscriptions, so commands created per-getter don't leak).

### Views

- Compiled bindings are on by default: every view and `DataTemplate` declares `x:DataType`.
- Styles are classes, not keys (see the WPF→Avalonia table in the spec). No global TextBlock color:
  text inherits `Foreground` from `MainWindow`.
- Colors: always `{DynamicResource Key}` so theme switching works.
- Images from a stored path need `Converter={StaticResource PathToBitmap}`.

### PDF / CBR rendering

`Docnet.Core` ships native pdfium for win/linux/osx (x64 + arm64); calls are serialized (pdfium is not
thread-safe). CBR/CBZ via SharpCompress, pages ordered by entry name (ordinal, case-insensitive).
`PageLoaderService` keeps a sliding window of `[current−1 .. current+2]` decoded pages. A missing or
corrupt file opens with 0 pages instead of throwing.

### Tests

`tests/Hakufu.Tests`: service tests plus view smoke tests. `ViewSmoke.Start()` boots the real `App` +
`MainWindow` headless with a sample library; `AssertShows<TView>()` checks that a screen actually
renders. Add one for every new view. `ViewSmoke.StartMobile(w, h)` hosts `MainView` alone like iOS
(with `AppPlatform.IsMobile`); `MobileLayoutTests` walks every screen at iPhone/iPad sizes, saves screenshots to
`bin/…/mobile-shots/` and fails if a button or text sticks out sideways.

### Phone/tablet layouts

Views adapt with styles under `.compact …` / `.narrow …` (classes on `MainView`). A style can't override a value
set directly in XAML (local value wins), so anything that changes per size (Grid.Column/Span, Margin, Width,
FontSize…) goes in a named style instead of an attribute. Shared page header: `Border.pageHeader`,
`StackPanel.pageTitle`, `TextBlock.pageTitleText` (GlobalStyles); on iPhone the title drops to a second row.

## Languages (i18n)

The UI is in Spanish or English (Ajustes → Idioma; first run follows the system, existing installs stay Spanish).
Never write visible text by hand:
- Views: `xmlns:i18n="using:Hakufu.I18n"` and `Text="{i18n:T area.key}"` (updates live on language change).
- Code: `L.Get("area.key")`, `L.Format("area.key", n)` and `L.Culture` for numbers/dates (`using Hakufu.I18n;`).
- Strings live in `Assets/i18n/<area>.es.json` + `<area>.en.json`, keys prefixed with the area.
  They ship as Content files in `<app>/i18n/` (read with plain File IO, so they work before Avalonia starts).
  Tests check both files have the same keys and placeholders, and that translated views have no hand-written text.
- Changing language calls `NavigationService.Reload()` so ViewModel-computed texts are rebuilt.

## Conventions

- Every change is tied to a Jira **HMR** ticket: branches `feat/HMR-12-…`, commits `HMR-12 …`.
- UI text is Spanish.
