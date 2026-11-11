using System.Text;

namespace TwoNineteenVolts.Venue.Infrastructure.Auth;

public class SystemConsole : IConsole
{
    public string ReadLine() => Console.ReadLine() ?? "";

    public string ReadSecret()
    {
        // Piped input (a script) has no keys to hide.
        if (Console.IsInputRedirected)
        {
            return ReadLine();
        }

        var secret = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return secret.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (secret.Length > 0)
                {
                    secret.Length--;
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                secret.Append(key.KeyChar);
            }
        }
    }

    public void WriteLine(string text) => Console.WriteLine(text);
}
