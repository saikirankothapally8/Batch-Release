# 9. Hosting on IIS (Windows Server)

The API and the Angular app are deployed together as **one IIS site**: the API serves the app from its
`wwwroot` folder, so there is no separate front-end site and no CORS to configure.

## 1. Prepare the server (once)
1. **IIS** with the *Web Server (IIS)* role. No extra ASP.NET features are needed.
2. **ASP.NET Core 8 Hosting Bundle** – download "Hosting Bundle" for .NET 8 from https://dotnet.microsoft.com/download/dotnet/8.0
   and install it **after** IIS. Then restart IIS from an administrator prompt:
   ```
   net stop was /y
   net start w3svc
   ```
3. **Database** reachable from the server: PostgreSQL or SQL Server, with an empty database created
   (for example `batchreleaseiq`). The tables are created automatically on first start, or a DBA can run
   `database/postgresql-schema.sql` or `database/sqlserver-schema.sql` first.
4. An **HTTPS certificate** for the host name users will type (internal CA certificate is fine).

## 2. Build the deployment package (on a build machine with Node.js 20+ and the .NET 8 SDK)
```
cd frontend
npm ci
npm run publish:api                 :: builds Angular and copies it into backend\src\BatchReleaseIQ.Api\wwwroot

cd ..\backend
dotnet publish src\BatchReleaseIQ.Api -c Release -o C:\deploy\BatchReleaseIQ
```
`C:\deploy\BatchReleaseIQ` now contains `BatchReleaseIQ.Api.dll`, `web.config` (generated for IIS), `appsettings.json`
and `wwwroot\`. Copy this folder to the server, e.g. `D:\Sites\BatchReleaseIQ`.

**Hosting under a sub-path** (e.g. `https://server/batchrelease` as an application inside an existing site) –
build the front end with that base path instead of `npm run publish:api`:
```
npx ng build --base-href /batchrelease/
node scripts/copy-to-api.mjs
```
IIS passes the sub-path to the application automatically.

## 3. Configure (settings are required in Production)
Development settings are not deployed. Create **`appsettings.Production.json`** next to `BatchReleaseIQ.Api.dll`
on the server (keep it out of source control):
```json
{
  "Database": { "Provider": "PostgreSQL", "SeedSampleData": false },
  "Bootstrap": { "AdminPassword": "<first administrator password>" },
  "ConnectionStrings": { "Default": "Host=dbserver;Port=5432;Database=batchreleaseiq;Username=brq_app;Password=..." },
  "Jwt": { "SigningKey": "<random string of at least 32 characters>", "ExpiryMinutes": 60 },
  "Site": { "TimeZone": "India Standard Time" }
}
```
- **SQL Server**: `"Provider": "SqlServer"` and e.g.
  `"Server=dbserver;Database=batchreleaseiq;Trusted_Connection=True;TrustServerCertificate=True"` (Windows
  authentication – see the app pool identity below) or `User Id=...;Password=...`.
- **`Jwt:SigningKey` is mandatory** – the app refuses to start without it. Generate one in PowerShell:
  `[Convert]::ToBase64String((1..48 | % { Get-Random -Max 256 }))`. Changing it signs everyone out.
- **`SeedSampleData`**: `true` loads the demo batches and demo users into an *empty* database (good for a demo
  server). `false` is for a real system: an empty database gets only the departments, the standard roles, the
  prioritisation settings and one **`admin`** account whose password is `Bootstrap:AdminPassword` (at least 8
  characters with letters and digits). Sign in as `admin`, create the real users, master data and templates, then
  remove `Bootstrap:AdminPassword` from the file. Both options run only when the database has no users yet.
- **Time zone**: Windows IDs (`India Standard Time`) and IANA IDs (`Asia/Kolkata`) both work.

Alternatively, set the same values as environment variables in IIS (*Configuration Editor →
system.webServer/aspNetCore → environmentVariables*), using `__` for nesting, e.g. `Jwt__SigningKey`,
`ConnectionStrings__Default`.

