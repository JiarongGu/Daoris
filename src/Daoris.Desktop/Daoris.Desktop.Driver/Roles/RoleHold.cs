using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The two kinds of appointed role in the catalogue: the supervisor, who answers the decisions of the kinds it decides under its grants
/// (<see cref="SupervisorKinds"/>) or leaves them to the person, and the steward, who makes development's management acts on the person's
/// behalf. A record names its kind; its acts are of that kind's vocabulary, as a working role's works are of the work vocabulary.
/// </summary>
public static class AppointedRoles
{
    public const string Supervisor = "supervisor";
    public const string Steward = "steward";

    public static IReadOnlyList<string> Kinds { get; } = [Steward, Supervisor];

    /// <summary>The catalogue's one steward among <paramref name="appointed"/>, which works for the machine alone; null where none reads.</summary>
    public static RoleRecord? StewardOf(IEnumerable<RoleRecord> appointed) => appointed.FirstOrDefault(role => role.Hold?.Kind == Steward);

    /// <summary>The steward's acts: the nine of its table, then the set-up act, held only where the person lists it.</summary>
    public static IReadOnlyList<string> StewardActs { get; } =
    [
        StewardActNames.Drive, StewardActNames.Undrive, StewardActNames.Hold, StewardActNames.Resume,
        StewardActNames.Stop, StewardActNames.Finish, StewardActNames.Retry, StewardActNames.Workflow,
        StewardActNames.Pause, StewardActNames.Unpause, StewardActNames.Done, StewardActNames.Delete,
        StewardActNames.UpdateNow, StewardActNames.UpdateCancel, StewardActNames.Clean, StewardActNames.HandOver,
        StewardActNames.Setup,
    ];

    /// <summary>The acts <paramref name="kind"/> may hold; none for a kind this build does not know.</summary>
    public static IReadOnlyList<string> ActsOf(string kind) => kind switch
    {
        Supervisor => SupervisorKinds.Acts,
        Steward => StewardActs,
        _ => [],
    };

    /// <summary>The kind whose acts <paramref name="acts"/> all are, or null where they are none, of both kinds, or unknown.</summary>
    public static string? KindOf(IReadOnlyList<string> acts) =>
        acts.Count == 0 ? null
        : acts.All(act => SupervisorKinds.Acts.Contains(act, StringComparer.Ordinal)) ? Supervisor
        : acts.All(act => StewardActs.Contains(act, StringComparer.Ordinal)) ? Steward
        : null;
}

/// <summary>The steward's acts by name, as <c>ROLE_ACT</c>'s <c>act</c> and a role's <c>authority</c> name them.</summary>
public static class StewardActNames
{
    public const string Drive = "drive";
    public const string Undrive = "undrive";
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

    /// <summary>A session's tree cleaned unforced.</summary>
    public const string Clean = "clean";
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
/// not staffed, which acts on nothing), the supervisor's window, the minutes an answer is held for the person's <i>Overrule</i>, and the
/// kinds of decision a supervisor decides (<c>decides</c>; none is its scope's generic supervisor).
/// </summary>
/// <remarks>
/// The supervisor's rules always act first and their act stands; its holder says who answers what they leave: <c>rules</c>, no one,
/// <c>terminal</c>, or <c>agent</c>, whose turns walk the role's own order (<see cref="AppointedStaffing"/>). The steward takes no agent.
/// </remarks>
public sealed record RoleHold(string Kind, IReadOnlyList<string> Acts, string? Holder, int Window = RoleHold.DefaultWindow)
{
    /// <summary>The window where a record names none, in minutes.</summary>
    public const int DefaultWindow = 5;

    /// <summary>The bounds a window keeps, in minutes: never 0.</summary>
    public const int ShortestWindow = 1, LongestWindow = 60;

    /// <summary>The kinds of decision a supervisor answers, of <see cref="SupervisorKinds.All"/>; none for the steward and a generic supervisor.</summary>
    public IReadOnlyList<string> Decides { get; init; } = [];

    /// <summary>Whether this is a supervisor answering every decision no supervisor of a kind takes in its scope.</summary>
    public bool Generic => Kind == AppointedRoles.Supervisor && Decides.Count == 0;

    /// <summary>Whether a terminal holds it.</summary>
    public bool Terminal => Holder == RoleHolders.Terminal;

