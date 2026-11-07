using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The two kinds of appointed role in the catalogue: the supervisor, who answers a parked session's recorded question with
/// one offered choice or leaves it to the person, and the steward, who makes development's management acts on the person's behalf. A
/// record names its kind; its acts are of that kind's vocabulary, as a working role's works are of the work vocabulary.
/// </summary>
public static class AppointedRoles
{
    public const string Supervisor = "supervisor";
    public const string Steward = "steward";

    /// <summary>Both kinds, in the screen's order.</summary>
    public static IReadOnlyList<string> Kinds { get; } = [Steward, Supervisor];

    /// <summary>The supervisor's acts: an answer to a parked question, and a question left to the person with a bound.</summary>
    public static IReadOnlyList<string> SupervisorActs { get; } = ["answer", "defer"];

    /// <summary>The steward's acts: the nine of its table, then the set-up act, held only where the person lists it.</summary>
    public static IReadOnlyList<string> StewardActs { get; } =
    [
        StewardActNames.Drive, StewardActNames.Undrive, StewardActNames.Trees, StewardActNames.Hold, StewardActNames.Resume,
        StewardActNames.Stop, StewardActNames.Finish, StewardActNames.Retry, StewardActNames.Workflow,
        StewardActNames.Pause, StewardActNames.Unpause, StewardActNames.Done, StewardActNames.Delete,
        StewardActNames.UpdateNow, StewardActNames.UpdateCancel, StewardActNames.Land, StewardActNames.Clean, StewardActNames.HandOver,
        StewardActNames.Setup,
    ];

    /// <summary>The acts <paramref name="kind"/> may hold; none for a kind this build does not know.</summary>
    public static IReadOnlyList<string> ActsOf(string kind) => kind switch
    {
        Supervisor => SupervisorActs,
        Steward => StewardActs,
        _ => [],
    };

    /// <summary>The kind whose acts <paramref name="acts"/> all are, or null where they are none, of both kinds, or unknown.</summary>
    public static string? KindOf(IReadOnlyList<string> acts) =>
        acts.Count == 0 ? null
        : acts.All(act => SupervisorActs.Contains(act, StringComparer.Ordinal)) ? Supervisor
        : acts.All(act => StewardActs.Contains(act, StringComparer.Ordinal)) ? Steward
        : null;
}

/// <summary>The steward's acts by name, as <c>ROLE_ACT</c>'s <c>act</c> and a role's <c>authority</c> name them.</summary>
public static class StewardActNames
{
    public const string Drive = "drive";
    public const string Undrive = "undrive";
    public const string Trees = "trees";
    public const string Hold = "hold";
    public const string Resume = "resume";
    public const string Stop = "stop";
    public const string Finish = "finish";
    public const string Retry = "retry";
    public const string Workflow = "workflow";

    /// <summary>A quest's work paused and resumed in the steward's own words (never the person's stop), its done, its delete.</summary>
    public const string Pause = "pause";
    public const string Unpause = "unpause";
    public const string Done = "done";
    public const string Delete = "delete";

    /// <summary>A staged update installed now, or put away for that build.</summary>
    public const string UpdateNow = "updatenow";
    public const string UpdateCancel = "updatecancel";

    /// <summary>A session's tree landed on its line, or cleaned unforced; neither pushes.</summary>
    public const string Land = "land";
    public const string Clean = "clean";

    /// <summary>A quest's own session handed over to an agent it names, on an account it may name.</summary>
    public const string HandOver = "handover";
    public const string Setup = "setup";
}

/// <summary>Who holds an appointed role: a terminal the window starts, the person's own rules (the supervisor's alone), or an agent.</summary>
public static class RoleHolders
{
    public const string Terminal = "terminal";
    public const string Rules = "rules";
    public const string Agent = "agent";

    public static IReadOnlyList<string> All { get; } = [Terminal, Rules, Agent];
}

/// <summary>
/// What an appointed role's record holds beside its id, name and scope: its kind, its acts (<c>authority</c>), its holder (none is a role
/// not staffed, which acts on nothing), and the supervisor's window, the minutes an answer is held for the person's <i>Overrule</i>.
/// </summary>
/// <remarks>
/// The supervisor's rules always act first and their act stands; its holder says who answers what they leave: <c>rules</c>, no one,
/// or <c>terminal</c>. An <c>agent</c> holder reads as the record's shape and is refused by the judge until one is built.
/// </remarks>
public sealed record RoleHold(string Kind, IReadOnlyList<string> Acts, string? Holder, int Window = RoleHold.DefaultWindow)
{
    /// <summary>The window where a record names none, in minutes.</summary>
    public const int DefaultWindow = 5;

