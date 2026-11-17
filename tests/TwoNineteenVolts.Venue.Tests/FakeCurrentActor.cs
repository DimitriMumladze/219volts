using TwoNineteenVolts.Venue.Application.Audit.Contracts;

namespace TwoNineteenVolts.Venue.Tests;

public class FakeCurrentActor(AuditActor actor) : ICurrentActor
{
    public AuditActor Get()
    {
        return actor;
    }
}
