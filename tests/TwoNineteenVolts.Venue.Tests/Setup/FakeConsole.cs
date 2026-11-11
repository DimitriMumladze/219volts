using TwoNineteenVolts.Venue.Commands.Setup;

namespace TwoNineteenVolts.Venue.Tests.Setup;

// Answers prompts from a script and keeps everything the command printed.
public class FakeConsole(params string[] inputs) : IConsole
{
    private readonly Queue<string> _inputs = new(inputs);

    public List<string> Output { get; } = [];

    public string ReadLine() => _inputs.Dequeue();

    public string ReadSecret() => _inputs.Dequeue();

    public void WriteLine(string text) => Output.Add(text);
}
