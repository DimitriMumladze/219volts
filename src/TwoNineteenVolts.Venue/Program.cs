using System.Text.Json.Serialization;
using Scalar.AspNetCore;
using TwoNineteenVolts.Venue.Endpoints.Audit;
using TwoNineteenVolts.Venue.Endpoints.Auth;
using TwoNineteenVolts.Venue.Endpoints.Users;
using TwoNineteenVolts.Venue.Infrastructure.Audit;
using TwoNineteenVolts.Venue.Infrastructure.Auth;
using TwoNineteenVolts.Venue.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWindowsService();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    // Enums travel as names ("Manager"), and a request missing a field is a 400 instead of a null.
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.RespectNullableAnnotations = true;
    options.SerializerOptions.RespectRequiredConstructorParameters = true;
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddData();
builder.Services.AddAudit();
builder.Services.AddAuth();

var app = builder.Build();

await app.Services.MigrateDatabaseAsync();

// "setup" and "reset-support" run once in this process and exit; anything else starts the service.
switch (args.FirstOrDefault())
{
    case "setup":
    {
        using var scope = app.Services.CreateScope();
        var setup = ActivatorUtilities.CreateInstance<SetupCommand>(scope.ServiceProvider, new SystemConsole());
        return await setup.RunAsync();
    }
    case "reset-support":
    {
        using var scope = app.Services.CreateScope();
        Func<bool> isAdministrator = () => Environment.IsPrivilegedProcess;
        var resetSupport = ActivatorUtilities.CreateInstance<ResetSupportCommand>(scope.ServiceProvider, new SystemConsole(), isAdministrator);
        return await resetSupport.RunAsync();
    }
}

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    // A malformed request is the client's fault: keep its 400 instead of turning it into a 500.
    StatusCodeSelector = exception => exception is BadHttpRequestException badRequest
        ? badRequest.StatusCode
        : StatusCodes.Status500InternalServerError
});
// Gives empty error responses (401, 403, 404, 400) a ProblemDetails body.
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

await app.RunAsync();
return 0;
