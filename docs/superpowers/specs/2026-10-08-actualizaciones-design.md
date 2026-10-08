# Actualizaciones automáticas — diseño

Fecha: 2026-10-08 · Rama: `feat/actualizaciones` · Jira: HMR

## Objetivo

Que actualizar Hakufu sea un clic en Windows, macOS y Linux: la app avisa de que hay versión
nueva, la descarga, se reinicia y listo. Hoy hay que volver a la web, bajar el `.zip`,
descomprimir y sustituir la app a mano (y en macOS vuelve a salir el aviso de Gatekeeper).

## Decisiones tomadas (Dani, 08/10/2026)

- **La app puede usar la red SOLO para actualizarse**, con aviso y desactivable. Es la única
  excepción a «nada de red» (`CLAUDE.md` se actualiza para dejarlo claro).
- **Velopack** en los 3 sistemas (no actualizador propio, no solo avisar).
- Comprueba al abrir y **pregunta** antes de descargar (no actualiza a escondidas).
- **Sin** cuenta de Apple Developer de momento: firma ad hoc como ahora.

## Experiencia de usuario

**Primera instalación (web):**

| Sistema | Archivo | Qué pasa |
|---|---|---|
| Windows | `Hakufu-win-Setup.exe` | Doble clic; se instala en `%LocalAppData%\Hakufu` sin admin, con acceso directo. |
| macOS | `Hakufu-osx-arm64.pkg` / `-osx-x64.pkg` | Instala en Aplicaciones. La primera vez: Ajustes → Privacidad y seguridad → «Abrir igualmente». |
| Linux | `Hakufu-linux-x64.AppImage` | `chmod +x` (o propiedades → ejecutable) y doble clic. |

Los `.zip`/`.tar.gz` actuales se siguen publicando con los mismos nombres (la web los usa) como
**versión portable**, que no se actualiza sola.

**Aviso de versión nueva:** unos segundos después de abrir, en segundo plano. Si hay versión
nueva, barra discreta abajo en `MainWindow`: «Hakufu X.Y.Z disponible · Actualizar · Más tarde».
Actualizar → progreso → «Reiniciar para terminar» (reinicia y aplica). «Más tarde» la oculta
hasta el próximo arranque. Sin conexión o con cualquier error: no se muestra nada.

**Ajustes → Acerca de:** versión actual, botón «Buscar actualizaciones» (con resultado: «Ya
tienes la última», «Hay versión nueva…» o «No se pudo comprobar») y casilla «Buscar
actualizaciones al abrir» (activada por defecto).

**Copia portable (.zip) o ejecución con `dotnet run`:** Velopack no puede aplicar la
actualización (`IsInstalled == false`). En ese caso la comprobación se hace igual contra la
API de GitHub y el aviso dice «Hay versión nueva · Descargar», que abre
`https://hakufu.vercel.app/#descargas`. Así quien tiene la 0.10.x en `.zip` se entera una vez y
se pasa al instalador.

## Arquitectura

```
Program.Main          → VelopackApp.Build().Run() lo primero (hooks de instalación/actualización)
Services/
  IUpdateService      → CheckAsync(), DownloadAsync(progress), ApplyAndRestart(), CanSelfUpdate
  UpdateService       → única pieza con red. Velopack UpdateManager + GithubSource("https://github.com/dap0ry/Hakufu")
                        Si !CanSelfUpdate: consulta releases/latest de la API de GitHub y compara versiones.
                        Captura todas las excepciones → resultado "sin novedades / error", nunca rompe la app.
MVVM/ViewModel/
  UpdateBannerViewModel → estados: Oculto, Disponible, Descargando(%), Listo, (Portable: Descargar)
  SettingsViewModel     → botón y casilla de Acerca de
MainWindow.axaml      → la barra, enlazada a UpdateBannerViewModel
AppDataStore.Updates  → { CheckOnStartup: bool = true }
```

- `UpdateService` se crea en `CompositionRoot` y se inyecta; los tests usan un falso
  (`IUpdateService`), así ningún test toca la red.
- Para pruebas reales sin publicar nada, `HAKUFU_UPDATE_SOURCE=<carpeta>` hace que
  `UpdateService` use una carpeta local de releases (`SimpleFileSource`) en vez de GitHub.
- Actualizar no toca `data.json`, la biblioteca ni nada de `<datos>`: Velopack solo sustituye
  los archivos de la app.

## Canales y compatibilidad con la 0.9.7

- `packId` = **`Hakufu`** (el mismo que la 0.9.7).
- Canales: `win` (el de la 0.9.7 → `releases.win.json`), `osx-arm64`, `osx-x64`, `linux`.
  Cada paquete recuerda su canal, así un Mac Intel nunca recibe el paquete de Apple Silicon.
