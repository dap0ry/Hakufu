# Hakufu para iPhone e iPad — diseño

Fecha: 2026-10-08 · Rama: `feat/ios` (sale de `main` con la 0.11.0) · Jira: HMR

## Objetivo

La misma app (biblioteca por carpetas, lector, perfil, ajustes, copia de seguridad, español e
inglés) en **iPhone y iPad**, 100 % offline como en escritorio. Sin Mac y sin cuenta de Apple
Developer de pago: el CI compila un `.ipa` **sin firmar** y se instala con un *sideloader* y un
Apple ID gratuito (la firma dura 7 días; se renueva reinstalando).

## Decisiones

- **Un solo proyecto**: `Hakufu.csproj` compila además `net10.0-ios` cuando se pide con
  `-p:HakufuIos=true`. Sin esa propiedad todo sigue como hoy (`dotnet run`, tests, `publish.sh`,
  CI de escritorio, Velopack): nadie necesita el workload de iOS ni Xcode para programar.
  No se mueve ningún archivo de sitio (Dani está trabajando encima).
- **Lo exclusivo de iOS** va en `Platforms/iOS/` (solo se compila para iOS); lo exclusivo de
  escritorio se excluye del target de iOS en el `.csproj` (`Program.cs`, el PDF con Docnet).
- **PDF**: en escritorio sigue pdfium (Docnet, que no tiene binarios para iOS); en iOS,
  CoreGraphics (`CGPDFDocument`) de Apple. Las dos detrás de la misma clase `PdfDocument`.
- **Biblioteca en iOS**: la carpeta *Documentos* de la app, que aparece en la app **Archivos**
  como «En mi iPhone/iPad → Hakufu». Cada subcarpeta es una colección (igual que en escritorio).
  No se puede elegir otra carpeta (iCloud, USB…) en esta versión: el sandbox de iOS solo deja leer
  fuera con permisos de seguridad por URL y todo Hakufu trabaja con rutas.
  «Abrir carpeta» abre Archivos en esa carpeta.
- **Datos en iOS**: `Library/Application Support/Hakufu` (no visible en Archivos; entra en la copia
  de iCloud del dispositivo). iOS puede cambiar la ruta del contenedor al reinstalar: al arrancar
  se reescriben las rutas guardadas al contenedor actual (mismo mecanismo que la copia de
  seguridad: `RebasePaths`).
- **Interfaz**: las mismas vistas, adaptadas al ancho. `MainView` (nuevo, compartido) lleva lo que
  hoy hay dentro de la ventana (contenido, diálogo modal, barra de actualización); `MainWindow`
  (escritorio) lo envuelve con su barra de título de Linux; en iOS `MainView` es la vista única.
  Por debajo de **700 px** de ancho `MainView` tiene la clase `compact` y las vistas reordenan
  su contenido con estilos `.compact …` (una columna, márgenes y rótulos más pequeños). El iPad
  usa la disposición de escritorio (se revisa que quepa desde 744 px, el iPad mini en vertical).
- **Lector táctil**: tocar el tercio izquierdo/derecho pasa página (respetando el sentido de
  lectura), deslizar el dedo también, tocar en el centro muestra/oculta las barras (modo zen).
  Pellizcar amplía y se arrastra la página ampliada; doble toque vuelve al tamaño normal.
- **Modo zen en iOS**: oculta la barra de estado (no hay pantalla completa de ventana).
- **Zonas seguras**: el contenido respeta muesca/isla y la barra de inicio
  (`InsetsManager.SafeAreaPadding`).
- **Lo que en iOS no aplica**: «Salir» en Ajustes (Apple no lo permite) y elegir carpeta de
  biblioteca. Las actualizaciones solo **avisan** (consultan la release de GitHub como hoy hace la
  versión portable) y «Descargar» abre la página de la release; instalar es volver a hacer
  sideload.
- **Selectores de archivos en iOS**: lo que se elige (foto de perfil, copia a restaurar) se copia a
  una carpeta temporal antes de usarlo; lo que se guarda (copia de seguridad, perfil en PNG) se
  escribe en temporal y se vuelca al archivo elegido con `OpenWriteAsync`. Así los servicios siguen
  trabajando con rutas.
- **Guardar al salir**: iOS no cierra las apps, las suspende. Al pasar a segundo plano se apunta el
  rato de lectura y se guarda `data.json` (lo que en escritorio hace `desktop.Exit`).
- **Identidad**: `com.dapory.hakufu` (la del Mac), nombre «Hakufu», versión la del `.csproj`
  (sin el sufijo `-beta.N`, que iOS no admite en `CFBundleVersion`), iOS 15 o superior, todas las
  orientaciones, icono a partir de `HakufuLogo.png`.

## Arquitectura

