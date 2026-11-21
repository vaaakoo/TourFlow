# Domain database design

This diagram reflects the EF Core schema implemented in Milestone 3. The initial migration is `InitialCreate`; it includes deterministic development data but no authentication credentials.

```mermaid
erDiagram
    APPLICATION_USER ||--o| CUSTOMER : has_profile
    APPLICATION_USER ||--o| DRIVER : has_profile
    APPLICATION_USER ||--o{ REFRESH_TOKEN : owns
    APPLICATION_USER ||--o{ BOOKING_STATUS_HISTORY : changes
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
        enum Role
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
        string TokenHash UK
    }
```

## Implemented database design

- All primary keys are application-generated `Guid` values. Every entity inherits UTC `CreatedAt`; mutable entities also retain `UpdatedAt`. EF Core converters restore `DateTimeKind.Utc` when SQL Server `datetime2` values are read.
- `Booking.TourId` is nullable only for `Transfer` bookings. A `Tour` booking requires a tour at the domain level.
- Unique constraints are implemented for `ApplicationUser.Email`, `Customer.UserId`, `Driver.UserId`, `Driver.LicenseNumber`, `Vehicle.RegistrationNumber`, `Booking.BookingNumber`, `Payment.TransactionReference` when present, and `RefreshToken.TokenHash`.
- Operational indexes are implemented for active tour/destination filters, customer booking history, booking date/status, and driver/vehicle assignment schedules. The scheduling indexes support overlap queries; interval-overlap enforcement remains an application/database transaction concern for a later operational milestone.
- Deleting a booking cascades only to its status history, payments, and assignments. Customers, tours, drivers, vehicles, destinations, users, and status-change actors are protected from deletion while referenced.
- Monetary values (`Tour.Price`, `Booking.TotalPrice`, and `Payment.Amount`) use SQL Server `decimal(18,2)`. `DateOnly` and `TimeOnly` are stored as `date` and `time(0)`; tour duration is stored as ticks in `bigint` to avoid SQL Server's 24-hour `time` limit.
- The Domain entities contain private materialization constructors only; public constructors and behaviors continue to enforce domain invariants.
