namespace TwoNineteenVolts.Venue.Endpoints;

public static class Problems
{
    private const string CodeKey = "code";

    public static IResult Create(
        string key,
        int statusCode,
        IDictionary<string, object?>? extra = null,
        params object[] textArguments) =>
        new ProblemResult(key, statusCode, extra, textArguments);

    public static void AddDefaultCode(ProblemDetailsContext context)
    {
        var extensions = context.ProblemDetails.Extensions;
        if (extensions.ContainsKey(CodeKey))
        {
            return;
        }

        extensions[CodeKey] = context.ProblemDetails.Status switch
        {
            StatusCodes.Status400BadRequest => "BadRequest",
            StatusCodes.Status401Unauthorized => "Unauthorized",
            StatusCodes.Status403Forbidden => "Forbidden",
            StatusCodes.Status404NotFound => "NotFound",
            _ => "InternalError"
        };
    }
}
