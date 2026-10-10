# Hakufu para Android — diseño

Fecha: 2026-10-09 · Rama: `feat/android` (sale de `feat/ios`) · Jira: HMR

## Objetivo

La misma app que en iPhone (biblioteca por carpetas, lector táctil, perfil, ajustes, copia de
seguridad, español e inglés) en **móviles y tablets Android**, 100 % offline como en escritorio.
Se distribuye como **APK** (GitHub Releases), sin Google Play. Nadie del equipo tiene Android: se
prueba en un **emulador** (en el PC de Risco y en el CI), y tiene que quedar bien sin un móvil real.

## Decisiones

- **Un solo proyecto, como iOS**: `Hakufu.csproj` compila además `net10.0-android` cuando se pide
  con `-p:HakufuAndroid=true`. Sin esa propiedad no cambia nada (escritorio, tests, CI, Velopack):
  nadie necesita el workload de Android para programar. No se mueve ningún archivo de sitio.
- **Lo exclusivo de Android** va en `Platforms/Android/` (solo se compila para Android); a Android
  no van `Program.cs`, `Services/PdfDocument.Docnet.cs` (pdfium no tiene binarios para Android)
  ni `Platforms/iOS/`.
- **Identidad**: paquete `com.dapory.hakufu`, nombre «Hakufu», `versionName` = `<Version>` del
  `.csproj` (con su `-beta.N` si lo lleva) y `versionCode` = mayor·10000 + menor·100 + parche
  (0.11.0 → 1100). Android 8.0 (API 26) o superior, todas las orientaciones, icono a partir de
  `HakufuLogo.png`.
- **Biblioteca: carpeta elegida, como en escritorio.** Hakufu pide una vez el permiso de
  **acceso a todos los archivos** (`MANAGE_EXTERNAL_STORAGE`, Android 11+; en 8–10,
  `READ/WRITE_EXTERNAL_STORAGE`) y trabaja con rutas normales. Carpeta por defecto:
  `Hakufu` en el almacenamiento interno (`/storage/emulated/0/Hakufu`, se crea al dar el permiso),
  visible desde cualquier gestor de archivos y desde el PC por USB. En Ajustes se puede cambiar
  con el selector de carpetas del sistema: devuelve una URI `content://…/tree/primary:Mangas`,
  que se traduce a la ruta (`/storage/emulated/0/Mangas`; tarjetas SD: `/storage/XXXX-XXXX/…`).
  Google Play no admite ese permiso, pero no vamos a Play.
- **Sin permiso** (primera vez, o si se quita): la biblioteca vacía explica para qué hace falta y
  tiene un botón que abre el ajuste del sistema; al volver a la app, si ya está dado, se crea la
  carpeta por defecto (si no hay otra elegida) y se vuelve a leer la biblioteca.
- **Datos**: carpeta privada de la app (`Context.FilesDir/Hakufu`). No se ve desde fuera; la copia
  de seguridad sigue siendo el .zip de siempre.
- **Lo que se guarda** (copia de seguridad .zip, perfil en PNG) va a **Descargas** sin selector
  (como iOS va a la carpeta de Hakufu): es donde se busca en Android. Lo que se elige (copia a
  restaurar) llega por el selector del sistema y se copia a una carpeta temporal, como en iOS.
- **Elegir foto** (perfil): el selector de fotos de Android (`PickVisualMedia`; en los Android que
  no lo traen, el de documentos con imágenes). La foto se guarda como JPEG derecho (orientación
  EXIF aplicada) de 2048 px como mucho, igual que en iOS.
- **PDF**: `android.graphics.pdf.PdfRenderer` detrás de la misma clase `PdfDocument` (como
  CoreGraphics en iOS). PdfRenderer no es seguro entre hilos: sigue el `PdfLock` de siempre.
- **Interfaz**: la misma `MainView`, con las clases `compact`/`narrow` que ya existen; el lector
  táctil, el modo zen (oculta las barras del sistema), las zonas seguras (Android 15 dibuja de
  borde a borde) y el guardado al pasar a segundo plano son el código de iOS, que ya es común.
- **Botón/gesto de atrás** (nuevo, solo Android): cierra el diálogo abierto; si no hay, sale del
  lector; si no, vuelve a Inicio; en Inicio deja que Android cierre la app.
- **Lo que no aplica**: «Salir» (en Android no se cierra así). «Abrir carpeta» abre el gestor de
  archivos del sistema en esa carpeta (si el móvil no sabe, no hace nada).
- **Actualizaciones**: como iOS: solo **avisan** (release de GitHub) y «Descargar» abre la página
  de la release; el APK nuevo se instala encima porque va firmado con la misma clave.
- **Firma**: una clave propia (`hakufu-release.keystore`, alias `hakufu`) generada en el PC de
  Risco y guardada fuera del repo (`~/.local/share/hakufu-android/`, contraseña en el llavero).
  El CI firma con ella desde los secrets `ANDROID_KEYSTORE_BASE64` y `ANDROID_KEYSTORE_PASSWORD`
  de `dap0ry/Hakufu`, que tiene que añadir alguien con permisos de administración (Dani). Sin
  esos secrets el CI firma con una clave temporal de depuración, y ese APK no se puede instalar
  encima del firmado (ni al revés).
