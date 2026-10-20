using Daoris.Driver;
using Shenora.Core.Events;

namespace Daoris.Desktop;

/// <summary>
/// Each repository's line and its rules as every window holds them, the push path's second slice (PUSH1b1, D182): judged from the
/// machine's facts and the driver's file, never from git or the service, and pushed per repository when one moves, so a tick reads
/// <c>LINES</c> again in no window.
/// </summary>
/// <remarks>
/// <para><b>One builder, two doors.</b> A partition is what <c>LINES</c> answers (<see cref="LinesRoutes.Answer"/>) for that
/// repository's rows, so the pull and the push cannot drift: a field <c>LINES</c> comes to answer is classed in <see cref="Fields"/>
/// or <see cref="PulledOnly"/>, which <c>LinesStateSliceTests</c> holds. The line is read as far as the machine's facts judged the
/// checkout's guess (<see cref="CanonicalLine.Judged"/>), as Ask Daoris's room reads it.</para>
///
/// <para><b>A partition per repository</b>, named by it: a ref moved in one repository, whose line is judged again alone
/// (<see cref="HelpFacts"/>), pushes that partition alone, and a line or a rule set for a workspace pushes its repositories'. A
/// repository whose line was not judged yet is pushed with its rules and its line said not read yet (no branch,
/// <c>notReadYet</c>), never left out, which a window would read as removed; one the registry no longer names is pushed gone.</para>
///
/// <para><b>Not judged until the facts land.</b> The slices start with the loop, the machine's facts once the service answers:
/// until the service's answers were judged the slice answers <see cref="StateSlice.NotJudged"/>, never an empty slice as the
/// truth.</para>
///
/// <para><b>Its wakes.</b> The facts it reads landing: the service's answers (judged again at a ledger move, the driver's file
/// moving and their minute's deadline) and each repository's guess, by their values' references, as <see cref="HelpFacts.Keep"/>
/// hears the answers; a change a press declared to the driver's file, the lines or the agents; and the service coming up. A
/// registry change a press declared moves no ledger family, so the slice's owner has the service's answers judged again at once:
/// a repository imported from a window is a partition without waiting for their deadline.</para>
///
/// <para>No machine path: a row names its repository and its workspace, never where its checkout is.</para>
/// </remarks>
public static class LinesStateSlice
{
    /// <summary>The slice's name, in the page's words.</summary>
    public const string Name = "lines";

    /// <summary>What each partition carries: every field of <c>LINES</c>' answer, each holding that repository's rows of it.</summary>
    public static readonly IReadOnlyList<string> Fields = ["lines", "landings", "languages", "reviews", "opinions", "helpers", "handOver", "commits", "access"];

    /// <summary>What <c>LINES</c> answers that is never pushed: none, every field a view reads.</summary>
    public static readonly IReadOnlyList<string> PulledOnly = [];

    /// <summary>
    /// What wakes it on the kit's bus: a change a press declared to the driver's file (a line, a language, a review, an opinion, a
    /// helper or a commit rule, an access setup), to the lines (a landing rule) or to the agents (the families an opinion rule names); and the service
    /// coming up. The facts it reads wake it too (<see cref="HearFacts"/>).
    /// </summary>
    public static readonly IReadOnlyList<SliceWake> WakesOn =
    [
        new(StatePublisher.Type, payload => payload is StateChanged changed
            && changed.Scopes.Any(scope => scope is "driver" or "lines" or "harnesses")),
        new("DRIVER_READY"),
    ];

    /// <summary>The slice the shell's loop owns, judging from <paramref name="loop"/>'s facts and its driver's file.</summary>
    public static StateSlice Of(DriverLoop loop, IEventBus bus) =>
        new(Name, bus, _ => Task.FromResult(Judge(loop.Facts.Current, DriverConfig.Load(loop.ConfigPath), loop.Harnesses.Adapters)))
        {
            WakesOn = WakesOn,
            Hears = [wake => HearFacts(loop.Facts, wake), _ => HearRegistry(bus, loop.Facts)],
            // The file unreadable: the windows keep what was judged, and the next wake judges again.
            Failed = error => loop.Log?.Warn("state.slice", ("slice", Name), ("message", error.Message)),
        };

