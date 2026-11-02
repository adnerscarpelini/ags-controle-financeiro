using System.Text;

namespace Ags.Finance.Web.Commands;

internal sealed class ConsoleTerminal : ITerminal
{
    public string? ReadLine(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine();
    }

    public string? ReadPassword(string prompt)
    {
        Console.Write(prompt);

        // Entrada redirecionada (um script que injeta a senha): não há tecla a interceptar.
        if (Console.IsInputRedirected)
        {
            return Console.ReadLine();
        }

        var password = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return password.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    password.Length--;
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
            }
        }
    }

    public void WriteLine(string message)
    {
        Console.WriteLine(message);
    }

    public void WriteError(string message)
    {
        Console.Error.WriteLine(message);
    }
}
