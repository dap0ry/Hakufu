# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

**Hakufu** is an offline manga manager/reader built with **Avalonia 11.3** on **.NET 10**, running on
Windows, macOS and Linux. Built by Daniel Poza and friends. It manages PDF and CBR/CBZ files locally —
**no backend, no accounts, no network access at all** (don't add any: it's a product decision).
Migrated from WPF in October 2026 (spec: `docs/superpowers/specs/2026-10-01-avalonia-offline-design.md`).

## Commands

```bash
dotnet build Hakufu.csproj            # compile
dotnet run --project Hakufu.csproj    # launch app
dotnet test tests/Hakufu.Tests        # tests (xUnit + Avalonia.Headless, real Skia)
./scripts/publish.sh [rid]            # macOS/Linux self-contained publish (.app on macOS)
.\scripts\publish.ps1 [-Rid win-x64]  # Windows self-contained publish + zip
```

Data folder: `AppPaths.DataDir` = `Environment.SpecialFolder.ApplicationData/Hakufu`
(`%APPDATA%\Hakufu` on Windows, `~/.config/Hakufu` on macOS/Linux), overridable with the
`HAKUFU_DATA_DIR` env var (tests rely on this). Never hardcode paths — use `Data/AppPaths.cs`.

## Architecture

Strict **MVVM** with manual dependency injection in `CompositionRoot.cs` (used by `App.axaml.cs`
and by the view smoke tests).

```
Program.cs / App.axaml(.cs)  ← Avalonia bootstrap; loads data.json, builds CompositionRoot, shows MainWindow
CompositionRoot.cs           ← creates all services + the ViewModel factory for NavigationService
ViewLocator.cs               ← XxxViewModel → XxxView by naming convention (no registration needed)
MainWindow.axaml(.cs)        ← shell: ContentControl + modal overlay; zen mode = WindowState.FullScreen
MVVM/
  Model/                     ← plain data classes (Manga, Collection, ReadingProgress, …)
  ViewModel/                 ← BaseViewModel, RelayCommand, AsyncRelayCommand, CommandRequery + all VMs
  View/                      ← UserControls (.axaml), one per ViewModel
Data/
  AppPaths                   ← all folder locations
  IDataRepository / JsonDataRepository  ← load/save AppDataStore to data.json
Services/
  NavigationService          ← ContentControl dispatch via Func<Type, object?, BaseViewModel>
  ThemeService               ← swaps Application.Resources.MergedDictionaries[0] + ThemeVariant
  DialogService              ← modal overlay callbacks wired into MainWindowViewModel
  LibraryService / ProfileService  ← collections, mangas, favorites, history
  CoverService / PageLoaderService ← PDF (Docnet/pdfium) + CBR/CBZ (SharpCompress) → Avalonia Bitmap
  BackupService              ← local .zip export/import; rebases stored paths to the new data folder
  FilePickerService          ← Avalonia StorageProvider (async) + OpenFolder (explorer/open/xdg-open)
  ProfileService             ← profile (name, photo in DataDir/profile), reading log per day (ReadingLog) for the profile charts
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
renders. Add one for every new view.

## Conventions

- Every change is tied to a Jira **HMR** ticket: branches `feat/HMR-12-…`, commits `HMR-12 …`.
- UI text is Spanish.
