namespace TwoNineteenVolts.Api.Features.Devices.Contracts;

public record DeviceStatus(string DeviceId, bool Online, bool? IsOn, int? TimerRemainingSeconds);
