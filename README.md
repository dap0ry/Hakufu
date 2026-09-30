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
| `web/` | Landing + API en Vercel (antes repo `HakufuWeb`) | **Solo para la 0.9.7**: la app nueva no la usa |
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

- **macOS:** `./scripts/publish.sh` crea `publish/Hakufu.app`. Arrástralo a Aplicaciones. No está firmado con cuenta de Apple, así que la primera vez se abre con **clic derecho → Abrir**.
- **CI:** cada push compila, pasa los tests y genera la app para `win-x64`, `linux-x64`, `osx-arm64` y `osx-x64` (pestaña *Actions* → *Artifacts*).

### Dónde guarda los datos

| Sistema | Carpeta |
|---|---|
| Windows | `%APPDATA%\Hakufu` (la misma que la 0.9.x: la biblioteca se conserva) |
| macOS / Linux | `~/.config/Hakufu` |

Dentro: `data.json` (biblioteca, progreso, ajustes), `covers/`, `customization/` y `biblioteca/` (mangas copiados a Hakufu). Con la variable `HAKUFU_DATA_DIR` se puede usar otra carpeta, útil para probar con una biblioteca de pruebas.

**Copia de seguridad:** Inicio → *Copia de seguridad* exporta un `.zip` que se puede importar en cualquier otro equipo, también de otro sistema (Windows → Mac, por ejemplo).

Arquitectura y convenciones: [`CLAUDE.md`](CLAUDE.md).

## `web/`: landing y API (hakufuweb.vercel.app)

Es la landing y el backend (cuentas, amigos, Dropbox) **que solo usa la versión 0.9.7 de Windows**. La app nueva no se conecta a nada.

- **Despliegue:** proyecto de Vercel `hakufuweb` (equipo *Proyectos Propios*) con **Root Directory `web`**. Un push a `main` que toque `web/` publica en producción.
- **Datos:** Postgres en **Neon** y ficheros en **Vercel Blob**. Las variables de entorno están solo en Vercel.
- Se podrá retirar cuando nadie use ya la 0.9.7. Hasta entonces **no se borra**: la 0.9.7 instalada llama a `/api/*`.

## Cómo trabajamos

- Todo cambio va ligado a una tarea de Jira **HMR** (`HMR-12`): ramas `feat/HMR-12-…`, commits `HMR-12 …`.
- `main` es lo que se publica: los cambios van en ramas `feat/*` y se hace merge cuando están listos (el CI tiene que estar en verde en los tres sistemas).
- Reparto de beneficios: 60 % Dani / 40 % colaboradores según el trabajo aportado en Jira y GitHub (acuerdo en 📄・documentos de Discord).
