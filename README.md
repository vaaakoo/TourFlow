# TourFlow

TourFlow is a portfolio-grade booking and operations platform for tour operators and airport transfer companies. It is being delivered incrementally using Clean Architecture and a modular-monolith design.

## Current scope

Milestones 1 and 2 are complete: the solution foundation and pure Domain model are in place. The Domain layer contains the TourFlow entities, value enums, entity relationships, and core booking/assignment/payment rules. EF Core mappings, Identity configuration, APIs, and user interfaces remain deliberately deferred to their scheduled milestones.

## Architecture

See [the architecture diagram](docs/architecture/overview.md). The backend projects are:

- `src/TourFlow.Domain` — business entities, enums, relationships, exceptions, and domain rules.
- `src/TourFlow.Application` — use cases and application contracts.
- `src/TourFlow.Infrastructure` — EF Core/SQL Server technical implementation.
- `src/TourFlow.API` — HTTP composition root.

## Prerequisites

- .NET 10 SDK
- Docker Desktop (for local SQL Server)

Angular CLI is not required until the frontend milestone.

## Run SQL Server locally

1. Copy `.env.example` to `.env` and set a strong local development password.
2. Start the database: `docker compose up -d sqlserver`.
3. Verify the container: `docker compose ps`.

The API development setting expects SQL Server at `localhost,1433`. It is intended only for local development. Production secrets must be supplied by environment-specific configuration.

## Run the API

After installing the .NET 10 SDK and starting SQL Server, provide the same local-only password to the API through its environment-specific connection string:

```powershell
$env:TOURFLOW_SQL_PASSWORD = "your-local-password"
$env:ConnectionStrings__TourFlowDatabase = "Server=localhost,1433;Database=TourFlow;User Id=sa;Password=$env:TOURFLOW_SQL_PASSWORD;TrustServerCertificate=True;Encrypt=False"
dotnet restore
dotnet build TourFlow.sln
dotnet run --project src/TourFlow.API
```

The health endpoint is `GET /health`; the development OpenAPI document is available at `/openapi/v1.json`.

## Database design

The implemented model and Milestone 3 persistence decisions are captured in [the Mermaid ER diagram](docs/database/er-diagram.md). EF Core entity configurations and migrations begin in Milestone 3.

## Testing

Test projects and test suites are scheduled for Milestone 12. Build the solution with `dotnet build TourFlow.sln` after each milestone.

## Development-only credentials

No application accounts exist in Milestone 1. Development administrator seeding is scheduled with Identity implementation; no production password will be committed.
