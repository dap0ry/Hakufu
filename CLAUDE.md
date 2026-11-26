# CLAUDE.md

**Hakufu** — offline manga manager, WPF / .NET 10 / Windows. No backend.

## Commands
```bash
dotnet build Hakufu.csproj
dotnet run --project Hakufu.csproj
dotnet restore
```
Data: `%LOCALAPPDATA%\Hakufu\data.json` · Covers: `%LOCALAPPDATA%\Hakufu\covers\`

## Architecture
Strict MVVM, manual DI composed in `App.xaml.cs`.

```
App.xaml.cs          ← composition root
MainWindow.xaml/.cs  ← shell: sidebar + ContentControl + modal overlay
MVVM/Model/          ← plain data classes
MVVM/ViewModel/      ← BaseViewModel, RelayCommand, AsyncRelayCommand, all VMs
MVVM/View/           ← UserControls (1:1 with VMs), Controls/AdaptiveItemsControl
Data/                ← IDataRepository / JsonDataRepository → AppDataStore (JSON)
Services/            ← Navigation, Theme, Dialog, Library, Profile, Cover, PageLoader, FilePicker
Assets/Themes/       ← LightTheme.xaml, DarkTheme.xaml (all brushes)
Assets/Styles/       ← GlobalStyles.xaml, NavButton.xaml
Converters/          ← BoolToVisibility, NullToVisibility, Equality, PercentageWidth
```

## Key constraints (WPF gotchas)
- `StackPanel.Spacing` **does not exist** — use `Margin` on children.
- `LetterSpacing` **does not exist** — use `Typography` or omit.
- Always `DynamicResource`, never `StaticResource` for colors/brushes.
- Always call `BitmapSource.Freeze()` before returning from a background thread.

## Patterns
- **Navigation**: `NavigationService.NavigateTo<TViewModel>(object? param)` — factory in `App.xaml.cs` casts param; `App.xaml` has `DataTemplate` per VM → auto-dispatch via `ContentControl`.
- **Modal**: `IDialogService.ShowModal(vm)` — overlay is a `Grid` at `ZIndex=100` in `MainWindow.xaml`, `Visibility` bound to `IsModalOpen`.
- **Theme**: `ThemeService.SetTheme()` swaps `MergedDictionaries[0]`; persisted in `AppDataStore.ActiveTheme`.
- **PDF**: Docnet.Core (pdfium). **CBR/CBZ**: `ZipArchive` (they're ZIP). `PageLoaderService` keeps sliding window `[current−1 .. current+2]`.
