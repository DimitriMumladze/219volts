using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace TwoNineteenVolts.Api.Features.Devices.ApiKey;

// Describes the X-Api-Key header in the OpenAPI document, so Scalar shows an Authentication box for it.
public class ApiKeyOpenApiTransformer : IOpenApiDocumentTransformer
{
    public const string SchemeName = "ApiKey";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = ApiKeyFilter.HeaderName
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeName, document)] = []
        });

        return Task.CompletedTask;
    }
}
