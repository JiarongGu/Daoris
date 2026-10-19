using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// The clean-up's list as every window holds it (PUSH1c4a, D182 points 3-4): what <c>SWEEP_PLAN</c> answers, judged from the machine's
/// facts' parts of the list (<see cref="HelpFacts.Sweep"/>, each repository's git half by the pull-request answers the landing record
/// keeps) and the facts' sessions' look, with no git, no service call and no plugin asked, and pushed per repository when one moves, so a
/// tick reads <c>SWEEP_PLAN</c> in no window and a mounted window asks the pull-request plugin nothing.
/// </summary>
/// <remarks>
/// <para><b>One builder, two doors.</b> A repository's part is written by <c>SWEEP_PLAN</c>'s own builder
/// (<see cref="LinesRoutes.SweepAnswer"/>: <see cref="LinesRoutes.SweepRow"/>, <see cref="LinesRoutes.LandedRow"/>), then kept to the
/// allow-list (<see cref="Fields"/>): a field the builders come to write is pushed only once it is named, and <c>SweepStateSliceTests</c>
/// classes every field the route writes and every field the page's rows read.</para>
///
/// <para><b>A partition per repository with a checkout here</b>, named by it, each <c>{branches, landed}</c>; none for a repository with
/// none, which the list never judges. A ref moved in one repository judges its part alone (<see cref="HelpFacts"/>), so it pushes that
/// partition alone. A row's session is read from the facts' look (<see cref="SessionLook.Records"/>), never the service's.</para>
///
/// <para><b>Not judged until the list is.</b> Until the service's answers land, and until every part they name and the sessions' look
/// were judged once, the slice answers <see cref="StateSlice.NotJudged"/>, so a window keeps the pull rather than install the parts
/// judged so far as the whole list, or rows that say no session where one would be named. After that, a part not judged yet (a
/// repository registered since) is left out until it is.</para>
///
/// <para><b>Its wakes.</b> The service's answers, the list's parts and the sessions' look landing, by their values' references
/// (<see cref="HearFacts"/>); a landing's or a sweep's declared scope judges every part afresh, the sessions' under the rest
/// (<see cref="HearPresses"/>), as the work's; and the person's look, whose kept answers wake their repositories' parts afresh
/// (<see cref="Asked"/>), since the landing record is a file no watch hears and the look moves no ref.</para>
///
/// <para><b>Desktop-only.</b> No field is a machine path (a row says whether its tree is here, never where), but a row's detail quotes
/// git's own words, which may name a tree's files (D182 point 2).</para>
/// </remarks>
public static class SweepStateSlice
{
    /// <summary>The slice's name, in the page's words.</summary>
    public const string Name = "sweep";

