namespace Daoris.Driver;

/// <summary>
/// What an abandon does with one piece of the work (PAUSE1d, D132 point 6, design §3.2), in the public spelling the page and
/// the terminal share: a word, never a sentence.
/// </summary>
public static class AbandonAct
{
    /// <summary>A quest open, or taken by a session of this machine's: declined with the person's reason.</summary>
    public const string Decline = "decline";

    /// <summary>A live session this machine runs, the ask's intake included: stopped, as the person's stop.</summary>
    public const string Stop = "stop";

    /// <summary>A session waiting on you: ended <c>stopped</c>, unanswered.</summary>
    public const string End = "end";

    /// <summary>A session that has ended: archived here.</summary>
    public const string Archive = "archive";

    /// <summary>A tree nothing it holds is anywhere else: discarded with its branch.</summary>
    public const string Discard = "discard";

    /// <summary>A <c>daoris/*</c> branch whose tree is gone, nothing it holds anywhere else: deleted.</summary>
    public const string Delete = "delete";

    /// <summary>The ask (ask scope): closed with the person's reason.</summary>
    public const string Close = "close";

    /// <summary>Kept, and named with why (<see cref="AbandonWhy"/>).</summary>
    public const string Keep = "keep";

    /// <summary>Nothing to do: a quest already declined, a session already archived, a tree and branch already gone.</summary>
    public const string None = "none";

    /// <summary>Whether the act takes the piece: what the first press lists to go, and the second press sends.</summary>
    public static bool Takes(string act) => act is Decline or Stop or End or Archive or Discard or Delete or Close;
}

/// <summary>Why an abandon keeps a piece (design §3.2), a word from a fixed list the page says in the reader's language.</summary>
public static class AbandonWhy
{
    /// <summary>A quest taken on another machine: its work is theirs; decline it on its page to stop it there.</summary>
    public const string TakenElsewhere = "taken-elsewhere";

    /// <summary>A quest whose take this machine lost to the person's own reopen of the work its step grew from (FABRIC-CLOSE1c20), not to another machine.</summary>
    public const string SourceReopened = "source-reopened";

    /// <summary>A quest taken here outside Daoris (this machine's take, and no session record that took it).</summary>
    public const string TakenOutside = "taken-outside";

    /// <summary>A quest done: finished work keeps its record.</summary>
    public const string Done = "done";

    /// <summary>A teammate's live session: its process is on their machine (D47 §4).</summary>
    public const string Teammate = "teammate";

    /// <summary>A session whose kept tree holds work: still to review (D126 §5.2).</summary>
    public const string Review = "review";

    /// <summary>A tree a landing names a session of, standing or a trace: landed work is the clean-up's (D102, D113).</summary>
    public const string Landed = "landed";

    /// <summary>A tree some of whose commits are on another ref: landed, pushed, or on a branch of the person's.</summary>
    public const string Elsewhere = "elsewhere";

    /// <summary>A tree whose branch is checked out somewhere other than its own tree.</summary>
    public const string CheckedOut = "checked-out";

    /// <summary>A tree git could not judge.</summary>
    public const string Unknown = "unknown";

    /// <summary>A tree a session still runs in, which the abandon could not stop.</summary>
    public const string InUse = "in-use";

    /// <summary>A session the abandon could not stop, or a quest whose session it could not stop: asked again by a second abandon.</summary>
    public const string Unreached = "unreached";

    /// <summary>The service would not take a decline or a close, or git a discard: its words are the keep's detail.</summary>
    public const string Refused = "refused";

    /// <summary>A piece listed to go that is gone or needs nothing now (declined meanwhile, archived meanwhile).</summary>
    public const string Gone = "gone";
}

/// <summary>
/// The key each piece of an abandon goes by: what the first press lists and the second press sends back. <c>WorkAbandoning</c>
/// forwards each under the name its callers use (STRUCT3g-i).
/// </summary>
public static class AbandonKeys
{
    public static string QuestKey(string quest) => $"quest:{quest}";

    public static string SessionKey(string session) => $"session:{session}";

    public static string TreeKey(string repository, string branch) => $"tree:{repository}:{branch}";

    public static string AskKey(string ask) => $"ask:{ask}";
}

/// <summary>How an abandon came out.</summary>
public enum AbandonVerdict
{
    /// <summary>The second press ran its steps: what went, what stayed and what changed are said.</summary>
    Abandoned,

    /// <summary>Nothing is left to take, or nothing was listed: said, never refused (D48 §6).</summary>
    Nothing,

    /// <summary>No such ask on this machine, or no such quest here: <c>WORK_UNKNOWN</c>.</summary>
    Unknown,

    /// <summary>No reason given: <c>WORK_REASON</c>, since each declined quest keeps it.</summary>
    Reason,
}
