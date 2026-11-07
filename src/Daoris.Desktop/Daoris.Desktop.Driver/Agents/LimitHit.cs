namespace Daoris.Driver;

/// <summary>
/// What a limit says is hit, where the door named a window rather than a sentence:
/// <see cref="LimitSeen.Hit"/> is a payload the account lines say, so it is Daoris's own window name, as a sentence names that window,
/// and never a vendor's.
/// </summary>
public static class LimitHit
{
    /// <summary>The hit for <paramref name="window"/>, a window by Daoris's name; null for any other, which waits to be stated as the door read it.</summary>
    public static string? OfWindow(string? window) =>
        string.Equals(window, AccountWindows.Weekly, StringComparison.Ordinal) ? AccountWindows.Weekly : null;
}
