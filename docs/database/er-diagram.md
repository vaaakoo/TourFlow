# Domain database design

The operational schema uses `InitialCreate` and `ProtectBookingRecords`. `AddIdentityAuthentication` extends the same user table with Identity and adds role membership and refresh-token family tracking. Demo data runs through opt-in Development seeding.

```mermaid
erDiagram
    APPLICATION_USER ||--o| CUSTOMER : has_profile
    APPLICATION_USER ||--o| DRIVER : has_profile
    APPLICATION_USER ||--o{ REFRESH_TOKEN : owns
    APPLICATION_USER ||--o{ BOOKING_STATUS_HISTORY : changes
    APPLICATION_USER ||--o{ ASPNET_USER_ROLE : has_membership
    ASPNET_ROLE ||--o{ ASPNET_USER_ROLE : grants
    CUSTOMER ||--o{ BOOKING : creates
    DESTINATION ||--o{ TOUR : contains
    TOUR o|--o{ BOOKING : booked_for
    BOOKING ||--o{ BOOKING_ASSIGNMENT : fulfilled_by
    DRIVER ||--o{ BOOKING_ASSIGNMENT : fulfils
    VEHICLE ||--o{ BOOKING_ASSIGNMENT : uses
    BOOKING ||--o{ BOOKING_STATUS_HISTORY : tracks
    BOOKING ||--o{ PAYMENT : has

    APPLICATION_USER {
        guid Id PK
        string Email UK
        string NormalizedEmail UK
        string NormalizedUserName UK
        enum Role
    }
    ASPNET_ROLE {
        guid Id PK
        string NormalizedName UK
    }
    ASPNET_USER_ROLE {
        guid UserId PK, FK
        guid RoleId PK, FK
    }
    CUSTOMER {
        guid Id PK
        guid UserId FK
    }
    DRIVER {
        guid Id PK
        guid UserId FK
        string LicenseNumber UK
    }
    DESTINATION {
        guid Id PK
    }
    TOUR {
        guid Id PK
        guid DestinationId FK
        decimal Price
    }
    BOOKING {
        guid Id PK
        string BookingNumber UK
        guid CustomerId FK
        guid TourId FK "nullable"
        decimal TotalPrice
        enum Status
    }
    BOOKING_ASSIGNMENT {
        guid Id PK
        guid BookingId FK
        guid DriverId FK
        guid VehicleId FK
        date AssignmentDate
        time StartTime
        time EndTime
    }
    BOOKING_STATUS_HISTORY {
        guid Id PK
        guid BookingId FK
        guid ChangedByUserId FK
    }
    PAYMENT {
        guid Id PK
        guid BookingId FK
        decimal Amount
    }
    REFRESH_TOKEN {
        guid Id PK
        guid UserId FK
        guid FamilyId
        string TokenHash UK
    }
```

## Implemented database design

- All primary keys are application-generated `Guid` values. Every entity inherits UTC `CreatedAt`; mutable entities also retain `UpdatedAt`. EF Core converters restore `DateTimeKind.Utc` when SQL Server `datetime2` values are read.
- `Booking.TourId` is nullable only for `Transfer` bookings. A `Tour` booking requires a tour at the domain level.
- Unique constraints are implemented for `ApplicationUser.Email`, `Customer.UserId`, `Driver.UserId`, `Driver.LicenseNumber`, `Vehicle.RegistrationNumber`, `Booking.BookingNumber`, `Payment.TransactionReference` when present, and `RefreshToken.TokenHash`.
- Operational indexes are implemented for active tour/destination filters, customer booking history, booking date/status, and driver/vehicle assignment schedules. The scheduling indexes support overlap queries; interval-overlap enforcement remains an application/database transaction concern for a later operational milestone.
- Booking status history, payment and assignment foreign keys prevent deletion of referenced bookings. Customers, tours, drivers, vehicles, destinations, users, and status-change actors are also protected from deletion while operational records reference them.
- Monetary values (`Tour.Price`, `Booking.TotalPrice`, and `Payment.Amount`) use SQL Server `decimal(18,2)`. `DateOnly` and `TimeOnly` are stored as `date` and `time(0)`; tour duration is stored as ticks in `bigint` to avoid SQL Server's 24-hour `time` limit.
- Private materialization constructors support EF Core. Public constructors retain domain validation. `ApplicationUser` derives from the lightweight Identity user model so profile relationships and Identity credentials share the same user key.
- The refresh-token table indexes hashed tokens and user/session families. SQL Server transaction-owned per-user locks serialize token rotation and revocation.
