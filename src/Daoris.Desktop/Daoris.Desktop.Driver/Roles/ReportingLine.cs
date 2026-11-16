namespace Daoris.Driver;

/// <summary>The actors above a step's on the reporting line, in climbing order.</summary>
public static class LineActors
{
    /// <summary>The place's: the repository's or its workspace's supervisor.</summary>
    public const string Lead = "lead";

    /// <summary>The machine's supervisor.</summary>
    public const string Head = "head";

    /// <summary>The top of every line, held by the person; nothing below re-casts it.</summary>
    public const string Owner = "owner";
}

/// <summary>Who holds a level: an agent, a terminal the window starts, or the person; none is a level nobody is appointed to.</summary>
public static class LineHolders
{
    public const string Agent = "agent";
    public const string Terminal = "terminal";
    public const string Person = "person";
}

/// <summary>Why help passed a level on its way up.</summary>
public static class LinePasses
{
    /// <summary>Nobody is appointed to the level.</summary>
    public const string Vacant = "vacant";

    /// <summary>An owner-class kind, never answered below the owner actor.</summary>
    public const string OwnerClass = "owner-class";

    /// <summary>Its holder passed it up, with its reading.</summary>
    public const string Left = "left";
}

/// <summary>
/// The owner-class kinds, doctrine rule 1's closed list: the help that reaches the owner actor, with the readings gathered below it. Every
/// other help a head leaves ends as gave up, never as a question to the owner actor.
/// </summary>
public static class OwnerClass
{
    public const string Direction = "direction";
    public const string Money = "money";
    public const string SignIn = "sign-in";
    public const string Release = "release";
    public const string History = "history";
    public const string Data = "data";
    public const string Publish = "publish";

    /// <summary>Direction and targets, money and budgets, sign-ins and credentials, releases marked as theirs, history rewrites, deleting their data, publishing to people outside.</summary>
    public static IReadOnlyList<string> Kinds { get; } = [Direction, Money, SignIn, Release, History, Data, Publish];

    public static bool Is(string? kind) => kind is not null && Kinds.Contains(kind, StringComparer.Ordinal);
}

/// <summary>One level of the line: its actor, the role that holds it and the holder's kind, or none where nobody is appointed; and why.</summary>
public sealed record LineLevel(string Actor, string? Role, string? Holder, string Why)
{
    public bool Vacant => Holder is null;

    /// <summary>Who holds it, as a wait says it: the person as <c>you</c>, else the role.</summary>
    public string Said() => Holder == LineHolders.Person ? "you" : $"`{Role}`";
}

/// <summary>One level help passed, with its code (<see cref="LinePasses"/>) and the sentence that says why.</summary>
public sealed record LinePass(string Actor, string Code, string Said);

/// <summary>Where help waits on the line, with every level it passed; no level is gave up, its passes what it tried.</summary>
public sealed record LineWait(LineLevel? At, IReadOnlyList<LinePass> Passed)
{
    public bool GaveUp => At is null;

    /// <summary><c>waiting on lead: `role`</c>, with what it passed; or <c>gave up: …</c> with what it tried.</summary>
    public string Said()
    {
        var passed = string.Join("; ", Passed.Select(pass => $"{pass.Actor}: {pass.Said}"));
        if (At is null) return $"gave up: {passed}";
        return Passed.Count == 0 ? $"waiting on {At.Actor}: {At.Said()}" : $"waiting on {At.Actor}: {At.Said()} · passed up: {passed}";
    }
}

/// <summary>
/// The reporting line above a step, the first cut on the supervisor resolver (<see cref="SupervisorResolution"/>): the lead is the
/// supervisor the resolver finds at the place below the machine (the repository's, else its workspace's), the head the machine's, and the
/// owner actor the top, held by the person. A holder that needs help asks the actor its actor reports to, which answers, finds another
/// holder or passes it up.
/// </summary>
/// <remarks>
/// No level is the person's by default (D305): a level held by the person's rules or by nobody is the person's only where the place's
/// risk record marks it (<see cref="PlaceMarks.Line"/>), else it is vacant, as is a level nobody is appointed to or whose holder is not
/// granted the act; a vacant level passes help up, saying why. A home whose record is unwritten reads such a level as the person's, the
/// older reading, until no home on a supported update path lacks the record. An unreadable role record or workspace map holds both
/// levels for the person, since what it would decide cannot be read.
/// </remarks>
public sealed record ReportingLine(LineLevel Lead, LineLevel Head)
{
    /// <summary>The owner actor, the top of every line.</summary>
    public static LineLevel Owner { get; } = new(LineActors.Owner, null, LineHolders.Person, "the owner actor, the top of every line");

