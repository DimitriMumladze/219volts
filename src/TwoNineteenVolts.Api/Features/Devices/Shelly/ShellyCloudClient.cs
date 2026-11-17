using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TwoNineteenVolts.Api.Common;
using TwoNineteenVolts.Api.Features.Devices.Contracts;

namespace TwoNineteenVolts.Api.Features.Devices.Shelly;

public class ShellyCloudClient(HttpClient http, IOptions<ShellyCloudOptions> options, ILogger<ShellyCloudClient> logger)
{
    private const int SwitchChannel = 0;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions JsonOptions = new() 
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull 
    };


    // Shelly Cloud allows one request per second per account, so every request waits its turn here.
    private static readonly TimeSpan MinTimeBetweenRequests = TimeSpan.FromSeconds(1);
    private static readonly SemaphoreSlim RequestGate = new(1, 1);
    private static DateTimeOffset _lastRequestAt = DateTimeOffset.MinValue;


    // Value is null when the device is not in the Shelly Cloud account.
    public async Task<Result<DeviceStatus?>> GetStatusAsync(string deviceId)
    {
        var body = new { ids = new[] { deviceId }, select = new[] { "status" } };
        var response = await PostAsync("/v2/devices/api/get", body);
        if (response.IsFailure)
        {
            return Result<DeviceStatus?>.Fail(response.Error!);
        }

        var devices = JsonSerializer.Deserialize<JsonElement>(response.Value!);
        if (devices.GetArrayLength() == 0)
        {
            return Result<DeviceStatus?>.Ok(null);
        }

        var device = devices[0];
        var online = device.GetProperty("online").GetInt32() == 1;
        if (!online
            || !device.TryGetProperty("status", out var status)
            || !status.TryGetProperty($"switch:{SwitchChannel}", out var switchStatus))
        {
            return Result<DeviceStatus?>.Ok(new DeviceStatus(deviceId, online, null, null));
        }

        var isOn = switchStatus.GetProperty("output").GetBoolean();
        return Result<DeviceStatus?>.Ok(new DeviceStatus(deviceId, true, isOn, GetTimerRemainingSeconds(switchStatus)));
    }

    // A new command replaces any running timer on the device.
    public async Task<Result<bool>> SetSwitchAsync(string deviceId, bool on, int? toggleAfterSeconds)
    {
        var body = new { id = deviceId, channel = SwitchChannel, on, toggle_after = toggleAfterSeconds };
        var response = await PostAsync("/v2/devices/api/set/switch", body);
        if (response.IsFailure)
        {
            return Result<bool>.Fail(response.Error!);
        }

        return Result<bool>.Ok(on);
    }

    private async Task<Result<string>> PostAsync(string path, object body)
    {
        var url = $"{options.Value.ServerUrl.TrimEnd('/')}{path}?auth_key={Uri.EscapeDataString(options.Value.AuthKey)}";

        // StringContent sends a Content-Length header like curl does; JsonContent would send the body chunked.
        using var content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");

        await RequestGate.WaitAsync();
        try
        {
            var wait = _lastRequestAt + MinTimeBetweenRequests - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait);
            }

            using var timeout = new CancellationTokenSource(RequestTimeout);
            using var response = await http.PostAsync(url, content, timeout.Token);
            var responseBody = await response.Content.ReadAsStringAsync(timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Shelly Cloud {Path} returned {StatusCode}: {Body}", path, (int)response.StatusCode, responseBody);
                return Result<string>.Fail($"Shelly Cloud returned {(int)response.StatusCode}: {responseBody}");
            }

            return Result<string>.Ok(responseBody);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Shelly Cloud {Path} unreachable", path);
            return Result<string>.Fail("Shelly Cloud unreachable");
        }
        finally
        {
            _lastRequestAt = DateTimeOffset.UtcNow;
            RequestGate.Release();
        }
    }

    private static int? GetTimerRemainingSeconds(JsonElement switchStatus)
    {
        if (!switchStatus.TryGetProperty("timer_started_at", out var startedAt)
            || !switchStatus.TryGetProperty("timer_duration", out var duration))
        {
            return null;
        }

        var endsAtUnixSeconds = startedAt.GetDouble() + duration.GetDouble();
        var remainingSeconds = endsAtUnixSeconds - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        return remainingSeconds > 0 ? (int)Math.Ceiling(remainingSeconds) : 0;
    }
}
