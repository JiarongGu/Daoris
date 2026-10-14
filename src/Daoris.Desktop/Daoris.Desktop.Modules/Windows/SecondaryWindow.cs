using System.Text.RegularExpressions;

namespace Daoris.Desktop;

/// <summary>
/// The shell's half of opening a window: the one thing a page genuinely cannot do for itself.
/// </summary>
/// <remarks>
/// The same seam shape the folder picker uses (D48 §7) and for the same reason — the judgement is
/// here in plain <c>net10.0</c> where it is tested, and only the WinForms act is in the window.
/// </remarks>
public interface ISecondaryWindows
{
    /// <summary>
    /// Open the named window at this page (a path on the engine's app origin, D92), or bring it forward when it is already open.
    /// </summary>
    /// <returns>True when a window was created; false when an existing one was activated.</returns>
    bool Open(string name, string address);

    /// <summary>The names open right now.</summary>
    IReadOnlyList<string> Opened { get; }

    /// <summary>
    /// Tell the named window which theme its page is in, for the caption its frame paints (WINDOW2).
    /// Shenora's window commands route a second window's <c>SET_THEME</c> nowhere (0.17: NO_ROUTE), so
    /// the page says it here, by the name it was opened with.
    /// </summary>
    /// <returns>False when no window by that name is open: nothing to tell, and nothing wrong.</returns>
    bool SetTheme(string name, bool dark);
}

/// <summary>
/// Where the platform is served on this machine — the address a window points at.
/// </summary>
/// <remarks>
/// A registered value rather than a constructor string, because the modules are composed through DI
/// and a raw <c>string</c> is not something a container can resolve. It is the one fact a window
/// needs and nothing else about the host.
/// </remarks>
public sealed record PlatformAddress(string Url);

/// <summary>
/// The windows this build opens, and what each name becomes (D55 §b, SURF8).
/// </summary>
/// <remarks>
/// <para>One name, three readers: the page parses it out of its own URL (<c>work/window.ts</c>), the
/// shell navigates to the address made from it, and the geometry store writes the file named after
/// it. Keeping the derivations in one place is what keeps those three agreeing.</para>
///
/// <para><b>The escape does both jobs.</b> A name goes into a query string and into a filename, and
/// <see cref="Uri.EscapeDataString"/> is injective over the alphabet <see cref="IsKnown"/> permits —
/// `%` is not in it — so two different windows can never share a geometry file. A `-` substitution
/// would have collided `session:laptop/a1b2` with `session:laptop-a1b2`.</para>
/// </remarks>
public static class SecondaryWindow
{
    /// <summary>The rail plus every live stream, read-only, for a second screen.</summary>
    public const string Monitor = "monitor";

    /// <summary>The query parameter a window's page reads its own name out of.</summary>
    public const string Parameter = "window";

    private const string SessionPrefix = "session:";

    /// <summary>
    /// Which session ids may be named — the alphabet the ledger mints, plus the `origin/id` a
    /// mirrored record wears (D47 §6). Narrow on purpose: a separator, a query character or `..` is
    /// not a session id, so none of them needs an answer.
    /// </summary>
    private static readonly Regex SessionId =
        new(@"^[A-Za-z0-9][A-Za-z0-9._-]*(?:/[A-Za-z0-9][A-Za-z0-9._-]*)?$", RegexOptions.Compiled);

    /// <summary>One session, detached into its own window.</summary>
    public static string ForSession(string id) => SessionPrefix + id;

    /// <summary>Whether this is a window this build knows how to open.</summary>
    public static bool IsKnown(string? name) =>
        name == Monitor
        || (name?.StartsWith(SessionPrefix, StringComparison.Ordinal) == true
            && SessionId.IsMatch(name[SessionPrefix.Length..]));

    /// <summary>The file this window's geometry is remembered in — one per name.</summary>
    public static string StateFile(string name) => $"{Uri.EscapeDataString(name)}.json";
}
