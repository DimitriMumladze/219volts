using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Devices;
using TwoNineteenVolts.Venue.Infrastructure.Auth;

namespace TwoNineteenVolts.Venue.Endpoints.Devices;

public static class DeviceEndpoints
{
    public static void MapDeviceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/devices").RequireAuthorization(Policies.Manager);

        group.MapGet("/", GetDevicesAsync);
        group.MapPost("/", RegisterAsync).RequireAuthorization(Policies.Support);
        group.MapPut("/{id:int}", UpdateAsync).RequireAuthorization(Policies.Support);
        group.MapDelete("/{id:int}", DeleteAsync).RequireAuthorization(Policies.Support);
    }


    private static async Task<IResult> GetDevicesAsync(IVenueDbContext db, DeviceStatuses statuses)
    {
        var devices = await db.Devices
            .OrderBy(d => d.Name)
            .ToListAsync();

        var responses = devices.Select(d => ToResponse(d, statuses)).ToList();
        return TypedResults.Ok(responses);
    }

    private static async Task<IResult> RegisterAsync(RegisterDeviceRequest request, DeviceService devices, DeviceStatuses statuses)
    {
        var result = await devices.RegisterAsync(request.Name, request.Kind, request.IpAddress, request.Port, request.ChannelCount);
        if (result.IsFailure)
        {
            return ToProblem(result.Error);
        }

        var device = result.Value;
        var response = ToResponse(device, statuses);
        return TypedResults.Created($"/api/devices/{device.Id}", response);
    }

    private static async Task<IResult> UpdateAsync(int id, UpdateDeviceRequest request, DeviceService devices, DeviceStatuses statuses)
    {
        var result = await devices.UpdateAsync(id, request.Name, request.IpAddress, request.Port);
        return ToResult(result, statuses);
    }

    private static async Task<IResult> DeleteAsync(int id, DeviceService devices)
    {
        var result = await devices.DeleteAsync(id);
        if (result.IsFailure)
        {
            return ToProblem(result.Error);
        }

        return TypedResults.NoContent();
    }

    private static IResult ToResult(Result<Device> result, DeviceStatuses statuses)
    {
        if (result.IsFailure)
        {
            return ToProblem(result.Error);
        }

        var response = ToResponse(result.Value, statuses);
        return TypedResults.Ok(response);
    }

    private static IResult ToProblem(string error)
    {
        var statusCode = error switch
        {
            DeviceError.NotFound => StatusCodes.Status404NotFound,
            DeviceError.InvalidName or DeviceError.InvalidIpAddress or DeviceError.InvalidPort or DeviceError.InvalidChannelCount
                => StatusCodes.Status400BadRequest,
            DeviceError.DuplicateName or DeviceError.DuplicateAddress => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        return Problems.Create(error, statusCode);
    }

    private static DeviceResponse ToResponse(Device device, DeviceStatuses statuses)
    {
        var status = statuses.Get(device.Id);
        return new DeviceResponse(
            device.Id,
            device.Name,
            device.Kind,
            device.IpAddress,
            device.Port,
            device.ChannelCount,
            status.IsOnline,
            status.LastSeen,
            device.CreatedAt);
    }
}
