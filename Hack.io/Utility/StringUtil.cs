using System.Text.RegularExpressions;

namespace Hack.io.Utility;

/// <summary>
/// A static class for string helper functions
/// </summary>
public static class StringUtil
{
    /// <summary>
    /// Converts a WildCard expression to a RegularExpression
    /// </summary>
    /// <param name="Value">The WildCard expression</param>
    /// <returns>a RegularExpression version of the WildCard</returns>
    public static string WildCardToRegex(string Value)
    {
        if (string.IsNullOrWhiteSpace(Value))
            throw new ArgumentException("value cannot be NULL or whitespace", nameof(Value));

        return "^" + Regex.Escape(Value).Replace("\\?", ".").Replace("\\*", ".*") + "$";
    }
}
