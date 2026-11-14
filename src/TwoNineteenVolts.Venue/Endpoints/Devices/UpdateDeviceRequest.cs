namespace TwoNineteenVolts.Venue.Endpoints.Devices;

public record UpdateDeviceRequest(string Name, string IpAddress, int Port);
