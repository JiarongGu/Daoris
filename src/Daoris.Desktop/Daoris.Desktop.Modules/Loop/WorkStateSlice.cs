using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// Where each unit of work has got to as every window holds it (PUSH1c2a, D182 points 3-4): what <c>WORK_STATE</c> answers, judged from
/// the machine's facts' work parts (FACTSPLIT1a) and the node records' held attempts, never from git or the service, and pushed per
/// repository when one moves, so a tick reads <c>WORK_STATE</c> again in no window.
/// </summary>
/// <remarks>
/// <para><b>One builder, two doors.</b> A unit and an entry are written by <c>WORK_STATE</c>'s own builders
/// (<see cref="LinesRoutes.WorkUnit"/>, <see cref="LinesRoutes.WorkEntry"/>, <see cref="LinesRoutes.HeldAnswer"/>), then kept to the
/// allow-list (<see cref="Fields"/>): a field the builders come to write is pushed only once it is named, and
/// <c>WorkStateSliceTests</c> classes every field the route answers.</para>
///
/// <para><b>A partition per part</b>, named as the facts name it: each repository with a checkout here, and
/// <see cref="HelpFacts.NoCheckout"/> for those with none, each <c>{units, recovery, unread}</c>, its part as last judged. A ref moved in
/// one repository judges its part alone (<see cref="HelpFacts"/>), so it pushes that partition alone. A part never judged is pushed
/// nothing, never an empty list, which a window would read as nothing to clean up; one the registry no longer names is pushed gone. The
/// attempts held for the person are a partition of their own, <see cref="Held"/>, read from the node records at each judgement (a file
/// read: no git, no service).</para>
///
/// <para><b>Not judged until the work is.</b> Until the service's answers land, and until every part they name was judged once, the
/// slice answers <see cref="StateSlice.NotJudged"/>, so a window keeps the pull rather than install the parts judged so far as all
/// the work there is. After that, a part not judged yet (a repository registered since) is left out until it is.</para>
///
/// <para><b>Its wakes.</b> The service's answers and the work's parts landing, by their values' references (<see cref="HearFacts"/>);
/// and a change a press declared to the landings, the clean-up or the sessions, which also reads the held attempts afresh. A landing
/// moves its repository's refs as Daoris's own move, which judges its part at once (<c>RefMoves.Mine</c>, OWNMOVES1), but the clean-up,
/// a discard, a recovery's drop and a work press tell no own move, and a tree-only removal or a recovery ref's delete may reach the ref
/// watch not at all: so a landing's or a sweep's declared scope wakes every part afresh, past the rest a burst of refs is held to, and
/// the sessions' wakes them under it (<see cref="HearPresses"/>). An attempt the runtime holds says nothing: the slice's period reads
/// it.</para>
///
/// <para><b>Desktop-only.</b> A tree's and a folder moved aside's resource is this machine's path, and an unread line may quote git's
/// own (D182 point 2).</para>
/// </remarks>
public static class WorkStateSlice
{
    /// <summary>The slice's name, in the page's words.</summary>
    public const string Name = "work";

    /// <summary>
    /// The partition of the attempts held for the person: a name no repository can hold, as <see cref="HelpFacts.NoCheckout"/> is (a
    /// repository's trees are kept in a folder of its name, which cannot hold <c>&lt;</c>).
    /// </summary>
    public const string Held = "<held>";

