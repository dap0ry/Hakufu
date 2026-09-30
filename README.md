# Hakufu

Gestor y lector de manga para Windows (WPF, .NET 10). Organiza PDF y CBR/CBZ en colecciones, guarda el progreso de lectura y sincroniza la biblioteca con Dropbox.

- **Versión actual:** 0.9.7 (17/08/2026)
- **Jira:** proyecto [HMR · Hakufu Manga Reader](https://dapory.atlassian.net/jira/software/projects/HMR)
- **Discord:** categoría *Hakufu* en Dapory´s WorkSpace (tickets, avances, documentos)

## Qué hay en este repositorio

Este es el **único repositorio de Hakufu**. Los antiguos `HakufuWeb`, `HakufuAPI` y `Hakufu-Manga-Reader` se retiraron el 30/09/2026; lo que seguía en uso de ellos vive ahora aquí.

| Carpeta | Qué es | Estado |
|---|---|---|
| raíz (`Hakufu.csproj`, `MVVM/`, `Services/`, `Data/`…) | La aplicación de escritorio WPF | **En uso** |
| `web/` | Landing + API en Vercel Functions (antes repo `HakufuWeb`) | **En uso**: la app la llama |
| `api/` | Backend FastAPI antiguo (antes repo `HakufuAPI`, desplegado en Render) | **Retirado**: Render ya no responde y la app no lo usa |
| `docs/superpowers/` | Specs y planes de diseño de cada cambio | Referencia |

### La app de escritorio

```bash
dotnet restore
dotnet tool restore                      # vpk (Velopack) como herramienta local
dotnet run --project Hakufu.csproj       # abrir la app
.\Build-Release.ps1 -Version "0.9.8"     # instalador → Releases\Hakufu-win-Setup.exe
```

- Datos locales en `%LOCALAPPDATA%\Hakufu\data.json`; portadas en `%LOCALAPPDATA%\Hakufu\covers\`.
- Instalación y actualizaciones con **Velopack**: se instala por usuario (sin admin) y se actualiza sola desde GitHub Releases de este repo.
- Arquitectura MVVM con inyección manual en `App.xaml.cs`. Detalle en [`CLAUDE.md`](CLAUDE.md).

### `web/` — landing y API (hakufuweb.vercel.app)

La app depende de esta parte. `Services/HakufuApiClient.cs` usa `https://hakufuweb.vercel.app/api/` para:

- **Dropbox:** la vuelta del inicio de sesión (`/api/auth/dropbox/callback`) y el acceso a la carpeta de la biblioteca (`/api/dropbox/*`).
- **Cuentas y amigos:** `/api/auth/*`, `/api/users/*`, `/api/friends/*`.

También es la landing y la versión web del lector (`index.html`, `webapp.js`, `reader.js`, `sw.js`).

- **Despliegue:** proyecto de Vercel `hakufuweb` (equipo *Proyectos Propios*) enlazado a este repo con **Root Directory `web`**. Un push a `main` que toque `web/` publica en producción.
- **Datos:** Postgres en **Neon** (`web/db/schema.sql`, migración `web/db/migrate-dropbox.sql`; aplicar con `node web/db/apply-schema.js`) y ficheros en **Vercel Blob**.
- **Variables de entorno:** solo en Vercel, nunca en el repo: `DATABASE_URL` (Neon), `JWT_SECRET`, `DROPBOX_APP_KEY`, `DROPBOX_APP_SECRET`, `DROPBOX_REDIRECT_URI` y el token de Vercel Blob.
- **En local:** `cd web && npm install && vercel dev`.

> **Rumbo nuevo (Jira HMR):** app local **sin cuentas ni amigos**, migración a **Avalonia**, más personalización (p. ej. animación 3D al pasar página) e Instagram. Cuando se quiten las cuentas, las rutas `/api/auth/*` (salvo Dropbox), `/api/users/*` y `/api/friends/*` dejarán de hacer falta. Hasta entonces **no se pueden borrar**: la versión 0.9.7 instalada las usa.

### `api/` — backend antiguo (retirado)

Primer backend en FastAPI (Python 3.12), desplegado en Render como `HakufuAPI`. Se sustituyó por las funciones de `web/api/` y ya no se usa. Se conserva solo como referencia; se puede borrar cuando se confirme que nada lo necesita.

## Cómo trabajamos

- Todo cambio va ligado a una tarea de Jira **HMR** (`HMR-12`): ramas `feat/HMR-12-…`, commits `HMR-12 …`.
- `main` es lo que se publica: cambios en ramas `feat/*` y merge cuando están listos.
- Reparto de beneficios: 60 % Dani / 40 % colaboradores según el trabajo aportado en Jira y GitHub (acuerdo en 📄・documentos de Discord).
