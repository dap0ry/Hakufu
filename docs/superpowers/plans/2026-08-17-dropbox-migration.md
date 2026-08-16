# Dropbox Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Google Drive with Dropbox (App-folder access) as Hakufu's backup/sync provider, across the backend, the web/mobile client, and the WPF desktop app.

**Architecture:** Dropbox's App-folder API addresses files by path, not by opaque ID+parent like Drive — this removes the folder find-or-create plumbing entirely. OAuth (authorization-code + refresh-token) is structurally the same as Google's, so `lib/dropbox.js` mirrors `lib/google.js`. Uploads always go through Dropbox's chunked `upload_session` API (never the 150MB-capped simple `upload` endpoint) so large manga files never hit a hard limit.

**Tech Stack:** Node.js (Vercel serverless functions, `HakufuWeb` repo), vanilla JS (web/PWA client, same repo), C#/.NET 10 WPF (`Hakufu` repo).

**Spec:** `docs/superpowers/specs/2026-08-17-dropbox-migration-design.md`

**Correction to the spec found during planning:** the spec's DB section says `ALTER TABLE mangas RENAME COLUMN drive_file_id TO dropbox_path`. There is no `mangas` SQL table — `mangas` is a `jsonb` column on `user_libraries` (see `db/schema.sql:19`), and `drive_file_id` is just a key inside each JSON object in that array, controlled entirely by the WPF client's serialization (`HakufuApiClient.MangaSyncItem`) and read the same way by the web client. **No SQL migration is needed for this field** — only code changes (Tasks 10, 11, 14). The only real SQL migration is renaming the `google_connections` table and dropping its unused `drive_folder_id` column (Task 1) — Dropbox App-folder paths never need a stored root folder ID at all.

## Global Constraints