    /// <summary>
    /// The allow-list: each shape a partition carries, and the fields of it pushed, in the builders' order. <c>part</c> is a repository's
    /// partition; the rest are what it holds (<see cref="Holds"/>).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Fields = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        ["part"] = ["branches", "landed"],
        ["branch"] =
        [
            "repository", "workspace", "branch", "hasTree", "kind", "commits", "where", "detail", "removable", "discardable", "session",
            "landable",
        ],
        ["session"] = ["id", "state"],
        ["landed"] = ["repository", "workspace", "branch", "kind", "where", "files", "detail", "pullRequest", "commits", "removable"],
    };

    /// <summary>Which shape a field holds, as <c>shape.field</c>, an object of it or a list of them; every other field is a value.</summary>
    public static readonly IReadOnlyDictionary<string, string> Holds = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["part.branches"] = "branch",
        ["part.landed"] = "landed",
        ["branch.session"] = "session",
    };

    /// <summary>What <c>SWEEP_PLAN</c> answers that is never pushed, as <c>shape.field</c>: none, the page's rows read it all.</summary>
    public static readonly IReadOnlyList<string> PulledOnly = [];

    /// <summary>The slice the shell's loop owns, judging from <paramref name="loop"/>'s facts.</summary>
    public static StateSlice Of(DriverLoop loop, IEventBus bus) => Of(loop.Facts, bus, loop.Log);

    /// <summary>The slice over <paramref name="facts"/>.</summary>
    public static StateSlice Of(MachineFacts facts, IEventBus bus, MachineLog? log = null)
    {
        // Whether a judgement found every part and the look judged: until one has, the slice stays not judged. Read and written only
        // within a judgement's turn, which the slice runs one at a time.
        var whole = false;
        return new(Name, bus, _ =>
        {
            var current = facts.Current;
            var judged = Judge(HelpFacts.SweepParts(current), current.Value<SessionLook>(HelpFacts.Sessions, HelpFacts.Whole), partly: whole);
            whole |= judged is not null;
            return Task.FromResult(judged);
        })
        {
            Hears = [wake => HearFacts(facts, wake), _ => HearPresses(bus, facts)],
            Failed = error => log?.Warn("state.slice", ("slice", Name), ("message", error.Message)),
        };
    }

    /// <summary>
    /// One judgement: each repository's part judged, a partition of what <c>SWEEP_PLAN</c> writes of it, its rows' sessions read from
    /// <paramref name="look"/>. Null where the service's answers were never judged (<paramref name="parts"/> null), or, unless
    /// <paramref name="partly"/>, while any part or the look was never judged: nothing to judge from yet.
    /// </summary>
    /// <param name="parts">The facts' parts (<see cref="HelpFacts.SweepParts"/>), each as last judged or null.</param>
    /// <param name="look">The sessions' look as the facts hold it; once judged whole, none reads no record, as the door's records that do not read.</param>
    /// <param name="partly">Whether a part never judged is left out rather than leaving the slice not judged: once one judgement was whole.</param>
    public static IReadOnlyList<SliceJudged>? Judge(IReadOnlyList<(string Part, SweepPlan? Plan)>? parts, SessionLook? look, bool partly)
    {
        if (parts is null || (!partly && (look is null || parts.Any(part => part.Plan is null)))) return null;
        var records = look?.Records ?? [];
        return
        [
            .. parts.Where(part => part.Plan is not null)
                .Select(part => new SliceJudged(part.Part, Allowed(LinesRoutes.SweepAnswer(part.Plan!, records), "part"))),
        ];
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
    /// The person's look asked (PUSH1c4a, D148 point 2): each repository whose kept pull-request answers it wrote (an answer or a failed
    /// ask, read from the landing record before and after it) has its part judged again afresh, past the rest, so the pushed list says
    /// what the look heard at once. The record is a file no watch hears and the look moves no ref, so nothing else would tell the part
    /// before its deadline. Only the parts the facts name: a repository with no checkout here has none.
    /// </summary>
    /// <returns>The repositories whose parts were woken.</returns>
    public static IReadOnlyList<string> Asked(MachineFacts facts, IReadOnlyList<LandedBranch> before, IReadOnlyList<LandedBranch> after)
    {
        if (HelpFacts.SweepParts(facts.Current) is not { } parts) return [];
        var (was, now) = (Kept(before), Kept(after));
        bool Same(string part) => was.TryGetValue(part, out var then)
            ? now.TryGetValue(part, out var since) && then.SequenceEqual(since)
            : !now.ContainsKey(part);
        var moved = parts.Select(part => part.Part).Where(part => !Same(part)).ToList();
        foreach (var part in moved) facts.Wake(HelpFacts.Sweep, part, afresh: true);
        return moved;
    }

    /// <summary>Each repository's kept answers, entry by entry in the record's order: what an ask writes and what it was written of.</summary>
    private static Dictionary<string, List<(string Branch, string Tip, PullRequestState? State, PullRequestAskFailed? Failed)>> Kept(
        IEnumerable<LandedBranch> entries) =>
        entries.GroupBy(entry => entry.Repository, StringComparer.OrdinalIgnoreCase).ToDictionary(
            repository => repository.Key,
            repository => repository.Select(entry => (entry.Branch, entry.Tip, entry.PullRequestState, entry.PullRequestAskFailed)).ToList(),
            StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Wake the slice (<paramref name="wake"/>) where a fact it is judged from landed with a value other than the one last heard: the
    /// service's answers, which name the parts, a part of the list, a dropped one among them, or the sessions' look its rows name their
    /// sessions from. The lines, the work, the trees and the history move nothing here; a snapshot told after a newer one is passed over.
    /// Disposed, it hears no more.
    /// </summary>
    public static IDisposable HearFacts(MachineFacts facts, Action wake)
    {
        var heard = new Heard(wake, facts.Current);
        facts.Changed += heard.Landed;
        return new Hearing(() => facts.Changed -= heard.Landed);
    }

    /// <summary>
    /// The list's parts asked again where a press declared what may move them with no ref the watch hears as Daoris's own (as the work's,
    /// PUSH1c2a): a landing's or a sweep's scope, which a clean-up, a discard and a recovery's drop declare, wakes every part afresh, past
    /// its rest, as the person waits on it; the sessions', told at every record's move, wakes them under it, a burst one judgement each
    /// rest, since afresh it would walk every repository's branches at each record's move. Their landing wakes the slice. Disposed, it
    /// hears no more.
    /// </summary>
    public static IDisposable HearPresses(IEventBus bus, MachineFacts facts) =>
        bus.Subscribe(StateSlice.Module, StatePublisher.Type, message =>
        {
            if (message.Payload is not StateChanged changed || HelpFacts.SweepParts(facts.Current) is not { } parts) return Task.CompletedTask;
            var afresh = changed.Scopes.Any(scope => scope is "landing" or "sweep");
            if (!afresh && !changed.Scopes.Contains("sessions")) return Task.CompletedTask;
            foreach (var (part, _) in parts) facts.Wake(HelpFacts.Sweep, part, afresh);
            return Task.CompletedTask;
        });

    /// <summary>What the slice last heard of its facts: their snapshot's number, and each value by its reference.</summary>
    private sealed class Heard
    {
        private readonly Action _wake;
        private readonly Lock _gate = new();
        private long _version;
        private object? _machine;
        private object? _look;
        private Dictionary<string, object> _parts;

        public Heard(Action wake, MachineFactsSnapshot now)
        {
            _wake = wake;
            (_version, _machine, _look, _parts) = Of(now);
        }

        public void Landed(MachineFactsSnapshot snapshot)
        {
            bool moved;
            lock (_gate)
            {
                if (snapshot.Version <= _version) return;
                var (version, machine, look, parts) = Of(snapshot);
                moved = !ReferenceEquals(machine, _machine) || !ReferenceEquals(look, _look) || parts.Count != _parts.Count
                    || parts.Any(each => !_parts.TryGetValue(each.Key, out var seen) || !ReferenceEquals(seen, each.Value));
                (_version, _machine, _look, _parts) = (version, machine, look, parts);
            }

            if (moved) _wake();
        }

        private static (long Version, object? Machine, object? Look, Dictionary<string, object> Parts) Of(MachineFactsSnapshot snapshot)
        {
            var parts = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var subject in snapshot.Subjects(HelpFacts.Sweep))
            {
                if (snapshot.Of(HelpFacts.Sweep, subject)?.Value is { } value) parts[subject] = value;
            }

            return (snapshot.Version, snapshot.Of(HelpFacts.Machine, HelpFacts.Whole)?.Value,
                snapshot.Of(HelpFacts.Sessions, HelpFacts.Whole)?.Value, parts);
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
