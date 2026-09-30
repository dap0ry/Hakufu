# Hakufu Avalonia + offline — plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Portar la app WPF a Avalonia 11.3 para que compile y funcione en Windows, macOS y Linux, borrando todo lo online.

**Architecture:** Se convierte el propio `Hakufu.csproj` a Avalonia. Los ViewModels y servicios se conservan y cambian `BitmapSource` por `Avalonia.Media.Imaging.Bitmap`. Las vistas se reescriben como `.axaml` con compiled bindings (`x:DataType`) y un `ViewLocator` por convención (`XxxViewModel` → `XxxView`), así cada vista es un fichero independiente sin tocar `App.axaml`.

**Tech Stack:** .NET 10, Avalonia 11.3.22 (Fluent + Inter), Docnet.Core 2.6.0, SharpCompress 0.47.4, xUnit.

**Spec:** `docs/superpowers/specs/2026-10-01-avalonia-offline-design.md`

## Global Constraints

- `TargetFramework` = `net10.0`. Nada de `-windows`, `UseWPF` ni `UseWindowsForms`.
- Avalonia y todos sus paquetes en **11.3.22** exactamente.
- `AvaloniaUseCompiledBindingsByDefault=true`: toda vista declara `x:DataType="vm:XxxViewModel"`.
- Ningún fichero `using System.Windows` (salvo `System.Windows.Input.ICommand`, que es multiplataforma).
- Ningún `HttpClient` ni URL de red en la app de escritorio.
- Colores siempre con `{DynamicResource Clave}`, con las mismas claves que hoy en `Assets/Themes/*.axaml`.
- Clases de estilo según la tabla del spec (`primary`, `ghost`, `icon`, `themeToggle`, `compact`, `card`, `title`, `subtitle`, `caption`).
- Textos de la interfaz en español, iguales a los actuales.
- Referencia del WPF original: `git show main:<ruta>` (p. ej. `git show main:MVVM/View/HomeView.xaml`).

## Review Focus

1. **Biblioteca vieja de Windows**: un `data.json` de la 0.9.7 con claves `friends`/`account` en personalización y portadas en `%APPDATA%\Hakufu\covers` tiene que cargar sin perder nada → test `JsonDataRepository` con un JSON de la 0.9.7.
2. **Ficheros que ya no existen** (manga movido o borrado): la portada y el lector no deben tirar la app → `CoverService`/`PageLoaderService` devuelven `null` o 0 páginas. Hay tests para ambos.
3. **Importar un backup corrupto o de otra cosa**: un `.zip` sin `data.json` no debe machacar la biblioteca actual → test en `BackupService`.
4. **CBZ con imágenes en subcarpetas y nombres desordenados** (`10.jpg` antes de `2.jpg`): se mantiene el orden ordinal actual (no cambiar el comportamiento) → test de `PageLoaderService`.
5. **Rutas con espacios y acentos** en macOS y Linux (`~/Mangas/Ataque a los Titanes/Tomo 1.cbz`) → los tests usan rutas temporales con espacios y tildes.

---

### Task 1: Base Avalonia (orquestador, secuencial)

**Files:**
- Modify: `Hakufu.csproj`. Create: `Program.cs`, `App.axaml`, `App.axaml.cs`, `ViewLocator.cs`, `MainWindow.axaml(.cs)`, `Assets/Themes/{Light,Dark}Theme.axaml`, `Assets/Styles/{GlobalStyles,NavButton,PixelIcons}.axaml`.
- Modify: `Converters/*`, `Services/{CoverService,PageLoaderService,ThemeService,WallpaperService,FilePickerService,ICoverService,IPageLoaderService,IFilePickerService}.cs`, `MVVM/ViewModel/*` (tipos de imagen, `RelayCommand`, llamadas async al selector de ficheros).
- Delete: `App.xaml*`, `MainWindow.xaml*`, `AssemblyInfo.cs`, `Assets/**/*.xaml`, `MVVM/View/*.xaml*`, `Services/{HakufuApiClient,SessionService,ISessionService,DropboxService,IDropboxService,SyncPayloadBuilder,CoverUploadHelper,UpdateService,IUpdateService,TrayIconService,ITrayIconService}.cs`, VMs `Account`, `Friends`, `FriendItem`, `FriendRequestItem`, `PublicProfile`, `PublicHistoryItem`, `Sync`, `api/`, `Build-Release.ps1`, `dotnet-tools.json`, `Properties/PublishProfiles`.
- Create: `Services/{IBackupService,BackupService}.cs`, `tests/Hakufu.Tests/*`.