- Access type: Dropbox **App folder**, not Full Dropbox (per spec — this is the entire reason for the switch, never require Dropbox app review).
- Addressing: paths relative to the app-folder root (e.g. `/One Piece/vol-01.cbz`), never Drive-style IDs.
- Uploads always use the chunked `upload_session/start` → `/append_v2` → `/finish` sequence, never the simple `/upload` endpoint (150MB cap risk).
- `Dropbox-API-Arg` header values must be `encodeURIComponent(JSON.stringify(args))` — percent-encoded, because manga titles can contain non-ASCII characters and Dropbox requires 7-bit-ASCII header values.
- Clean-cut replacement, no dual-provider support, no data migration for existing connections (confirmed: no real users connected yet).
- Env vars already set on Vercel (production + preview): `DROPBOX_APP_KEY`, `DROPBOX_APP_SECRET`, `DROPBOX_REDIRECT_URI` (= `https://hakufuweb.vercel.app/api/auth/dropbox/callback`).
- No automated test suite in either repo — verification is `node --check` (JS) / `dotnet build` (C#), consistent with the rest of both projects.

---

## Part A — Backend (`HakufuWeb`)

### Task 1: Database migration

**Files:**
- Modify: `HakufuWeb/db/schema.sql:39-48` (update the schema doc to match reality going forward)
- No new file — the migration SQL runs directly against Neon (manual step, see below)

**Interfaces:**
- Produces: table `dropbox_connections(username, refresh_token, connected_at, updated_at)` — same shape as `google_connections` minus the unused `drive_folder_id` column.

- [ ] **Step 1: Update `db/schema.sql`** — replace lines 39-48:

```sql
-- Copia de seguridad en Dropbox: un refresh_token por usuario (cada persona
-- conecta su propio Dropbox, carpeta dedicada de la app — "App folder" — así
-- Hakufu solo ve su propia carpeta, nunca el resto del Dropbox del usuario).
-- Los access tokens nunca se guardan, se piden a Dropbox al vuelo a partir
-- del refresh_token (ver /api/dropbox/token).
create table if not exists dropbox_connections (
  username           text primary key references users(username) on delete cascade,
  refresh_token      text not null,
  connected_at       timestamptz not null default now(),
  updated_at         timestamptz not null default now()
);
```

- [ ] **Step 2: Write the migration SQL for production** (this does NOT run automatically — see the "Manual step" note below):

```sql
ALTER TABLE google_connections RENAME TO dropbox_connections;
ALTER TABLE dropbox_connections DROP COLUMN IF EXISTS drive_folder_id;
```

- [ ] **Step 3: Run it against production Neon.** No local `DATABASE_URL` is configured in this environment (checked: no `.env.local` file). Ask the user how they want this run — options are (a) they paste it into Neon's SQL console themselves (fastest, no credential sharing needed), or (b) they hand over a Postgres connection string / the `DATABASE_URL` value some other way and it gets run programmatically. Do not proceed past this step assuming either path — confirm with the user first, since this touches production data.

- [ ] **Step 4: Commit the schema.sql change**

```bash
cd HakufuWeb
git add db/schema.sql
git commit -m "docs: dropbox_connections reemplaza a google_connections en el esquema"
```

(This commit documents the schema for future reference — it doesn't itself run anything against Neon. Step 3 is the actual migration.)

---

### Task 2: `lib/dropbox.js` (replaces `lib/google.js`)

**Files:**
- Create: `HakufuWeb/lib/dropbox.js`
- Delete: `HakufuWeb/lib/google.js`

**Interfaces:**
- Produces: `buildConsentUrl(state)`, `exchangeCodeForTokens(code)`, `refreshAccessToken(refreshToken)`, `revokeToken(refreshToken)` — same names/shapes as `lib/google.js` had, so callers (Task 3, 4) barely change.

- [ ] **Step 1: Write `lib/dropbox.js`**

```js
// Thin wrappers around Dropbox's OAuth + token endpoints. No SDK — just fetch.
// Mirrors lib/google.js's shape closely; Dropbox's OAuth2 flow is structurally
// the same (authorization_code exchange, refresh_token grant).
const AUTH_URL   = 'https://www.dropbox.com/oauth2/authorize';
const TOKEN_URL  = 'https://api.dropboxapi.com/oauth2/token';
const REVOKE_URL = 'https://api.dropboxapi.com/2/auth/token/revoke';

const APP_KEY      = process.env.DROPBOX_APP_KEY;
const APP_SECRET   = process.env.DROPBOX_APP_SECRET;
const REDIRECT_URI = process.env.DROPBOX_REDIRECT_URI;

function requireConfig() {
  if (!APP_KEY || !APP_SECRET || !REDIRECT_URI) {
    throw new Error(
      'Faltan DROPBOX_APP_KEY / DROPBOX_APP_SECRET / DROPBOX_REDIRECT_URI'
    );
  }
}

function buildConsentUrl(state) {
  requireConfig();
  const params = new URLSearchParams({
    client_id: APP_KEY,
    redirect_uri: REDIRECT_URI,
    response_type: 'code',
    token_access_type: 'offline', // pide refresh_token, igual que access_type=offline en Google
    state,
  });
  return `${AUTH_URL}?${params.toString()}`;
}

async function exchangeCodeForTokens(code) {
  requireConfig();
  const res = await fetch(TOKEN_URL, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      code,
      client_id: APP_KEY,
      client_secret: APP_SECRET,
      redirect_uri: REDIRECT_URI,
      grant_type: 'authorization_code',
    }),
  });
  const data = await res.json();
  if (!res.ok) throw new Error(data.error_description || data.error || 'Error de OAuth');
  return data; // { access_token, refresh_token, expires_in, ... }
}

async function refreshAccessToken(refreshToken) {
  requireConfig();
  const res = await fetch(TOKEN_URL, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      refresh_token: refreshToken,
      client_id: APP_KEY,
      client_secret: APP_SECRET,
      grant_type: 'refresh_token',
    }),
  });
  const data = await res.json();
  if (!res.ok) throw new Error(data.error_description || data.error || 'Error refrescando el token');
  return data; // { access_token, expires_in, ... } — sin nuevo refresh_token
}

// A diferencia de Google (que revoca cualquier token que le pases como
// parámetro), el endpoint de revocación de Dropbox revoca el token que se usa
// para AUTENTICAR la propia llamada — así que hay que pedir un access_token
// fresco a partir del refresh_token guardado, y revocar ESE.
async function revokeToken(refreshToken) {
  try {
    const { access_token } = await refreshAccessToken(refreshToken);
    await fetch(REVOKE_URL, {
      method: 'POST',
      headers: { Authorization: `Bearer ${access_token}` },
    });
  } catch { /* best-effort — igualmente borramos la conexión localmente */ }
}

module.exports = { buildConsentUrl, exchangeCodeForTokens, refreshAccessToken, revokeToken };
```

- [ ] **Step 2: Verify syntax**

Run: `node --check HakufuWeb/lib/dropbox.js`
Expected: no output (success)

- [ ] **Step 3: Delete the old file and commit**

```bash
cd HakufuWeb
git rm lib/google.js
git add lib/dropbox.js
git commit -m "feat: lib/dropbox.js sustituye a lib/google.js"
```

---

### Task 3: `api/auth/dropbox/[action].js` (replaces `api/auth/google/[action].js`)

**Files:**
- Create: `HakufuWeb/api/auth/dropbox/[action].js`
- Delete: `HakufuWeb/api/auth/google/[action].js` (and the now-empty `HakufuWeb/api/auth/google/` directory)

**Interfaces:**
- Consumes: `require('../../../lib/dropbox')` → `buildConsentUrl`, `exchangeCodeForTokens` (Task 2). `require('../../../lib/db')` → `sql`.
- Produces: `GET /api/auth/dropbox/start?state=...` (redirects to Dropbox), `GET /api/auth/dropbox/callback?code=...&state=...` (writes to `dropbox_connections`, Task 1).

- [ ] **Step 1: Write the file** — identical structure to today's `api/auth/google/[action].js`, with the `dropbox_connections` table name and Dropbox-flavored copy:

```js
// /api/auth/dropbox/start + /api/auth/dropbox/callback in one function. Same
// external paths — this is the exact redirect URI registered in the Dropbox
// App Console, unaffected by this internal reorganization.
const { sql } = require('../../../lib/db');
const { buildConsentUrl, exchangeCodeForTokens } = require('../../../lib/dropbox');

module.exports = async (req, res) => {
  const { action } = req.query;
  if (action === 'start'    && req.method === 'GET') return start(req, res);
  if (action === 'callback' && req.method === 'GET') return callback(req, res);
  return res.status(404).json({ detail: 'Not found' });
};

// Público a propósito — este endpoint solo redirige a Dropbox. La identidad real
// se resuelve en el callback a partir de `state`, que es un link_code de un solo
// uso ya asociado a un username (ver /api/dropbox/link-start).
async function start(req, res) {
  const { state } = req.query;
  if (!state) {
    res.setHeader('Content-Type', 'text/html; charset=utf-8');
    return res.status(400).send(page('Enlace inválido', 'Falta el parámetro state.', false));
  }

  let url;
  try {
    url = buildConsentUrl(state);
  } catch (err) {
    // Casi seguro DROPBOX_APP_KEY/APP_SECRET/REDIRECT_URI no están configurados
    // todavía en Vercel — este endpoint se visita directamente en el
    // navegador, así que el error debe ser una página legible, no JSON.
    res.setHeader('Content-Type', 'text/html; charset=utf-8');
    return res.status(500).send(page(
      'Dropbox no está disponible todavía',
      'El servidor de Hakufu aún no tiene configuradas las credenciales de Dropbox. Avisa al desarrollador — no es algo que puedas arreglar tú desde aquí.',
      false
    ));
  }

  res.writeHead(302, { Location: url });
  res.end();
}

function page(title, message, ok) {
  return `<!DOCTYPE html><html lang="es"><head><meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>${title}</title>
<style>
  body{font-family:'Segoe UI',system-ui,sans-serif;background:#0D0D0D;color:#F0F0F0;
    display:flex;align-items:center;justify-content:center;min-height:100vh;margin:0;text-align:center;padding:24px}
  .card{max-width:360px}
  .icon{font-size:40px;margin-bottom:16px}
  h1{font-size:18px;margin:0 0 8px}
  p{font-size:13px;color:#AAAAAA;line-height:1.5}
</style></head><body>
  <div class="card">
    <div class="icon">${ok ? '✓' : '✕'}</div>
    <h1>${title}</h1>
    <p>${message}</p>
  </div>
</body></html>`;
}

async function callback(req, res) {
  const { code, state, error } = req.query;

  if (error) {
    res.setHeader('Content-Type', 'text/html; charset=utf-8');
    return res.status(200).send(page('Conexión cancelada', 'No se concedió acceso a Dropbox. Puedes cerrar esta pestaña.', false));
  }
  if (!code || !state) {
    res.setHeader('Content-Type', 'text/html; charset=utf-8');
    return res.status(400).send(page('Enlace inválido', 'Faltan parámetros en la respuesta de Dropbox.', false));
  }

  try {
    const codes = await sql`select username from link_codes where code = ${state} and expires_at > now()`;
    const linkRow = codes[0];
    if (!linkRow) {
      res.setHeader('Content-Type', 'text/html; charset=utf-8');
      return res.status(400).send(page('Enlace caducado', 'Vuelve a pulsar "Conectar Dropbox" desde la app.', false));
    }

    const tokens = await exchangeCodeForTokens(code);
    if (!tokens.refresh_token) {
      res.setHeader('Content-Type', 'text/html; charset=utf-8');
      return res.status(400).send(page(
        'No se recibió acceso permanente',
        'Revoca el acceso de Hakufu en tu cuenta de Dropbox (dropbox.com/account/connected_apps) y vuelve a intentarlo.',
        false
      ));
    }

    await sql`
      insert into dropbox_connections (username, refresh_token, connected_at, updated_at)
      values (${linkRow.username}, ${tokens.refresh_token}, now(), now())
      on conflict (username) do update set
        refresh_token = excluded.refresh_token,
        updated_at = now()
    `;
    await sql`delete from link_codes where code = ${state}`;

    res.setHeader('Content-Type', 'text/html; charset=utf-8');
    return res.status(200).send(page('Conectado ✓', 'Tu Dropbox está conectado a Hakufu. Puedes cerrar esta pestaña.', true));
  } catch (err) {
    res.setHeader('Content-Type', 'text/html; charset=utf-8');
    return res.status(500).send(page('Error', err.message || 'No se pudo completar la conexión.', false));
  }
}
```

- [ ] **Step 2: Verify syntax**

Run: `node --check "HakufuWeb/api/auth/dropbox/[action].js"`
Expected: no output (success)

- [ ] **Step 3: Delete the old Google auth endpoint and commit**

```bash
cd HakufuWeb
git rm "api/auth/google/[action].js"
git add "api/auth/dropbox/[action].js"
git commit -m "feat: /api/auth/dropbox sustituye a /api/auth/google"
```

---

### Task 4: `api/dropbox/[action].js` (replaces `api/drive/[action].js`)

**Files:**
- Create: `HakufuWeb/api/dropbox/[action].js`
- Delete: `HakufuWeb/api/drive/[action].js`

**Interfaces:**
- Consumes: `require('../../lib/dropbox')` → `refreshAccessToken`, `revokeToken` (Task 2).
- Produces: `GET /api/dropbox/status`, `GET /api/dropbox/token`, `POST /api/dropbox/disconnect`, `POST /api/dropbox/link-start` — same shapes as the old `/api/drive/*` routes had.

- [ ] **Step 1: Write the file**

```js
// /api/dropbox/status + /token + /disconnect + /link-start in one function.
const crypto = require('crypto');
const { sql } = require('../../lib/db');
const { getCurrentUser } = require('../../lib/auth');
const { applyCors } = require('../../lib/cors');
const { refreshAccessToken, revokeToken } = require('../../lib/dropbox');

const LINK_CODE_TTL_MS = 10 * 60 * 1000; // 10 minutos

module.exports = async (req, res) => {
  if (applyCors(req, res)) return;

  const { action } = req.query;

  if (action === 'status'     && req.method === 'GET')  return status(req, res);
  if (action === 'token'      && req.method === 'GET')  return token(req, res);
  if (action === 'disconnect' && req.method === 'POST') return disconnect(req, res);
  if (action === 'link-start' && req.method === 'POST') return linkStart(req, res);

  return res.status(404).json({ detail: 'Not found' });
};

async function status(req, res) {
  const me = getCurrentUser(req);
  if (!me) return res.status(401).json({ detail: 'Token inválido o expirado' });

  const rows = await sql`select connected_at from dropbox_connections where username = ${me}`;
  const row = rows[0];
  return res.status(200).json({ connected: !!row, connected_at: row ? row.connected_at : null });
}

async function token(req, res) {
  const me = getCurrentUser(req);
  if (!me) return res.status(401).json({ detail: 'Token inválido o expirado' });

  const rows = await sql`select refresh_token from dropbox_connections where username = ${me}`;
  const row = rows[0];
  if (!row) return res.status(404).json({ detail: 'Dropbox no está conectado' });

  try {
    const tokens = await refreshAccessToken(row.refresh_token);
    return res.status(200).json({ access_token: tokens.access_token, expires_in: tokens.expires_in });
  } catch (err) {
    return res.status(502).json({ detail: err.message || 'No se pudo renovar el acceso a Dropbox' });
  }
}

async function disconnect(req, res) {
  const me = getCurrentUser(req);
  if (!me) return res.status(401).json({ detail: 'Token inválido o expirado' });

  const rows = await sql`delete from dropbox_connections where username = ${me} returning refresh_token`;
  const row = rows[0];
  if (row) await revokeToken(row.refresh_token);

  return res.status(204).end();
}

async function linkStart(req, res) {
  const me = getCurrentUser(req);
  if (!me) return res.status(401).json({ detail: 'Token inválido o expirado' });

  const code = crypto.randomBytes(24).toString('base64url');
  const expiresAt = new Date(Date.now() + LINK_CODE_TTL_MS);
  await sql`insert into link_codes (code, username, expires_at) values (${code}, ${me}, ${expiresAt.toISOString()})`;

  const base = `https://${req.headers.host}`;
  return res.status(200).json({ link_url: `${base}/api/auth/dropbox/start?state=${encodeURIComponent(code)}` });
}
```

- [ ] **Step 2: Verify syntax**

Run: `node --check "HakufuWeb/api/dropbox/[action].js"`
Expected: no output (success)

- [ ] **Step 3: Delete the old Drive API endpoint and commit**

```bash
cd HakufuWeb
git rm "api/drive/[action].js"
git add "api/dropbox/[action].js"
git commit -m "feat: /api/dropbox sustituye a /api/drive"
```

---

## Part B — Web / mobile client (`HakufuWeb`)

### Task 5: `api.js` — rename Drive methods to Dropbox

**Files:**
- Modify: `HakufuWeb/api.js:52-55`

**Interfaces:**
- Produces: `api.dropboxStatus()`, `api.dropboxConnectStart()`, `api.dropboxDisconnect()`, `api.dropboxToken()` (renamed from `driveStatus`/`driveConnectStart`/`driveDisconnect`/`driveToken`) — Tasks 6, 7, 8, 9 all call these new names.

- [ ] **Step 1: Replace lines 52-55**

Old:
```js
  driveStatus: () => request('/drive/status'),
  driveConnectStart: () => request('/drive/link-start', { method: 'POST' }),
  driveDisconnect: () => request('/drive/disconnect', { method: 'POST' }),
  driveToken: () => request('/drive/token'),
```

New:
```js
  dropboxStatus: () => request('/dropbox/status'),
  dropboxConnectStart: () => request('/dropbox/link-start', { method: 'POST' }),
  dropboxDisconnect: () => request('/dropbox/disconnect', { method: 'POST' }),
  dropboxToken: () => request('/dropbox/token'),
```

- [ ] **Step 2: Verify syntax**

Run: `node --check HakufuWeb/api.js`
Expected: no output (success)

- [ ] **Step 3: Commit**

```bash
cd HakufuWeb
git add api.js
git commit -m "feat: api.js expone dropbox* en vez de drive*"
```

---

### Task 6: Dropbox content-API helper module

**Files:**
- Create: `HakufuWeb/dropbox-content.js`

**Interfaces:**
- Consumes: nothing (pure fetch wrapper, called with an access token the caller already has via `api.dropboxToken()`).
- Produces: `downloadFromDropbox(accessToken, path) → Promise<Blob>` and `dropboxArgHeader(args) → string` (the percent-encoding helper, exported so Task 8/9 don't duplicate it). Used by Tasks 7, 8, 9.

This is a new shared module because three different files (`webapp.js`, `reader.js`, `settings.js`) each need to build a Dropbox download call — better to have one place that gets the header-encoding rule right than three copies.

- [ ] **Step 1: Write the file**

```js
// Pequeño helper compartido para hablar con la API de contenido de Dropbox
// (content.dropboxapi.com) — usado por webapp.js, reader.js y settings.js
// para descargar mangas respaldados. La subida real solo la hace la app de
// escritorio (ver Hakufu/Services/DropboxService.cs); aquí solo se lee.

// Los valores de la cabecera Dropbox-API-Arg deben ser ASCII de 7 bits — los
// títulos de manga pueden llevar tildes/ñ, así que hay que codificar el JSON
// con encodeURIComponent antes de meterlo en la cabecera.
export function dropboxArgHeader(args) {
  return encodeURIComponent(JSON.stringify(args));
}

export async function downloadFromDropbox(accessToken, path) {
  const resp = await fetch('https://content.dropboxapi.com/2/files/download', {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${accessToken}`,
      'Dropbox-API-Arg': dropboxArgHeader({ path }),
    },
  });
  if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
  return resp;
}
```

- [ ] **Step 2: Verify syntax**

Run: `node --check HakufuWeb/dropbox-content.js`
Expected: no output (success)

- [ ] **Step 3: Commit**

```bash
cd HakufuWeb
git add dropbox-content.js
git commit -m "feat: dropbox-content.js — helper compartido de descarga"
```

---

### Task 7: `account.js` — Dropbox card

**Files:**
- Modify: `HakufuWeb/account.js:23-27` (heading), `HakufuWeb/account.js:106-156` (`renderDriveCard`)

**Interfaces:**
- Consumes: `api.dropboxStatus`, `api.dropboxDisconnect`, `api.dropboxConnectStart` (Task 5).

- [ ] **Step 1: Rename the heading (lines 23-27)**

Old:
```js
  const driveHeading = document.createElement('h3');
  driveHeading.textContent = 'Google Drive';
  driveHeading.style.cssText = 'font-size:13px;color:var(--secondary);margin:24px 0 12px;text-transform:uppercase;letter-spacing:0.5px;';
  container.appendChild(driveHeading);
  container.appendChild(await renderDriveCard());
```

New:
```js
  const dropboxHeading = document.createElement('h3');
  dropboxHeading.textContent = 'Dropbox';
  dropboxHeading.style.cssText = 'font-size:13px;color:var(--secondary);margin:24px 0 12px;text-transform:uppercase;letter-spacing:0.5px;';
  container.appendChild(dropboxHeading);
  container.appendChild(await renderDropboxCard());
```

- [ ] **Step 2: Rename and update the function (lines 106-156)**

Old function name `renderDriveCard`, new `renderDropboxCard` — replace the whole function body:

```js
async function renderDropboxCard() {
  const card = document.createElement('div');
  card.className = 'card';
  card.innerHTML = '<div class="empty-state">Comprobando conexión…</div>';

  let status;
  try {
    status = await api.dropboxStatus();
  } catch (err) {
    card.innerHTML = `<div class="empty-state">Error: ${escapeHtml(err.message)}</div>`;
    return card;
  }

  if (status.connected) {
    const connectedAt = status.connected_at ? new Date(status.connected_at).toLocaleDateString() : '';
    card.innerHTML = `
      <div class="status-line on">● Dropbox conectado</div>
      <p>Conectado desde ${connectedAt}. La subida de mangas se hace desde la app de escritorio; aquí puedes leerlos y desconectar la cuenta.</p>
      <button class="btn btn-danger" id="disconnect-btn">Desconectar</button>
    `;
    card.querySelector('#disconnect-btn').addEventListener('click', async (e) => {
      e.target.disabled = true;
      try {
        await api.dropboxDisconnect();
        const fresh = await renderDropboxCard();
        card.replaceWith(fresh);
      } catch (err) {
        alert(err.message);
        e.target.disabled = false;
      }
    });
  } else {
    card.innerHTML = `
      <div class="status-line off">○ Dropbox no conectado</div>
      <p>Conecta tu cuenta de Dropbox para leer aquí los mangas que hayas respaldado desde la app de escritorio. Hakufu solo accede a su propia carpeta dentro de tu Dropbox, nunca al resto.</p>
      <button class="btn" id="connect-btn">Conectar Dropbox</button>
    `;
    card.querySelector('#connect-btn').addEventListener('click', async (e) => {
      e.target.disabled = true;
      try {
        const { link_url } = await api.dropboxConnectStart();
        location.href = link_url;
      } catch (err) {
        alert(err.message);
        e.target.disabled = false;
      }
    });
  }

  return card;
}
```

Also update the doc comment on line 3-7 (`Pestaña "Cuenta": ... conexión con Google Drive.`) to say Dropbox instead.

- [ ] **Step 2: Verify syntax**

Run: `node --check HakufuWeb/account.js`
Expected: no output (success)

- [ ] **Step 3: Commit**

```bash
cd HakufuWeb
git add account.js
git commit -m "feat: account.js — tarjeta de Dropbox sustituye a la de Drive"
```

---

### Task 8: `settings.js` — Dropbox references

**Files:**
- Modify: `HakufuWeb/settings.js` (doc comment lines 7-9, line 27, line 41, lines 111, 136-140)

**Interfaces:**
- Consumes: `api.dropboxStatus`, `api.dropboxToken` (Task 5); `downloadFromDropbox` (Task 6).

- [ ] **Step 1: Update the doc comment (lines 7-9)**

Old: `// Pestaña "Configuración": ... La conexión con Google Drive en sí vive en Cuenta...`
New: `// Pestaña "Configuración": ... La conexión con Dropbox en sí vive en Cuenta...`

- [ ] **Step 2: Line 27** — `status = await api.driveStatus();` → `status = await api.dropboxStatus();`

- [ ] **Step 3: Line 41** — `note.textContent = 'Conecta Google Drive desde Cuenta...'` → `note.textContent = 'Conecta Dropbox desde Cuenta para poder descargar mangas y leerlos sin conexión.';`

- [ ] **Step 4: Line 111** — `const mangas = (library.mangas || []).filter((m) => m.drive_file_id);` → `const mangas = (library.mangas || []).filter((m) => m.dropbox_path);`

- [ ] **Step 5: Replace the download block (lines 136-140)**

Add the import at the top of the file:
```js
import { downloadFromDropbox } from './dropbox-content.js';
```

Old:
```js
        const { access_token } = await api.driveToken();
        const resp = await fetch(
          `https://www.googleapis.com/drive/v3/files/${manga.drive_file_id}?alt=media`,
          { headers: { Authorization: `Bearer ${access_token}` } }
        );
        if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
```

New:
```js
        const { access_token } = await api.dropboxToken();
        const resp = await downloadFromDropbox(access_token, manga.dropbox_path);
```

- [ ] **Step 6: Verify syntax**

Run: `node --check HakufuWeb/settings.js`
Expected: no output (success)

- [ ] **Step 7: Commit**

```bash
cd HakufuWeb
git add settings.js
git commit -m "feat: settings.js — descarga offline usa Dropbox"
```

---

### Task 9: `webapp.js` and `reader.js` — Dropbox references

**Files:**
- Modify: `HakufuWeb/webapp.js:391,404,412-450` (the drive-synced-library rendering + `downloadManga`)
- Modify: `HakufuWeb/reader.js:1-2,46,58-66` (the drive-backed manga read path)

**Interfaces:**
- Consumes: `api.dropboxToken` (Task 5), `downloadFromDropbox` (Task 6).

- [ ] **Step 1: `webapp.js` — the click handler around line 390-394**

Old:
```js
    card.addEventListener('click', () => {
      if (!manga.drive_file_id) return; // no respaldado — nada que hacer aquí
      if (isOffline) { navigate(`/read/${manga.id}`); return; }
      downloadManga(manga, card);
    });
```

New:
```js
    card.addEventListener('click', () => {
      if (!manga.dropbox_path) return; // no respaldado — nada que hacer aquí
      if (isOffline) { navigate(`/read/${manga.id}`); return; }
      downloadManga(manga, card);
    });
```

- [ ] **Step 2: `webapp.js` — `mangaStatusLabel` (around line 402-406)**

Old:
```js
function mangaStatusLabel(manga, isOffline) {
  if (isOffline) return 'Offline ✓';
  if (manga.drive_file_id) return 'Toca para descargar';
  return 'No respaldado';
}
```

New:
```js
function mangaStatusLabel(manga, isOffline) {
  if (isOffline) return 'Offline ✓';
  if (manga.dropbox_path) return 'Toca para descargar';
  return 'No respaldado';
}
```

- [ ] **Step 3: `webapp.js` — `downloadManga` (around lines 412-451)**

Add the import at the top:
```js
import { downloadFromDropbox } from './dropbox-content.js';
```

Replace the fetch block inside `downloadManga`:

Old:
```js
  try {
    const { access_token } = await api.driveToken();
    const resp = await fetch(
      `https://www.googleapis.com/drive/v3/files/${manga.drive_file_id}?alt=media`,
      { headers: { Authorization: `Bearer ${access_token}` } }
    );
    if (!resp.ok) throw new Error(`HTTP ${resp.status}`);

    const contentType = resp.headers.get('Content-Type') || 'application/octet-stream';
```

New:
```js
  try {
    const { access_token } = await api.dropboxToken();
    const resp = await downloadFromDropbox(access_token, manga.dropbox_path);

    // Dropbox no manda Content-Type útil en /files/download — el propio
    // manga.dropbox_path ya trae la extensión, así que se deduce de ahí en
    // vez de confiar en la cabecera (a diferencia de Drive, que sí la daba).
    const ext = (manga.dropbox_path.split('.').pop() || '').toLowerCase();
    const contentType = ext === 'pdf' ? 'application/pdf'
      : (ext === 'cbz' || ext === 'zip') ? 'application/zip'
      : 'application/octet-stream';
```

The rest of the function (progress reading, `saveOffline`, `navigate`) is unchanged — it already reads the body as a stream regardless of provider.

- [ ] **Step 4: `reader.js` — imports (lines 1-2)**

Old:
```js
import { api } from './api.js';
import { getOffline } from './offline-store.js';
```

New:
```js
import { api } from './api.js';
import { getOffline } from './offline-store.js';
import { downloadFromDropbox } from './dropbox-content.js';
```

- [ ] **Step 5: `reader.js` — the Drive-backed fetch (lines 46, 58-66)**

Old (line 46):
```js
    if (!manga.drive_file_id) throw new Error('Este manga no está respaldado en Drive.');
```
New:
```js
    if (!manga.dropbox_path) throw new Error('Este manga no está respaldado en Dropbox.');
```

Old (lines 58-66):
```js
    } else {
      const { access_token } = await api.driveToken();
      const resp = await fetch(
        `https://www.googleapis.com/drive/v3/files/${manga.drive_file_id}?alt=media`,
        { headers: { Authorization: `Bearer ${access_token}` } }
      );
      if (!resp.ok) throw new Error('No se pudo descargar el archivo desde Drive.');
      contentType = resp.headers.get('Content-Type') || '';
      blob = await resp.blob();
    }
```
New:
```js
    } else {
      const { access_token } = await api.dropboxToken();
      const resp = await downloadFromDropbox(access_token, manga.dropbox_path).catch(() => {
        throw new Error('No se pudo descargar el archivo desde Dropbox.');
      });
      const ext = (manga.dropbox_path.split('.').pop() || '').toLowerCase();
      contentType = ext === 'pdf' ? 'application/pdf' : (ext === 'cbz' || ext === 'zip') ? 'application/zip' : '';
      blob = await resp.blob();
    }
```

- [ ] **Step 6: Verify syntax**

Run: `node --check HakufuWeb/webapp.js && node --check HakufuWeb/reader.js`
Expected: no output (success)

- [ ] **Step 7: Commit**

```bash
cd HakufuWeb
git add webapp.js reader.js
git commit -m "feat: webapp.js/reader.js leen mangas respaldados desde Dropbox"
```

---

### Task 10: `sw.js` cache bump

**Files:**
- Modify: `HakufuWeb/sw.js`

**Interfaces:** none — this is just cache invalidation so returning visitors get the new JS instead of a stale cached copy.

- [ ] **Step 1: Add the new shared module to `SHELL_FILES` and bump `CACHE_NAME`**

Find the current `CACHE_NAME` value (it was `hakufu-shell-v8` as of the last release touching `sw.js`) and bump the version number by one, e.g. `hakufu-shell-v9`. Add `/dropbox-content.js` to the `SHELL_FILES` array alongside the other imported modules (`/offline-store.js`, `/local-library.js`, etc.).

- [ ] **Step 2: Verify syntax**

Run: `node --check HakufuWeb/sw.js`
Expected: no output (success)

- [ ] **Step 3: Commit**

```bash
cd HakufuWeb
git add sw.js
git commit -m "chore: sw.js cachea dropbox-content.js, bump de versión"
```

---

## Part C — Desktop (`Hakufu`, WPF)

### Task 11: `IDropboxService` / `DropboxService` (replaces `IGoogleDriveService` / `GoogleDriveService`)

**Files:**
- Create: `Hakufu/Services/IDropboxService.cs`
- Create: `Hakufu/Services/DropboxService.cs`
- Delete: `Hakufu/Services/IGoogleDriveService.cs`
- Delete: `Hakufu/Services/GoogleDriveService.cs`

**Interfaces:**
- Produces: `IsConnectedAsync()`, `StartConnectFlowAsync()`, `DisconnectAsync()`, `GetAccessTokenAsync()`, `UploadFileAsync(accessToken, path, localFilePath, progress, ct) → Task<string>` (returns the final `path_lower` Dropbox reports), `DownloadFileAsync(accessToken, path, destinationPath, progress, ct)`. Consumed by Task 13 (`BackupViewModel`).
- No `FindOrCreateFolderAsync`/`FindOrCreateBackupFolderAsync`/`GetFileNameAsync` — Dropbox's path addressing makes them unnecessary (see spec).

- [ ] **Step 1: Write `IDropboxService.cs`**

```csharp
namespace Hakufu.Services;

public interface IDropboxService
{
    Task<bool> IsConnectedAsync();

    // Pide un link de conexión al backend y lo abre en el navegador del sistema.
    Task<string> StartConnectFlowAsync();

    Task DisconnectAsync();

    // Access token de Dropbox de corta duración, listo para llamar a
    // dropboxapi.com directamente.
    Task<string> GetAccessTokenAsync();

    // path es la ruta completa dentro de la carpeta de la app, ej.
    // "/One Piece/vol-01.cbz" — Dropbox crea las carpetas intermedias que
    // falten él solo, no hace falta buscarlas/crearlas antes (a diferencia
    // de Drive, que direcciona por ID en vez de por ruta).
    Task<string> UploadFileAsync(
        string accessToken, string path, string localFilePath,
        IProgress<double>? progress = null, CancellationToken ct = default);

    Task DownloadFileAsync(
        string accessToken, string path, string destinationPath,
        IProgress<double>? progress = null, CancellationToken ct = default);
}
```

- [ ] **Step 2: Write `DropboxService.cs`**

```csharp
using System.IO;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Hakufu.Services;

public class DropboxService : IDropboxService
{
    private const string ContentApiUrl = "https://content.dropboxapi.com/2";
    private const string ApiUrl        = "https://api.dropboxapi.com/2";
    private const int    ChunkSize     = 8 * 1024 * 1024; // 8 MB por chunk

    private readonly ISessionService _session;
    private readonly HttpClient _api  = new() { BaseAddress = new Uri(HakufuApiClient.BaseUrl) };
    private readonly HttpClient _http = new();

    public DropboxService(ISessionService session) => _session = session;

    // ── Backend (nuestra API) ───────────────────────────────────────────────
    private HttpRequestMessage AuthReq(HttpMethod method, string url)
    {
        var req = new HttpRequestMessage(method, url);
        if (_session.Token is { } t)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", t);
        return req;
    }

    public async Task<bool> IsConnectedAsync()
    {
        var resp = await _api.SendAsync(AuthReq(HttpMethod.Get, "dropbox/status"));
        if (!resp.IsSuccessStatusCode) return false;
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("connected", out var c) && c.GetBoolean();
    }

    public async Task<string> StartConnectFlowAsync()
    {
        var resp = await _api.SendAsync(AuthReq(HttpMethod.Post, "dropbox/link-start"));
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException("No se pudo iniciar la conexión con Dropbox.");

        var doc     = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var linkUrl = doc.RootElement.GetProperty("link_url").GetString()!;
        Process.Start(new ProcessStartInfo(linkUrl) { UseShellExecute = true });
        return linkUrl;
    }

    public async Task DisconnectAsync()
    {
        var resp = await _api.SendAsync(AuthReq(HttpMethod.Post, "dropbox/disconnect"));
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException("No se pudo desconectar Dropbox.");
    }

    public async Task<string> GetAccessTokenAsync()
    {
        var resp = await _api.SendAsync(AuthReq(HttpMethod.Get, "dropbox/token"));
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException("Dropbox no está conectado o la conexión expiró. Conéctalo de nuevo.");

        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("access_token").GetString()!;
    }

    // Los valores de la cabecera Dropbox-API-Arg deben ser ASCII de 7 bits —
    // los títulos de manga pueden llevar tildes/ñ, así que se codifica el
    // JSON con Uri.EscapeDataString antes de meterlo en la cabecera.
    private static string ArgHeader(object args) =>
        Uri.EscapeDataString(JsonSerializer.Serialize(args));

    // ── Dropbox (directo, con el access token) — subida por sesión/chunks ──
    // Siempre por sesión, nunca el endpoint simple de /upload (limitado a
    // 150 MB) — algunos PDF de manga pueden superarlo.
    public async Task<string> UploadFileAsync(
        string accessToken, string path, string localFilePath,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        await using var fs = File.OpenRead(localFilePath);
        var total = fs.Length;
        var buffer = new byte[ChunkSize];

        // 1. Empezar la sesión con el primer chunk.
        int firstRead = await ReadFullyAsync(fs, buffer, ct);
        var startReq = new HttpRequestMessage(HttpMethod.Post, $"{ContentApiUrl}/files/upload_session/start");
        startReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        startReq.Headers.Add("Dropbox-API-Arg", ArgHeader(new { close = false }));
        startReq.Content = new ByteArrayContent(buffer, 0, firstRead);
        startReq.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        var startResp = await _http.SendAsync(startReq, ct);
        startResp.EnsureSuccessStatusCode();
        var startDoc = JsonDocument.Parse(await startResp.Content.ReadAsStringAsync());
        var sessionId = startDoc.RootElement.GetProperty("session_id").GetString()!;

        long sent = firstRead;
        progress?.Report(total > 0 ? (double)sent / total * 100 : 100);

        // 2. Añadir el resto en chunks.
        int read;
        while ((read = await ReadFullyAsync(fs, buffer, ct)) > 0)
        {
            var appendReq = new HttpRequestMessage(HttpMethod.Post, $"{ContentApiUrl}/files/upload_session/append_v2");
            appendReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            appendReq.Headers.Add("Dropbox-API-Arg", ArgHeader(new
            {
                cursor = new { session_id = sessionId, offset = sent },
                close = false,
            }));
            appendReq.Content = new ByteArrayContent(buffer, 0, read);
            appendReq.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var appendResp = await _http.SendAsync(appendReq, ct);
            appendResp.EnsureSuccessStatusCode();

            sent += read;
            progress?.Report(total > 0 ? (double)sent / total * 100 : 100);
        }

        // 3. Cerrar la sesión y guardar el archivo en la ruta final.
        var finishReq = new HttpRequestMessage(HttpMethod.Post, $"{ContentApiUrl}/files/upload_session/finish");
        finishReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        finishReq.Headers.Add("Dropbox-API-Arg", ArgHeader(new
        {
            cursor = new { session_id = sessionId, offset = sent },
            commit = new { path, mode = "overwrite" },
        }));
        finishReq.Content = new ByteArrayContent([]);
        finishReq.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        var finishResp = await _http.SendAsync(finishReq, ct);
        finishResp.EnsureSuccessStatusCode();
        var finishDoc = JsonDocument.Parse(await finishResp.Content.ReadAsStringAsync());
        return finishDoc.RootElement.GetProperty("path_lower").GetString()!;
    }

    private static async Task<int> ReadFullyAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), ct);
            if (read == 0) break;
            total += read;
        }
        return total;
    }

    public async Task DownloadFileAsync(
        string accessToken, string path, string destinationPath,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"{ContentApiUrl}/files/download");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Headers.Add("Dropbox-API-Arg", ArgHeader(new { path }));

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        var total  = resp.Content.Headers.ContentLength ?? -1L;
        var buffer = new byte[81920];
        long read  = 0;

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        await using var src  = await resp.Content.ReadAsStreamAsync(ct);
        await using var dest = File.Create(destinationPath);

        int bytesRead;
        while ((bytesRead = await src.ReadAsync(buffer, ct)) > 0)
        {
            await dest.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            read += bytesRead;
            if (total > 0) progress?.Report((double)read / total * 100);
        }
    }
}
```

- [ ] **Step 3: Delete the old Drive service files**

```bash
cd Hakufu
git rm Services/IGoogleDriveService.cs Services/GoogleDriveService.cs
git add Services/IDropboxService.cs Services/DropboxService.cs
```

(Don't commit yet — Task 12 needs to compile alongside this before the build is green. Commit at the end of Task 13.)

---

### Task 12: Wire `DropboxService` into `App.xaml.cs`

**Files:**
- Modify: `Hakufu/App.xaml.cs:49` and `:106-107`

**Interfaces:**
- Consumes: `DropboxService` (Task 11), `IDropboxService`.
- Produces: the `driveService` local var is renamed `dropboxService` and passed into `BackupViewModel`'s factory case (Task 13's new constructor signature).

- [ ] **Step 1: Line 49** — `var driveService   = new GoogleDriveService(sessionService);` → `var dropboxService = new DropboxService(sessionService);`

- [ ] **Step 2: Lines 106-107** — inside the `Factory` switch:

Old:
```csharp
                    nameof(BackupViewModel) => new BackupViewModel(
                        driveService, apiClient, navService!, _repo!, coverService),