    /// <summary>
    /// One judgement: each repository the service's answers name, a partition of its rows of <c>LINES</c>' answer, its line as far as
    /// its checkout's guess was judged; null where the service's answers were never judged, which judges nothing. Reads nothing
    /// outside <paramref name="facts"/>, <paramref name="config"/> and <paramref name="adapters"/>: no git, no service.
    /// </summary>
    public static IReadOnlyList<SliceJudged>? Judge(MachineFactsSnapshot facts, DriverConfig config, AdapterSet adapters)
    {
        if (facts.Value<HelpMachineWhole>(HelpFacts.Machine, HelpFacts.Whole) is not { } whole) return null;
        return
        [
            .. whole.Snapshot.Repositories
                .OrderBy(known => known.Repository, StringComparer.Ordinal)
                .GroupBy(known => known.Repository, StringComparer.Ordinal)
                .Select(rows => new SliceJudged(rows.Key, LinesRoutes.Answer([.. rows.Select(known => Row(facts, config, known))], config, adapters))),
        ];
    }

    /// <summary>A repository's line as far as it was judged; with no guess judged yet, none, said not read yet.</summary>
    private static LineRow Row(MachineFactsSnapshot facts, DriverConfig config, RepoView known)
    {
        var workspace = RemoteTarget.Workspace(known.Workspace);
        var guessed = facts.Value<HelpLineGuess>(HelpFacts.Lines, known.Repository);
        return CanonicalLine.Judged(config, known.Repository, known.Workspace, known.Root, guessed) is { } line
            ? new LineRow(known.Repository, workspace, line.Branch, line.Source)
            : new LineRow(known.Repository, workspace, null, LineSource.None) { NotReadYet = true };
    }

    /// <summary>
    /// Wake the slice (<paramref name="wake"/>) where a fact it is judged from landed with a value other than the one last heard:
    /// the service's answers, or a repository's guess at its line, a dropped one among them. The sessions, the history and the work
    /// land often and move nothing here; a snapshot told after a newer one is passed over. Disposed, it hears no more.
    /// </summary>
    public static IDisposable HearFacts(MachineFacts facts, Action wake)
    {
        var heard = new Heard(wake, facts.Current);
        facts.Changed += heard.Landed;
        return new Hearing(() => facts.Changed -= heard.Landed);
    }

    /// <summary>
    /// The service's answers judged again where a press declared the registry changed (a repository imported, retired or moved from
    /// a window), which no ledger family tells: their landing wakes the slice. Disposed, it hears no more.
    /// </summary>
    public static IDisposable HearRegistry(IEventBus bus, MachineFacts facts) =>
        bus.Subscribe(StateSlice.Module, StatePublisher.Type, message =>
        {
            if (message.Payload is StateChanged changed && changed.Scopes.Contains("registry")) facts.Wake(HelpFacts.Machine, HelpFacts.Whole);
            return Task.CompletedTask;
        });

    /// <summary>What the slice last heard of its facts: their snapshot's number, and each value by its reference.</summary>
    private sealed class Heard
    {
        private readonly Action _wake;
        private readonly Lock _gate = new();
        private long _version;
        private object? _machine;
        private Dictionary<string, object> _lines;

        public Heard(Action wake, MachineFactsSnapshot now)
        {
            _wake = wake;
            (_version, _machine, _lines) = Of(now);
        }

        public void Landed(MachineFactsSnapshot snapshot)
        {
            bool moved;
            lock (_gate)
            {
                if (snapshot.Version <= _version) return;
                var (version, machine, lines) = Of(snapshot);
                moved = !ReferenceEquals(machine, _machine) || lines.Count != _lines.Count
                    || lines.Any(each => !_lines.TryGetValue(each.Key, out var seen) || !ReferenceEquals(seen, each.Value));
                (_version, _machine, _lines) = (version, machine, lines);
            }

            if (moved) _wake();
        }

        private static (long Version, object? Machine, Dictionary<string, object> Lines) Of(MachineFactsSnapshot snapshot)
        {
            var lines = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var subject in snapshot.Subjects(HelpFacts.Lines))
            {
                if (snapshot.Of(HelpFacts.Lines, subject)?.Value is { } value) lines[subject] = value;
            }

            return (snapshot.Version, snapshot.Of(HelpFacts.Machine, HelpFacts.Whole)?.Value, lines);
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
