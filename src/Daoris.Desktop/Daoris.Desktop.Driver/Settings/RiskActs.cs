namespace Daoris.Driver;

/// <summary>
/// The closed census of act kinds the risk policy classes: each named by the door or tool the act comes through, never by an
/// actor's words, with its seed class. A door naming a kind outside it is refused (<see cref="RiskAct.Of"/>), so no unknown kind reaches the
/// judge (Workflows' <c>RiskGate</c>); a subdomain below the judge speaks it through a port the judge's side installs.
/// </summary>
public static class RiskActs
{
    public const string WorkDone = "work.done";
    public const string WorkAccept = "work.accept";
    public const string TaskApprove = "task.approve";
    public const string WorkRecover = "work.recover";
    public const string LineLand = "line.land";
    public const string RemotePush = "remote.push";
    public const string ReleaseStart = "release.start";
    public const string PublishOutside = "publish.outside";
    public const string SpendMoney = "spend.money";
    public const string DirectionSet = "direction.set";
    public const string CheckNarrow = "check.narrow";
    public const string ReachWiden = "reach.widen";
    public const string CommandChange = "command.change";
    public const string HistoryRewrite = "history.rewrite";
    public const string DataDelete = "data.delete";
    public const string PolicyChange = "policy.change";

    /// <summary>Every kind, in the census's order: what <c>daoris settings risk</c> prints, row for row.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        WorkDone, WorkAccept, TaskApprove, WorkRecover, LineLand, RemotePush, ReleaseStart, PublishOutside, SpendMoney, DirectionSet, CheckNarrow,
        ReachWiden, CommandChange, HistoryRewrite, DataDelete, PolicyChange,
    ];

    /// <summary>Whether <paramref name="kind"/> is one of the census, unqualified.</summary>
    public static bool Is(string? kind) => kind is not null && All.Contains(kind, StringComparer.Ordinal);

    /// <summary>
    /// The seed's class of <paramref name="kind"/> under the place's access mode: <c>none</c> everywhere but a held permission (<c>role</c>
    /// under restricted access), a change to what a repository's checks run (<c>owner</c> under restricted access), a rewrite of published
    /// history, deleting the owner's data, and a change to this policy (<c>owner</c>).
    /// </summary>
    public static string Seed(string kind, bool full) => kind switch
    {
        ReachWiden => full ? RiskClasses.None : RiskClasses.Role,
        CommandChange => full ? RiskClasses.None : RiskClasses.Owner,
        HistoryRewrite or DataDelete or PolicyChange => RiskClasses.Owner,
        _ => RiskClasses.None,
    };
}

/// <summary>The policy's three classes: no approval, a role's yes, the owner's own.</summary>
public static class RiskClasses
{
    public const string None = "none";
    public const string Role = "role";
    public const string Owner = "owner";

    public static bool Is(string? value) => value is None or Role or Owner;
}

/// <summary>An act as a door names it: a kind of the census and an optional qualifier (<c>release.start/production</c>).</summary>
public sealed record RiskAct(string Kind, string? Qualifier)
{
    /// <summary>The act <paramref name="spelled"/> names, <c>kind[/qualifier]</c>.</summary>
    /// <exception cref="ArgumentException">A kind outside the census: the census is closed at build time.</exception>
    public static RiskAct Of(string spelled)
    {
        var at = spelled.IndexOf('/', StringComparison.Ordinal);
        var kind = at < 0 ? spelled : spelled[..at];
        var qualifier = at < 0 ? null : spelled[(at + 1)..];
        if (!RiskActs.Is(kind) || qualifier is { Length: 0 }) throw new ArgumentException($"`{spelled}` names no act kind of the census", nameof(spelled));
        return new(kind, qualifier);
    }

    public override string ToString() => Qualifier is { } named ? $"{Kind}/{named}" : Kind;
}
