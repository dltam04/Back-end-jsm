MovieApi/                        ← project root
├─ Controllers/
│  └─ MoviesController.cs        ← HTTP endpoints (routes, input/output, status codes)
│
├─ Models/
│  ├─ Movie.cs                   ← Entity/DTO classes (data shapes)
│  └─ MovieContext.cs            ← EF Core DbContext (DB connection + entity mapping)
│
├─ Migrations/                   ← EF migrations (schema history for code-first)
│  └─ <timestamp>_Init...cs      ← “diffs” that create/alter tables when applied
│
├─ Properties/
│  └─ launchSettings.json        ← Local run profiles (Project/Kestrel, IIS Express, ports)
│
├─ appsettings.json              ← Base configuration (no secrets)
├─ appsettings.Development.json  ← Dev overrides (no user secrets)
├─ Program.cs                    ← Startup: DI registrations, middleware pipeline, routing
├─ MovieApi.http                 ← Handy REST test file for VS (send requests from editor)
├─ MovieApi.csproj               ← Build settings, target framework, package refs
└─ .gitignore                    ← What Git should ignore (bin/obj/.vs, etc.)
