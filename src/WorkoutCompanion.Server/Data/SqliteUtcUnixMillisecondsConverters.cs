using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace WorkoutCompanion.Server.Data;

/// <summary>
/// Stores absolute timestamps as UTC Unix epoch milliseconds in SQLite INTEGER columns.
/// This representation is directly sortable, filterable, and indexable by SQLite.
/// </summary>
internal static class SqliteUtcUnixMillisecondsConverters
{
    public static readonly ValueConverter<DateTimeOffset, long> Required = new(
        value => value.ToUnixTimeMilliseconds(),
        value => DateTimeOffset.FromUnixTimeMilliseconds(value));

    public static readonly ValueConverter<DateTimeOffset?, long?> Optional = new(
        value => value.HasValue ? value.Value.ToUnixTimeMilliseconds() : null,
        value => value.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(value.Value) : null);
}
