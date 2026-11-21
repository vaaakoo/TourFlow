# Project status

## Current milestone

Consolidated backend phase (Milestones 1–8): implemented and verified against native SQL Server.

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

Milestone 9 — Angular customer portal. Frontend work has not started.

## Milestone 4 follow-up audit

- Administrator access updates now require an explicit disabled state. An omitted field can no longer re-enable an account. Malformed HTTP request bodies retain their client-error status and receive a generic ProblemDetails response.
- Build passed with zero warnings and errors. Three unit tests and 20 HTTP integration tests passed. Coverage includes all five roles on the administrator route, JWT issuer, audience, signature, expiry, server-side session rejection, profile ownership, malformed requests, OpenAPI, and unhealthy database readiness.
- EF found no pending model changes. The audit requires no new migration.
- Neither Docker context can reach its engine. No local SQL Server listener or configured `TOURFLOW_TEST_SQLSERVER` connection is available. Three SQL-backed tests were skipped. Live migrations and seed replay, registration and login, session rotation and concurrent replay, and healthy database readiness remain pending.

### Live SQL Server follow-up

- Native SQL Server accepted Windows authentication. All three SQL-backed authentication tests passed against isolated `TourFlowTest_*` databases, including migration and seed reapplication, registration and login, account restrictions, token rotation, concurrent replay, and logout.
- The running API returned HTTP 200 from `/health` against a migrated test database. This validation used no Docker or WSL.

## Milestone 5 implementation and validation

- Added MediatR requests and handlers for destinations, tours, customers, vehicles, and drivers. FluentValidation checks inputs, filter ranges, page bounds, and each module's supported sort fields. SQL Server applies search, filters, sorting, paging, and DTO projections.
- Added public catalogue listing and detail routes with active-only visibility, manager/administrator writes, staff customer management, JWT-scoped customer self-service, and staff vehicle and driver directories. Driver profile creation uses an existing eligible Identity account. Authentication profiles use the active role to distinguish a driver profile from any retained customer history.
- Added deactivation endpoints that preserve referenced records, SQL-backed uniqueness handling, vehicle capacity protection for active assignments, and read-only vehicle and driver availability checks.
- Reused existing tables, constraints, and indexes. EF found no pending model changes. Existing migrations and seed replay were exercised on isolated local SQL Server databases.
- Solution build passed with zero warnings and errors. Ten unit tests and 28 integration tests passed, with zero skipped. The integration suite exercised both authentication regressions and the five new modules, including authorization, filtering, pagination, uniqueness, profile isolation, availability, OpenAPI, and health.

## Milestones 6–8 — completed backend

- Implemented customer and staff booking creation, detail/history, operational search/filter/sort/pagination, itinerary updates, cancellation, confirmation, rejection, and explicit status transitions. Tour prices use the current server-side per-passenger price. Transfer prices use configurable server-side base and per-passenger rates; the default development currency is USD.
- Added SQL Server `rowversion` optimistic concurrency for booking writes. Clients return the opaque base64 `version` value when updating itineraries, status, or assignments. Stale writes return HTTP 409.
- Enforced booking ownership from JWT identity claims. Customers see and modify only their own eligible bookings; operators have operational booking authority; managers and administrators assign resources; drivers see their own assignments and may only start or complete their assigned trips.
- Implemented transactional assignment, reassignment, and cancellation. SQL Server application locks and serializable overlap checks prevent concurrent driver/vehicle conflicts. Capacity and active-resource rules are enforced. Superseded assignments remain as inactive history.
- Booking status history is append-only and records actor, UTC timestamp, previous/new states, and comments. Assignment and cancellation workflows create lifecycle history through the aggregate.
- Implemented internal payment records, payment search/history, ISO currency and amount validation, booking-total limits, unique transaction references, and explicit pending-to-paid/failed and paid-to-refunded transitions. No gateway or card data is stored.
- Added management dashboard totals, status counts, revenue, recent bookings, popular tours, active resources, and upcoming assignments. Added date-ranged booking, revenue, driver assignment, and vehicle utilization reports using SQL projections and aggregation.
- Added migration `CompleteBackendOperations` for booking row versions, payment currency, and retained assignment cancellation state. Existing payment and assignment rows are safely backfilled to `USD` and active respectively. No data reset is required.
- Native SQL Server verification applies every migration twice in isolated databases and exercises seeding, auth regressions, booking ownership and server pricing, stale writes, assignment overlap/history, driver status permissions, payment consistency, reports, OpenAPI, and health. Final verification: build 0 warnings/0 errors; 15 unit tests and 32 integration tests passed; 0 skipped; EF Core reported no pending model changes.

## Backend limitations and boundaries

- Transfer pricing is an internal configurable formula (`BookingPricing:TransferBasePrice`, `TransferPerPassenger`, and `Currency`), not a distance-based quote engine.
- Payments are simulated operational records only. There is no payment gateway and no cardholder data.
- Assignment intervals are same-day half-open ranges. Multi-day scheduling and route/travel-time optimization are outside the current specification.
- Reports return current aggregate rows and do not provide exports or accounting/tax reconciliation.
