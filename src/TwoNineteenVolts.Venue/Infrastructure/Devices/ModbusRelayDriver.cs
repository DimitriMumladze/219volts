using System.Net;
using System.Net.Sockets;
using FluentModbus;
using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Devices;

namespace TwoNineteenVolts.Venue.Infrastructure.Devices;

public class ModbusRelayDriver : IRelayDriver
{
    private const byte UnitId = 1;
    private static readonly int TimeoutMilliseconds = (int)RelayCall.Timeout.TotalMilliseconds;

    public DeviceKind Kind => DeviceKind.WaveshareModbusTcp;

    public int DefaultPort => 502;

    public IReadOnlyList<int> AllowedChannelCounts => [8, 16, 30];

    public Task<Result<IReadOnlyList<bool>>> ReadAllAsync(Device device, CancellationToken cancellationToken)
    {
        return CallAsync(
            device,
            client => ReadCoilsAsync(client, 0, device.ChannelCount, cancellationToken),
            Result<IReadOnlyList<bool>>.Fail,
            cancellationToken);
    }

    public async Task<Result<bool>> ReadIsOnAsync(Device device, int channel, CancellationToken cancellationToken)
    {
        var coil = channel - 1;
        var read = await CallAsync(
            device,
            client => ReadCoilsAsync(client, coil, 1, cancellationToken),
            Result<IReadOnlyList<bool>>.Fail,
            cancellationToken);
        if (read.IsFailure)
        {
            return Result<bool>.Fail(read.Error);
        }

        var isOn = read.Value[0];
        return Result<bool>.Ok(isOn);
    }

    public Task<Result> SetAsync(Device device, int channel, bool shouldBeOn, CancellationToken cancellationToken)
    {
        var coil = channel - 1;
        return CallAsync(
            device,
            client => WriteCoilAsync(client, coil, shouldBeOn, cancellationToken),
            Result.Fail,
            cancellationToken);
    }


    private async Task<TResult> CallAsync<TResult>(
        Device device,
        Func<ModbusTcpClient, Task<TResult>> call,
        Func<string, TResult> failWith,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = new ModbusTcpClient
            {
                ConnectTimeout = TimeoutMilliseconds,
                ReadTimeout = TimeoutMilliseconds,
                WriteTimeout = TimeoutMilliseconds
            };
            var isConnected = await Task.Run(() => TryConnect(client, device), cancellationToken);
            if (!isConnected)
            {
                return failWith(DeviceError.NoAnswer);
            }

            return await call(client);
        }
        catch (ModbusException)
        {
            return failWith(DeviceError.ErrorAnswer);
        }
        catch (Exception)
        {
            return failWith(DeviceError.NoAnswer);
        }
    }

    private static bool TryConnect(ModbusTcpClient client, Device device)
    {
        var address = IPAddress.Parse(device.IpAddress);
        var endpoint = new IPEndPoint(address, device.Port);
        try
        {
            client.Connect(endpoint);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static async Task<Result<IReadOnlyList<bool>>> ReadCoilsAsync(
        ModbusTcpClient client,
        int firstCoil,
        int count,
        CancellationToken cancellationToken)
    {
        var reply = await client.ReadCoilsAsync(UnitId, firstCoil, count, cancellationToken);
        var neededBytes = (count + 7) / 8;
        if (reply.Length < neededBytes)
        {
            return Result<IReadOnlyList<bool>>.Fail(DeviceError.ErrorAnswer);
        }

        var bytes = reply.ToArray();
        var states = new List<bool>();
        for (var index = 0; index < count; index++)
        {
            var bit = (bytes[index / 8] >> (index % 8)) & 1;
            states.Add(bit == 1);
        }

        return Result<IReadOnlyList<bool>>.Ok(states);
    }

    private static async Task<Result> WriteCoilAsync(
        ModbusTcpClient client,
        int coil,
        bool shouldBeOn,
        CancellationToken cancellationToken)
    {
        await client.WriteSingleCoilAsync(UnitId, coil, shouldBeOn, cancellationToken);
        return Result.Ok();
    }
}
