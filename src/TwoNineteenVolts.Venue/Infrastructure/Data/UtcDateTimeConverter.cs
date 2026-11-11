using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TwoNineteenVolts.Venue.Infrastructure.Data;

// SQLite stores dates as text without a time zone, so EF reads them back as Kind = Unspecified.
// All dates are saved as UTC, so marking them Utc on read keeps the "Z" in JSON responses.
public class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    toDatabase => toDatabase,
    fromDatabase => DateTime.SpecifyKind(fromDatabase, DateTimeKind.Utc));
