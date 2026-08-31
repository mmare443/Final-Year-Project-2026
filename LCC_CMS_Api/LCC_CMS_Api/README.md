# LCC-CMS Api (ASP.NET Core Web API)

**Note on how this was built:** I don't have the .NET SDK available in my
own environment, so unlike the React SPA (which I built, compiled, and
verified), I wrote these files by hand to match the exact structure
`dotnet new webapi` + the required packages would produce. Follow the
verification steps below carefully — if something doesn't compile, tell me
the exact error and I'll fix it from that, rather than guessing.

## First-time setup

```bash
cd LCC_CMS_Api
dotnet restore
dotnet build
```

`dotnet restore` needs internet access to NuGet to pull the 4 packages
listed in `LCC_CMS_Api.csproj` (EF Core SqlServer/Tools/Design,
Microsoft.Identity.Web, Swashbuckle). If `dotnet build` reports errors,
share the exact output and I'll fix the project files.

## Run it

```bash
dotnet run
```

You should see something like:
```
Now listening on: https://localhost:7xxx
Now listening on: http://localhost:5xxx
```

## Verify it's actually working

1. Open `https://localhost:7xxx/swagger` in a browser (use the port your
   terminal printed) — you should see the Swagger UI listing two
   controllers: `Health` and `Admissions`.
2. Try `GET /api/health` — expect `{ "status": "ok", ... }`.
3. Try `GET /api/admissions` — expect `[]` (empty array, no applications
   submitted yet — this is a separate in-memory store from the React app's
   mock data, they aren't connected yet).

If Swagger doesn't load or either of these fails, that's exactly the kind
of concrete error to bring back — "swagger loads but /api/health returns a
500" is something I can actually debug; "it doesn't work" isn't.

## What's deliberately stubbed right now

- **`AuthEnabled: false`** in `appsettings.json` — the whole app runs with
  no authentication enforced at all. This is intentional: it lets you build
  and test every controller before Entra ID access is sorted out. Once your
  Azure/GCP decision lands and Entra ID app registrations exist (Backend
  Scaffold Guide v2, Step 3), set `AuthEnabled: true` in
  `appsettings.Development.json` and fill in the real `TenantId`/`ClientId`.
- **No database connection wired in yet** — `Program.cs` has the
  `AddDbContext` line commented out, because `LccCmsDbContext` doesn't
  exist yet. That gets scaffolded from a live SQL Server (local Docker or
  cloud) per Step 2 of the Backend Scaffold Guide — once you run that
  scaffold command, uncomment that line and connect the two.
- **`AdmissionsController`** uses an in-memory `List<>` instead of the
  database — same reason. The method signatures and response shapes
  already match the Module Specification's M1 process and the React app's
  mock data shape, so swapping the storage layer later doesn't change the
  API contract.

## Connecting this to the React SPA later
Once both are running (API on its https port, SPA on :5173), the SPA's
`MockDataContext.jsx` gets its `addApplication`/`decideApplication`
functions replaced with `fetch()` calls to this API's
`/api/admissions` endpoints — see that file's own header comment for the
exact plan.
