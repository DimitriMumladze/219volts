using Microsoft.Extensions.Localization;
using TwoNineteenVolts.Venue.Resources;

namespace TwoNineteenVolts.Venue.Endpoints;

public class ProblemResult(string key, int statusCode, IDictionary<string, object?>? extra, object[] textArguments) : IResult
{
    public Task ExecuteAsync(HttpContext httpContext)
    {
        var texts = httpContext.RequestServices.GetRequiredService<IStringLocalizer<Messages>>();

        Dictionary<string, object?> extensions = new() { ["code"] = key };
        foreach (var (name, value) in extra ?? new Dictionary<string, object?>())
        {
            extensions[name] = value;
        }

        var problem = TypedResults.Problem(
            title: texts[key, textArguments],
            statusCode: statusCode,
            extensions: extensions);

        return problem.ExecuteAsync(httpContext);
    }
}
