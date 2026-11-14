using TwoNineteenVolts.Venue.Domain.Audit;

namespace TwoNineteenVolts.Venue.Tests.Domain;

public class AuditEntryTests
{
    [Fact]
    public void Entry_keeps_the_values_it_was_created_with()
    {
        var occurredAt = new DateTime(2026, 11, 11, 10, 0, 0, DateTimeKind.Utc);

        var entry = new AuditEntry
        {
            OccurredAt = occurredAt,
            ActorUserId = 7,
            ActorName = "Nino",
            Action = AuditActions.UserRenamed,
            Target = "user:7",
            Details = "Nina"
        };

        Assert.Equal(occurredAt, entry.OccurredAt);
        Assert.Equal(7, entry.ActorUserId);
        Assert.Equal("Nino", entry.ActorName);
        Assert.Equal(AuditActions.UserRenamed, entry.Action);
        Assert.Equal("user:7", entry.Target);
        Assert.Equal("Nina", entry.Details);
    }
}
