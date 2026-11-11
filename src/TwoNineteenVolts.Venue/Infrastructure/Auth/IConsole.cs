namespace TwoNineteenVolts.Venue.Infrastructure.Auth;

// The terminal the setup commands talk to; tests replace it with a scripted one.
public interface IConsole
{
    string ReadLine();

    // Reads a line without showing what is typed.
    string ReadSecret();

    void WriteLine(string text);
}