```
New:
```csharp
                    nameof(BackupViewModel) => new BackupViewModel(
                        dropboxService, apiClient, navService!, _repo!, coverService),
```

- [ ] **Step 3: Build will fail here until Task 13 updates `BackupViewModel`'s constructor to take `IDropboxService` — that's expected, this task and Task 13 land in the same commit.**

---

### Task 13: `BackupViewModel.cs` — rewire to `IDropboxService`, path-based upload/restore

**Files:**
- Modify: `Hakufu/MVVM/ViewModel/BackupViewModel.cs` (whole file — field, constructor, `SanitizeDriveName`→`SanitizeDropboxName`, `DoBackupAsync`, `DoRestoreAsync`)

**Interfaces:**
- Consumes: `IDropboxService` (Task 11) instead of `IGoogleDriveService`.
- Consumes: `Manga.DropboxPath` (Task 14, renamed from `Manga.DriveFileId`).

- [ ] **Step 1: Field + constructor (lines 10, 46-54)**

Old:
```csharp
    private readonly IGoogleDriveService _drive;
```
New:
```csharp
    private readonly IDropboxService _dropbox;
```

Old:
```csharp
    public BackupViewModel(IGoogleDriveService drive, HakufuApiClient api, INavigationService nav,
                           IDataRepository repo, ICoverService cover)
    {
        _drive = drive;
        _api   = api;
        _nav   = nav;
        _repo  = repo;
        _cover = cover;
        _ = RefreshStatusAsync();
    }
