using Microsoft.EntityFrameworkCore;

namespace TourFlow.Infrastructure.Persistence;

/// <summary>Entity Framework database context. Entity sets are introduced in Milestone 2.</summary>
public sealed class TourFlowDbContext(DbContextOptions<TourFlowDbContext> options) : DbContext(options)
{
}
