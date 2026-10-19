using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// The session list's groups as every window holds them (PUSH1c3a, D182 points 2 to 4): what <c>SESSION_GROUPS</c> answers, each
/// session's row and each line of work's entry, judged once per wake for every window and pushed when it moves, so a tick reads
/// <c>SESSION_GROUPS</c> again in no window.
/// </summary>
/// <remarks>
/// <para><b>The pull's own look, once.</b> The judge takes the look <c>SESSION_GROUPS</c> takes (<see cref="LookAsync"/>): the records,
/// the quests, the loop's last verdicts (a fresh plan where no loop has looked), the marks, the waits and the calls read now, and each
/// tree as the machine's facts' parts judged it, a tree no part asked walked here (<see cref="HelpFacts.WithTreesAsync"/>). So a pushed
/// row is as fresh as the pull's, and the walk the pull made per window is made once. Not the facts' sessions' look, which may lag a
/// ledger move by half a minute. Null before the service answers, which keeps the slice not judged.</para>
///
/// <para><b>Two partitions, keyed</b> (<see cref="StateSlice.Keyed"/>): <see cref="Rows"/>, each row under its session as
/// <see cref="SessionsRoutes.Grouped"/> writes it, and <see cref="Entries"/>, each entry under its key as
/// <see cref="SessionsRoutes.Entry"/> writes it. A session's move pushes its row and its entry alone. Judged empty, a partition is pushed
/// empty, never gone.</para>
///
/// <para><b>The order is the window's to compose.</b> A list position is no field: one session moving would move every row after it.
/// Each row carries the sort inputs its group is ordered by (<see cref="OrderedBy"/>), which the route answers none of, and an entry is
/// placed by its leading row's place, as <see cref="SessionEntries.Read"/> places it. A working session's record, written at every word,
/// pushes nothing: its group is ordered by when it began.</para>
///
/// <para><b>Its wakes</b>, each only asking. The change stream's <c>sessions</c> and <c>quests</c> families (<see cref="HearLedger"/>),
/// which is how a connector's write arrives; a change declared to the sessions (an archive, a delete, a stop, the driver's own record
/// moves), a conversation's end and the service coming up; the loop's look told to the windows (<c>DRIVER_TICK</c>), where its verdicts
/// or its waits moved, which is when every window pulled the groups before; a conversation's turns moving (<c>SESSION_QUEUED</c>), whose
/// end offers its tree's work to land (ACCEPTFOOT1b); and a tree's part landing (<see cref="HearTrees"/>). Never a
/// running call's move: <c>calling</c> is the pull's, and the <c>calls</c> slice pushes it.</para>
///
/// <para><b>Desktop-only.</b> No field is a machine path (an offer names its tree, never its path), but a discard's sentences may name
/// a tree's own files, and the rows are this machine's (D47, D182 point 2).</para>
/// </remarks>
public static class GroupsStateSlice
{
    /// <summary>The slice's name, in the page's words.</summary>
    public const string Name = "groups";

    /// <summary>The rows' partition: each session's row, keyed by its id.</summary>
    public const string Rows = "rows";

    /// <summary>The entries' partition: each line of work, keyed by its entry.</summary>
    public const string Entries = "entries";

