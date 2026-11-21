# Project status

## Current milestone

Milestone 4 — authentication, JWT, and authorization: implemented and partially verified.

## Milestones completed

### Milestone 1 — project setup and architecture

- Created the Clean Architecture solution and four backend projects.
- Set dependency direction and DI extension foundations.
- Added SQL Server connection configuration, Docker Compose, OpenAPI, health endpoint, and global exception handling foundation.
- Added architecture and database-design documentation.
- Completed a static Milestone 1 review and corrected explicit dependency declarations and local-secret configuration.

### Milestone 2 — domain entities and database design
- Implemented the Milestone 2 pure Domain model: `ApplicationUser`, `Customer`, `Driver`, `Vehicle`, `Destination`, `Tour`, `Booking`, `BookingAssignment`, `BookingStatusHistory`, `Payment`, and `RefreshToken`.
- Added domain enums for user roles, booking type/status, payment method/status, vehicle type, and tour type.
- Added domain invariants for required values, positive capacities/passenger counts, monetary values, UTC timestamps, tour capacity, inactive resource assignment, payment lifecycle, refresh-token validity, and explicit booking status transitions.
- Updated the database ER diagram and documented planned database constraints, indexes, delete behaviors, precision, and timestamps for Milestone 3.

### Milestone 3 — EF Core, database configuration, migrations, and seed data

- Added `TourFlowDbContext` DbSets, SQL Server registration, DbContext health check, explicit EF Core Fluent configurations, UTC converters, precision, relationships, delete behaviors, unique constraints, and operational indexes.
- Added the `InitialCreate` migration and model snapshot.
- Added deterministic development seed records for application profiles, one customer, one driver, one vehicle, one destination, one tour, one assigned booking, booking status history, and payment. No passwords, refresh tokens, or authentication credentials are seeded.
- Added repository-local `dotnet-ef` tooling and the required private EF Core Design dependencies.
- Added private materialization constructors to Domain entities so EF Core can hydrate the existing invariant-protected model without changing public behavior.

## Verification

- `dotnet restore TourFlow.sln` succeeded with no warnings or errors.
- `dotnet build TourFlow.sln --no-restore` succeeded with 0 warnings and 0 errors.
- `dotnet list TourFlow.sln package --vulnerable --include-transitive` reported no vulnerable packages.
- NuGet security remediation: updated the direct Microsoft platform packages to the 10.0.14 servicing release. `Microsoft.AspNetCore.OpenApi 10.0.14` resolves patched `Microsoft.OpenApi 2.12.2`; `System.Security.Cryptography.Pkcs 10.0.14` is an explicit security pin because SQL Client's compatible dependency otherwise resolves a vulnerable 9.x package.
- Angular CLI and Docker CLI are not installed or not on `PATH`.
- `dotnet build TourFlow.sln --no-restore` succeeded after Milestone 2 with 0 warnings and 0 errors.
- No test projects exist yet; consequently, no automated tests apply in this milestone.
- `dotnet restore TourFlow.sln`, `dotnet build TourFlow.sln --no-restore`, and initial migration generation succeeded in Milestone 3.
- Docker Desktop is installed but its daemon was unavailable on 2026-11-21. Database update, seeded-data verification, and SQL Server health-check runtime validation remain pending until the daemon is started.

## Milestone 3 audit

- Verified project references, SQL Server mappings, schema constraints, timestamps, money precision, and design-time migration consistency. The solution built cleanly, and EF generated an idempotent SQL script.
- Removed unconditional demo inserts from `InitialCreate` for fresh installs. Databases that already applied the original migration retain existing data. Demo inserts now require Development and `DevelopmentData__Enabled=true`, and the deterministic booking ID guards repeat runs.
- Added `ProtectBookingRecords` to replace cascading booking foreign keys with restrictive keys for payments, assignments, and status history.
- SQL Server application, migration replay, and live seed verification remain pending because the Docker daemon is unavailable and no local SQL Server instance is listening.

## Milestone 4 implementation and validation

- Integrated Identity with the existing `ApplicationUsers` table and operational user relationships. `AddIdentityAuthentication` adds credential and normalized lookup columns, Identity tables, five roles, and refresh-token family tracking. It backfills the existing user records and role memberships and invalidates any pre-existing refresh tokens.
- Added registration, login, refresh, logout, authenticated profile, reset foundation, and administrator user management. Authorization uses persisted Identity roles, server-side JWT account validation, and an administrator guard for account-access changes. Public registration creates customer accounts only.
- Refresh tokens are opaque, hashed at rest, and rotated transactionally. SQL Server per-account locks serialize rotation, logout, and account updates. Reuse revokes the affected family.
- The solution builds with zero warnings and errors. Three unit tests and 13 HTTP integration tests passed. Three SQL-backed tests were skipped because `TOURFLOW_TEST_SQLSERVER` is unset. EF reported no pending model changes and generated an idempotent script for all three migrations. NuGet reported no vulnerable packages.
- The development API started and served OpenAPI with the JWT bearer scheme. The health endpoint returned HTTP 503 with SQL Server unavailable. An EF database update targeting a dedicated local validation database timed out before connecting. The live Identity migration, database-backed auth flows, refresh concurrency, and healthy SQL Server response remain unverified.
- Password reset requires a configured delivery provider and a verified email. The default provider leaves delivery disabled. Administrator credentials are supplied only for an explicit Development bootstrap command.

## Next milestone (requires approval)

Milestone 5 — Tours, Destinations, Customers, Vehicles and Drivers. The Milestone 4 audit passed build, design-time migration checks, and HTTP checks. Its database pass is pending live SQL Server verification. Run the three SQL-backed tests and verify a healthy database response before starting Milestone 5.
