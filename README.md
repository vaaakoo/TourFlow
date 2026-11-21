# TourFlow

TourFlow is a portfolio-grade booking and operations platform for tour operators and airport transfer companies. It is being delivered incrementally using Clean Architecture and a modular-monolith design.

## Current scope

The backend phase is complete through dashboard and reporting. The solution contains SQL Server persistence, authentication, catalog and resource management, booking workflows, assignments, payment records, and management reporting. Verification is tracked in [project status](PROJECT_STATUS.md).

## Architecture

See [the architecture diagram](docs/architecture/overview.md). The backend projects are:

- `src/TourFlow.Domain` — business entities, enums, relationships, exceptions, and domain rules.
- `src/TourFlow.Application` — use cases and application contracts.
- `src/TourFlow.Infrastructure` — EF Core/SQL Server technical implementation.
- `src/TourFlow.API` — HTTP composition root.

## Prerequisites

- .NET 10 SDK
- SQL Server, either a native installation or the Docker Compose service

Angular CLI is not required until the frontend milestone.

## Run SQL Server locally

1. Copy `.env.example` to `.env` and set a strong local development password.
2. Start the database: `docker compose up -d sqlserver`.
3. Verify the container: `docker compose ps`.

The API development setting expects SQL Server at `localhost,1433`. It is intended only for local development. Production secrets must be supplied by environment-specific configuration.

For native SQL Server with Windows authentication, use `Server=localhost;Database=TourFlow;Trusted_Connection=True;TrustServerCertificate=True` for `ConnectionStrings__TourFlowDatabase` in local development.

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

Send both `role` and `isDisabled` in every administrator access update. Use an explicit `false` to enable an account.

Login and refresh return tokens in JSON and set `Cache-Control: no-store`. Send the refresh token in the JSON body as `refreshToken`, and keep it in memory or secure platform storage. The server persists SHA-256 digests. Rotation keeps the original session expiry. Reusing an already rotated token revokes that token family. Clients should serialize refresh requests. Logout revokes the refresh family and issued access tokens can remain valid until expiration. Disabling an account, changing its role, or resetting its password invalidates its existing access and refresh sessions.

Forgot-password returns the same response for all valid requests. The default reset delivery provider is disabled. Use an `IPasswordResetDelivery` implementation that queues secure email delivery before enabling this workflow. Reset delivery also requires an account with a verified email address. Configure a shared, persistent ASP.NET Core Data Protection key ring for multi-instance deployments that enable reset delivery.

## Operations API

Destination and tour list and detail routes are public. Public results include active destinations and active tours at active destinations. Operators can view inactive catalogue entries and read the vehicle and driver directories. Managers and administrators can also create, update, and deactivate catalogue, vehicle, and driver records. The three staff roles can list, view, and update customer profiles. Customers can read `/api/customers/me` and update their profile through `PUT /api/auth/me`. Drivers have their own profile through `/api/auth/me`.

| Routes | Methods |
| --- | --- |
| `/api/destinations`, `/api/tours`, `/api/vehicles`, `/api/drivers` | GET list, POST create |
| `/api/destinations/{id}`, `/api/tours/{id}`, `/api/vehicles/{id}`, `/api/drivers/{id}` | GET detail, PUT update, DELETE deactivate |
| `/api/customers`, `/api/customers/{id}`, `/api/customers/me` | GET list, GET/PUT by ID for staff, GET own profile |
| `/api/vehicles/{id}/availability`, `/api/drivers/{id}/availability` | GET availability for a same-day interval |

Lists accept `page`, `pageSize` (default 20, maximum 100), `search`, `sort`, and `descending`. Responses include `items`, `page`, `pageSize`, and `totalCount`. Available sort keys are `name/country/active` for destinations, `name/price/duration/createdAt` for tours, `name/email/createdAt` for customers, `name/registrationNumber/capacity` for vehicles, and `name/licenseNumber/active` for drivers.

Example queries:

