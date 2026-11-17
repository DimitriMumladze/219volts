using TwoNineteenVolts.Api.Features.Devices.ApiKey;
using TwoNineteenVolts.Api.Features.Devices.Contracts;
using TwoNineteenVolts.Api.Features.Devices.Registry;
using TwoNineteenVolts.Api.Features.Devices.Shelly;

namespace TwoNineteenVolts.Api.Features.Devices.Endpoints;

public static class DeviceEndpoints
{
    public static void MapDeviceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/devices")
            .AddEndpointFilter<ApiKeyFilter>();

        group.MapPost("/", RegisterAsync);
        group.MapGet("/{id}/status", GetStatusAsync);
        group.MapPost("/{id}/on", TurnOnAsync);
        group.MapPost("/{id}/off", TurnOffAsync);
        group.MapPost("/{id}/timer", TurnOnWithTimerAsync);
    }

    private static async Task<IResult> RegisterAsync(RegisterDeviceRequest request, ShellyCloudClient shelly, DeviceRegistry registry)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId))
        {
            return TypedResults.Problem(title: "DeviceId is required", statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await shelly.GetStatusAsync(request.DeviceId);
        if (result.IsFailure)
        {
            return ShellyCloudFailed(result.Error!);
        }

        if (result.Value is null)
        {
            return DeviceNotInCloudAccount();
        }

        registry.Add(request.DeviceId);
        return TypedResults.Created($"/api/devices/{request.DeviceId}/status", result.Value);
    }

    private static async Task<IResult> GetStatusAsync(string id, ShellyCloudClient shelly, DeviceRegistry registry)
    {
        if (!registry.Contains(id))
        {
            return DeviceNotRegistered();
        }

        var result = await shelly.GetStatusAsync(id);
        if (result.IsFailure)
        {
            return ShellyCloudFailed(result.Error!);
        }

        if (result.Value is null)
        {
            return DeviceNotInCloudAccount();
        }

        return TypedResults.Ok(result.Value);
    }

    private static async Task<IResult> TurnOnAsync(string id, ShellyCloudClient shelly, DeviceRegistry registry)
    {
        return await SetSwitchAsync(id, on: true, toggleAfterSeconds: null, shelly, registry);
    }

    private static async Task<IResult> TurnOffAsync(string id, ShellyCloudClient shelly, DeviceRegistry registry)
    {
        return await SetSwitchAsync(id, on: false, toggleAfterSeconds: null, shelly, registry);
    }

    private static async Task<IResult> TurnOnWithTimerAsync(string id, TimerRequest request, ShellyCloudClient shelly, DeviceRegistry registry)
    {
        if (request.Seconds <= 0)
        {
            return TypedResults.Problem(title: "Seconds must be greater than 0", statusCode: StatusCodes.Status400BadRequest);
        }

        return await SetSwitchAsync(id, on: true, toggleAfterSeconds: request.Seconds, shelly, registry);
    }

    private static async Task<IResult> SetSwitchAsync(string id, bool on, int? toggleAfterSeconds, ShellyCloudClient shelly, DeviceRegistry registry)
    {
        if (!registry.Contains(id))
        {
            return DeviceNotRegistered();
        }

        var result = await shelly.SetSwitchAsync(id, on, toggleAfterSeconds);
        if (result.IsFailure)
        {
            return ShellyCloudFailed(result.Error!);
        }

        return TypedResults.NoContent();
    }

    private static IResult DeviceNotRegistered()
    {
        return TypedResults.Problem(title: "Device is not registered", statusCode: StatusCodes.Status404NotFound);
    }

    private static IResult DeviceNotInCloudAccount()
    {
        return TypedResults.Problem(title: "Device not found in the Shelly Cloud account", statusCode: StatusCodes.Status404NotFound);
    }

    private static IResult ShellyCloudFailed(string error)
    {
        return TypedResults.Problem(title: "Shelly Cloud request failed", detail: error, statusCode: StatusCodes.Status502BadGateway);
    }
}
