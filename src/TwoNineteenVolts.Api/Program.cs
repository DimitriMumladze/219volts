using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using TwoNineteenVolts.Api.Features.Devices;
using TwoNineteenVolts.Api.Features.Devices.ApiKey;
using TwoNineteenVolts.Api.Features.Devices.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<ApiKeyOpenApiTransformer>());
builder.Services.AddProblemDetails();
builder.Services.AddDevices();

var app = builder.Build();

// The server's nginx publishes the app under /219wats/ and passes that prefix in X-Forwarded-Prefix,
// so links in the OpenAPI document and Scalar point at /219wats/... instead of the site root.
// The container only listens on 127.0.0.1, so nginx is the only possible sender of this header.
var forwardedHeaders = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedPrefix };
forwardedHeaders.KnownIPNetworks.Clear();
forwardedHeaders.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeaders);

// Scalar (/scalar) is the browser test page. Every call still needs the API key;
// it is filled in automatically only on a developer's own machine.
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.AddPreferredSecuritySchemes(ApiKeyOpenApiTransformer.SchemeName);
    if (app.Environment.IsDevelopment())
    {
        var localApiKey = app.Services.GetRequiredService<IOptions<TwoNineteenVolts.Api.Features.Devices.ApiKey.ApiKeyOptions>>().Value.Key;
        options.AddApiKeyAuthentication(ApiKeyOpenApiTransformer.SchemeName, apiKey => apiKey.Value = localApiKey);
    }
});

app.UseExceptionHandler();
app.UseHttpsRedirection();

// Serves the control panel (src/TwoNineteenVolts.Web, copied into wwwroot at build) from the same origin as the API.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapDeviceEndpoints();

app.Run();