```
New:
```csharp
    public BackupViewModel(IDropboxService dropbox, HakufuApiClient api, INavigationService nav,
                           IDataRepository repo, ICoverService cover)
    {
        _dropbox = dropbox;
        _api     = api;
        _nav     = nav;
        _repo    = repo;
        _cover   = cover;
        _ = RefreshStatusAsync();
    }
```

- [ ] **Step 2: `RefreshStatusAsync`, `DoConnectAsync`, `DoDisconnectAsync` (lines 57-101)** — every `_drive.` becomes `_dropbox.`, and the two hardcoded status strings change:

Line 60: `try { IsConnected = await _drive.IsConnectedAsync(); }` → `try { IsConnected = await _dropbox.IsConnectedAsync(); }`

Line 72: `await _drive.StartConnectFlowAsync();` → `await _dropbox.StartConnectFlowAsync();`

Line 90: `await _drive.DisconnectAsync();` → `await _dropbox.DisconnectAsync();`

Line 93: `StatusMessage = "Google Drive desconectado.";` → `StatusMessage = "Dropbox desconectado.";`

- [ ] **Step 3: Rename `SanitizeDriveName` and change its role (lines 113-125)**

Dropbox paths are built by hand now (no folder-ID lookups), so this helper's job stays the same (strip characters invalid in a path segment) but it's renamed to match, and `UncategorizedFolderName` stays as-is (it's just a path segment name):

Old:
```csharp
    private const string UncategorizedFolderName = "Sin colección";

    // Nombre legible para carpetas/archivos en Drive — a diferencia del Slugify
    // que usa SyncViewModel para Cloudinary (piensa en URLs), aquí queremos que
    // se vea bien al navegar el Drive a mano: se conservan mayúsculas y
    // espacios, solo se quitan los caracteres que dan problemas.
    private static string SanitizeDriveName(string text)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(text.Where(c => !invalid.Contains(c) && c != '/' && c != '\\').ToArray());
        clean = string.Join(" ", clean.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(clean) ? "Sin título" : clean.Trim();
    }
