namespace TwoNineteenVolts.Venue.Domain.Devices;

// Each kind has its own relay driver. A new kind = a new value here + a new driver class.
public enum DeviceKind
{
    ShellyGen2,
    WaveshareModbusTcp
}
