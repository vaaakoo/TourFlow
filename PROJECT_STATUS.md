# Project status

## Current milestone

Milestone 3 — EF Core, database configuration, migrations, and seed data: complete with database-runtime validation pending Docker Desktop.

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

## Next step (requires approval)

Milestone 4: authentication, JWT, refresh tokens, and authorization. Identity credential persistence and safe development administrator credential seeding begin in that milestone; booking assignment overlap enforcement remains in the later assignment workflow milestone.
