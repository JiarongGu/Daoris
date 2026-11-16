namespace Daoris.Driver;

/// <summary>
/// The levels of the reporting line above a step, in climbing order, each named by the supervisor holding it: the
/// repository's supervisor or its workspace's, the machine's, then the owner role. The window's word census reads these codes
/// (<c>scripts/shared-words.mjs</c>), so no level takes a word a seeded role or a glossary term already says.
/// </summary>
public static class LineLevels
{
    /// <summary>The repository's supervisor.</summary>
    public const string Repository = "repository-supervisor";

    /// <summary>The workspace's supervisor, the place's level where the repository has none of its own.</summary>
    public const string Workspace = "workspace-supervisor";

    /// <summary>The machine's supervisor (本机主管).</summary>
    public const string Machine = "machine-supervisor";

    /// <summary>The owner role, the top of every line, held by the person; nothing below re-casts it.</summary>
    public const string Owner = "owner";

    /// <summary>The level a supervisor scoped to <paramref name="scope"/> holds.</summary>
    public static string Of(RoleScope scope) => scope.Kind switch
    {
        RoleScope.Repository => Repository,
        RoleScope.Workspace => Workspace,
        _ => Machine,
    };

    /// <summary>The level below the machine at <paramref name="place"/>: the repository's where it names one, else the workspace's.</summary>
    public static string Below(RolePlace place) => place.Repository is null ? Workspace : Repository;

    /// <summary>Whether <paramref name="level"/> is below the machine's: the repository's or the workspace's supervisor.</summary>
    public static bool IsBelowMachine(string level) => level is Repository or Workspace;

    /// <summary>The level as a line says it: <c>the repository's supervisor</c>, …, <c>the owner</c>.</summary>
    public static string Said(string level) => level switch
    {
        Repository => "the repository's supervisor",
        Workspace => "the workspace's supervisor",
        Machine => "the machine's supervisor",
        Owner => "the owner",
        _ => level,
    };
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

    /// <summary>An owner-class kind, never answered below the owner role.</summary>
    public const string OwnerClass = "owner-class";

    /// <summary>Its holder passed it up, with its reading.</summary>
    public const string Left = "left";
}

/// <summary>
/// The owner-class kinds, doctrine rule 1's closed list: the help that reaches the owner role, with the readings gathered below it. Every
/// other help the machine's supervisor leaves ends as gave up, never as a question to the owner role.
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

/// <summary>One level of the line: its code (<see cref="LineLevels"/>), the role that holds it and the holder's kind, or none where nobody is appointed; and why.</summary>
public sealed record LineLevel(string Level, string? Role, string? Holder, string Why)
{
    public bool Vacant => Holder is null;

    /// <summary>Who holds it, as a wait says it: the person as <c>you</c>, else the role.</summary>
    public string Said() => Holder == LineHolders.Person ? "you" : $"`{Role}`";
}

/// <summary>One level help passed (<see cref="LineLevels"/>), with its code (<see cref="LinePasses"/>) and the sentence that says why.</summary>
public sealed record LinePass(string Level, string Code, string Said);

/// <summary>Where help waits on the line, with every level it passed; no level is gave up, its passes what it tried.</summary>
public sealed record LineWait(LineLevel? At, IReadOnlyList<LinePass> Passed)
{
    public bool GaveUp => At is null;

    /// <summary><c>waiting on the repository's supervisor: `role`</c>, with what it passed; or <c>gave up: …</c> with what it tried.</summary>
    public string Said()
    {
        var passed = string.Join("; ", Passed.Select(pass => $"{LineLevels.Said(pass.Level)}: {pass.Said}"));
        if (At is null) return $"gave up: {passed}";
        var waiting = $"waiting on {LineLevels.Said(At.Level)}: {At.Said()}";
        return Passed.Count == 0 ? waiting : $"{waiting} · passed up: {passed}";
    }
}

/// <summary>
/// The reporting line above a step, the first cut on the supervisor resolver (<see cref="SupervisorResolution"/>): the place's level is
/// the supervisor the resolver finds below the machine (the repository's, else its workspace's), then the machine's supervisor, and the
/// owner role the top, held by the person. A holder that needs help asks the level its role reports to, which answers, finds another
/// holder or passes it up.
/// </summary>
/// <remarks>
/// No level is the person's by default: a level held by the person's rules or by nobody is the person's only where the place's
/// risk record marks it (<see cref="PlaceMarks.Line"/>), else it is vacant, as is a level nobody is appointed to or whose holder is not
/// granted the act; a vacant level passes help up, saying why. A home whose record is unwritten reads such a level as the person's, the
/// older reading, until no home on a supported update path lacks the record. An unreadable role record or workspace map holds both
/// levels for the person, since what it would decide cannot be read.
/// </remarks>
public sealed record ReportingLine(LineLevel Place, LineLevel Machine)
{
    /// <summary>The owner role, the top of every line.</summary>
    public static LineLevel Owner { get; } = new(LineLevels.Owner, null, LineHolders.Person, "the owner role, the top of every line");

