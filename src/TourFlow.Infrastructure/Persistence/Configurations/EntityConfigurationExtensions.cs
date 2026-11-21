using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TourFlow.Domain.Common;

namespace TourFlow.Infrastructure.Persistence.Configurations;

internal static class EntityConfigurationExtensions
{
    internal static void ConfigureEntity<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : Entity
    {
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property(entity => entity.CreatedAt)
            .HasColumnType("datetime2")
            .HasConversion(UtcDateTimeConverter.Instance)
            .IsRequired();
        builder.Property(entity => entity.UpdatedAt)
            .HasColumnType("datetime2")
            .HasConversion(UtcDateTimeConverter.NullableInstance);
    }
}

internal static class UtcDateTimeConverter
{
    internal static readonly ValueConverter<DateTime, DateTime> Instance = new(
        value => DateTime.SpecifyKind(value, DateTimeKind.Unspecified),
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    internal static readonly ValueConverter<DateTime?, DateTime?> NullableInstance = new(
        value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Unspecified) : null,
        value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null);
}
