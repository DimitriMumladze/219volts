namespace TwoNineteenVolts.Venue.Commands.Setup;

public interface IConsole
{
    string ReadLine();

    string ReadSecret();

    void WriteLine(string text);
}
