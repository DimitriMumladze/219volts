using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TwoNineteenVolts.Venue.Infrastructure.Data;

public class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    toDatabase => toDatabase,
    fromDatabase => DateTime.SpecifyKind(fromDatabase, DateTimeKind.Utc));
