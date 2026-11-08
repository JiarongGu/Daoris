namespace Daoris.Driver;

/// <summary>The kinds of claim a slot is under.</summary>
public static class TreeClaimKinds
{
    public const string Make = "make";

    public const string Take = "take";

    public const string Return = "return";

    public const string Finish = "finish";

    public const string End = "end";

    public const string Retire = "retire";

    /// <summary>The kinds that switch the slot's branch or move its folder: a visit holds these back.</summary>
    public static IReadOnlyList<string> Switching { get; } = [Take, Return, Finish, Retire];
}

/// <summary>The phases a claim writes before each effect and after it.</summary>
public static class TreeClaimPhases
{
    public const string Opening = "opening";

    public const string Made = "made";

    public const string Judged = "judged";

    public const string Switched = "switched";

    public const string Cleared = "cleared";

    public const string Checked = "checked";

    public const string OpeningRecord = "opening-record";

    public const string Opened = "opened";

    public const string Recorded = "recorded";

    public const string Locked = "locked";

    public const string SettingAside = "setting-aside";

    public const string SetAside = "set-aside";

    public const string Detached = "detached";

    public const string SlimJudged = "slim-judged";

    public const string Moving = "moving";

    public const string Moved = "moved";

    public const string Dropped = "dropped";

    public const string Undoing = "undoing";

    public const string UndoWaiting = "undo-waiting";

    public static IReadOnlyList<string> ClearedOn { get; } = [Cleared, Checked, OpeningRecord, Opened];

    /// <summary>The phases of an undo under way, the slot's row naming no line, the waiting one taken by its own run's look.</summary>
    public static IReadOnlyList<string> Restoring { get; } = [Undoing, UndoWaiting];
}

/// <summary>
/// The one claim in flight on a slot, its whole intent written before its first effect.
/// </summary>
/// <param name="Holder">The quest, session or press it serves.</param>
/// <param name="Line">The line's path it brings the slot to, or ends.</param>
/// <param name="Start">The commit a new branch grows at, or a returning line's tip.</param>
/// <param name="Expect">The HEAD the judgement read.</param>
/// <param name="Opens">The key its record's open is made under, written before that open; empty where a kind opens none.</param>
/// <param name="Job">The name of the claim's own job, so a takeover can end the git processes of a dead run.</param>
/// <param name="Record">The opened record's id; empty until known.</param>
/// <param name="ClaimRun">The store run that holds the claim.</param>
public sealed record TreeClaim(
    string Workspace, string Repository, int Number, string Kind, string Holder, string Line, string Branch, string Start, string Expect,
    string Opens, string Job, string Record, string Phase, string ClaimRun, long ClaimGeneration, DateTimeOffset At)
{
    /// <summary>The branch the slot stood on as the claim found it; empty where detached.</summary>
    public string ExpectBranch { get; init; } = "";

    /// <summary>The line the slot's row named as the claim found it; empty where it served none.</summary>
    public string ExpectLine { get; init; } = "";
}
