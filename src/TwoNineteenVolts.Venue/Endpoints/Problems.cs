namespace TwoNineteenVolts.Venue.Endpoints;

public static class Problems
{
    // key: an error key from Resources/Messages.resx. textArguments fill the {0}, {1}… in its text.
    public static IResult Create(
        string key,
        int statusCode,
        IDictionary<string, object?>? extra = null,
        params object[] textArguments) =>
        new ProblemResult(key, statusCode, extra, textArguments);
}
