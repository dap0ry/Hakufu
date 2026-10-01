# Hakufu multiplataforma y 100 % offline — diseño

**Fecha:** 01/10/2026 · **Rama:** `feat/avalonia-migration` · **Estado:** aprobado por Dani ("migra entero, no preguntes")

## Objetivo

Que Hakufu se abra **mañana** en Windows, macOS (Apple Silicon e Intel) y Linux desde el mismo código, y que los tres (Dani en Mac, los otros dos en Linux) puedan seguir programando funciones y mejorando el diseño sobre esa base.

Al mismo tiempo, la app pasa a ser **totalmente offline**: sin cuentas, amigos, servidor, Dropbox ni auto-actualización.

## Decisiones

| Tema | Decisión | Por qué |
|---|---|---|
| Framework de UI | **Avalonia 11.3.22** (tema Fluent, fuente Inter) en .NET 10 | Avalonia 12 salió hace poco y hay mucho menos material. La 11.3 es estable y sobradamente conocida |
| Estructura | **Un solo proyecto** `Hakufu.csproj` en la raíz, convertido en el sitio | Menos movimiento de ficheros. El historial y el mapa del `CLAUDE.md` siguen valiendo. Separar un `Hakufu.Core` hoy no aporta nada (YAGNI) |
| MVVM | Se mantienen `BaseViewModel`, `RelayCommand`, `AsyncRelayCommand`, `NavigationService` y `DialogService` | Solo cambia `CommandManager` (solo existe en WPF) por un evento propio |
| Imágenes | VMs y servicios usan `Avalonia.Media.Imaging.Bitmap` en vez de `BitmapSource` | `Bitmap` vive en `Avalonia.Base`, que no depende de ninguna plataforma |
| PDF | Se queda **Docnet.Core 2.6.0**: trae pdfium nativo para `win-x64`, `linux-x64`, `linux-arm64`, `osx-x64` y `osx-arm64` | Los BGRA crudos se convierten a `WriteableBitmap` (Bgra8888, Premul) |
| CBR/CBZ | Se queda SharpCompress | Es C# puro |
| Ventana | **Decoraciones nativas del sistema** (sin barra de título propia). "Maximizar" es el nativo. El modo zen del lector usa `WindowState.FullScreen` | La barra propia de WPF iba con hacks de `WorkArea`. En Linux (X11/Wayland) una barra propia se comporta mal |
| Bandeja del sistema | **Se quita** | La X ya cerraba la app del todo. En Linux la bandeja no es fiable |
| Online | **Se borra**: `HakufuApiClient`, `SessionService`, `DropboxService`, `SyncPayloadBuilder`, `CoverUploadHelper`, `UpdateService`, Velopack, WebView2, y los VM/vistas Account, Friends, PublicProfile y Sync | Pedido explícito |
| Copia de seguridad | `BackupViewModel` se reescribe como **copia local**: exportar e importar un `.zip` con `data.json`, `covers/`, `customization/` y, si se marca la opción (por defecto sí), los mangas de la carpeta `biblioteca/` de Hakufu. Los mangas que están fuera de esa carpeta no se copian. Al importar, las rutas se reescriben para la carpeta de datos del equipo nuevo, así sirve para pasar de Windows a Mac o Linux | Sustituye a Dropbox sin red. Para sincronizar, el usuario deja la carpeta de mangas dentro de Dropbox, iCloud o Drive y lo hace esa app |
| Inicio | La tesela **Amigos** desaparece. La tesela **Cuenta** pasa a ser **Copia de seguridad** (`NavBackupCommand`) y conserva la clave de personalización `"account"` para no perder imágenes ya elegidas | |
| Rutas de datos | Se mantiene `Environment.SpecialFolder.ApplicationData/Hakufu`: `%APPDATA%\Hakufu` en Windows y `~/.config/Hakufu` en Linux/macOS | Los usuarios actuales de Windows conservan su `data.json` |
| Fuente | `Segoe UI` → **Inter** (`Avalonia.Fonts.Inter`, `fonts:Inter`) | Segoe no existe en Mac ni en Linux |
| `web/` (Vercel) | **No se toca** en esta rama | La 0.9.7 instalada todavía llama a `/api/*`. Borrarlo rompe a esos usuarios y a la landing. Se hará en otra tarea cuando nadie use la 0.9.7 |
| `api/` (FastAPI) | **Se borra** | Ya estaba retirado |
| Distribución | `dotnet run` para desarrollar. `scripts/publish.sh` / `scripts/publish.ps1` generan una carpeta self-contained por RID. GitHub Actions compila `win-x64`, `linux-x64`, `osx-arm64` y `osx-x64` en cada push | Sin firma de Apple de momento: en Mac se abre con clic derecho → Abrir |

## Estilos: equivalencias WPF → Avalonia

En Avalonia los estilos con nombre son **clases** (`Classes="primary"`), no `x:Key`. Esta tabla es el contrato común para todos los que porten vistas:

| WPF | Avalonia |
|---|---|
| `Style="{StaticResource PrimaryButton}"` | `Classes="primary"` |
| `GhostButton` | `Classes="ghost"` |
| `IconButton` | `Classes="icon"` |
| `ThemeToggle` (ToggleButton) | `Classes="themeToggle"` |
| `CompactSlider` | `Classes="compact"` |
| `Card` (Border) | `Classes="card"` |
| `TitleText` / `SubtitleText` / `CaptionText` | `Classes="title"` / `"subtitle"` / `"caption"` |
| `NavButton`, `HomeNavItem`, `FavoriteStarButton` | No se portan: ninguna vista los usaba |
| Estilos locales de una vista (`NavTile`, `IconBadge`, `FieldLabel`, `BackButton`…) | Se portan dentro de la propia vista (`UserControl.Styles` con clase) |
| `<Image Source="{Binding X.Path}"/>` (ruta en texto) | `Source="{Binding X.Path, Converter={StaticResource PathToBitmap}}"` |
| Iconos pixel (`Template="{StaticResource IconPixel…}"`) | Igual: siguen siendo `ControlTemplate` de `ContentControl` con `x:Key` |
| `{DynamicResource X}` de colores | Igual (`DynamicResource`). Los brushes viven en `Assets/Themes/*.axaml` |
| `Visibility="{Binding B, Converter={StaticResource BoolToVisibility}}"` | `IsVisible="{Binding B}"`; invertido: `IsVisible="{Binding !B}"`; null: `IsVisible="{Binding X, Converter={x:Static ObjectConverters.IsNotNull}}"` |
| `Triggers` / `DataTrigger` | Selectores de estilo (`:pointerover`, `:pressed`, `:disabled`, `:checked`) o bindings a `Classes.x` |
| `StackPanel` + `Margin` en hijos | Se puede usar `Spacing` (en Avalonia sí existe) |
| `UniformGrid` en `AdaptiveItemsControl` | `ItemsPanel` con `UniformGrid Columns="{Binding …}"` |
| `MouseDown`/`MouseLeftButtonUp` | `PointerPressed`/`PointerReleased`, o un `Button` |
| `Cursor="Hand"` | `Cursor="Hand"` (igual) |

## Fuera de alcance

- Pasar a Avalonia 12, instaladores firmados, auto-actualización offline, carpeta de biblioteca configurable, animación 3D de página. Son tareas HMR posteriores sobre esta base.
- Cambios de diseño: la migración busca el mismo aspecto que la 0.9.7.