## 4. Create the IIS site
1. **Application pool** – *Add Application Pool*: name `BatchReleaseIQ`, **.NET CLR version: No Managed Code**,
   pipeline *Integrated*. In *Advanced Settings*: *Start Mode = AlwaysRunning*, *Idle Time-out = 0*
   (the first start creates tables and may take a moment), *Load User Profile = True*.
2. **Site** – *Add Website*: name `BatchReleaseIQ`, application pool `BatchReleaseIQ`, physical path
   `D:\Sites\BatchReleaseIQ`, binding **https**, port 443, host name (e.g. `batchrelease.company.local`) and the
   certificate. (For a sub-path: right-click an existing site → *Add Application*, alias `batchrelease`, same pool and path.)
3. **Folder permissions** – give `IIS AppPool\BatchReleaseIQ` *Read & execute* on `D:\Sites\BatchReleaseIQ`, and
   *Modify* on a `logs` sub-folder if you enable stdout logging.
4. **Database login for Windows authentication (SQL Server)** – create a login for `IIS AppPool\BatchReleaseIQ`
   (on another server: the machine account `DOMAIN\SERVERNAME$`) and grant it `db_owner` on the database for the
   first start (it creates tables), then reduce to `db_datareader` + `db_datawriter`.

Browse to `https://batchrelease.company.local/` – the login page appears. `/api/health` returns `{"status":"ok"}`.

## 5. Updating to a new version
1. Build the package again (step 2).
2. Put an `app_offline.htm` file in the site folder (IIS stops the app and releases the files).
3. Copy the new files over the old ones – **do not overwrite `appsettings.Production.json`**.
4. Delete `app_offline.htm`.

## 6. Removing or loading the sample data
Two commands empty the database and set it up again. They use the same settings as the site
(`appsettings.json` + `appsettings.Production.json`), so run them from the site folder:
```
cd "D:\Sites\BatchReleaseIQ"
dotnet BatchReleaseIQ.Api.dll --remove-sample-data
dotnet BatchReleaseIQ.Api.dll --load-sample-data
```
| Command | Result |
|---|---|
| `--remove-sample-data` | Empty system: departments, standard roles, prioritisation settings and one `admin` user. The admin password comes from `--admin-password <password>`, else `Bootstrap:AdminPassword`, else you are asked for it |
| `--load-sample-data` | The full demo data again: about 260 batches, demo users (password `Passw0rd!`), master data and templates |

- Both **delete all data in the application's tables, including the audit trail and signatures**. Use them on a demo,
  test or newly installed system, never on a database holding real GxP records.
- They ask you to type the database name to confirm; a wrong name changes nothing. For scripts, pass it with
  `--confirm <database name>`.
- Stop the site first (put `app_offline.htm` in the folder, or stop the application pool), then start it again afterwards.
- Only this application's tables are dropped and recreated; other objects in the same database are left alone.

## Troubleshooting
| Symptom | Cause and fix |
|---|---|
| **HTTP Error 500.19** | Hosting Bundle not installed, or installed before IIS – reinstall it and restart IIS |
| **HTTP Error 500.30 / 500.31** "failed to start" | The app stopped at startup. Most often: `Jwt:SigningKey` missing, database unreachable or wrong credentials. See the cause in *Event Viewer → Windows Logs → Application*, or set `stdoutLogEnabled="true"` in `web.config` (create the `logs` folder) and read `logs\stdout_*.log` |
| Page says "Batch Release IQ API is running … not been built" | `wwwroot` is missing – run `npm run publish:api` before `dotnet publish` |
| Blank page or 404 for `main-*.js` under a sub-path | Front end built without `--base-href /<alias>/` |
| Login works but you are signed out on every page | Different `Jwt:SigningKey` on servers behind a load balancer – use the same key everywhere |
| Viewing works but saving fails with **405 Method Not Allowed** (`Allow: GET, HEAD, OPTIONS, TRACE`) | IIS WebDAV blocks PUT/DELETE. The `web.config` shipped since this version removes it; for an older deployment add `<remove name="WebDAVModule" />` under `<modules>` and `<remove name="WebDAV" />` under `<handlers>` in `web.config` (or uninstall the *WebDAV Publishing* IIS feature) |
| `/swagger` returns 404 | Expected – the API explorer is only enabled in Development |
