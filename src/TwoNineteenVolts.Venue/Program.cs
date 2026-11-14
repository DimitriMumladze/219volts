using System.Text.Json.Serialization;
using Scalar.AspNetCore;
using TwoNineteenVolts.Venue.Commands.Setup;
using TwoNineteenVolts.Venue.Endpoints.Audit;
using TwoNineteenVolts.Venue.Endpoints.Auth;
using TwoNineteenVolts.Venue.Endpoints.Devices;
using TwoNineteenVolts.Venue.Endpoints.Spots;
using TwoNineteenVolts.Venue.Endpoints.Users;
using TwoNineteenVolts.Venue.Infrastructure.Audit;
using TwoNineteenVolts.Venue.Infrastructure.Auth;
using TwoNineteenVolts.Venue.Infrastructure.Data;
using TwoNineteenVolts.Venue.Infrastructure.Devices;
using TwoNineteenVolts.Venue.Infrastructure.Setup;
using TwoNineteenVolts.Venue.Infrastructure.Spots;
using TwoNineteenVolts.Venue.Infrastructure.Users;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWindowsService();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.RespectNullableAnnotations = true;
    options.SerializerOptions.RespectRequiredConstructorParameters = true;
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddLocalization();
builder.Services.AddData();
builder.Services.AddAudit();
builder.Services.AddAuth();
builder.Services.AddUsers();
builder.Services.AddSetup();
builder.Services.AddDevices();
builder.Services.AddSpots();

var app = builder.Build();

switch (args.FirstOrDefault())
{
    case "setup":
        return await new SetupCommand(app.Services, new SystemConsole()).RunAsync();
    case "reset-support":
        return await new ResetSupportCommand(app.Services, new SystemConsole(), () => Environment.IsPrivilegedProcess).RunAsync();
}

await app.Services.MigrateDatabaseAsync();

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = exception => exception is BadHttpRequestException badRequest
        ? badRequest.StatusCode
        : StatusCodes.Status500InternalServerError
});
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapAuditEndpoints();
app.MapDeviceEndpoints();
app.MapSpotEndpoints();

await app.RunAsync();
return 0;