- **Arranque en una pantalla** (solo CI y pruebas): el extra `start_screen` del intent hace lo
  mismo que `HAKUFU_START_SCREEN` en iOS.

## Arquitectura

```
Hakufu.csproj               ← + net10.0-android con HakufuAndroid=true: Avalonia.Android, sin Avalonia.Desktop
                              ni Docnet; excluye Program.cs, PdfDocument.Docnet.cs y Platforms/iOS
Data/AppPaths               ← + SaveDir: dónde se guarda sin selector (iOS: Documentos; Android: Descargas)
Services/AppPlatform        ← IsMobile (táctil, sin «Salir») deja de significar «biblioteca fija»: eso es
                              AppPaths.FixedLibraryRoot (solo iOS). Nuevos ganchos: PickFolderAsync
                              (Android: selector del sistema → ruta), HasLibraryAccess / RequestLibraryAccess
Services/AndroidStorage     ← (común, para poder probarlo en Linux) URI content:// de árbol → ruta
Services/FilePickerService  ← PickFolderAsync usa el gancho; SaveFileAsync en móvil → AppPaths.SaveDir
MainView.axaml(.cs)         ← + HandleBack(): diálogo → lector → Inicio → false (que salga Android)
MVVM/View/LibraryView        ← biblioteca vacía: «Elegir carpeta» si se puede elegir, el texto de iOS si es
                              fija, o el aviso de permiso con su botón si falta el permiso
MVVM/ViewModel/Settings      ← «Cambiar carpeta» si FixedLibraryRoot es null (también en Android)
Platforms/Android/
  MainActivity.cs           ← AvaloniaMainActivity<App>: AndroidPlatform.Configure antes de arrancar,
                              start_screen, BackRequested → MainView.HandleBack, resultado de selectores
  AndroidPlatform.cs        ← AppPlatform y AppPaths de Android, permisos, abrir carpeta/URL
  PdfDocument.Android.cs    ← PdfRenderer → Bitmap ARGB → Bitmap de Avalonia
  PhotoPicker.cs            ← selector de fotos → JPEG derecho de 2048 px como mucho
  AndroidManifest.xml       ← permisos, requestLegacyExternalStorage (Android 10), orientaciones
  Resources/                ← icono (mipmap) y tema de arranque
```

## CI (GitHub Actions)

Job `android` nuevo en `build.yml`, en `ubuntu-latest` (con KVM, gratis en repos públicos):

1. JDK 17 y `dotnet workload install android`.
2. **APK** de Release, firmado con la clave de los secrets (o la temporal si no están) →
   `Hakufu-android.apk` (artefacto).
3. **Emulador** (Android 14, x86_64): instala el APK, da el permiso por `adb`, copia una biblioteca
   de ejemplo (CBZ y PDF) a `/sdcard/Hakufu` y saca **capturas** de Inicio, Biblioteca, Colección,
   Lector, Perfil y Ajustes con `start_screen`. Es la prueba de que arranca y pinta en Android.
4. En un tag `v*`, la release lleva también `Hakufu-android.apk`. Si el job de Android falla, la
   release de escritorio sale igual (como con iOS).

## Instalar (README)

Descargar `Hakufu-android.apk` de la release en el móvil y abrirlo (Android pide permitir
«instalar apps de origen desconocido» al navegador o al gestor de archivos). Al abrir Hakufu,
dar el permiso de acceso a todos los archivos. Los mangas van a la carpeta `Hakufu` del
almacenamiento interno: desde el PC por USB (modo «Transferencia de archivos»), o con cualquier
gestor de archivos (descargas, Drive, LocalSend…). Cada subcarpeta es una colección.

## Errores y casos raros

- PDF dañado o que PdfRenderer no abre (p. ej. con contraseña) → 0 páginas, igual que en escritorio.
- Permiso denegado → biblioteca vacía con el aviso; nada falla.
- Carpeta elegida que no es del almacenamiento (p. ej. Drive en el selector) → no se puede traducir
  a ruta: se avisa y se queda la que había.
- Tarjeta SD quitada → la carpeta no existe: la biblioteca sale vacía, como un disco desconectado
  en escritorio.
- Memoria: el lector guarda 4 páginas decodificadas, como en escritorio e iOS.

## Pruebas

- En Linux (`dotnet test`): todo lo de hoy sigue pasando; nuevas: traducción de URIs
  (`primary:`, tarjeta SD, Drive → null), `HandleBack` (diálogo, lector, pantalla, Inicio), aviso de
  permiso en la biblioteca vacía, «Cambiar carpeta» visible en móvil sin biblioteca fija, guardar en
  `SaveDir`.
- En el CI: compilación Android + capturas en el emulador.
- En el PC de Risco: emulador de Android con `adb`, recorriendo la app como un usuario (permiso,
  mangas, lector, gestos, tema, elegir foto, copia de seguridad, botón de atrás, girar la pantalla).

## Fuera de alcance

Google Play, biblioteca solo con el selector del sistema (URIs `content://` en vez de rutas),
instalar actualizaciones desde la propia app, «Abrir con Hakufu» desde otras apps, Android TV.