    /// <summary>The bounds a window keeps, in minutes: never 0.</summary>
    public const int ShortestWindow = 1, LongestWindow = 60;

    /// <summary>Whether a terminal holds it.</summary>
    public bool Terminal => Holder == RoleHolders.Terminal;

    /// <summary>Whether <paramref name="minutes"/> is a window a supervisor may keep.</summary>
    public static bool InWindow(long minutes) => minutes is >= ShortestWindow and <= LongestWindow;

    /// <summary>The fields a record writes after <c>filled</c>: <c>kind</c>, <c>authority</c>, <c>holder</c> where one holds it, the supervisor's <c>window</c>.</summary>
    public void Write(Utf8JsonWriter writer)
    {
        writer.WriteString("kind", Kind);
        writer.WriteStartArray("authority");
        foreach (var act in Acts) writer.WriteStringValue(act);
        writer.WriteEndArray();
        if (Holder is { } holder) writer.WriteString("holder", holder);
        if (Kind == AppointedRoles.Supervisor) writer.WriteNumber("window", Window);
    }

    /// <summary>The appointed fields of <paramref name="root"/>, or why they do not read.</summary>
    public static (RoleHold? Hold, string? Why) Read(JsonElement root)
    {
        var kind = root.TryGetProperty("kind", out var named) && named.ValueKind == JsonValueKind.String ? named.GetString() : null;
        if (kind is null || !AppointedRoles.Kinds.Contains(kind, StringComparer.Ordinal)) return (null, "names no `kind` of `supervisor` or `steward`");
        if (!root.TryGetProperty("authority", out var listed) || listed.ValueKind != JsonValueKind.Array
            || listed.EnumerateArray().Any(act => act.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(act.GetString())))
        {
            return (null, "`authority` is not a list of acts");
        }

        IReadOnlyList<string> acts = [.. listed.EnumerateArray().Select(act => act.GetString()!)];
        var holder = root.TryGetProperty("holder", out var held) && held.ValueKind == JsonValueKind.String ? held.GetString() : null;
        if (holder is not null && !RoleHolders.All.Contains(holder, StringComparer.Ordinal)) return (null, $"is held by `{holder}`, a holder this build does not know");
        var window = root.TryGetProperty("window", out var minutes) && minutes.TryGetInt64(out var set) ? set : DefaultWindow;
        return InWindow(window) ? (new RoleHold(kind, acts, holder, (int)window), null) : (null, $"holds answers {window} minutes, outside {ShortestWindow} to {LongestWindow}");
    }

    /// <summary>
    /// The judge's word on <paramref name="role"/>'s appointed fields, for every door alike, or null where they may be set: a known kind,
    /// acts of its vocabulary, a holder that kind takes, the steward on the machine alone, and a window within its bounds.
    /// </summary>
    public static RoleRefusal? Judge(RoleRecord role)
    {
        if (role.Hold is not { } hold) return new("mixed-shape", $"`{role.Id}` is appointed and holds no acts: a role takes work or holds acts.");
        var kind = hold.Kind;
        if (!AppointedRoles.Kinds.Contains(kind, StringComparer.Ordinal)) return new("bad-record", $"`{role.Id}` is of no kind this build knows: `supervisor` or `steward`.");
        if (hold.Acts.FirstOrDefault(act => !AppointedRoles.ActsOf(kind).Contains(act, StringComparer.Ordinal)) is { } unknown)
        {
            return new("bad-record", $"`{unknown}` is no act of the {kind}'s: one of {string.Join(", ", AppointedRoles.ActsOf(kind))}.");
        }

        if (hold.Holder == RoleHolders.Agent)
        {
            return new("appointed-not-configurable", $"`{role.Id}` is held by an agent, which this build does not run yet: a terminal holds it{(kind == AppointedRoles.Supervisor ? ", or your rules alone" : "")}.");
        }

        if (kind == AppointedRoles.Supervisor && hold.Holder is null)
        {
            return new("bad-record", $"`{role.Id}` is a supervisor: your rules hold it, alone or with a terminal.");
        }

        if (kind == AppointedRoles.Steward && hold.Holder == RoleHolders.Rules) return new("bad-record", $"`{role.Id}` is a steward: a terminal holds it, or no one.");
        if (kind == AppointedRoles.Steward && role.Scope.Kind != RoleScope.Machine) return new("bad-record", $"`{role.Id}` is a steward, which works for this machine alone.");
        return InWindow(hold.Window) ? null : new("bad-record", $"`{role.Id}`'s answers are held from {ShortestWindow} to {LongestWindow} minutes for your overrule.");
    }
}