    /// <summary>
    /// The allow-list: each shape a partition carries, and the fields of it pushed. <c>row</c> is a row, <c>entry</c> an entry; the
    /// rest are what they hold (<see cref="Holds"/>). A field the route comes to write is pushed only once it is named here, which
    /// <c>GroupsStateSliceTests</c> holds against the route and the page's types.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Fields = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        ["row"] =
        [
            "session", "group", "shown", "archived", "teammate", "strikes", "awaits", "awaitsOf", "work", "holdsQuest", "pausedBy", "holds",
            "deletable", "lands", "discards", "repository", "created", "updated",
        ],
        ["work"] = ["commits", "uncommitted", "branch"],
        ["pausedBy"] = ["scope", "id"],
        ["holds"] = ["why", "reason", "repository", "until"],
        ["lands"] = ["branch", "tree", "commits", "uncommitted"],
        ["discards"] = ["branch", "tree", "says", "keeps", "keptAt"],
        ["entry"] = ["entry", "origin", "repository", "group", "leading", "parts"],
        ["part"] = ["session", "quest", "shown", "group", "began", "after", "askedBy"],
    };

    /// <summary>Which shape a field holds, as <c>shape.field</c>, an object of it or a list of them; every other field is a value.</summary>
    public static readonly IReadOnlyDictionary<string, string> Holds = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["row.work"] = "work",
        ["row.pausedBy"] = "pausedBy",
        ["row.holds"] = "holds",
        ["row.lands"] = "lands",
        ["row.discards"] = "discards",
        ["entry.parts"] = "part",
    };

    /// <summary>
    /// What <c>SESSION_GROUPS</c> answers that is never pushed, as <c>shape.field</c>: a row's running call, whose title is the agent's
    /// words and moves at every call, which the <c>calls</c> slice pushes and the page lays over the rows (WAITSAY1c2).
    /// </summary>
    public static readonly IReadOnlyList<string> PulledOnly = ["row.calling"];

    /// <summary>The sort inputs a row carries, the push's alone: its record's repository, when it began and when it last moved.</summary>
    public static readonly IReadOnlyList<string> OrderFields = ["repository", "created", "updated"];

    /// <summary>
    /// What wakes it on the kit's bus: a change declared to the sessions, a conversation's end, the service coming up, and the loop's
    /// look told to the windows. The change stream's families and the trees' parts wake it too (<see cref="HearLedger"/>,
    /// <see cref="HearTrees"/>).
    /// </summary>
    public static readonly IReadOnlyList<SliceWake> WakesOn =
    [
        new(StatePublisher.Type, payload => payload is StateChanged changed && changed.Scopes.Contains("sessions")),
        new(DriverLoop.EndedType),
        new("DRIVER_READY"),
        new("DRIVER_TICK"),
        // ACCEPTFOOT1b: a conversation's turns moving, told as the queue is: its turn's end offers its tree's work to land, and its next
        // turn takes the offer back. Human-paced: a word said, a turn begun or ended.
        new("SESSION_QUEUED"),
    ];

    /// <summary>The slice the shell's loop owns, taking <paramref name="loop"/>'s look once its service answers.</summary>
    public static StateSlice Of(DriverLoop loop, IEventBus bus) =>
        new(Name, bus, async ct => loop.Service is { } service ? Judge(await LookAsync(loop, service, ct).ConfigureAwait(false)) : null)
        {
            Keyed = true,
            WakesOn = WakesOn,
            Hears = [wake => HearLedger(loop, wake), wake => HearTrees(loop.Facts, wake)],
            // The service away or refusing: the windows keep what was judged, and the next wake looks again.
            Failed = error => loop.Log?.Warn("state.slice", ("slice", Name), ("message", error.Message)),
        };

    /// <summary>
    /// The look <c>SESSION_GROUPS</c> takes of every session (PUSH1c1): read now without trees, the loop's waits and calls laid on it,
    /// kept for Ask Daoris's room as the route's whole look is, then each tree as the facts' parts judged it.
    /// <param name="only">The sessions asked (null for every one); only a look at every session is kept for Ask Daoris's room.</param>
    /// <param name="walked">
    /// Whether the asked sessions' trees are walked with git now, as an archive's judgement walks them (it never hides work a tree
    /// gained since), instead of read as the facts judged them. The route and this slice take the one look here (PUSH1c3c).
    /// </param>
    public static async Task<SessionLook> LookAsync(
        DriverLoop loop, ServiceClient service, CancellationToken ct, IReadOnlyCollection<string>? only = null, bool walked = false)
    {
        var config = DriverConfig.Load(loop.ConfigPath);
        var at = DateTimeOffset.UtcNow;
        // ACCEPTFOOT1b: an open conversation's turns by the word of the runner that holds it, as the page's foot reads them.
        var chat = loop.Chat;
        var look = await SessionGroups.LookAsync(
                service, config, DriverContext.DoorOf(loop, config), loop.Look.Latest, loop.Home, walked ? only : [], ct,
                turnEnded: chat is null ? null : chat.TurnEnded)
            .ConfigureAwait(false);
        look = look with { Waits = loop.Look.Waits, Calls = RunningCalls.Of(loop.Events).Now() };
        if (only is null) loop.SessionsLooked.Keep(look, at);
        if (walked) return look;
        var trees = new SessionTrees(loop.Home);
        return await HelpFacts.WithTreesAsync(loop.Facts, look, trees.Holds, trees.WorkAsync, only, ct: ct).ConfigureAwait(false);
    }

    /// <summary>
    /// One judgement of a look: the rows, each under its session with the sort inputs its group reads, and the entries, each under its
    /// key. A session listed twice is pushed once, its first row, as an entry reads its parts' rows.
    /// </summary>
    public static IReadOnlyList<SliceJudged> Judge(SessionLook look)
    {
        var records = look.Records.GroupBy(record => record.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var rows = new JsonObject();
        foreach (var row in SessionGroups.Read(look))
        {
            if (rows.ContainsKey(row.Session)) continue;
            var written = Written(SessionsRoutes.Grouped(row));
            var record = records[row.Session];
            foreach (var field in OrderedBy(row.Group))
            {
                written[field] = field switch
                {
                    "repository" => JsonValue.Create(record.Repository),
                    "created" => JsonSerializer.SerializeToNode(record.Created, IpcJson.Options),
                    _ => JsonSerializer.SerializeToNode(record.Updated, IpcJson.Options),
                };
            }

            rows[row.Session] = Pick(written, "row");
        }

        var entries = new JsonObject();
        foreach (var entry in SessionGroups.ReadEntries(look))
        {
            if (!entries.ContainsKey(entry.Key)) entries[entry.Key] = Pick(Written(SessionsRoutes.Entry(entry)), "entry");
        }

        return [new SliceJudged(Entries, entries), new SliceJudged(Rows, rows)];
    }

    /// <summary>
    /// The sort inputs a row of <paramref name="group"/> carries, as <c>SessionGroups.Ordered</c> orders the group (D126 §2.1): waiting
    /// and resuming oldest first by when the record last moved; to review and working by repository without case, then when it began;
    /// ended and archived newest first by when it last moved. The session's id breaks every tie.
    /// </summary>
    public static IReadOnlyList<string> OrderedBy(string group) => group switch
    {
        SessionGroup.Review or SessionGroup.Working => ["repository", "created"],
        _ => ["updated"],
    };

    /// <summary>
    /// Wake the slice (<paramref name="wake"/>) where the loop hears the change stream name the <c>sessions</c> or the <c>quests</c>
    /// family: a record or a quest written by any door, a connector's among them. Another family, or another topic, wakes nothing.
    /// Disposed, it hears no more.
    /// </summary>
    public static IDisposable HearLedger(DriverLoop loop, Action wake)
    {
        void Heard(string topic, string subject)
        {
            if (topic == ChangeTopics.Ledger && subject.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Any(family => family is "sessions" or "quests"))
            {
                wake();
            }
        }

        loop.MoveHeard += Heard;
        return new Hearing(() => loop.MoveHeard -= Heard);
    }

    /// <summary>
    /// Wake the slice (<paramref name="wake"/>) where a tree's part landed with a value other than the one last heard, a dropped one
    /// among them: a tree's judgement moved what a row offers. The sessions' look, the lines and the work land often and move nothing
    /// here; a snapshot told after a newer one is passed over. Disposed, it hears no more.
    /// </summary>
    public static IDisposable HearTrees(MachineFacts facts, Action wake)
    {
        var heard = new Heard(wake, facts.Current);
        facts.Changed += heard.Landed;
        return new Hearing(() => facts.Changed -= heard.Landed);
    }

    private static JsonObject Written(object answer) =>
        JsonSerializer.SerializeToNode(answer, IpcJson.Options) as JsonObject ?? new JsonObject();

    private static JsonNode? Pick(JsonNode? node, string shape) => node switch
    {
        JsonArray list => new JsonArray([.. list.Select(each => Pick(each, shape))]),
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

    /// <summary>What the slice last heard of the trees' parts: their snapshot's number, and each part's value by its reference.</summary>
    private sealed class Heard
    {
        private readonly Action _wake;
        private readonly Lock _gate = new();
        private long _version;
        private Dictionary<string, object> _parts;

        public Heard(Action wake, MachineFactsSnapshot now)
        {
            _wake = wake;
            (_version, _parts) = Of(now);
        }

        public void Landed(MachineFactsSnapshot snapshot)
        {
            bool moved;
            lock (_gate)
            {
                if (snapshot.Version <= _version) return;
                var (version, parts) = Of(snapshot);
                moved = parts.Count != _parts.Count
                    || parts.Any(each => !_parts.TryGetValue(each.Key, out var seen) || !ReferenceEquals(seen, each.Value));
                (_version, _parts) = (version, parts);
            }

            if (moved) _wake();
        }

        private static (long Version, Dictionary<string, object> Parts) Of(MachineFactsSnapshot snapshot)
        {
            var parts = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var subject in snapshot.Subjects(HelpFacts.Trees))
            {
                if (snapshot.Of(HelpFacts.Trees, subject)?.Value is { } value) parts[subject] = value;
            }

            return (snapshot.Version, parts);
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
