# Hakufu

Gestor y lector de manga para **Windows, macOS y Linux** (Avalonia, .NET 10). Organiza PDF y CBR/CBZ en colecciones y guarda el progreso de lectura. Es **100 % offline**: sin cuentas, sin servidor y sin conexión a internet.

- **Versión actual:** 0.10.0 (primera multiplataforma). La 0.9.7 es la última de WPF, solo para Windows.
- **Jira:** proyecto [HMR · Hakufu Manga Reader](https://dapory.atlassian.net/jira/software/projects/HMR)
- **Discord:** categoría *Hakufu* en Dapory´s WorkSpace (tickets, avances, documentos)

## Qué hay en este repositorio

Este es el **único repositorio de Hakufu**. Los antiguos `HakufuWeb`, `HakufuAPI` y `Hakufu-Manga-Reader` se retiraron el 30/09/2026.

| Carpeta | Qué es | Estado |
|---|---|---|
| raíz (`Hakufu.csproj`, `MVVM/`, `Services/`, `Data/`…) | La app de escritorio (Avalonia) | **En uso** |
| `tests/Hakufu.Tests` | Tests (xUnit + Avalonia headless) | **En uso** |
| `scripts/` | Publicar e instalar en cada sistema | **En uso** |
| `web/` | Landing estática en Vercel (antes repo `HakufuWeb`) | **En uso** |
| `docs/superpowers/` | Specs y planes de diseño de cada cambio | Referencia |

## La app de escritorio

### Requisitos para programar

- [.NET SDK 10](https://dotnet.microsoft.com/download)
  - **macOS:** `brew install --cask dotnet-sdk`
  - **Linux (Ubuntu/Debian):** `sudo apt install dotnet-sdk-10.0` (o el script oficial `dotnet-install.sh`)
  - **Windows:** instalador de la web o `winget install Microsoft.DotNet.SDK.10`
- **Linux:** librerías que Avalonia necesita (casi todas las distros de escritorio ya las traen): `sudo apt install libfontconfig1 libice6 libsm6`.
- Editor: **Rider** (gratis para uso no comercial) o **VS Code** con la extensión *C# Dev Kit* y *Avalonia for VS Code* (previsualizador de `.axaml`).

### Comandos

```bash
dotnet run --project Hakufu.csproj    # abrir la app
dotnet test tests/Hakufu.Tests        # tests
./scripts/publish.sh                  # macOS/Linux: app autocontenida en publish/
.\scripts\publish.ps1                 # Windows: publish\Hakufu-win-x64.zip
./scripts/install-linux.sh            # Linux: instala en ~/.local y lo añade al menú
```

- **macOS:** `./scripts/publish.sh` crea `publish/Hakufu.app`. Arrástralo a Aplicaciones. Hecho en tu propio Mac se abre sin más. Si lo descargas (p. ej. el zip de *Actions*), macOS lo bloquea porque no está firmado con cuenta de Apple: `xattr -dr com.apple.quarantine /Applications/Hakufu.app`, o Ajustes del Sistema → Privacidad y seguridad → *Abrir igualmente*.
- **CI:** cada push compila, pasa los tests y genera la app para `win-x64`, `linux-x64`, `osx-arm64` y `osx-x64` (pestaña *Actions* → *Artifacts*).

### Dónde guarda los datos

| Sistema | Carpeta |
|---|---|
| Windows | `%APPDATA%\Hakufu` (la misma que la 0.9.x: la biblioteca se conserva) |
| macOS / Linux | `~/.config/Hakufu` |

Dentro: `data.json` (colecciones, progreso, ajustes), `covers/` y `profile/` (foto de perfil). Con la variable `HAKUFU_DATA_DIR` se puede usar otra carpeta, útil para probar con una biblioteca de pruebas.

**Los mangas no se copian a Hakufu.** La biblioteca es una carpeta tuya (Ajustes → *Carpeta de la biblioteca*): cada subcarpeta es una colección y sus `.cbz`, `.cbr` y `.pdf` son los tomos. Hakufu solo la lee; para añadir o quitar tomos, cámbialos en esa carpeta y dale a *Actualizar*. Las versiones anteriores copiaban los mangas a `biblioteca/` dentro de la carpeta de datos: si existe, se usa como carpeta de la biblioteca hasta que elijas otra, así no se pierde el progreso.

**Copia de seguridad:** Inicio → *Copia de seguridad* exporta un `.zip` (datos, portadas y perfil, sin los mangas) que se puede importar en cualquier otro equipo, también de otro sistema (Windows → Mac, por ejemplo). El progreso se aplica a los tomos de la carpeta de la biblioteca de ese equipo que estén en la misma subcarpeta y con el mismo nombre.

Arquitectura y convenciones: [`CLAUDE.md`](CLAUDE.md).

## `web/`: la landing (hakufuweb.vercel.app)

Página estática de presentación y descarga (HTML, imágenes e iconos; sin backend). Lee la última versión de las releases de GitHub para enlazar las descargas.

- **Despliegue:** proyecto de Vercel `hakufuweb` (equipo *Proyectos Propios*) con **Root Directory `web`**. Un push a `main` que toque `web/` publica en producción.
- El antiguo backend de la 0.9.x (cuentas, amigos y copia en la nube) se retiró el 01/10/2026: la app no se conecta a nada.

## Cómo trabajamos

- Todo cambio va ligado a una tarea de Jira **HMR** (`HMR-12`): ramas `feat/HMR-12-…`, commits `HMR-12 …`.
- `main` es lo que se publica: los cambios van en ramas `feat/*` y se hace merge cuando están listos (el CI tiene que estar en verde en los tres sistemas).
- Reparto de beneficios: 60 % Dani / 40 % colaboradores según el trabajo aportado en Jira y GitHub (acuerdo en 📄・documentos de Discord).
