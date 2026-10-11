# Afterpelago

Generate post-game reports from Archipelago Randomizer sessions and track stats across your playgroup ("pods").

The application now includes the Solid/Vite development
workflow, the C#→TypeScript contract pipeline, SQLite persistence, Discord sign-in with manual approval, and a
single-process production deployment, and the core WebLog session importer described below.

## Prerequisites

| Tool     | Version                     | Notes                                                                                      |
| -------- | --------------------------- | ------------------------------------------------------------------------------------------ |
| .NET SDK | 10.0.x (`global.json`)      | C# 14 is enabled explicitly (`LangVersion 14.0`).                                          |
| Node.js  | 22.18+ (24 LTS recommended) | Build/dev tooling only; **not** needed to run the published app. Orval declares `>=22.18`. |
| Git      |                             |                                                                                            |

## Repository structure

```
Afterpelago.slnx            solution (single production project)
global.json                 pins SDK band
dotnet-tools.json           local dotnet-ef
server/                     the ONE production project: Afterpelago.csproj (ASP.NET Core, .NET 10)
  Api/                      minimal-API endpoints + request/response contracts (the source of truth for the TS client)
  Auth/                     Discord + cookie auth, antiforgery, access-record revalidation
  Data/                     EF Core DbContext, data-directory resolution, Migrations/
  Domain/                   entities, including the Session aggregate
  Services/                 session import and lifecycle processing
web/                        Solid + Vite + Tailwind + daisyUI SPA (a normal npm project)
  openapi/                  GENERATED OpenAPI contract (committed)
  src/api/generated/        GENERATED TypeScript models + TanStack Solid Query hooks (never edit)
  src/api/transport.ts      thin fetch wrapper (errors + CSRF header); no DTOs
  scripts/                  API generation tooling
.vscode/                    optional launch/tasks (nothing requires VS Code)
```

Folders are organisation, not architecture; add `Services/` etc. only when something needs a home.

## First-time setup

```powershell
dotnet restore
dotnet tool restore
npm --prefix web ci
```

## Development: two independent processes

```powershell
# terminal 1 – backend (http://localhost:5041), normal C# debugging / dotnet watch
dotnet watch --project server --no-hot-reload

# terminal 2 – frontend with Vite HMR (http://localhost:5173)
npm --prefix web run dev
```

Browse **http://localhost:5173**. Vite proxies every `/api/*` request to ASP.NET (`AFTERPELAGO_API_URL` overrides the
target); frontend code only uses relative URLs. Editing a Solid/CSS file updates instantly with no backend rebuild;
editing C# restarts only the backend. The lifetimes are not coupled. VS Code task **dev: start both** simply launches
the two commands side by side.

