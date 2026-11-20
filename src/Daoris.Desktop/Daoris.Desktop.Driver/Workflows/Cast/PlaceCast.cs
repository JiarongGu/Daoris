using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// Who may hold a role, as a cast, a tenure and a hand-over name it: the person, the agent a role's staffing runs, or an agent by its name. A
/// holder is said alike whoever it is: only its channel differs.
/// </summary>
public static class CastHolders
{
    /// <summary>The person: their act, in the window, the terminal or the platform.</summary>
    public const string Person = "person";

    /// <summary>The agent the role's staffing runs, unnamed: the catalogue says which.</summary>
    public const string Agent = "agent";

    /// <summary>An agent by its name: <c>agent:&lt;name&gt;</c>, the name an agent's provider gives it.</summary>
    public const string AgentPrefix = "agent:";

    private static readonly Regex Name = new(@"^[a-z0-9][a-z0-9._-]{0,39}\z", RegexOptions.CultureInvariant);

    /// <summary>Whether <paramref name="holder"/> is a holder's word: <c>person</c>, <c>agent</c> or <c>agent:&lt;name&gt;</c>.</summary>
    public static bool Is(string? holder) =>
        holder is Person or Agent || (holder is { } named && named.StartsWith(AgentPrefix, StringComparison.Ordinal) && Name.IsMatch(named[AgentPrefix.Length..]));

    /// <summary>The holder an agent's name makes: <c>agent:&lt;name&gt;</c>.</summary>
    public static string AgentNamed(string name) => AgentPrefix + name;

    /// <summary>The holder as a step's wait reads it (<see cref="StepHolder"/>): the person, or an agent with its name where it has one.</summary>
    public static StepHolder StepOf(string holder) => holder == Person
        ? new StepHolder(StepHolders.Person)
        : new StepHolder(StepHolders.Agent, holder.StartsWith(AgentPrefix, StringComparison.Ordinal) ? holder[AgentPrefix.Length..] : null);

    /// <summary>The holder as a sentence says it: <c>you</c>, <c>the role's agent</c> or the agent's name.</summary>
    public static string Said(string holder) => holder switch
    {
        Person => "you",
        Agent => "the role's agent",
        _ => $"`{holder[AgentPrefix.Length..]}`",
    };
}

/// <summary>Why a role's holder at a place is who it is now, the first that stands of these.</summary>
public static class CastWhy
{
    /// <summary>The owner role is the person's, the top of every line; nothing re-casts it.</summary>
    public const string Owner = "owner";

    /// <summary>The owner's mark keeps it theirs: a marked place's approver (<see cref="PlaceMarks.ProductionReleases"/>).</summary>
    public const string Marked = "marked";

    /// <summary>The place's risk is high: no appointed agent holds a role there.</summary>
    public const string Risk = "risk";

    /// <summary>The owner cast it, with the reason the cast keeps.</summary>
    public const string Cast = "cast";

    /// <summary>No cast names one, and the role's staffing runs an agent: the work and the second opinion.</summary>
    public const string Staffed = "staffed";

    /// <summary>No cast names one, and the role is the keys' keeper: a sign-in is the person's act in a browser, which no setting grants.</summary>
    public const string SignIn = "sign-in";

    /// <summary>No cast names one: the role is unfilled, so its agent holds it until a cast names another, never the person by default.</summary>
    public const string Default = "default";
}

/// <summary>The owner's cast of one role at one place: its holder, the reason given and since when.</summary>
public sealed record RoleCast(string Holder, string? Why, DateTimeOffset Since);

/// <summary>
/// A role's holder at a place now, and why (<see cref="CastWhy"/>): where a cast stands, the place it is set on (<see cref="PlaceRiskBook"/>'s
/// sources) and its reason.
/// </summary>
public sealed record CastNow(string Role, string Holder, string Why, string Source, string? Words = null);

/// <summary>
/// Every place's cast, keyed as the place-risk record keys a place (<c>machine</c>, <c>workspace/&lt;name&gt;</c>,
/// <c>repository/&lt;name&gt;</c>), each naming a holder for the roles the owner cast there; a role no place casts reads its default. The place
/// casts a role once, never step by step; a run's hand-over is a tenure of its attempt, never a cast.
/// </summary>
public sealed record PlaceCastBook(IReadOnlyDictionary<string, IReadOnlyDictionary<string, RoleCast>> Places)
{
    public static PlaceCastBook Empty { get; } = new(new Dictionary<string, IReadOnlyDictionary<string, RoleCast>>(StringComparer.OrdinalIgnoreCase));

