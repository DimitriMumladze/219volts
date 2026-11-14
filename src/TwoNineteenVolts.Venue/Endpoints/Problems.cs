namespace TwoNineteenVolts.Venue.Endpoints;

public static class Problems
{
    public static IResult Create(
        string key,
        int statusCode,
        IDictionary<string, object?>? extra = null,
        params object[] textArguments) =>
        new ProblemResult(key, statusCode, extra, textArguments);
}
