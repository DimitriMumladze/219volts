using System.Text.Json.Serialization;
using Scalar.AspNetCore;
using TwoNineteenVolts.Venue.Endpoints.Auth;
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

app.Run();
