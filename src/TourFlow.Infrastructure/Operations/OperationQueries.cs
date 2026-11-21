using System.Linq.Expressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TourFlow.Application.Operations;
using TourFlow.Domain.Common;
using TourFlow.Domain.Entities;
using TourFlow.Domain.Enums;
using TourFlow.Infrastructure.Persistence;

namespace TourFlow.Infrastructure.Operations;

internal static class OperationQueries
{
    internal static IOrderedQueryable<TEntity> Ordered<TEntity, TKey>(
        this IQueryable<TEntity> rows, Expression<Func<TEntity, TKey>> key, bool descending) where TEntity : Entity =>
        (descending ? rows.OrderByDescending(key) : rows.OrderBy(key)).ThenBy(row => row.Id);

    internal static async Task<PageResult<TDto>> ReadPageAsync<TEntity, TDto>(
        IQueryable<TEntity> rows, IOrderedQueryable<TEntity> ordered, Expression<Func<TEntity, TDto>> projection,
        ListOptions options, CancellationToken cancellationToken)
    {
        var page = options.Page ?? 1;
        var size = options.PageSize ?? 20;
        var count = await rows.CountAsync(cancellationToken);
        var items = await ordered.Skip((page - 1) * size).Take(size).Select(projection).ToListAsync(cancellationToken);
        return new PageResult<TDto>(items, page, size, count);
    }

    internal static async Task SaveAsync(TourFlowDbContext db, string? conflictMessage, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (conflictMessage is not null &&
            exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new OperationConflictException(conflictMessage);
        }
    }

    internal static IQueryable<BookingAssignment> LiveAssignments(this TourFlowDbContext db) =>
        db.BookingAssignments.Where(assignment => assignment.IsActive && assignment.Booking.Status != BookingStatus.Cancelled &&
            assignment.Booking.Status != BookingStatus.Rejected && assignment.Booking.Status != BookingStatus.Completed);
}