    /// <summary>
    /// The allow-list: each shape a partition carries, and the fields of it pushed. <c>part</c> is a repository's partition,
    /// <c>held</c> the held attempts'; the rest are what they hold (<see cref="Holds"/>).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Fields = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        ["part"] = ["units", "recovery", "unread"],
        ["held"] = ["held"],
        ["unit"] = ["unit", "repository", "branch", "session", "end", "result", "cleanup", "recovery", "entries"],
        ["end"] = ["state", "by", "on", "revision", "at", "said"],
        ["cleanup"] = ["state", "waits", "gone"],
        ["entry"] = ["unit", "kind", "resource", "role", "disposition", "detail", "until", "proof", "repository", "commit", "code", "facts"],
        ["attempt"] =
        [
            "unit", "node", "attempt", "plugin", "step", "nodeState", "why", "outcome", "revision", "at", "resolution", "resolvedAt", "said",
            "words", "code",
        ],
    };

    /// <summary>Which shape a field holds, as <c>shape.field</c>, an object of it or a list of them; every other field is a value.</summary>
    public static readonly IReadOnlyDictionary<string, string> Holds = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["part.units"] = "unit",
        ["part.recovery"] = "entry",
        ["held.held"] = "attempt",
        ["unit.end"] = "end",
        ["unit.result"] = "entry",
        ["unit.cleanup"] = "cleanup",
        ["unit.recovery"] = "entry",
        ["unit.entries"] = "entry",
        ["cleanup.waits"] = "entry",
        ["cleanup.gone"] = "entry",
    };

    /// <summary>What <c>WORK_STATE</c> answers that is never pushed, as <c>shape.field</c>: none, the page's <c>WorkState</c> reads it all.</summary>
    public static readonly IReadOnlyList<string> PulledOnly = [];

    /// <summary>
    /// What wakes it on the kit's bus: a change a press declared to the landings, the clean-up or the sessions (an attempt worded
    /// among them, <c>WORK_HELD</c>). The facts it reads wake it too (<see cref="HearFacts"/>).
    /// </summary>
    public static readonly IReadOnlyList<SliceWake> WakesOn =
    [
        new(StatePublisher.Type, payload => payload is StateChanged changed
            && changed.Scopes.Any(scope => scope is "landing" or "sweep" or "sessions")),
    ];

    /// <summary>The slice the shell's loop owns, judging from <paramref name="loop"/>'s facts and its home's node records.</summary>
    public static StateSlice Of(DriverLoop loop, IEventBus bus) =>
        Of(loop.Facts, bus, () => HeldAttempts.Read(new NodeRecordFiles(loop.Home), quests: loop.LastQuests), loop.Log);

    /// <summary>The slice over <paramref name="facts"/>, its held attempts read by <paramref name="held"/> at each judgement.</summary>
    public static StateSlice Of(MachineFacts facts, IEventBus bus, Func<IReadOnlyList<HeldAttempt>> held, MachineLog? log = null)
    {
        // Whether a judgement found every part judged: until one has, a part never judged leaves the slice not judged. Read and written
        // only within a judgement's turn, which the slice runs one at a time.
        var whole = false;
        return new(Name, bus, _ =>
        {
            // The node records are read only once the service's answers name the parts: before, nothing is judged.
            var judged = HelpFacts.WorkParts(facts.Current) is { } parts ? Judge(parts, held(), partly: whole) : null;
            whole |= judged is not null;
            return Task.FromResult(judged);
        })
        {
            WakesOn = WakesOn,
            Hears = [wake => HearFacts(facts, wake), _ => HearPresses(bus, facts)],
            // A node record unreadable: the windows keep what was judged, and the next wake judges again.
            Failed = error => log?.Warn("state.slice", ("slice", Name), ("message", error.Message)),
        };
    }

    /// <summary>
    /// One judgement: each part judged, a partition of its units, its recoveries and what it could not read, as <c>WORK_STATE</c> writes
    /// them, and the held attempts' partition. Null where the service's answers were never judged (<paramref name="parts"/> null), or,
    /// unless <paramref name="partly"/>, while any part was never judged: nothing to judge from yet.
    /// </summary>
    /// <param name="parts">The facts' parts (<see cref="HelpFacts.WorkParts(MachineFactsSnapshot)"/>), each as last judged or null.</param>
    /// <param name="held">The attempts held for the person, every unit's.</param>
    /// <param name="partly">Whether a part never judged is left out rather than leaving the slice not judged: once one judgement was whole.</param>
    public static IReadOnlyList<SliceJudged>? Judge(IReadOnlyList<(string Part, HelpWork? Work)>? parts, IReadOnlyList<HeldAttempt> held, bool partly)
    {
        if (parts is null || (!partly && parts.Any(part => part.Work is null))) return null;
        var judged = new List<SliceJudged>();
        foreach (var (part, work) in parts)
        {
            if (work is null) continue;
            judged.Add(new SliceJudged(part, Allowed(new
            {
                Units = work.Units.Select(LinesRoutes.WorkUnit).ToArray(),
                Recovery = work.Recovery.Select(LinesRoutes.WorkEntry).ToArray(),
                Unread = work.Unread.Select(unread => unread.Said).ToArray(),
            }, "part")));
        }

        judged.Add(new SliceJudged(Held, Allowed(new { Held = LinesRoutes.HeldAnswer(held) }, "held")));
        return judged;
    }

    /// <summary>
    /// What <paramref name="written"/> may push as <paramref name="shape"/>: written as the wire writes it, then only the fields
    /// <see cref="Fields"/> names, in its order, and in each shape it holds the same.
    /// </summary>
    public static JsonObject Allowed(object written, string shape) =>
        Pick(JsonSerializer.SerializeToNode(written, IpcJson.Options), shape) as JsonObject ?? new JsonObject();

    private static JsonNode? Pick(JsonNode? node, string shape) => node switch
    {
        JsonArray list => new JsonArray(list.Select(each => Pick(each, shape)).ToArray()),
        JsonObject written => PickFields(written, shape),
        _ => node?.DeepClone(),
    };

    private static JsonObject PickFields(JsonObject written, string shape)
    {
        var picked = new JsonObject();
        foreach (var field in Fields[shape])
        {
            if (!written.TryGetPropertyValue(field, out var value)) continue;
            picked[field] = Holds.TryGetValue($"{shape}.{field}", out var inner) ? Pick(value, inner) : value?.DeepClone();
        }

        return picked;
    }

    /// <summary>
    /// Wake the slice (<paramref name="wake"/>) where a fact it is judged from landed with a value other than the one last heard: the
    /// service's answers, which name the parts, or a part of the work, a dropped one among them. The lines, the sessions, the trees and
    /// the history land often and move nothing here; a snapshot told after a newer one is passed over. Disposed, it hears no more.
    /// </summary>
    public static IDisposable HearFacts(MachineFacts facts, Action wake)
    {
        var heard = new Heard(wake, facts.Current);
        facts.Changed += heard.Landed;
        return new Hearing(() => facts.Changed -= heard.Landed);
    }

    /// <summary>
    /// The work's parts asked again where a press declared what may move them with no ref the watch hears as Daoris's own (PUSH1c2a): a
    /// landing's or a sweep's scope wakes every part afresh (<see cref="MachineFacts.Wake"/>'s <c>afresh</c>), past its rest, as the person
    /// waits on it; the sessions', told at every record's move, wakes them under it, a burst one judgement each rest. Every loop on the home
    /// keeps the facts and hears the scopes, a terminal's `trees` press's among them, where only the lease's holder runs the ref watch.
    /// Their landing wakes the slice. Disposed, it hears no more.
    /// </summary>
    public static IDisposable HearPresses(IEventBus bus, MachineFacts facts) =>
        bus.Subscribe(StateSlice.Module, StatePublisher.Type, message =>
        {
            if (message.Payload is not StateChanged changed || HelpFacts.WorkParts(facts.Current) is not { } parts) return Task.CompletedTask;
            var afresh = changed.Scopes.Any(scope => scope is "landing" or "sweep");
            if (!afresh && !changed.Scopes.Contains("sessions")) return Task.CompletedTask;
            foreach (var (part, _) in parts) facts.Wake(HelpFacts.Work, part, afresh);
            return Task.CompletedTask;
        });

    /// <summary>What the slice last heard of its facts: their snapshot's number, and each value by its reference.</summary>
    private sealed class Heard
    {
        private readonly Action _wake;
        private readonly Lock _gate = new();
        private long _version;
        private object? _machine;
        private Dictionary<string, object> _parts;

        public Heard(Action wake, MachineFactsSnapshot now)
        {
            _wake = wake;
            (_version, _machine, _parts) = Of(now);
        }

        public void Landed(MachineFactsSnapshot snapshot)
        {
            bool moved;
            lock (_gate)
            {
                if (snapshot.Version <= _version) return;
                var (version, machine, parts) = Of(snapshot);
                moved = !ReferenceEquals(machine, _machine) || parts.Count != _parts.Count
                    || parts.Any(each => !_parts.TryGetValue(each.Key, out var seen) || !ReferenceEquals(seen, each.Value));
                (_version, _machine, _parts) = (version, machine, parts);
            }

            if (moved) _wake();
        }

        private static (long Version, object? Machine, Dictionary<string, object> Parts) Of(MachineFactsSnapshot snapshot)
        {
            var parts = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var subject in snapshot.Subjects(HelpFacts.Work))
            {
                if (snapshot.Of(HelpFacts.Work, subject)?.Value is { } value) parts[subject] = value;
            }

            return (snapshot.Version, snapshot.Of(HelpFacts.Machine, HelpFacts.Whole)?.Value, parts);
        }
    }

    private sealed class Hearing(Action stop) : IDisposable
    {
        private int _stopped;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _stopped, 1) == 0) stop();
        }
    }
}