    /// <summary>Whether <paramref name="minutes"/> is a window a supervisor may keep.</summary>
    public static bool InWindow(long minutes) => minutes is >= ShortestWindow and <= LongestWindow;

    /// <summary>
    /// The fields a record writes after <c>filled</c>: <c>kind</c>, a supervisor's <c>decides</c> where it names kinds, <c>authority</c>,
    /// <c>holder</c> where one holds it, the supervisor's <c>window</c>.
    /// </summary>
    public void Write(Utf8JsonWriter writer)
    {
        writer.WriteString("kind", Kind);
        if (Decides.Count > 0)
        {
            writer.WriteStartArray("decides");
            foreach (var kind in Decides) writer.WriteStringValue(kind);
            writer.WriteEndArray();
        }

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
        IReadOnlyList<string> decides = [];
        if (root.TryGetProperty("decides", out var kinds))
        {
            if (kinds.ValueKind != JsonValueKind.Array || kinds.EnumerateArray().Any(each => each.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(each.GetString())))
            {
                return (null, "`decides` is not a list of kinds of decision");
            }

            decides = [.. kinds.EnumerateArray().Select(each => each.GetString()!)];
        }

        var holder = root.TryGetProperty("holder", out var held) && held.ValueKind == JsonValueKind.String ? held.GetString() : null;
        if (holder is not null && !RoleHolders.All.Contains(holder, StringComparer.Ordinal)) return (null, $"is held by `{holder}`, a holder this build does not know");
        long window = DefaultWindow;
        if (root.TryGetProperty("window", out var minutes) && minutes.ValueKind != JsonValueKind.Null)
        {
            if (minutes.ValueKind != JsonValueKind.Number || !minutes.TryGetInt64(out window)) return (null, "`window` is not whole minutes");
        }

        return InWindow(window)
            ? (new RoleHold(kind, acts, holder, (int)window) { Decides = decides }, null)
            : (null, $"holds answers {window} minutes, outside {ShortestWindow} to {LongestWindow}");
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

        if (kind != AppointedRoles.Supervisor && hold.Decides.Count > 0) return new("bad-record", $"`{role.Id}` is a {kind}, which decides no kind of decision.");
        if (hold.Decides.FirstOrDefault(each => !SupervisorKinds.Known(each)) is { } strange)
        {
            return new("bad-record", $"`{strange}` is no kind of decision: one of {string.Join(", ", SupervisorKinds.All)}.");
        }

        if (hold.Decides.Distinct(StringComparer.Ordinal).Count() != hold.Decides.Count) return new("bad-record", $"`{role.Id}` names a kind of decision twice.");
        if (kind == AppointedRoles.Supervisor && hold.Acts.FirstOrDefault(act => !SupervisorGrants.Decides(role, SupervisorKinds.KindOf(act)!)) is { } outside)
        {
            return new("bad-record", $"`{outside}` is an act of a kind `{role.Id}` does not decide: it decides {string.Join(", ", hold.Decides)}.");
        }

        if (role.Scope.Kind != RoleScope.Machine && hold.Decides.FirstOrDefault(SupervisorKinds.MachineOnly) is { } machine)
        {
            return new("machine-only", $"`{role.Id}` decides `{machine}`, whose every subject is this machine's: only a supervisor placed at the machine decides it.");
        }

        if (kind == AppointedRoles.Steward && hold.Holder == RoleHolders.Agent)
        {
            return new("appointed-not-configurable", $"`{role.Id}` is the steward, which no agent holds: a terminal holds it, or no one.");
        }

        if (kind == AppointedRoles.Supervisor && hold.Holder is null)
        {
            return new("bad-record", $"`{role.Id}` is a supervisor: your rules hold it, alone, with a terminal or with an agent.");
        }

        if (kind == AppointedRoles.Steward && hold.Holder == RoleHolders.Rules) return new("bad-record", $"`{role.Id}` is a steward: a terminal holds it, or no one.");
        if (kind == AppointedRoles.Steward && role.Scope.Kind != RoleScope.Machine) return new("bad-record", $"`{role.Id}` is a steward, which works for this machine alone.");
        return InWindow(hold.Window) ? null : new("bad-record", $"`{role.Id}`'s answers are held from {ShortestWindow} to {LongestWindow} minutes for your overrule.");
    }
}