    /// <summary>The line above help of <paramref name="kind"/> at <paramref name="place"/>, each level read for <paramref name="act"/> where one is named.</summary>
    public static ReportingLine Of(
        IReadOnlyList<RoleSlot> slots, string kind, RolePlace place, RuleWiderPlaces? wider = null, string? act = null, PlaceRiskBook? risk = null)
    {
        var found = SupervisorResolution.Find(slots, kind, place, wider);
        var machine = SupervisorResolution.Find(slots, kind, new RolePlace());
        if (found.Role is null)
        {
            var why = found.Held ?? "nobody decides it here";
            return new(new(LineLevels.Below(place), null, LineHolders.Person, why), new(LineLevels.Machine, null, LineHolders.Person, why));
        }

        var below = found.Role.Scope.Rank > 0
            ? Level(LineLevels.Of(found.Role.Scope), found.Role, kind, act, risk)
            : new LineLevel(LineLevels.Below(place), null, null, $"no supervisor of {Named(place)} decides `{kind}`");
        var top = machine.Role is { } role
            ? Level(LineLevels.Machine, role, kind, act, risk)
            : new LineLevel(LineLevels.Machine, null, LineHolders.Person, machine.Held ?? "no machine's supervisor is appointed, so you hold it");
        return new(below, top);
    }

    /// <summary>
    /// Where help waits: an owner-class kind passes every level below the owner role (<c>owner-class</c>), carrying the reading each left;
    /// other help waits on the first level that holds it, a vacant one or one in <paramref name="left"/> passing it up, and ends as gave
    /// up where the machine's supervisor passed it.
    /// </summary>
    /// <param name="ownerClass">Whether the help is of an owner-class kind (<see cref="OwnerClass"/>).</param>
    /// <param name="left">The levels whose holders passed it up, each with its reading, keyed by level (<see cref="LineLevels"/>).</param>
    public LineWait Climb(bool ownerClass, IReadOnlyDictionary<string, string>? left = null)
    {
        var passed = new List<LinePass>();
        foreach (var level in new[] { Place, Machine })
        {
            var reading = left?.GetValueOrDefault(level.Level);
            if (ownerClass)
            {
                passed.Add(new(level.Level, LinePasses.OwnerClass, reading ?? "an owner-class kind is never answered below the owner role"));
            }
            else if (level.Vacant)
            {
                passed.Add(new(level.Level, LinePasses.Vacant, level.Why));
            }
            else if (reading is not null)
            {
                passed.Add(new(level.Level, LinePasses.Left, reading));
            }
            else
            {
                return new(level, passed);
            }
        }

        return new(ownerClass ? Owner : null, passed);
    }

    private static LineLevel Level(string level, RoleRecord role, string kind, string? act, PlaceRiskBook? risk)
    {
        var hold = role.Hold!;
        if (hold.Holder is not (RoleHolders.Agent or RoleHolders.Terminal))
        {
            var heldBy = hold.Holder is null ? "nobody" : "your rules";
            // The person holds a level only by the owner's line mark; a home whose record is unwritten reads it as before the record.
            return risk is not { Written: true } || risk.OwnerHoldsLine(role.Scope, kind)
                ? new(level, role.Id, LineHolders.Person, $"`{role.Id}` is held by {heldBy}, and the level is marked yours")
                : new(level, role.Id, null, $"`{role.Id}` is held by {heldBy} and no mark makes the level yours");
        }

        if (act is not null && !hold.Acts.Contains(SupervisorKinds.Qualified(kind, act), StringComparer.Ordinal))
        {
            return new(level, role.Id, null, $"`{role.Id}` is not granted `{SupervisorKinds.Qualified(kind, act)}`");
        }

        return new(level, role.Id, hold.Holder == RoleHolders.Agent ? LineHolders.Agent : LineHolders.Terminal, $"`{role.Id}` holds it");
    }

    private static string Named(RolePlace place) => place switch
    {
        { Repository: { } repository } => $"the repository `{repository}` or its workspace",
        { Workspace: { } workspace } => $"the workspace `{workspace}`",
        _ => "this place",
    };
}
