# Domain database design

This diagram reflects the Milestone 2 domain model. EF Core mappings, indexes, delete behavior, migrations, and database constraints will be implemented in Milestone 3.

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

## Design decisions for Milestone 3

- All primary keys are generated `Guid` values. Every entity inherits UTC `CreatedAt`; mutable entities also retain `UpdatedAt`.
- `Booking.TourId` is nullable only for `Transfer` bookings. A `Tour` booking requires a tour at the domain level.
- Required unique constraints: `ApplicationUser.Email`, `Customer.UserId`, `Driver.UserId`, `Driver.LicenseNumber`, `Vehicle.RegistrationNumber`, `Booking.BookingNumber`, and `RefreshToken.TokenHash`.
- Required operational indexes include booking lookups by customer/date/status and assignment lookups by driver/date/time and vehicle/date/time. These indexes support the later overlap checks but cannot alone enforce interval overlap prevention.
- Deleting a booking should cascade only to its status history, payments, and assignments. Customers, tours, drivers, vehicles, destinations, and users must be protected from deletion while operational records reference them.
- Monetary values (`Tour.Price`, `Booking.TotalPrice`, and `Payment.Amount`) will use SQL Server `decimal(18,2)`. Timestamps are stored as UTC; `DateOnly` and `TimeOnly` represent business schedules.