```
New:
```csharp
    private const string UncategorizedFolderName = "Sin colección";

    // Nombre legible para la ruta en Dropbox — a diferencia del Slugify
    // que usa SyncViewModel para Cloudinary (piensa en URLs), aquí queremos que
    // se vea bien al navegar el Dropbox a mano: se conservan mayúsculas y
    // espacios, solo se quitan los caracteres que dan problemas en una ruta.
    private static string SanitizeDropboxName(string text)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(text.Where(c => !invalid.Contains(c) && c != '/' && c != '\\').ToArray());
        clean = string.Join(" ", clean.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(clean) ? "Sin título" : clean.Trim();
    }
```

- [ ] **Step 4: `DoBackupAsync` — remove folder ID plumbing, build paths directly (lines 127-220)**

Old (lines 127-173, the connect + folder-lookup + upload section):
```csharp
    private async Task DoBackupAsync()
    {
        IsBusy = true; StatusMessage = null; IsSuccess = false;
        try
        {
            ProgressText = "Conectando con Google Drive…";
            var token = await _drive.GetAccessTokenAsync();
            var rootFolderId = await _drive.FindOrCreateBackupFolderAsync(token);

            var pending = _repo.Current.Mangas
                .Where(m => string.IsNullOrEmpty(m.DriveFileId) && File.Exists(m.FilePath))
                .ToList();

            // Un manga puede estar en varias colecciones — se sube una vez y se
            // coloca en la carpeta de la primera a la que pertenezca.
            var collections = _repo.Current.Collections;
            string CollectionFolderNameFor(Manga manga)
            {
                var col = collections.FirstOrDefault(c => c.MangaIds.Contains(manga.Id));
                return col is not null ? SanitizeDriveName(col.Name) : UncategorizedFolderName;
            }

            // Cachear el id de carpeta por nombre — si hay 30 mangas en la misma
            // colección no hace falta buscar/crear esa carpeta 30 veces.
            var folderCache = new Dictionary<string, string>();
            async Task<string> GetCollectionFolderIdAsync(string name)
            {
                if (folderCache.TryGetValue(name, out var id)) return id;
                id = await _drive.FindOrCreateFolderAsync(token, name, rootFolderId);
                folderCache[name] = id;
                return id;
            }

            int total = pending.Count, current = 0;
            foreach (var manga in pending)
            {
                current++;
                var label = $"Subiendo {current} / {total} — {manga.Title}";
                ProgressText = label;
                var progress = new Progress<double>(p => ProgressText = $"{label} ({p:F0}%)");

                var collectionFolderId = await GetCollectionFolderIdAsync(CollectionFolderNameFor(manga));

                var ext = Path.GetExtension(manga.FilePath).ToLowerInvariant();
                var fileName = $"{SanitizeDriveName(manga.Title)}{ext}";
                manga.DriveFileId = await _drive.UploadFileAsync(
                    token, collectionFolderId, fileName, MimeTypeFor(ext), manga.FilePath, progress);
```

New — Dropbox needs no root/collection folder IDs at all, the path itself is the address, and `UploadFileAsync`'s signature dropped the `mimeType` param (Dropbox doesn't need it, unlike Drive):
```csharp
    private async Task DoBackupAsync()
    {
        IsBusy = true; StatusMessage = null; IsSuccess = false;
        try
        {
            ProgressText = "Conectando con Dropbox…";
            var token = await _dropbox.GetAccessTokenAsync();

            var pending = _repo.Current.Mangas
                .Where(m => string.IsNullOrEmpty(m.DropboxPath) && File.Exists(m.FilePath))
                .ToList();

            // Un manga puede estar en varias colecciones — se sube una vez y se
            // coloca en la carpeta de la primera a la que pertenezca.
            var collections = _repo.Current.Collections;
            string CollectionFolderNameFor(Manga manga)
            {
                var col = collections.FirstOrDefault(c => c.MangaIds.Contains(manga.Id));
                return col is not null ? SanitizeDropboxName(col.Name) : UncategorizedFolderName;
            }

            int total = pending.Count, current = 0;
            foreach (var manga in pending)
            {
                current++;
                var label = $"Subiendo {current} / {total} — {manga.Title}";
                ProgressText = label;
                var progress = new Progress<double>(p => ProgressText = $"{label} ({p:F0}%)");

                var ext = Path.GetExtension(manga.FilePath).ToLowerInvariant();
                var fileName = $"{SanitizeDropboxName(manga.Title)}{ext}";
                var path = $"/{CollectionFolderNameFor(manga)}/{fileName}";
                manga.DropboxPath = await _dropbox.UploadFileAsync(
                    token, path, manga.FilePath, progress);
```

The rest of `DoBackupAsync` (cover upload, `_repo.SaveAsync()`, the final status message) is unchanged except:
- Line ~204 (`ProgressText = "Subiendo metadatos de la biblioteca…";`) — unchanged.
- The `MimeTypeFor` helper (lines 105-111) is now unused — delete it.

- [ ] **Step 5: `DoRestoreAsync` — drop `GetFileNameAsync`, use the stored path directly (lines 222-293)**

Old (the restore loop, lines 238-276):
```csharp
            var toRestore = data.Mangas
                .Where(m => !string.IsNullOrEmpty(m.DriveFileId) && Guid.TryParse(m.Id, out _))
                .Where(m => !_repo.Current.Mangas.Any(local =>
                    local.Id == Guid.Parse(m.Id) && File.Exists(local.FilePath)))
                .ToList();

            int total = toRestore.Count, current = 0;
            foreach (var item in toRestore)
            {
                current++;
                var label = $"Descargando {current} / {total} — {item.Title}";
                ProgressText = label;
                var progress = new Progress<double>(p => ProgressText = $"{label} ({p:F0}%)");

                var remoteName = await _drive.GetFileNameAsync(token, item.DriveFileId);
                var ext        = string.IsNullOrEmpty(remoteName) ? "" : Path.GetExtension(remoteName);
                var destPath   = Path.Combine(LibraryDir, $"{item.Id}{ext}");

                await _drive.DownloadFileAsync(token, item.DriveFileId, destPath, progress);

                var id    = Guid.Parse(item.Id);
                var local = _repo.Current.Mangas.FirstOrDefault(m => m.Id == id);
                if (local is not null)
                {
                    local.FilePath = destPath;
                }
                else
                {
                    _repo.Current.Mangas.Add(new Manga
                    {
                        Id                 = id,
                        Title              = item.Title,
                        FilePath           = destPath,
                        TotalPages         = item.TotalPages,
                        DateAdded          = item.DateAdded,
                        CloudinaryCoverUrl = item.CoverCloudinaryUrl,
                        DriveFileId        = item.DriveFileId,
                    });
                }
            }
```

New — the path already contains the extension (`GetFileNameAsync` is gone from `IDropboxService`, so there's nothing to call), extension comes straight from `item.DropboxPath`:
```csharp
            var toRestore = data.Mangas
                .Where(m => !string.IsNullOrEmpty(m.DropboxPath) && Guid.TryParse(m.Id, out _))
                .Where(m => !_repo.Current.Mangas.Any(local =>
                    local.Id == Guid.Parse(m.Id) && File.Exists(local.FilePath)))
                .ToList();

            int total = toRestore.Count, current = 0;
            foreach (var item in toRestore)
            {
                current++;
                var label = $"Descargando {current} / {total} — {item.Title}";
                ProgressText = label;
                var progress = new Progress<double>(p => ProgressText = $"{label} ({p:F0}%)");

                var ext      = Path.GetExtension(item.DropboxPath);
                var destPath = Path.Combine(LibraryDir, $"{item.Id}{ext}");

                await _dropbox.DownloadFileAsync(token, item.DropboxPath, destPath, progress);

                var id    = Guid.Parse(item.Id);
                var local = _repo.Current.Mangas.FirstOrDefault(m => m.Id == id);
                if (local is not null)
                {
                    local.FilePath = destPath;
                }
                else
                {
                    _repo.Current.Mangas.Add(new Manga
                    {
                        Id                 = id,
                        Title              = item.Title,
                        FilePath           = destPath,
                        TotalPages         = item.TotalPages,
                        DateAdded          = item.DateAdded,
                        CloudinaryCoverUrl = item.CoverCloudinaryUrl,
                        DropboxPath        = item.DropboxPath,
                    });
                }
            }
```

Also update line 235 (`var token = await _drive.GetAccessTokenAsync();` → `var token = await _dropbox.GetAccessTokenAsync();`) and the final status messages ("Restaurados {total} archivo(s) desde Google Drive." → "...desde Dropbox.").

- [ ] **Step 6: `BackCommand` (line 302)** — unchanged, still navigates to `SyncViewModel`.

- [ ] **Step 7: Build**

Run: `cd Hakufu && dotnet build --nologo -v:q`
Expected: `Compilación correcta.` — this is the point where Tasks 11, 12, 13 all need to compile together (BackupViewModel is the only consumer of IDropboxService today).

- [ ] **Step 8: Commit Tasks 11-13 together**

```bash
cd Hakufu
git add -A
git commit -m "feat: DropboxService sustituye a GoogleDriveService en el backup"
```

---

### Task 14: `Manga.cs` model + `SyncPayloadBuilder`/`HakufuApiClient` JSON contract

**Files:**
- Modify: `Hakufu/MVVM/Model/Manga.cs:15`
- Modify: `Hakufu/Services/SyncPayloadBuilder.cs:29`
- Modify: `Hakufu/Services/HakufuApiClient.cs:34` (the `MangaSyncItem` record's `JsonPropertyName`)

**Interfaces:**
- Produces: `Manga.DropboxPath` (renamed from `Manga.DriveFileId`), JSON key `dropbox_path` (renamed from `drive_file_id`) — this is the same JSON key Task 8/9's web client reads as `manga.dropbox_path`, so this task and those must agree exactly on the string `dropbox_path`.

- [ ] **Step 1: `Manga.cs:15`**

Old: `    public string   DriveFileId        { get; set; } = string.Empty;`
New: `    public string   DropboxPath        { get; set; } = string.Empty;`

- [ ] **Step 2: `SyncPayloadBuilder.cs:29`**

Old: `                m.CloudinaryCoverUrl, m.DateAdded, m.DriveFileId)).ToList(),`
New: `                m.CloudinaryCoverUrl, m.DateAdded, m.DropboxPath)).ToList(),`

- [ ] **Step 3: `HakufuApiClient.cs` — `MangaSyncItem` record (around line 28-34)**

Old:
```csharp
    public record MangaSyncItem(
        [property: JsonPropertyName("id")]                   string   Id,
        [property: JsonPropertyName("title")]                string   Title,
        [property: JsonPropertyName("total_pages")]          int      TotalPages,
        [property: JsonPropertyName("cover_cloudinary_url")] string   CoverCloudinaryUrl,
        [property: JsonPropertyName("date_added")]           DateTime DateAdded,
        [property: JsonPropertyName("drive_file_id")]        string   DriveFileId = "");
```
New:
```csharp
    public record MangaSyncItem(
        [property: JsonPropertyName("id")]                   string   Id,
        [property: JsonPropertyName("title")]                string   Title,
        [property: JsonPropertyName("total_pages")]          int      TotalPages,
        [property: JsonPropertyName("cover_cloudinary_url")] string   CoverCloudinaryUrl,
        [property: JsonPropertyName("date_added")]           DateTime DateAdded,
        [property: JsonPropertyName("dropbox_path")]         string   DropboxPath = "");
```

- [ ] **Step 4: `BackupViewModel.cs` restore loop (Task 13, Step 5) already references `item.DropboxPath` and `Manga.DropboxPath` — no further change there, this task just makes those names actually exist.**

- [ ] **Step 5: Build**

Run: `cd Hakufu && dotnet build --nologo -v:q`
Expected: `Compilación correcta.`

- [ ] **Step 6: Commit**

```bash
cd Hakufu
git add MVVM/Model/Manga.cs Services/SyncPayloadBuilder.cs Services/HakufuApiClient.cs
git commit -m "feat: Manga.DropboxPath (JSON dropbox_path) sustituye a DriveFileId"
```

---

### Task 15: `BackupView.xaml` + `SyncView.xaml` copy

**Files:**
- Modify: `Hakufu/MVVM/View/BackupView.xaml:34,98,109,115,207`
- Modify: `Hakufu/MVVM/View/SyncView.xaml:269,275`

**Interfaces:** none — pure copy changes, no bindings affected (`IsConnected`/`ConnectCommand`/etc. property names on `BackupViewModel` are unchanged, only their backing implementation moved to `IDropboxService`).

- [ ] **Step 1: `BackupView.xaml:34`** — `<TextBlock Text="Tus mangas en Google Drive"` → `<TextBlock Text="Tus mangas en Dropbox"`

- [ ] **Step 2: `BackupView.xaml:98`** — `<TextBlock Text="Google Drive: " FontSize="14" FontWeight="SemiBold"` → `<TextBlock Text="Dropbox: " FontSize="14" FontWeight="SemiBold"`

- [ ] **Step 3: `BackupView.xaml:109`**

Old: `Conecta tu propia cuenta de Google. Hakufu solo accede a los archivos que él mismo crea en tu Drive (carpeta "Hakufu Backups"), nada más.`
New: `Conecta tu propia cuenta de Dropbox. Hakufu solo accede a su propia carpeta dedicada dentro de tu Dropbox — nunca al resto de tus archivos.`

- [ ] **Step 4: `BackupView.xaml:115`** — `Content="Conectar Google Drive"` → `Content="Conectar Dropbox"`

- [ ] **Step 5: `BackupView.xaml:207`**

Old: `Descarga desde Drive cualquier manga respaldado que no tengas ya en este equipo. Útil tras reinstalar Hakufu o en un PC nuevo.`
New: `Descarga desde Dropbox cualquier manga respaldado que no tengas ya en este equipo. Útil tras reinstalar Hakufu o en un PC nuevo.`

- [ ] **Step 6: `SyncView.xaml:269`** — `<TextBlock Text="Google Drive"` → `<TextBlock Text="Dropbox"`

- [ ] **Step 7: `SyncView.xaml:275`**

Old: `Respalda tus archivos de manga (no solo los datos) en tu propio Google Drive, y léelos desde cualquier dispositivo en la web.`
New: `Respalda tus archivos de manga (no solo los datos) en tu propio Dropbox, y léelos desde cualquier dispositivo en la web.`

- [ ] **Step 8: Build**

Run: `cd Hakufu && dotnet build --nologo -v:q`
Expected: `Compilación correcta.`

- [ ] **Step 9: Commit**

```bash
cd Hakufu
git add MVVM/View/BackupView.xaml MVVM/View/SyncView.xaml
git commit -m "feat: BackupView/SyncView — texto de Dropbox sustituye a Google Drive"
```

---

## Part D — Legal text, verification, release

### Task 16: `HelpViewModel.cs` §6 — Dropbox wording

**Files:**
- Modify: `Hakufu/MVVM/ViewModel/HelpViewModel.cs` (the `LegalSection` with title `"6. Cuentas de usuario, sincronización con servicios de terceros y protección de datos"`)

**Interfaces:** none — this is a data-only change (the `LegalSections` list), no code path depends on the exact wording.

- [ ] **Step 1: Replace every occurrence of "Google Drive" in that section's body with "Dropbox"**, and reword the OAuth-mechanism sentence — the current text says:

> "...incluyendo, entre otros, Google Drive— se realiza íntegramente mediante los mecanismos oficiales de autenticación del tercero correspondiente (OAuth)..."

Change to:

> "...incluyendo, entre otros, Dropbox— se realiza íntegramente mediante los mecanismos oficiales de autenticación del tercero correspondiente (OAuth)..."

(The rest of that section — RGPD/LOPDGDD citations, the "no llega a conocer las credenciales" sentence, LSSICE — needs no change, it was already provider-agnostic apart from the two literal "Google Drive" mentions.)

- [ ] **Step 2: Build**

Run: `cd Hakufu && dotnet build --nologo -v:q`
Expected: `Compilación correcta.`

- [ ] **Step 3: Commit**

```bash
cd Hakufu
git add MVVM/ViewModel/HelpViewModel.cs
git commit -m "docs: términos legales — Dropbox sustituye a Google Drive en la sección 6"
```

---

### Task 17: Final verification and version bump

**Files:**
- Modify: `Hakufu/Hakufu.csproj` (`<Version>`/`<AssemblyVersion>`/`<FileVersion>`)

**Interfaces:** none.

- [ ] **Step 1: Full syntax/build sweep**

```bash
cd HakufuWeb
node --check lib/dropbox.js
node --check "api/auth/dropbox/[action].js"
node --check "api/dropbox/[action].js"
node --check api.js
node --check dropbox-content.js
node --check account.js
node --check settings.js
node --check webapp.js
node --check reader.js
node --check sw.js
```
Expected: no output from any of them (all pass).

```bash
cd Hakufu
dotnet build --nologo -v:q
```
Expected: `Compilación correcta.`

- [ ] **Step 2: Confirm no leftover references** — grep both repos for anything still pointing at the old provider:

```bash
cd HakufuWeb && grep -rn "drive_file_id\|googleapis.com/drive\|/api/drive\|driveStatus\|driveToken\|driveConnectStart\|driveDisconnect" --include=*.js . | grep -v node_modules
```
Expected: no matches (empty output). If anything shows up, it's a file this plan missed — fix it before continuing.

```bash
cd Hakufu && grep -rn "IGoogleDriveService\|GoogleDriveService\|DriveFileId" --include=*.cs . | grep -v "/bin/\|/obj/"
```
Expected: no matches.

- [ ] **Step 3: Push both repos**

```bash
cd HakufuWeb && git push origin main
cd Hakufu && git push origin main
```

- [ ] **Step 4: Confirm the Task 1 database migration has actually been run** against production Neon before considering this done end-to-end — without it, `dropbox_connections` doesn't exist and every `/api/dropbox/*` call 500s. If it hasn't run yet, stop here and go run it (see Task 1, Step 3) before the manual smoke test.

- [ ] **Step 5: Manual smoke test** (needs a real Dropbox account to click through — this is the one thing that can't be automated):
  1. Open the web app, go to Cuenta, click "Conectar Dropbox" → should redirect to Dropbox's consent screen showing only "access to a folder named Hakufu" (confirms App-folder scope, not Full Dropbox).
  2. Approve → should land back on the "Conectado ✓" confirmation page.
  3. In the WPF app, go to Sync → Copia de seguridad → "Comprobar conexión" → should show "Dropbox: Conectado".
  4. Add a manga to the WPF library, run a backup → should upload without error and set `manga.DropboxPath`.
  5. In the web app's Biblioteca, the manga's card should show "Toca para descargar" and download successfully.
  6. In Cuenta, "Desconectar" → status flips back to "Dropbox no conectado".

- [ ] **Step 6: Bump the WPF version** (following the pattern already established in this project for every prior release this session — v0.9.2 through v0.9.6):

```bash
cd Hakufu
# Edit Hakufu.csproj: bump Version/AssemblyVersion/FileVersion to the next
# patch number (whatever comes after the last shipped release).
git add Hakufu.csproj
git commit -m "chore: bump de versión"
git push origin main
.\Build-Release.ps1 -Version "<the version you just set>"
gh release create v<version> Releases/Hakufu-win-Setup.exe Releases/Hakufu-win-Portable.zip "Releases/Hakufu-<version>-full.nupkg" Releases/RELEASES Releases/assets.win.json Releases/releases.win.json --title "v<version>" --notes "Cambios: Dropbox sustituye a Google Drive para la copia de seguridad — misma funcionalidad, distinta nube. Si vienes de una versión anterior, esta actualización se aplicará sola la próxima vez que cierres Hakufu del todo (con la X) y lo reabras."
```

Ask the user to confirm the exact version number before running this — don't guess it from a stale memory of "the last release was v0.9.6," check `gh release list --limit 1` first.

---

## Self-Review Notes (from the plan author, not a task)

- **Spec coverage:** all 4 sections of the design doc (backend, web client, desktop, other references) map to Tasks 1-4 (backend), 5-10 (web client), 11-15 (desktop), 16 (legal text). The spec's "Risks" section item about chunked upload is Task 11's `UploadFileAsync`. The spec's 3 manual steps are: Dropbox app creation (done, before this plan started), Vercel env vars (done, before this plan started), DB migration (Task 1, Step 3 — explicitly flagged as needing user confirmation on how to run it).
- **Correction applied:** the spec assumed a SQL `mangas.drive_file_id` column; planning found `mangas` is actually a `jsonb` array column on `user_libraries`, so that "migration" is really just the JSON key rename already covered by Tasks 8/9 (web) and 14 (WPF). Noted at the top of this plan.
- **Type consistency check:** `Manga.DropboxPath` (Task 14) ↔ `item.DropboxPath`/`manga.DropboxPath` (Task 13) ↔ JSON key `dropbox_path` (Task 14's `JsonPropertyName`) ↔ `manga.dropbox_path` (Tasks 8/9, web client) — all agree on the same string. `IDropboxService.UploadFileAsync(accessToken, path, localFilePath, progress, ct)` (Task 11) ↔ the call site in `BackupViewModel.DoBackupAsync` (Task 13) — same 4 positional args after `accessToken`, in the same order.