**Interfaces (producidas, que usan las Tasks 2–5):**
- `ICoverService.GetCoverAsync(Manga) : Task<Bitmap?>`
- `IPageLoaderService.LoadPageAsync(int) : Task<Bitmap?>`
- `IFilePickerService.PickFilesAsync(string title, FileFilter filter, bool multiSelect = true) : Task<string[]>`, `SaveFileAsync(string title, string suggestedName, FileFilter filter) : Task<string?>`, `OpenFolder(string path)` (`FileFilter.Mangas` / `.Images` / `.Backup`)
- `IBackupService.ExportAsync(string zipPath, bool includeLibraryFiles, IProgress<double>?) : Task`, `ImportAsync(string zipPath, IProgress<double>?) : Task<bool>` (false = zip no válido, no toca nada). `BackupViewModel` ya está escrito en la Task 1 (la Task 5 solo hace su vista)
- Converters en `App.axaml`: `PathToBitmap` (ruta → Bitmap para `Image.Source`), `PathAndOpacityToImageBrush` (multi), `BoolToOpacity`, `EqualityConverter` (multi), `PercentageWidth` (multi)
- `HomeViewModel.NavBackupCommand` (sustituye a `NavAccountCommand`/`NavFriendsCommand`)
- `ReaderViewModel.ZenModeChanged` sin cambios. `MainWindow` responde con `WindowState.FullScreen`.
- `ViewLocator`: `Hakufu.MVVM.ViewModel.XxxViewModel` → `Hakufu.MVVM.View.XxxView`. Una vista que no existe muestra un `TextBlock` "Vista pendiente: Xxx" (así la app arranca aunque falten vistas).

- [ ] Paso 1: csproj Avalonia, borrar lo online, `dotnet build` en verde (las vistas pendientes se ven con el placeholder).
- [ ] Paso 2: tests (`tests/Hakufu.Tests`): repositorio con JSON de la 0.9.7, `PageLoaderService` con CBZ temporal (orden, fichero inexistente), `BackupService` export/import y zip inválido. `dotnet test` en verde.
- [ ] Paso 3: commit `HMR avalonia: base multiplataforma y fuera todo lo online`.

### Tasks 2–5: vistas (subagentes en paralelo, un worktree cada uno)

Cada subagente **solo** crea o modifica los ficheros de su lista, más sus propios ViewModels si un binding lo exige. Para cada vista:
1. Leer el original con `git show main:MVVM/View/X.xaml` y `…xaml.cs`.
2. Crear `MVVM/View/X.axaml` + `X.axaml.cs` (`partial class X : UserControl`) con `x:DataType`, aplicando la tabla de equivalencias del spec.
3. `dotnet build` sin errores ni warnings nuevos de binding (AVLN).
4. Commit por vista: `HMR avalonia: porta XView`.

| Task | Vistas |
|---|---|
| 2 | `HomeView`, `HelpView`, `SettingsView`, `CustomizationView` (Inicio sin Amigos; la tesela Cuenta pasa a "Copia de seguridad" → `NavBackupCommand`) |
| 3 | `LibraryView`, `CollectionDetailView`, `CreateCollectionView`, `MangaPickerView`, `ConfirmDeleteView`, `ReorderMangaView` + `Controls/AdaptiveItemsControl` |
| 4 | `ReaderView` (teclado ← →, zen, zoom si lo había), `ProfileView` |
| 5 | `StorageManagerView`, nueva `BackupView` + `BackupViewModel` reescrito sobre `IBackupService` (botones Exportar/Importar con `IFilePickerService`, estado y error en texto) |

### Task 6: Integración (orquestador)

- [ ] Merge de las 4 ramas, `dotnet build` y `dotnet test`, arrancar la app y recorrer todas las pantallas.
- [ ] `scripts/publish.sh` + `scripts/publish.ps1` (self-contained, una carpeta por RID; en macOS genera `Hakufu.app`).
- [ ] `.github/workflows/build.yml`: matriz `windows-latest`/`ubuntu-latest`/`macos-latest` → build, test y publish de artefactos.
- [ ] README y CLAUDE.md: nueva arquitectura, cómo arrancar en Mac/Linux y dependencias de Linux (`libfontconfig1`, `libice6`, `libsm6`).
- [ ] Revisión final de toda la rama y push de `feat/avalonia-migration`.
