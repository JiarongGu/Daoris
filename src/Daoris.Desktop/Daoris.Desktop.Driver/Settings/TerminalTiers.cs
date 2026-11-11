namespace Daoris.Driver;

/// <summary>
/// The tier of a person's press sent from a terminal on this machine that no Daoris session owns: <c>driver.json</c>'s
/// <c>terminal</c>. <see cref="Ask"/>, the default, puts each on the window's Confirm card; <see cref="Act"/> runs each at once as the
/// person's own, attributed to a terminal. Read at each press, so a change takes the next.
/// </summary>
/// <remarks>
/// 🔴 <b>Ask unless the file names act</b>: a value that is neither reads as ask, so only the person's own choice widens it. Written only
/// while act, so an edit elsewhere never pins the default.
/// </remarks>
public static class TerminalTiers
{
    /// <summary>Its field in <c>driver.json</c>, and the setting's one key.</summary>
    public const string File = "terminal";

    /// <summary>Each press waits on the window's Confirm card.</summary>
    public const string Ask = "ask";

    /// <summary>Each press runs at once as the person's own.</summary>
    public const string Act = "act";

    /// <summary>The refusal every door says for any other tier.</summary>
    public const string Refusal = "a terminal's presses are `ask` or `act` — e.g. `daoris settings terminal act`.";

    /// <summary>The tier named, or null where it is neither.</summary>
    public static string? Named(string? tier) => tier is Ask or Act ? tier : null;

    /// <summary>The tier the file holds: act only where it names act.</summary>
    internal static string Read(string? stored) => stored == Act ? Act : Ask;

    /// <summary>Set the tier of a terminal's presses.</summary>
    /// <exception cref="DriverException">A tier that is neither ask nor act.</exception>
    public static DriverConfig WithTerminal(this DriverConfig config, string? tier) =>
        config with { Terminal = Named(tier) ?? throw new DriverException(Refusal) };
}