    /// <summary>The line above help of <paramref name="kind"/> at <paramref name="place"/>, each level read for <paramref name="act"/> where one is named.</summary>
    public static ReportingLine Of(
        IReadOnlyList<RoleSlot> slots, string kind, RolePlace place, RuleWiderPlaces? wider = null, string? act = null, PlaceRiskBook? risk = null)
    {
        var found = SupervisorResolution.Find(slots, kind, place, wider);
        var machine = SupervisorResolution.Find(slots, kind, new RolePlace());
        if (found.Role is null)
        {
            var why = found.Held ?? "nobody decides it here";
            return new(new(LineActors.Lead, null, LineHolders.Person, why), new(LineActors.Head, null, LineHolders.Person, why));
        }

        var lead = found.Role.Scope.Rank > 0
            ? Level(LineActors.Lead, found.Role, kind, act, risk)
            : new LineLevel(LineActors.Lead, null, null, $"no lead is appointed: no supervisor of {Named(place)} decides `{kind}`");
        var head = machine.Role is { } role
            ? Level(LineActors.Head, role, kind, act, risk)
            : new LineLevel(LineActors.Head, null, LineHolders.Person, machine.Held ?? "no head is appointed, so you hold it");
        return new(lead, head);
    }

    /// <summary>
    /// Where help waits: an owner-class kind passes every level below the owner actor (<c>owner-class</c>), carrying the reading each left;
    /// other help waits on the first level that holds it, a vacant one or one in <paramref name="left"/> passing it up, and ends as gave
    /// up where the head passed it.
    /// </summary>
    /// <param name="ownerClass">Whether the help is of an owner-class kind (<see cref="OwnerClass"/>).</param>
    /// <param name="left">The levels whose holders passed it up, each with its reading.</param>
    public LineWait Climb(bool ownerClass, IReadOnlyDictionary<string, string>? left = null)
    {
        var passed = new List<LinePass>();
        foreach (var level in new[] { Lead, Head })
        {
            var reading = left?.GetValueOrDefault(level.Actor);
            if (ownerClass)
            {
                passed.Add(new(level.Actor, LinePasses.OwnerClass, reading ?? "an owner-class kind is never answered below the owner actor"));
            }
            else if (level.Vacant)
            {
                passed.Add(new(level.Actor, LinePasses.Vacant, level.Why));
            }
            else if (reading is not null)
            {
                passed.Add(new(level.Actor, LinePasses.Left, reading));
            }
            else
            {
                return new(level, passed);
            }
        }

        return new(ownerClass ? Owner : null, passed);
    }

    private static LineLevel Level(string actor, RoleRecord role, string kind, string? act, PlaceRiskBook? risk)
    {
        var hold = role.Hold!;
        if (hold.Holder is not (RoleHolders.Agent or RoleHolders.Terminal))
        {
            var heldBy = hold.Holder is null ? "nobody" : "your rules";
            // The person holds a level only by the owner's line mark (D305); a home whose record is unwritten reads it as before the record.
            return risk is not { Written: true } || risk.OwnerHoldsLine(role.Scope, kind)
                ? new(actor, role.Id, LineHolders.Person, $"`{role.Id}` is held by {heldBy}, and the level is marked yours")
                : new(actor, role.Id, null, $"`{role.Id}` is held by {heldBy} and no mark makes the level yours");
        }

        if (act is not null && !hold.Acts.Contains(SupervisorKinds.Qualified(kind, act), StringComparer.Ordinal))
        {
            return new(actor, role.Id, null, $"`{role.Id}` is not granted `{SupervisorKinds.Qualified(kind, act)}`");
        }

        return new(actor, role.Id, hold.Holder == RoleHolders.Agent ? LineHolders.Agent : LineHolders.Terminal, $"`{role.Id}` holds it");
    }

    private static string Named(RolePlace place) => place switch
    {
        { Repository: { } repository } => $"the repository `{repository}` or its workspace",
        { Workspace: { } workspace } => $"the workspace `{workspace}`",
        _ => "this place",
    };
}
