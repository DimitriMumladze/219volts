using System.Collections.Concurrent;
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
    private const int TimeoutMilliseconds = 2000;

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _boards = new();

    public DeviceKind Kind => DeviceKind.WaveshareModbusTcp;

    public int DefaultPort => 502;

    public IReadOnlyList<int> AllowedChannelCounts => [8, 16, 30];

    public Task<Result<IReadOnlyList<bool>>> ReadAllAsync(Device device, CancellationToken cancellationToken)
    {
        return CallAsync(device, client => ReadCoilsAsync(client, 0, device.ChannelCount, cancellationToken), cancellationToken);
    }

    public async Task<Result<bool>> ReadAsync(Device device, int channel, CancellationToken cancellationToken)
    {
        var coil = channel - 1;
        var result = await CallAsync(device, client => ReadCoilsAsync(client, coil, 1, cancellationToken), cancellationToken);
        if (result.IsFailure)
        {
            return Result<bool>.Fail(result.Error);
        }

        var state = result.Value[0];
        return Result<bool>.Ok(state);
    }

    public Task<Result<bool>> SetAsync(Device device, int channel, bool on, CancellationToken cancellationToken)
    {
        var coil = channel - 1;
        return CallAsync(device, client => WriteCoilAsync(client, coil, on, cancellationToken), cancellationToken);
    }


    private async Task<Result<T>> CallAsync<T>(
        Device device,
        Func<ModbusTcpClient, Task<Result<T>>> call,
        CancellationToken cancellationToken)
    {
        var board = _boards.GetOrAdd($"{device.IpAddress}:{device.Port}", _ => new SemaphoreSlim(1, 1));
        try
        {
            await board.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result<T>.Fail(DeviceError.NoAnswer);
        }

        try
        {
            using var client = new ModbusTcpClient
            {
                ConnectTimeout = TimeoutMilliseconds,
                ReadTimeout = TimeoutMilliseconds,
                WriteTimeout = TimeoutMilliseconds
            };
            var connected = await Task.Run(() => Connect(client, device), cancellationToken);
            if (!connected)
            {
                return Result<T>.Fail(DeviceError.NoAnswer);
            }

            return await call(client);
        }
        catch (ModbusException)
        {
            return Result<T>.Fail(DeviceError.ErrorAnswer);
        }
        catch (Exception)
        {
            return Result<T>.Fail(DeviceError.NoAnswer);
        }
        finally
        {
            board.Release();
        }
    }

    private static bool Connect(ModbusTcpClient client, Device device)
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

    private static async Task<Result<bool>> WriteCoilAsync(
        ModbusTcpClient client,
        int coil,
        bool on,
        CancellationToken cancellationToken)
    {
        await client.WriteSingleCoilAsync(UnitId, coil, on, cancellationToken);
        return Result<bool>.Ok(on);
    }
}
