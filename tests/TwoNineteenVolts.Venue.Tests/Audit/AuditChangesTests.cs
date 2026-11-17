using TwoNineteenVolts.Venue.Application.Audit;

namespace TwoNineteenVolts.Venue.Tests.Audit;

public class AuditChangesTests
{
    [Fact]
    public void Only_changed_fields_are_listed_in_order()
    {
        var text = AuditChanges.Fields(
            ("name", "Panel 1", "Panel A"),
            ("ip", "192.168.1.20", "192.168.1.20"),
            ("port", "502", "503"));

        Assert.Equal("name: Panel 1 → Panel A; port: 502 → 503", text);
    }

    [Fact]
    public void Nothing_changed_gives_empty_text()
    {
        var text = AuditChanges.Fields(("name", "Panel 1", "Panel 1"));

        Assert.Equal("", text);
    }

    [Fact]
    public void Single_change_reads_old_arrow_new()
    {
        var text = AuditChanges.Change("Billiards", "Pool");

        Assert.Equal("Billiards → Pool", text);
    }
}