```
Hakufu.csproj               ← TargetFrameworks: net10.0 (+ net10.0-ios con HakufuIos=true)
                              iOS: Avalonia.iOS, sin Avalonia.Desktop ni Docnet; excluye Program.cs y
                              Services/PdfDocument.Docnet.cs. Escritorio: excluye Platforms/**
MainView.axaml(.cs)         ← NUEVO compartido: contenido + modal + barra de actualización + tinta,
                              lector (Dispose, evento ZenModeChanged), clase "compact" según ancho
MainWindow.axaml(.cs)       ← escritorio: barra de título Linux + MainView + bordes; zen = pantalla completa
App.axaml.cs                ← arranque común; escritorio → MainWindow, vista única (iOS) → MainView
Data/AppPaths               ← DataDir y FixedLibraryRoot (null en escritorio) que fija la cabecera de iOS
Services/AppPlatform        ← IsMobile (iOS) para lo que no aplica (Salir, elegir carpeta)
Services/PdfDocument.Docnet.cs   ← escritorio (pdfium)
Platforms/iOS/
  Main.cs, AppDelegate.cs   ← UIApplication.Main + AvaloniaAppDelegate<App>; fija rutas antes de arrancar
  PdfDocument.iOS.cs        ← CoreGraphics: CGPDFDocument → BGRA → Bitmap
  IosShell.cs               ← abrir Archivos/URL, zonas seguras, barra de estado (zen), segundo plano
  Info.plist (parcial)      ← UIFileSharingEnabled, LSSupportsOpeningDocumentsInPlace, UILaunchScreen,
                              orientaciones, ITSAppUsesNonExemptEncryption=false
  Assets.xcassets/AppIcon.appiconset   ← icono 1024×1024
```

`PdfDocument` (mismo API en los dos): `Open(path, maxWidth, maxHeight)`, `PageCount`,
`RenderPage(index) → Bitmap`, `Dispose`. La usan `PageLoaderService`, `CoverService` y
`LibraryScanner` en lugar de Docnet directamente.

## CI (GitHub Actions)

Job `ios` nuevo en `build.yml`, en `macos-latest` (el repo es público: gratis):

1. `dotnet workload install ios` (y la versión de Xcode que pida el workload).
2. **Simulador**: compila para `iossimulator-arm64`, arranca un iPhone, copia una biblioteca de
   ejemplo (CBZ y PDF) a su carpeta Documentos, abre Hakufu y hace **capturas** de Inicio,
   Biblioteca, Colección y Lector (la app abre la pantalla que diga `HAKUFU_START_SCREEN`, solo
   para esto). Las capturas quedan como artefacto: es la prueba de que arranca y pinta en iOS.
3. **Dispositivo**: `ios-arm64` sin firma → `Payload/Hakufu.app` → `Hakufu-ios.ipa` (artefacto).
4. En un tag `v*`, la release lleva también `Hakufu-ios.ipa`.

## Instalar (README)

Impactor (AppImage/Flathub; Linux, Windows y macOS) con el Apple ID: conectar el iPhone por cable,
arrastrar `Hakufu-ios.ipa`, iniciar sesión. En el iPhone: Ajustes → Privacidad y seguridad → Modo
de desarrollador, y Ajustes → General → VPN y gestión de dispositivos → confiar en el perfil.
Cada 7 días hay que volver a instalarla (los datos se conservan). Los mangas se pasan a la app
Archivos (iCloud Drive, Google Drive, LocalSend, descargas de Safari…) y se mueven a
«En mi iPhone → Hakufu».

## Errores y casos raros

- PDF dañado o que CoreGraphics no abre → 0 páginas, igual que en escritorio.
- Carpeta Documentos vacía → la pantalla de biblioteca vacía explica cómo añadir mangas en iOS.
- Ruta del contenedor cambiada → `RebasePaths` al arrancar; si algo no se encuentra, la
  siguiente lectura de la carpeta lo recoloca por su ruta relativa (como ya hace el escáner).
- Memoria: el lector guarda 4 páginas decodificadas (igual que en escritorio, ~90 MB en el peor
  caso); suficiente en cualquier iPhone con iOS 15.

## Pruebas

- En Linux (`dotnet test`): todo lo de hoy sigue pasando; nuevas: `PdfDocument` con Docnet,
  `RebasePaths` de contenedor, raíz fija de la biblioteca, gestos del lector (tocar/deslizar →
  comando), selectores que escriben vía temporal, y **humo de vistas a tamaño iPhone (390×844) e
  iPad (744×1133 y 1180×820)** con `MainView`, con capturas para revisarlas a ojo.
- En el CI de macOS: compilación iOS + capturas en el simulador.
- En dispositivo: Risco instala el `.ipa` en su iPhone y su iPad.

## Fuera de alcance

App Store y TestFlight (necesitan la cuenta de pago), elegir carpetas fuera de la app
(iCloud/USB), «Abrir con Hakufu» desde otras apps, Android, la animación de pasar hoja (ya está
apagada en escritorio).
