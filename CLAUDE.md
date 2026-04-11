# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

**Hakufu** is an offline manga manager WPF desktop application targeting **.NET 10 on Windows**, built by Daniel Poza.
It manages PDF and CBR/CBZ manga files locally — no backend, no internet required.

## Commands

```bash
dotnet build Hakufu.csproj         # compile
dotnet run --project Hakufu.csproj # launch app
dotnet restore                     # restore NuGet packages
```

Local data is stored at `%LOCALAPPDATA%\Hakufu\data.json`; cover image cache at `%LOCALAPPDATA%\Hakufu\covers\`.

## Architecture

The project follows strict **MVVM** with manual dependency injection composed in `App.xaml.cs`.

```
App.xaml.cs                  ← composition root; creates all services + MainWindow
MainWindow.xaml/.cs          ← shell: sidebar nav + ContentControl + modal overlay
MVVM/
  Model/                     ← plain data classes (Manga, Collection, ReadingProgress, …)
  ViewModel/                 ← BaseViewModel, RelayCommand, AsyncRelayCommand + all VMs
  View/                      ← UserControls (one per ViewModel), Controls/AdaptiveItemsControl
Data/
  IDataRepository / JsonDataRepository  ← load/save AppDataStore to JSON
Services/
  NavigationService          ← ContentControl dispatch via Func<Type, object?, BaseViewModel>
  ThemeService               ← swaps MergedDictionaries[0] at runtime
  DialogService              ← modal overlay callbacks wired into MainWindowViewModel
  LibraryService             ← collection + manga CRUD on top of IDataRepository
  ProfileService             ← favorites + reading history
  CoverService               ← PDF/CBR first-page extraction via Docnet.Core / ZipArchive
  PageLoaderService          ← per-session page loader with sliding-window memory cache
  FilePickerService          ← wraps OpenFileDialog
Assets/
  Themes/LightTheme.xaml, DarkTheme.xaml   ← all brushes; DynamicResource used everywhere
  Styles/GlobalStyles.xaml, NavButton.xaml  ← shared control templates + styles
Converters/                  ← BoolToVisibility, NullToVisibility, Equality, PercentageWidth
```

### Navigation

`NavigationService` holds a factory `Func<Type, object?, BaseViewModel>` defined in `App.xaml.cs`. Calling `NavigateTo<T>()` or `NavigateTo<T>(param)` creates the VM and sets `CurrentViewModel`. `MainWindowViewModel` propagates this to `CurrentView`. `App.xaml` contains `DataTemplate` entries mapping each ViewModel type to its View — `ContentControl` dispatches automatically.

### Modal overlay

`DialogService.Register(show, close)` is called once by `MainWindowViewModel` to wire up overlay callbacks. Any service or VM can call `IDialogService.ShowModal(vm)` to display a centered card over a dimmed background. The overlay is a `Grid` with `Panel.ZIndex=100` in `MainWindow.xaml`, `Visibility` bound to `IsModalOpen`.

### Theming

All color references in every XAML file use `DynamicResource` (never `StaticResource`). `ThemeService.SetTheme()` replaces `MergedDictionaries[0]` with the new dictionary. The active theme name is persisted in `AppDataStore.ActiveTheme` and reapplied on startup.

### PDF / CBR rendering

`CoverService` and `PageLoaderService` use **Docnet.Core** (wraps native pdfium) for PDF pages and `System.IO.Compression.ZipArchive` for CBR/CBZ files (which are ZIP archives of images). `BitmapSource.Freeze()` is always called before returning from a background thread. `PageLoaderService` keeps a sliding window of `[current−1 .. current+2]` decoded pages in memory and disposes the rest.

### Key constraints

- `StackPanel.Spacing` is **not available in WPF** — use `Margin` on child elements instead.
- `LetterSpacing` is **not a WPF property** — use `Typography` or omit it.
- Parameterized navigation (e.g., opening a collection or reader) uses `NavigateTo<T>(object param)` where the factory in `App.xaml.cs` casts `param` to the expected type.
