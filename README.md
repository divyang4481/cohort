# Cohort (ASP.NET Core .NET 8 + Angular 21)

This solution hosts **Razor/MVC Areas + Web API + Angular** from the **same ASP.NET Core URL**.

## Projects

- `src/Cohort.Web` – main app host (Razor + Areas + `/api/*` + serves Angular from `wwwroot`)
- `src/Cohort.Idp` – OIDC Identity Provider (OpenIddict + ASP.NET Core Identity)
- `src/Cohort.Shared` – shared constants (policies/claims)

## Architecture (current)

- Single-origin web host: `Cohort.Web` is an ASP.NET Core 8 app that serves Razor/MVC Areas, Web API, and the built Angular SPA from `wwwroot` so UI, API, and static assets share the same origin (`https://localhost:5003`).
- Identity Provider: `Cohort.Idp` is a separate ASP.NET Core Identity + OpenIddict server (`https://localhost:5001`) issuing OIDC cookies/tokens; the web app treats OIDC as external auth and then enforces roles/permissions in its own DB.
- Shared contracts: `Cohort.Shared` holds cross-project constants (policies/claims) referenced by both the web host and the IdP to keep auth policies in sync.
- Data stores: development defaults to a local SQLite DB for `Cohort.Web`; IdP uses its own ASP.NET Core Identity store (also SQLite by default unless overridden).
- Frontend build: Angular 21 lives in `src/Cohort.Web/ClientApp` and compiles into `wwwroot` via `npm run build:dotnet` (or `watch:dotnet`) so the SPA is deployed with the server.

### System Diagram

```mermaid
graph TD
    Browser[Browser / User]

    subgraph "Local Development Environment"
        IdP["Identity Provider (Cohort.Idp)<br/>https://localhost:5001"]
        Web["Web Host (Cohort.Web)<br/>https://localhost:5003"]
        Angular["Angular SPA<br/>(in Cohort.Web wwwroot)"]

        Web <-->|Shared Auth Policies| Shared["Cohort.Shared"]
        IdP <-->|Shared Auth Policies| Shared
    end

    Browser -->|Navigates| Web
    Browser -->|OIDC Redirect| IdP
    Web -->|Serves| Angular
    Angular -->|API Calls (401 handled)| Web
    Web -->|Redirection| IdP
```

## Build Instructions

You can build the components individually or the entire solution.

### Prerequisites

- .NET 8 SDK
- Node.js (Latest LTS recommended)

### 1. Build Entire Solution

```powershell
dotnet build Cohort.sln
```

### 2. Build Components Individually

#### Identity Provider (Cohort.Idp)

```powershell
dotnet build src/Cohort.Idp/Cohort.Idp.csproj
```

#### Web Backend (Cohort.Web)

This builds the ASP.NET Core backend. Note that this **does not** automatically build the Angular frontend unless configured in the csproj (which it is via a simplified exec task, but for explicit control see below).

```powershell
dotnet build src/Cohort.Web/Cohort.Web.csproj
```

#### Angular Frontend (ClientApp)

To build the Angular application and output artifacts to `src/Cohort.Web/wwwroot`:

```powershell
cd src/Cohort.Web/ClientApp
npm install
npm run build:dotnet
```

_Note: `build:dotnet` is a custom script that ensures output goes to the correct wwwroot folder._

## Run (development)

You need **both** the IdP and the Web host running at the same time.

### Option A (recommended): two terminals

1. Terminal 1: start the IdP:

- `dotnet run --launch-profile https --project src/Cohort.Idp`
- URL: `https://localhost:5001`

2. Terminal 2: build Angular into the web host `wwwroot` (single-origin):

- `cd src/Cohort.Web/ClientApp`
- `npm run build:dotnet`

(Optional) Watch-build Angular continuously:

- `npm run watch:dotnet`

3. Start the Web host:

- `dotnet run --launch-profile https --project src/Cohort.Web`
- URL: `https://localhost:5003`

### Option B: start both from repo root (Windows PowerShell)

This opens two separate PowerShell windows:

```powershell
Set-Location "c:\workspace\CTS\cohort"

Start-Process powershell -ArgumentList '-NoExit', '-Command', 'dotnet run --project src\Cohort.Idp'
Start-Process powershell -ArgumentList '-NoExit', '-Command', 'dotnet run --launch-profile https --project src\Cohort.Idp'
Start-Process powershell -ArgumentList '-NoExit', '-Command', 'dotnet run --launch-profile https --project src\Cohort.Web'
```

### Option C: one-command scripts (repo root)

- PowerShell: `./run-dev.ps1`
- Batch: `run-dev.bat`

Both start **Cohort.Idp** and **Cohort.Web** using the `https` launch profiles.

#### Troubleshooting: "address already in use"

If you see an error like `Failed to bind to address http://127.0.0.1:5002: address already in use`, another process is already using that port.

- Close the previous `dotnet run` window, or find and kill the PID:
  - `netstat -ano | findstr :5002`
  - `taskkill /PID <pid> /F`

## Authentication / Areas

- Admin area: `https://localhost:5003/Admin`
- Host area: `https://localhost:5003/Host`
- Participant area (authenticated): `https://localhost:5003/Participant`
- Participant anonymous entry: `https://localhost:5003/Participant/Home/SignIn`

### Seed users (IdP)

The IdP seeds two dev accounts (see `src/Cohort.Idp/appsettings.Development.json`):

- Admin: `admin@example.com` / `Pass123$`
- Host: `host@example.com` / `Pass123$`

### App authorization (Cohort.Web DB)

Admin/Host access is **DB-gated** in the Cohort.Web database:

- OIDC is used only to sign in.
- After sign-in, the app checks the Cohort.Web `AppUsers` table.
- If the user is present and active with `AppRole` = `admin` or `host`, they can access `/Admin` or `/Host`.
- Otherwise, they will see `https://localhost:5003/access/not-authorized`.

For local development, Cohort.Web seeds `admin@example.com` and `host@example.com` into the `AppUsers` table on startup.

## Quick API check

- `GET https://localhost:5003/api/me` (returns 401 unless you are signed in)

## Troubleshooting

### SQLite schema changes (dev)

`Cohort.Web` uses a local SQLite file by default (`cohort-web.db`). If you pull changes that add tables/columns, your existing dev DB may be missing them.

- Stop the app
- Delete `cohort-web.db` from the working directory you run the app from
- Start the app again (it will recreate the DB in Development)
- Test divyang
