namespace Daoris.Driver;

/// <summary>
/// Why a start ran on its account, by code: each step of the walk as a conversation's first line says it
/// (<see cref="RotationWords.Steps"/>), the page's <c>note.opening.why.*</c>. A code nothing writes now is kept for the notes
/// already on machines.
/// </summary>
public static class OpeningWhy
{
    /// <summary>The account its scope begins at is cooling.</summary>
    public const string Cooling = "cooling";

    /// <summary>Its provider refused the account its scope begins at.</summary>
    public const string Refused = "refused";

    /// <summary>The account its scope begins at is not signed in.</summary>
    public const string SignedOut = "signed-out";

    /// <summary>The account its scope begins at was passed because another question held its lock past the start's patience.</summary>
    public const string Locked = "locked";

    /// <summary>It runs on the account kept for conversations, which is where its scope begins.</summary>
    public const string KeptSelf = "kept-self";

    /// <summary>A conversation passed the kept account, which conversations take last.</summary>
    public const string KeptLast = "kept-last";

    /// <summary>Driven work passed the kept account.</summary>
    public const string Kept = "kept";

    /// <summary>The account passed said a limit is reached.</summary>
    public const string NearReached = "near-reached";

    /// <summary>The account passed said it is near a limit: its pace warning, which nothing writes now.</summary>
    public const string NearWord = "near-word";

    /// <summary>The account passed said it is drawing on usage credits.</summary>
    public const string NearCredits = "near-credits";

    /// <summary>The account passed has used a window at or over the scope's one number, which the curve replaced: nothing writes it now.</summary>
    public const string NearUsed = "near-used";

    /// <summary>The account passed has used a window over its curve: the window, its use and the curve's value now.</summary>
    public const string OverCurve = "over-curve";

    /// <summary>The account passed is near its limit, with nothing it said to name.</summary>
    public const string Near = "near";

    /// <summary>Both it and the account weighed against are near, and it has the more room: nothing writes it now.</summary>
    public const string Room = "room";

    /// <summary>It runs the fewest of Daoris's sessions.</summary>
    public const string Fewest = "fewest";

    /// <summary>Its week resets first, when the reset's time alone ranked: nothing writes it now (<see cref="Lapses"/> does).</summary>
    public const string Lapsing = "lapsing";

    /// <summary>More of its week would lapse at its reset, per hour left: the window, its unused share, its reset and when that reading was seen.</summary>
    public const string Lapses = "lapses";

    /// <summary>It has the most room under its curve, on its tightest window.</summary>
    public const string MostRoom = "most-room";

    /// <summary>It is furthest behind its week's pace: written under <c>switch: off</c> only.</summary>
    public const string Behind = "behind";

    /// <summary>The account it was weighed against is ahead of its week's pace: under <c>switch: off</c> only, as <see cref="Behind"/>.</summary>
    public const string Ahead = "ahead";

    /// <summary>Daoris has not started on it yet.</summary>
    public const string NotStarted = "not-started";

    /// <summary>Daoris started on it least recently.</summary>
    public const string LeastRecent = "least-recent";

    /// <summary>It is a workspace's default.</summary>
    public const string Default = "default";

    /// <summary>It was the default this machine's own list gave: nothing writes it now, and it is said without the list's name.</summary>
    public const string DefaultHere = "default-here";

    /// <summary>It comes first in a workspace's list.</summary>
    public const string First = "first";

    /// <summary>It came first in this machine's own list: nothing writes it now, and it is said without the list's name.</summary>
    public const string FirstHere = "first-here";

    /// <summary>It comes first in the order of Daoris's accounts, the set every workspace's start reads.</summary>
    public const string FirstOrder = "first-order";
}
