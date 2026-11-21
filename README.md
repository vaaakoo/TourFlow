# TourFlow

TourFlow is a portfolio-grade booking and operations platform for tour operators and airport transfer companies. It is being delivered incrementally using Clean Architecture and a modular-monolith design.

## Current scope

The solution contains the TourFlow domain model, SQL Server persistence, and the authentication and authorization API. Live SQL Server verification is tracked in [project status](PROJECT_STATUS.md).

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

After installing the .NET 10 SDK and starting SQL Server, configure the JWT key and apply migrations as described below. Provide the same local-only password to the API through its environment-specific connection string.

```powershell
$env:TOURFLOW_SQL_PASSWORD = "your-local-password"
$env:ConnectionStrings__TourFlowDatabase = "Server=localhost,1433;Database=TourFlow;User Id=sa;Password=$env:TOURFLOW_SQL_PASSWORD;TrustServerCertificate=True;Encrypt=False"
dotnet restore
dotnet build TourFlow.sln
dotnet run --project src/TourFlow.API
```

The health endpoint is `GET /health`; the development OpenAPI document is available at `/openapi/v1.json`.

## Database design

The SQL Server schema is documented in [the Mermaid ER diagram](docs/database/er-diagram.md). The migrations apply schema changes and install the five Identity roles. Demo data is separate from migration SQL. To opt in during development, set both `ASPNETCORE_ENVIRONMENT=Development` and `DevelopmentData__Enabled=true` before running the EF database update. Reapplying the update leaves the existing demo booking graph in place. Demo profiles have no passwords.

### EF Core migrations

Restore the repository-local EF Core tool and apply the initial schema after SQL Server is running:

```powershell
dotnet tool restore
$env:ConnectionStrings__TourFlowDatabase = "Server=localhost,1433;Database=TourFlow;User Id=sa;Password=$env:TOURFLOW_SQL_PASSWORD;TrustServerCertificate=True;Encrypt=False"
dotnet tool run dotnet-ef database update --project src/TourFlow.Infrastructure --startup-project src/TourFlow.API --context TourFlowDbContext
```

Use the same command shape to add future migrations, replacing `database update` with `migrations add <Name>`. The `/health` endpoint includes a SQL Server DbContext check and returns unhealthy when the database cannot be reached.

## Authentication

ASP.NET Core Identity uses the existing `ApplicationUsers` table. Identity role membership drives authorization. JWT bearer validation checks the signature, issuer, audience, expiry, and current account status, security stamp and roles. The five roles are `ADMINISTRATOR`, `MANAGER`, `OPERATOR`, `DRIVER`, and `CUSTOMER`.

Set `Jwt__SigningKey` to a base64-encoded random key of at least 32 bytes using your secret store or environment. Optional settings are `Jwt__Issuer` (default TourFlow), `Jwt__Audience` (default TourFlow.Client), `Jwt__AccessMinutes` (default 10), and `Jwt__RefreshDays` (default 14). List trusted browser origins under `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, and so on. Serve authentication traffic over HTTPS.

Public endpoints under `/api/auth` accept POST requests to `/register`, `/login`, `/refresh`, `/forgot-password`, and `/reset-password`. Registration always grants the customer role. Use the issued access token as `Authorization: Bearer <accessToken>` for `GET /api/auth/me`, `PUT /api/auth/me`, and `POST /api/auth/logout`. The administrator routes are `GET /api/users?page=1`, `GET /api/users/{id}`, and `PUT /api/users/{id}/access`. The access update accepts a numeric `UserRole` and `isDisabled`. Development OpenAPI at `/openapi/v1.json` describes the bearer scheme. Import it into an API client to exercise the protected routes.

Login and refresh return tokens in JSON and set `Cache-Control: no-store`. Send the refresh token in the JSON body as `refreshToken`, and keep it in memory or secure platform storage. The server persists SHA-256 digests. Rotation keeps the original session expiry. Reusing an already rotated token revokes that token family. Clients should serialize refresh requests. Logout revokes the refresh family and issued access tokens can remain valid until expiration. Disabling an account, changing its role, or resetting its password invalidates its existing access and refresh sessions.

Forgot-password returns the same response for all valid requests. The default reset delivery provider is disabled. Use an `IPasswordResetDelivery` implementation that queues secure email delivery before enabling this workflow. Reset delivery also requires an account with a verified email address. Configure a shared, persistent ASP.NET Core Data Protection key ring for multi-instance deployments that enable reset delivery.

## Testing

Run `dotnet test TourFlow.sln` for unit and HTTP integration tests. Set `TOURFLOW_TEST_SQLSERVER` to a connection string for a disposable development SQL Server with permission to create databases to run the SQL-backed cases. Each case creates an isolated `TourFlowTest_*` database on that server and leaves it available for inspection.

## Development-only credentials

After applying the migrations, set `AuthBootstrap__Email` and `AuthBootstrap__Password` through development configuration. Use a dedicated email address and a strong password. Run the following one-time command in the Development environment. The command exits after creating the administrator or finding the same existing active administrator.

```powershell
dotnet run --project src/TourFlow.API -- --AuthBootstrap:Run=true
```

Start the API normally after bootstrap.