The default `http` launch profile (used by `dotnet watch`, VS Code and Visual Studio) sets
`Authentication__DevLogin__Enabled=true`, which shows a **Development sign-in** button on the login page. See
[Authentication](#authentication-discord--manual-approval). For real Discord sign-in use the `http-discord` profile
(`dotnet watch --project server --launch-profile http-discord`) after configuring secrets.

### Where development data lives

`appsettings.Development.json` sets `Afterpelago:DataDirectory` to `../.data`, resolved from the project folder, i.e.
**`<repo>/.data/`** (git-ignored): `afterpelago.db` (SQLite), `keys/` (cookie-protection keys), `imports/`
(original session logs named by import ID), and `games/` (scraped item images). Delete the folder to reset.
Open the database with any SQLite tool (DB Browser for SQLite, `sqlite3`, the VS Code SQLite extensions).

## API contract and generated client

C# is authoritative. One command regenerates everything:

```powershell
npm --prefix web run api:generate
```

It builds the server with build-time OpenAPI generation (`Microsoft.Extensions.ApiDescription.Server`, no running
server needed) → `web/openapi/afterpelago.json` → Orval (`solid-query` + native `fetch`) → `web/src/api/generated/`.
After changing a DTO, run it, then `npm --prefix web run typecheck`: the compiler flags every stale usage (verified by
renaming `ServerDataResponse.Greeting`, which broke `DemoPage.tsx` until updated). `npm --prefix web run api:check`
regenerates into scratch folders and fails if the committed files are stale (CI-friendly). Ordinary `dotnet build`
and `dotnet watch` never run this or touch npm. No Swagger UI/Scalar is included; Development serves the raw JSON at
`/openapi/v1.json`, and a UI is a two-line addition in `Program.cs` later.

## Database and EF Core migrations

SQLite via EF Core; migrations run automatically at startup (`MigrateAsync`). Run the EF tool from `server/`:

```powershell
cd server
dotnet tool run dotnet-ef migrations add <Name> --output-dir Data/Migrations
dotnet tool run dotnet-ef database update        # optional; the app also migrates on start
```

The design-time factory (`AfterpelagoDbContextFactory`) needs no Discord settings and does not boot the web host.
Back up the entire data directory while the app is stopped. A complete backup needs `afterpelago.db`, `imports/`,
`keys/`, and `games/`; the database alone does not contain the original session logs or item images.

## Configuration and secrets

Standard ASP.NET configuration; environment variables use `__` for `:` and override files.

| Key                                                                        | Purpose                                                                                                            |
| -------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------ |
| `Afterpelago:DataDirectory`                                                | **All** mutable state (SQLite file, data-protection keys). Required; must be an absolute path outside Development. |
| `Authentication:Discord:ClientId` / `ClientSecret`                         | Discord OAuth application. Sign-in is disabled (503, UI says so) until both are set.                               |
| `Authentication:DevLogin:Enabled`                                          | Development-only test identity. The app **refuses to start** if this is true in any other environment.             |
| `Afterpelago:DataProtection:KeyCertificatePath` / `KeyCertificatePassword` | Optional PFX that encrypts the cookie-protection keys at rest (portable; no Windows DPAPI dependency).             |

Development secrets (never committed):

```powershell
dotnet user-secrets set "Authentication:Discord:ClientId" "<id>" --project server
dotnet user-secrets set "Authentication:Discord:ClientSecret" "<secret>" --project server
```

Production: environment variables or a protected local `appsettings.Production.json` next to the exe. Because keys are
persisted to files, restrict permissions on `<DataDirectory>/keys` to the service account (and prefer the optional
certificate encryption).

## Authentication: Discord + manual approval

Discord OAuth (authorization-code + PKCE, **`identify` scope only**, tokens discarded) → ASP.NET cookie session.
Chosen over OpenIddict Client because this is one provider, a cookie app and a tiny friend group; OpenIddict is the
better foundation if multiple providers/OIDC are ever needed (`AspNet.Security.OAuth.Discord`'s own docs steer new,
larger apps there).

Signing in with Discord never grants access by itself. The first sign-in **upserts one row in `AccessRecords`**
(Discord ID, display name, username, `RequestedAt`, `LastSeenAt`) with `Status = 'Pending'` and shows
"Waiting for approval". Access requires `Status = 'Approved'`, which you set by hand:

```sql
-- see who is waiting (DiscordUserId is the immutable snowflake; never approve by name)
SELECT DiscordUserId, DisplayName, Username, Status, RequestedAt, LastSeenAt FROM AccessRecords ORDER BY RequestedAt;

UPDATE AccessRecords SET Status = 'Approved' WHERE DiscordUserId = '123456789012345678';
UPDATE AccessRecords SET Status = 'Denied'   WHERE DiscordUserId = '123456789012345678';  -- or 'Pending'
```

The cookie is re-validated against this table on every request, so changing a user to `Denied`/`Pending` (or deleting
the row) ends their session immediately. Mutations and logout require the antiforgery token (`X-CSRF-TOKEN`, obtained
from `GET /api/auth/antiforgery`); the frontend transport does this automatically. API calls from signed-out users
return 401 JSON, never a login redirect.

Discord developer portal: register redirect URLs `http://localhost:5173/api/auth/discord-callback` (via the Vite proxy)
and, if you use the backend port directly, `http://localhost:5041/api/auth/discord-callback`. Production needs the exact
public URL (`https://<host>/api/auth/discord-callback`); serve it over HTTPS (cookies are `Secure` whenever the request is HTTPS).

**Development sign-in**: with `Authentication__DevLogin__Enabled=true` (Development only; loopback only) the login page
offers a button that signs you in as a fixed approved test user through the same cookie/CSRF rules.

## Contract checks

```powershell
npm --prefix web run api:check                # generated contract is current
npm --prefix web run typecheck
```

## Production build and publish

One documented command (needs .NET + Node on the **build** machine only):

```powershell
dotnet publish server/Afterpelago.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish/win-x64
```

A publish-only MSBuild target (`BuildWebForPublish` in `server/Afterpelago.csproj`) runs before files are collected:
`npm ci` → `npm run api:generate` (regenerates the OpenAPI contract and TS client, so a stale client cannot ship) →
`npm run build` (typecheck + Vite production build into `web/dist`). `IncludeWebInPublish` then adds `web/dist/**` to the
publish items under `wwwroot/`. Nothing is copied into `server/wwwroot` (it does not exist in source), and plain
`dotnet build`/`dotnet watch` never run any of this. The output folder is self-contained: no Node, npm, Vite or installed
.NET runtime is needed to run it. (`-p:SkipWebBuild=true` reuses an existing `web/dist`.) Publish for Linux with
`-r linux-x64`; the source has no Windows-only dependencies (no IIS, registry, drive letters or service-account paths).

Run the published app locally exactly as production would:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Production'
$env:ASPNETCORE_URLS = 'http://localhost:5000'
$env:Afterpelago__DataDirectory = 'C:\path\to\afterpelago-data'   # any absolute, writable folder
.\artifacts\publish\win-x64\Afterpelago.exe
```

ASP.NET serves `/api/*`, the compiled static assets, and the SPA shell for extension-less client routes (so refresh /
deep links work). Unknown `/api/*` paths and missing files are real 404s. The content root is the application folder,
not the working directory, so it starts correctly from anywhere (for example as a service).

## Game Scraper

Administrators can open **Game Scraper** in the sidebar (`/game-scraper`). Upload a PopTracker ZIP,
select its imported game, click **Scrape**, review image proposals, and click **Save**. Uploads are validated
before Scrape is enabled. The review URL includes the draft ID and can be reopened for seven days; unsaved
manual edits are local to the page. Selecting a game also opens its saved mappings without another upload.

Games receive stable IDs from session imports. Existing slots and receiver-side check/hint item names are
backfilled by migration. Deleting a session does not delete its game catalog or images. Administrators can
add exact canonical item names before those names appear in a log. Item names are case-sensitive identities;
only unique image suggestions tolerate case and whitespace differences. The Wind Waker pack's longer
manifest title is an explicit alias of the imported game name `The Wind Waker`.

The scraper reads item-shaped JSON (including comments/trailing commas), literal `Tracker:AddItems` paths,
and literal decimal/hexadecimal Lua item maps. It never executes Lua or downloads URLs from a pack.
Settings/static/internal definitions and modifier-bearing images are manual-only candidates. Progressive
stages remain selectable. Missing/corrupt images produce warnings; packs with no usable icons cannot be saved.
Lua-generated items and rendered image modifiers are not emulated. AP IDs are retained as provenance, not
guessed into log item names. Exact/unique name matches fill gaps; repeat uploads preserve every existing
assignment, including manual **No image** choices. Review conflicts return 409 and require reloading.

Icons are decoded with SkiaSharp and stored as lossless WebP with their original dimensions/transparency;
animated images use their first frame. Runtime packages include Windows and Linux native assets. Saved files
live at `<DataDirectory>/games/{gameId}/{sha256}.webp`. SQLite stores the mapping and provenance, not image
bytes. Staged files live under `games/.staging/{draftId}` and are removed on successful commit or after seven
days at startup/subsequent uploads. Back up `games/` together with the database while the app is stopped.

Approved users can read `GET /api/games`, `GET /api/games/{id}/items` and the image URLs returned by those APIs.
Images use authenticated, same-origin `/api/games/{id}/images/{hash}.webp` responses with ETags and private
revalidation. Draft preview and mutation endpoints are admin-only; writes use the normal CSRF header.
The existing Vite `/api` proxy also handles images. Session item displays do not use these images yet.

Limits: 50 MiB per ZIP, 250 MiB expanded/read budget, 10,000 entries, 16 MiB per processed entry, 16 megapixels
per image, 20,000 candidates, and two minutes per processing request. ZIP paths, links, encryption, duplicate
paths and expanded sizes are checked. One game-data write/processing operation runs at a time per process;
competing operations receive 503 with `Retry-After`. Session-log upload limits are unchanged.

Validation uses builds, type checking, generated API contract checks, code inspection and manual/API
verification with real packs. Do not add tests, fixtures, test scripts or testing dependencies.

## Postponed (deliberately not built)

Archipelago WebSocket connection / recorder / protocol parsing, live session tracking, Pod models,
cross-session analytics, SignalR, background workers, DeathLink, session item-image display, Docker, cloud/Azure deployment, reverse
proxy/TLS/DuckDNS setup, Windows Service installation (it will be a thin hosting configuration, `UseWindowsService`,
on top of the same app), complex roles, an access-request admin UI/CLI (approval is by SQL for now).
