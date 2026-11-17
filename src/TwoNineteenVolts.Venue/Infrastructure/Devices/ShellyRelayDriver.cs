using System.Text.Json;
using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Devices;

namespace TwoNineteenVolts.Venue.Infrastructure.Devices;

public class ShellyRelayDriver(HttpClient http) : IRelayDriver
{
    public DeviceKind Kind => DeviceKind.ShellyGen2;

    public int DefaultPort => 80;

    public IReadOnlyList<int> AllowedChannelCounts => [1, 2, 3, 4];

    public async Task<Result<IReadOnlyList<bool>>> ReadAllAsync(Device device, CancellationToken cancellationToken)
    {
        var reply = await GetAsync(device, "/rpc/Shelly.GetStatus", cancellationToken);
        if (reply.IsFailure)
        {
            return Result<IReadOnlyList<bool>>.Fail(reply.Error);
        }

        var root = reply.Value;
        var states = new List<bool>();
        for (var id = 0; id < device.ChannelCount; id++)
        {
            var hasSwitch = root.TryGetProperty($"switch:{id}", out var status);
            if (!hasSwitch)
            {
                return Result<IReadOnlyList<bool>>.Fail(DeviceError.ErrorAnswer);
            }

            var output = IsOutputOn(status);
            if (output is not bool isOn)
            {
                return Result<IReadOnlyList<bool>>.Fail(DeviceError.ErrorAnswer);
            }

            states.Add(isOn);
        }

        return Result<IReadOnlyList<bool>>.Ok(states);
    }

    public async Task<Result<bool>> ReadIsOnAsync(Device device, int channel, CancellationToken cancellationToken)
    {
        var id = channel - 1;
        var reply = await GetAsync(device, $"/rpc/Switch.GetStatus?id={id}", cancellationToken);
        if (reply.IsFailure)
        {
            return Result<bool>.Fail(reply.Error);
        }

        var output = IsOutputOn(reply.Value);
        if (output is not bool isOn)
        {
            return Result<bool>.Fail(DeviceError.ErrorAnswer);
        }

        return Result<bool>.Ok(isOn);
    }

    public async Task<Result> SetAsync(Device device, int channel, bool shouldBeOn, CancellationToken cancellationToken)
    {
        var id = channel - 1;
        var state = shouldBeOn ? "true" : "false";
        var reply = await GetAsync(device, $"/rpc/Switch.Set?id={id}&on={state}", cancellationToken);
        if (reply.IsFailure)
        {
            return Result.Fail(reply.Error);
        }

        return Result.Ok();
    }


    private async Task<Result<JsonElement>> GetAsync(Device device, string path, CancellationToken cancellationToken)
    {
        var url = $"http://{device.IpAddress}:{device.Port}{path}";
        try
        {
            using var response = await http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Result<JsonElement>.Fail(DeviceError.ErrorAnswer);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement.Clone();
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Result<JsonElement>.Fail(DeviceError.ErrorAnswer);
            }

            return Result<JsonElement>.Ok(root);
        }
        catch (JsonException)
        {
            return Result<JsonElement>.Fail(DeviceError.ErrorAnswer);
        }
        catch (HttpRequestException)
        {
            return Result<JsonElement>.Fail(DeviceError.NoAnswer);
        }
        catch (OperationCanceledException)
        {
            return Result<JsonElement>.Fail(DeviceError.NoAnswer);
        }
    }

    private static bool? IsOutputOn(JsonElement status)
    {
        if (status.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var hasOutput = status.TryGetProperty("output", out var output);
        if (!hasOutput)
        {
            return null;
        }

        if (output.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        if (output.ValueKind == JsonValueKind.False)
        {
            return false;
        }

        return null;
    }
}