- La 0.9.7 instalada (WPF) busca en la última release de `dap0ry/Hakufu`. En cuanto una release
  traiga `releases.win.json`, se actualizará sola a la versión Avalonia. **Hay que verificarlo en
  el ThinkPad** (0.9.7 real → beta): el ejecutable principal debe seguir siendo `Hakufu.exe`.

## Publicación (CI)

`git tag vX.Y.Z && git push origin vX.Y.Z` sigue siendo todo lo que hay que hacer.
`.github/workflows/build.yml`, en cada fila de la matriz, después del publish actual:

1. `dotnet tool install -g vpk` (versión fijada, la misma que el paquete NuGet `Velopack`).
2. `vpk pack --packId Hakufu --packVersion <Version> --packDir <salida del publish> --mainExe Hakufu(.exe) --channel <canal> --icon …`
   - macOS: se ejecuta en el runner de Mac (genera `.pkg` y el `.app`); sin `--signAppIdentity`.
     Comprobar que el `.app` sale con firma ad hoc (obligatoria en arm64); si no, `codesign -s -` antes de empaquetar.
   - Linux: genera el `.AppImage` (`--icon HakufuLogo.png`, `--categories Graphics`).
   - Windows: genera `Setup.exe`, `Portable.zip`, `.nupkg` y `releases.win.json`.
3. Se suben los archivos de `Releases/` como artefacto y el job de release los añade a la
   GitHub Release junto a los `.zip` de siempre.

Las versiones de prueba se publican como **pre-release** (`v0.11.0-beta.1`): `GithubSource` se
crea con `prerelease: false`, así los usuarios normales no las ven.

## Web

En `web/index.html`, los botones principales de cada sistema pasan a los instaladores
(`Setup.exe`, `.pkg` por chip, `.AppImage`); el `.zip` queda como enlace secundario «Versión
portable (no se actualiza sola)». Las guías de instalación paso a paso se actualizan a los
instaladores. La lógica que detecta el sistema y lee `releases/latest` se mantiene.

## Errores y casos raros

- Sin internet, GitHub caído o límite de la API: silencio al arrancar; mensaje corto solo si
  el usuario pulsó «Buscar actualizaciones».
- Descarga cortada: la barra vuelve a «Disponible» con «Reintentar».
- App instalada en una carpeta sin permisos (Linux en `/opt`, Mac en `/Applications` de otro
  usuario): Velopack pide elevación (pkexec / AppleScript); si se cancela, se queda la versión actual.
- La comprobación nunca bloquea el arranque ni el lector (todo asíncrono, con timeout de 10 s).

## Pruebas

- **Tests (xUnit + Avalonia.Headless):** `UpdateBannerViewModel` con un `IUpdateService` falso
  (sin novedades, hay versión, error, descarga con progreso, modo portable); la casilla
  `CheckOnStartup` desactiva la comprobación; comparación de versiones (incluidas `-beta`).
  Ningún test usa la red.
- **Prueba real en Mac (yo):** empaquetar 0.11.0-beta.1 y 0.11.0-beta.2 en una carpeta local,
  instalar la 1 con el `.pkg`, abrir con `HAKUFU_UPDATE_SOURCE` → aparece el aviso → actualiza →
  arranca la 2 sin aviso de Gatekeeper.
- **Prueba de punta a punta con GitHub:** pre-release `v0.11.0-beta.1` y `-beta.2` (build de prueba
  con `prerelease: true`). Windows y Linux en el ThinkPad / quien diga Dani; 0.9.7 → beta en Windows.

## Verificado

- **Mac arm64, 08/10/2026:** beta.1 (zip portable de Velopack) con `HAKUFU_UPDATE_SOURCE` → barra
  «0.11.0-beta.2 disponible» → Actualizar → Reiniciar → Velopack sustituye el `.app` y lo reabre
  (`open -n`, hereda el entorno) como beta.2, que ya no ve nada más nuevo. Datos reales intactos.
  Ojo: tras actualizar, `codesign --verify --deep --strict` falla en los `.dll` (Velopack extrae el
  nupkg sin los atributos extendidos donde va la firma de archivos que no son Mach-O). Arranca igual
  porque lo descargado por la app no lleva cuarentena; solo importaría si alguien copia ese `.app`
  a otro Mac con AirDrop o similar.
- Pendiente: Windows, Linux y 0.9.7 → nueva (necesita publicar una pre-release).

## Fuera de alcance

- Firma/notarización de Apple y firma de código en Windows (SmartScreen seguirá avisando la
  primera vez).
- Tiendas (winget, Homebrew, Flathub): se pueden añadir más tarde encima de las mismas releases.
- Notas de la versión dentro de la app (de momento, enlace a la release de GitHub).
