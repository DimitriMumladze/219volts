using Microsoft.Extensions.Localization;
using TwoNineteenVolts.Venue.Resources;

namespace TwoNineteenVolts.Venue.Endpoints;

// An error reply that looks up its text only when it is written, from the request's own services,
// so endpoint handlers return a key and never need the texts themselves.
public class ProblemResult(string key, int statusCode, IDictionary<string, object?>? extra, object[] textArguments) : IResult
{
    public Task ExecuteAsync(HttpContext httpContext)
    {
        var texts = httpContext.RequestServices.GetRequiredService<IStringLocalizer<Messages>>();

        // The title is the text for the user; "code" is the key, so the UI can show its own translation.
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
