namespace Hack.io.Utility;

/// <summary>
/// A static class for additional console functions
/// </summary>
public static class ConsoleUtil
{
    /// <summary>
    /// Checks to see if the user presses a certain "yes" key, or a certain "no" key
    /// </summary>
    /// <param name="YesKey">The key to indicate "Yes"</param>
    /// <param name="NoKey">The key to indicate "No"</param>
    /// <returns>true if the user presses the YesKey</returns>
    public static bool Confirm(ConsoleKey YesKey = ConsoleKey.Y, ConsoleKey NoKey = ConsoleKey.N)
    {
        ConsoleKey response;
        Console.WriteLine();
        do
        {
            response = Console.ReadKey(false).Key;
        } while (response != YesKey && response != NoKey);

        return response == YesKey;
    }

    /// <summary>
    /// Writes a coloured message to the console
    /// </summary>
    /// <param name="Message">Message to print</param>
    /// <param name="ForeColour">ConsoleColor to use for the text</param>
    /// <param name="BackColour">ConsoleColor to use for the background of the text</param>
    /// <param name="Newline">Switch to the next line?</param>
    public static void WriteColoured(string Message, ConsoleColor ForeColour = ConsoleColor.White, ConsoleColor BackColour = ConsoleColor.Black, bool Newline = false)
    {
        Console.BackgroundColor = BackColour;
        Console.ForegroundColor = ForeColour;
        Console.Write(Message);
        if (Newline)
            Console.WriteLine();
        Console.ResetColor();
    }

    private static bool? _console_present;
    /// <summary>
    /// returns TRUE if a console is present
    /// </summary>
    public static bool IsConsolePresent
    {
        get
        {
            if (_console_present == null)
            {
                _console_present = true;
                try { int window_height = Console.WindowHeight; }
                catch { _console_present = false; }
            }
            return _console_present.Value;
        }
    }
}
