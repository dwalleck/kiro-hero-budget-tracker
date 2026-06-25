using System.Globalization;

namespace BudgetTracker;

/// <summary>
/// Money is stored and passed around as integer <b>cents</b> — never float or decimal — so
/// arithmetic can't drift. Every display goes through <see cref="Format"/>, so currency
/// rendering lives in exactly one place. (This is the convention the Act 1 steering lesson enforces.)
/// </summary>
public static class Money
{
    public static string Format(long cents) =>
        (cents / 100m).ToString("C", CultureInfo.GetCultureInfo("en-US"));
}