- `GET /api/destinations?country=GE&search=Kutaisi&sort=name&pageSize=10`
- `GET /api/tours?minPrice=25&maxPrice=100&sort=price&descending=true&page=2`
- `GET /api/customers?search=Beridze&createdFrom=2026-11-01`
- `GET /api/vehicles?minCapacity=8&isActive=true`
- `GET /api/vehicles/{id}/availability?date=2026-12-05&start=10:00&end=12:00&passengers=4`

Destinations accept an English country name or ISO country code. Tour prices use two decimal places, and tour durations are supplied in minutes. Vehicle capacity reductions are checked against current and future assignments. Availability is a read of existing assignments and does not reserve a slot.

To create a driver profile, first have an administrator provision an enabled Identity account with the `DRIVER` role, then pass its `userId` to `POST /api/drivers`. A manager can create the operational profile for this account. Driver deactivation affects assignments and availability. Account login access is managed separately. Customer email comes from Identity, and customer phone comes from the customer profile.

## Bookings, assignments, and payments

| Routes | Access |
| --- | --- |
| `GET/POST /api/bookings`, `GET/PUT /api/bookings/{id}` | Customers own data; staff operational access |
| `GET /api/bookings/{id}/history`, `PUT /api/bookings/{id}/status` | Owner/staff/assigned driver according to transition policy |
| `POST /api/bookings/{id}/confirm`, `POST /api/bookings/{id}/cancel` | Staff confirmation; eligible owner or staff cancellation |
| `POST /api/bookings/{id}/assign`, `DELETE /api/bookings/{id}/assignments/{assignmentId}` | Manager or administrator |
| `GET /api/assignments` | Staff see operations; drivers see only their assignments |
| `GET/POST /api/payments`, `GET /api/payments/{id}`, `PUT /api/payments/{id}/status` | Administrator, manager, or operator |

Booking lists support paging, search, status/type/date/customer filters, and allowlisted sorting. The API calculates prices: tour bookings use the current tour price multiplied by passenger count, while transfers use `BookingPricing:TransferBasePrice + TransferPerPassenger × passengers`. Configure the formula and three-letter currency through environment-specific configuration.

Booking write responses include a base64 `version`. Send it back for itinerary updates (`?version=...`), status bodies, assignment bodies, and assignment cancellation (`?version=...`). A stale value returns HTTP 409. Assignment writes use SQL Server transactions and application locks to prevent overlapping driver or vehicle intervals under concurrency. Reassignment and cancellation retain inactive assignment history.

Payments are simulated records only. Amounts cannot exceed the booking total, currency must match the configured booking currency, and transaction references are unique. Valid status changes are pending to paid or failed, and paid to refunded. Never send or store card data.

## Dashboard and reports

Managers and administrators can use:

- `GET /api/dashboard?from=2027-01-01&to=2027-01-31`
- `GET /api/reports/bookings`
- `GET /api/reports/revenue`
- `GET /api/reports/drivers`
- `GET /api/reports/vehicles`

The dashboard returns booking totals by status, paid revenue, customer and active-resource counts, recent bookings, popular tours, and upcoming assignments. Reports accept optional inclusive `from` and `to` service dates and use database-side SQL Server aggregation.

## Testing

Run `dotnet test TourFlow.sln` for unit and HTTP integration tests. Set `TOURFLOW_TEST_SQLSERVER` to a connection string for a disposable development SQL Server with permission to create databases to run the SQL-backed cases. For native Windows authentication, use `Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=5`. Each SQL case creates an isolated `TourFlowTest_*` database and leaves it available for inspection.

## Development-only credentials

After applying the migrations, set `AuthBootstrap__Email` and `AuthBootstrap__Password` through development configuration. Use a dedicated email address and a strong password. Run the following one-time command in the Development environment. The command exits after creating the administrator or finding the same existing active administrator.

```powershell
dotnet run --project src/TourFlow.API -- --AuthBootstrap:Run=true
```

Start the API normally after bootstrap.
