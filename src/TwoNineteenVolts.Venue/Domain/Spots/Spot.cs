namespace TwoNineteenVolts.Venue.Domain.Spots;

public class Spot(string code, string name, int? deviceId, int? channel, DateTime createdAt)
{
    public int Id { get; private set; }

    public string Code { get; private set; } = code;

    public string Name { get; private set; } = name;

    public string NameKey { get; private set; } = name.ToUpperInvariant();

    public int? DeviceId { get; private set; } = deviceId;

    public int? Channel { get; private set; } = channel;

    public bool IsArchived { get; private set; }

    public DateTime CreatedAt { get; private set; } = createdAt;

    public bool IsMapped => DeviceId is not null;

    public void Rename(string name)
    {
        Name = name;
        NameKey = name.ToUpperInvariant();
    }

    public void MapTo(int deviceId, int channel)
    {
        DeviceId = deviceId;
        Channel = channel;
    }

    public void Unmap()
    {
        DeviceId = null;
        Channel = null;
    }

    public void ChangeCode(string code) => Code = code;

    public void Archive()
    {
        IsArchived = true;
        Unmap();
    }
}