    /// <summary>The roles whose staffing runs an agent where no cast names a holder: the work's and the second opinion's.</summary>
    public static IReadOnlyList<string> Staffed { get; } = [StepRoles.Design, StepRoles.Opinion];

    /// <summary>
    /// Who holds <paramref name="role"/> at the place now, and why: the owner role and a marked place's approver the person's; at high risk the
    /// person; else the narrowest cast; else the role's staffing, the keys' keeper the person's, and every other role its agent's, never theirs.
    /// </summary>
    public CastNow At(string role, string? workspace, string? repository, PlaceRiskNow risk)
    {
        if (role == StepRoles.Owner) return new(role, CastHolders.Person, CastWhy.Owner, PlaceRiskBook.DefaultSource);
        if (role == StepRoles.Approver && risk.Marked(PlaceMarks.ProductionReleases)) return new(role, CastHolders.Person, CastWhy.Marked, PlaceRiskBook.DefaultSource);
        if (!risk.RoleMayHold()) return new(role, CastHolders.Person, CastWhy.Risk, risk.Source);
        foreach (var (source, key) in Chain(workspace, repository))
        {
            if (Places.GetValueOrDefault(key)?.GetValueOrDefault(role) is { } cast) return new(role, cast.Holder, CastWhy.Cast, source, cast.Why);
        }

        if (Staffed.Contains(role, StringComparer.Ordinal)) return new(role, CastHolders.Agent, CastWhy.Staffed, PlaceRiskBook.DefaultSource);
        return role == StepRoles.CredentialsKeeper
            ? new(role, CastHolders.Person, CastWhy.SignIn, PlaceRiskBook.DefaultSource)
            : new(role, CastHolders.Agent, CastWhy.Default, PlaceRiskBook.DefaultSource);
    }

    public static string Uncast(string role) => Empty.At(role, null, null, PlaceRiskBook.Unwritten.At(null, null)).Holder;

    /// <summary>The book with <paramref name="role"/> at the place cast to <paramref name="holder"/>, or its cast taken away where null.</summary>
    public PlaceCastBook With(string place, string role, RoleCast? cast)
    {
        var spelled = Places.Keys.FirstOrDefault(each => string.Equals(each, place, StringComparison.OrdinalIgnoreCase)) ?? place;
        var roles = new Dictionary<string, RoleCast>(Places.GetValueOrDefault(spelled) ?? new Dictionary<string, RoleCast>(), StringComparer.Ordinal);
        if (cast is null) roles.Remove(role);
        else roles[role] = cast;
        var places = new Dictionary<string, IReadOnlyDictionary<string, RoleCast>>(Places, StringComparer.OrdinalIgnoreCase);
        if (roles.Count == 0) places.Remove(spelled);
        else places[spelled] = roles;
        return new(places);
    }

    private static IEnumerable<(string Source, string Key)> Chain(string? workspace, string? repository)
    {
        var (w, r) = (Trimmed(workspace), Trimmed(repository));
        if (r is not null) yield return (PlaceRiskBook.OwnSource, $"repository/{r}");
        if (w is not null) yield return (r is null ? PlaceRiskBook.OwnSource : PlaceRiskBook.WorkspaceSource, $"workspace/{w}");
        yield return (r is null && w is null ? PlaceRiskBook.OwnSource : PlaceRiskBook.MachineSource, PlaceRiskBook.MachineKey);
    }

    private static string? Trimmed(string? name) => name?.Trim() is { Length: > 0 } trimmed ? trimmed : null;
}

/// <summary>The acts a cast's journal keeps: a place's cast, and a step taken, handed to another or given back to its cast.</summary>
public static class CastActs
{
    public const string Cast = "cast";
    public const string Take = "take";
    public const string Hand = "hand";
    public const string GiveBack = "give-back";

    /// <summary>The acts on a run's step, in the order the window draws them.</summary>
    public static IReadOnlyList<string> OnStep { get; } = [Take, Hand, GiveBack];
}

/// <summary>
/// One act the journal keeps, with its reason: a place's cast (<see cref="Place"/>) or a hand-over of a run's step (<see cref="Run"/>,
/// <see cref="Node"/>, its <see cref="Attempt"/> and the <see cref="Epoch"/> it began).
/// </summary>
public sealed record CastEntry(DateTimeOffset At, string Act, string Role, string? Holder, string? Why)
{
    public string? Place { get; init; }

    public string? Run { get; init; }

    public string? Node { get; init; }

    public int? Attempt { get; init; }

    public int? Epoch { get; init; }
}
