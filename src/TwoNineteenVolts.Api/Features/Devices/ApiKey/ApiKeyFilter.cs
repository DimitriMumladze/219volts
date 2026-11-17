using Microsoft.Extensions.Options;

namespace TwoNineteenVolts.Api.Features.Devices.ApiKey;

public class ApiKeyFilter(IOptions<ApiKeyOptions> options) : IEndpointFilter
{
    public const string HeaderName = "X-Api-Key";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var sentKey = context.HttpContext.Request.Headers[HeaderName].ToString();
        if (sentKey != options.Value.Key)
        {
            return TypedResults.Problem(title: "Invalid API key", statusCode: StatusCodes.Status401Unauthorized);
        }

        return await next(context);
    }
}
