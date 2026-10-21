using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The service's doors a look crosses, standing in: the open quests, the registry, the session ledger,
/// where this machine's claim on a quest stands (DEV3), the asks an intake answers (UX6d1), and the say and taken doors a
/// park the person answers goes on through (ANSWER2). In-process, reached through the real client over a
/// handler, so a look is driven with no port and no process, and the suite's fast half can hold it.
/// </summary>
/// <remarks>
/// It answers as the service does, and only as far as a look reads: active means queued, starting, working
/// or parked; a quest list holds the open and the taken; a session names its quest, its repository and when
/// it was made, which is how the driver reads its last run on a quest.
/// </remarks>
internal sealed class StandInLedger : HttpMessageHandler
{
    public const string Url = "http://ledger.test";

    private readonly object _gate = new();
    private readonly List<JsonObject> _quests = [];
    private readonly List<JsonObject> _registry = [];
    private readonly List<JsonObject> _sessions = [];
    private readonly List<JsonObject> _asks = [];
    private readonly Dictionary<string, int> _claims = new(StringComparer.Ordinal);
    private DateTimeOffset _clock = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>What <c>/api/quests/{id}/claim</c> answers for every quest (D68 §4).</summary>
    public string Claim
    {
        get { lock (_gate) return _claim; }
        set { lock (_gate) _claim = value; }
    }

    private string _claim = "held";

    /// <summary>Why a lost claim lost, as the claim's answer says it beside the claim (FABRIC-CLOSE1c18); none while null.</summary>
    public string? ClaimWhy
    {
        get { lock (_gate) return _claimWhy; }
        set { lock (_gate) _claimWhy = value; }
    }

    private string? _claimWhy;

    /// <summary>
    /// The take a person's bring-forward made (SYNCMERGE1b0), as the claim's answer names it beside the claim, for every quest; none while
    /// null, as a host before the field answers.
    /// </summary>
    public ForwardTake? ClaimForward
    {
        get { lock (_gate) return _claimForward; }
        set { lock (_gate) _claimForward = value; }
    }

    private ForwardTake? _claimForward;

    /// <summary>
    /// The person brings this machine's lost take forward now (SYNCMERGE1a): its fresh take, made after every record so far, stands as
    /// <paramref name="standing"/>, and so does the claim, unless it lost again.
    /// </summary>
    public ForwardTake BringForward(string standing = "held", string holder = "desk")
    {
        lock (_gate)
        {
            _clock = _clock.AddSeconds(1);
            _claimForward = new ForwardTake("desk", 7, _clock, standing);
            _claim = standing;
            // The quest list names the holder beside this machine's id (SYNCMERGE1b1), as the press settles it: here is "desk" always.
            _holder = holder;
            return _claimForward;
        }
    }

    private string? _holder;

    /// <summary>While true the quest list does not answer, as a service that went away does.</summary>
    public bool Down { get; set; }

    /// <summary>
    /// While true the claim door never answers (DEV3a): it is counted, then holds until the client gives up, as a host that took
    /// the question and stalled does, so the driver meets the client's own timeout.
    /// </summary>
    public bool ClaimHangs { get; set; }

    /// <summary>
    /// While true the quest list never answers (DEV3b): it is counted, then holds until the client gives up or its caller
    /// closes, as a host that took a look's own question and stalled does.
    /// </summary>
    public bool QuestsHang { get; set; }

    /// <summary>How often a look asked for the quest list while it hung.</summary>
    public int QuestsAsked => Volatile.Read(ref _questsAsked);

    private int _questsAsked;

    /// <summary>
    /// What this host's sync pass meets at its remote (DEV3b): null for a pass that went through, or the wall it names, as a
    /// host whose remote is away answers <c>/api/sync</c>. The remote's own doors answer from this ledger's rows.
    /// </summary>
    public string? SyncProblem
    {
        get { lock (_gate) return _syncProblem; }
        set { lock (_gate) _syncProblem = value; }
    }

    private string? _syncProblem;

    /// <summary>
    /// The sentence the open-session door refuses every open with (TICKSHOWN1), as the service does one that someone else got to
    /// first: null for an open that goes through.
    /// </summary>
    public string? OpenRefusal
    {
        get { lock (_gate) return _openRefusal; }
        set { lock (_gate) _openRefusal = value; }
    }

    private string? _openRefusal;

    /// <summary>How many sync passes a driver asked this host for.</summary>
    public int SyncPasses => Volatile.Read(ref _syncPasses);

    private int _syncPasses;

    /// <summary>What the change stream's door does (FABRIC-EVENT3b).</summary>
    public enum ChangeDoor
    {
        /// <summary>No such door, as a host from before answers: 404.</summary>
        Absent,

        /// <summary>The door, as a local host answers it: the quests family moves with each publish.</summary>
        Answers,

        /// <summary>The door refuses every wait: 503.</summary>
        Refuses,
    }

    /// <summary>The change stream's door (FABRIC-EVENT3b): absent unless a test opens it, as every older stand-in had none.</summary>
    public ChangeDoor Changes { get; set; }

    /// <summary>How many waits the driver asked the change stream for.</summary>
    public int ChangesAsked => Volatile.Read(ref _changesAsked);

    /// <summary>How many waits are held now.</summary>
    public int ChangesHeld => Volatile.Read(ref _changesHeld);

    private int _changesAsked;
    private int _changesHeld;

    // The stream's count and where each family last moved; one epoch for this stand-in's life.
    private long _changeCount;
    private readonly Dictionary<string, long> _movedAt = new(StringComparer.Ordinal);
    private TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private const string ChangeEpoch = "5ad1e";

    /// <summary>Every family the service's change stream answers, in its order (PUSH1d1).</summary>
    private static readonly string[] ChangeFamilies = ["sessions", "quests", "asks", "channel", "words"];

    /// <summary>
    /// A wait on the change stream, as the service answers it (the design §A4.2): a cursor it does not know answered every family
    /// at once, and its own cursor held until a family moves or the wait ends. Held outside the gate.
    /// </summary>
    private async Task<HttpResponseMessage> ChangesAsync(string query, CancellationToken ct)
    {
        if (Changes is ChangeDoor.Refuses)
        {
            return Answer(HttpStatusCode.ServiceUnavailable, new JsonObject { ["error"] = "the stand-in refuses the change stream" });
        }

        var asked = System.Web.HttpUtility.ParseQueryString(query);
        var wait = TimeSpan.FromSeconds(int.TryParse(asked["wait"], out var seconds) ? seconds : 0);
        var since = asked["since"];
        var until = DateTimeOffset.UtcNow + wait;
        Interlocked.Increment(ref _changesHeld);
        try
        {
            while (true)
            {
                Task changed;
                JsonObject answer;
                lock (_gate)
                {
                    changed = _changed.Task;
                    var cursor = $"{ChangeEpoch}.{_changeCount}";
                    var known = since is { } s && s.StartsWith(ChangeEpoch + ".", StringComparison.Ordinal)
                        && long.TryParse(s[(ChangeEpoch.Length + 1)..], out var n) && n <= _changeCount;
                    var after = known ? long.Parse(since![(ChangeEpoch.Length + 1)..]) : -1;
                    JsonArray moved = [.. ChangeFamilies.Where(family => !known || _movedAt.GetValueOrDefault(family) > after)];
                    answer = new JsonObject { ["since"] = cursor, ["moved"] = moved };
                }

                var left = until - DateTimeOffset.UtcNow;
                if (answer["moved"]!.AsArray().Count > 0 || left <= TimeSpan.Zero) return Answer(HttpStatusCode.OK, answer);
                try
                {
                    await changed.WaitAsync(left, ct);
                }
                catch (TimeoutException)
                {
                }
            }
        }
        finally
        {
            Interlocked.Decrement(ref _changesHeld);
        }
    }

    // The quests family moved: a publish. Under the gate.
    private void QuestsMoved() => Moved("quests");

    // Families moved under one sample of the stream's. Under the gate.
    private void Moved(params string[] families)
    {
        ++_changeCount;
        foreach (var family in families) _movedAt[family] = _changeCount;
        Interlocked.Exchange(ref _changed, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
    }

    /// <summary>
    /// A write the change stream answers as <paramref name="families"/>, moved together: a connector's ask or channel post (PUSH1d1),
    /// which this stand-in holds nothing of.
    /// </summary>
    public void Wrote(params string[] families)
    {
        lock (_gate) Moved(families);
    }

    // Each ask's channel (FABRIC-EVENT3c): its messages oldest first, and each entry's acknowledgment of a message by its id.
    private readonly Dictionary<string, List<JsonObject>> _channels = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Ask, string Entry, string Message), (string State, string Session)> _acks = [];

    /// <summary>
    /// The person's message <paramref name="id"/> on ask <paramref name="ask"/>'s channel, to <paramref name="to"/> or everyone, as a
    /// connector or the page keeps it (FABRIC-EVENT3c): the <c>channel</c> family moves with it, as the service's trigger moves it.
    /// </summary>
    public void Posted(string ask, string id, string text, string? to = null, bool? urgent = null)
    {
        lock (_gate)
        {
            _clock = _clock.AddSeconds(1);
            var message = new JsonObject
            {
                ["kind"] = "message", ["id"] = id, ["person"] = true, ["text"] = text, ["at"] = _clock.ToString("O"),
            };
            if (to is not null) message["to"] = to;
            if (urgent is { } level) message["urgent"] = level;
            if (!_channels.TryGetValue(ask, out var channel)) _channels[ask] = channel = [];
            channel.Add(message);
            Moved("channel");
        }
    }

    /// <summary>What entry <paramref name="entry"/> acknowledged of message <paramref name="id"/> on ask <paramref name="ask"/>'s channel, or null.</summary>
    public string? AckOf(string ask, string entry, string id)
    {
        lock (_gate) return _acks.TryGetValue((ask, entry, id), out var ack) ? ack.State : null;
    }

    /// <summary>A client over this ledger, as a driver is handed one; <paramref name="timeout"/> is the client's own.</summary>
    public ServiceClient Client(TimeSpan? timeout = null)
    {
        var http = new HttpClient(this, disposeHandler: false);
        if (timeout is { } within) http.Timeout = within;
        return new(Url, null, http);
    }

    /// <summary>A repository registered and adopted here, at <paramref name="root"/>.</summary>
    public StandInLedger Register(string repository, string root)
    {
        lock (_gate)
        {
            _registry.Add(new JsonObject
            {
                ["repository"] = repository, ["adopted"] = true, ["registered"] = true, ["root"] = root, ["workspace"] = "default",
            });
        }

        return this;
    }

    /// <summary>
    /// An open quest to <paramref name="to"/>, the newest: the service answers oldest first. <paramref name="from"/> is its
    /// sender, <c>ask #id</c> for a quest an ask asked (GOAHEAD2).
    /// </summary>
    /// <param name="parent">The chain's step before it (D65 §4), whose run a step in the same repository shares (WORKFLOW1e).</param>
    public void Publish(string id, string to, string? title = null, string from = "game", string? parent = null)
    {
        lock (_gate)
        {
            var quest = new JsonObject
            {
                ["id"] = id, ["from"] = from, ["to"] = to, ["title"] = title ?? $"The work of #{id}",
                ["body"] = "Stand-in work.", ["status"] = "Open",
            };
            if (parent is not null) quest["parent"] = parent;
            _quests.Add(quest);
            QuestsMoved();
        }
    }

    // The asks a quest was asked by, by id, as `/api/asks/{id}` answers one, with the go-aheads its sessions asked (GOAHEAD2).
    // Apart from the list above, which holds the asks an intake answers.
    private readonly Dictionary<string, JsonObject> _askedBy = new(StringComparer.Ordinal);

    /// <summary>
    /// <paramref name="session"/> asks the person on ask <paramref name="ask"/> for a go-ahead (KNOWUSE1a): kept as the ask's
    /// next number, waiting on them, the ask held from its first.
    /// </summary>
    public void GoAheadAsked(string ask, string session, string kind, string on, string act)
    {
        lock (_gate)
        {
            if (!_askedBy.TryGetValue(ask, out var held))
            {
                _askedBy[ask] = held = new JsonObject
                {
                    ["id"] = ask, ["workspace"] = "default", ["sentence"] = $"The words of ask #{ask}.", ["state"] = "Published",
                    ["tier"] = "named", ["goAheads"] = new JsonArray(),
                };
            }

            var goAheads = held["goAheads"]!.AsArray();
            _clock = _clock.AddSeconds(1);
            goAheads.Add(new JsonObject
            {
                ["number"] = goAheads.Count + 1, ["kind"] = kind, ["on"] = on, ["act"] = act, ["state"] = "asked",
                ["asked"] = new JsonArray(new JsonObject { ["session"] = session, ["at"] = _clock.ToString("O"), ["why"] = "The work needs it." }),
            });
        }
    }

    /// <summary>
    /// The person's kind and workflow on ask <paramref name="ask"/> (WORKFLOW1e), kept as its latest choice, as <c>/api/asks/{id}</c>
    /// answers it: what a run of the ask's work binds by at its first start.
    /// </summary>
    public void WorkflowChosen(string ask, string? kind, string? workflow, string workspace = "default")
    {
        lock (_gate)
        {
            if (!_askedBy.TryGetValue(ask, out var held))
            {
                _askedBy[ask] = held = new JsonObject
                {
                    ["id"] = ask, ["workspace"] = workspace, ["sentence"] = $"The words of ask #{ask}.", ["state"] = "Published",
                    ["tier"] = "named",
                };
            }

            held["workflowChoices"] ??= new JsonArray();
            _clock = _clock.AddSeconds(1);
            var choice = new JsonObject { ["at"] = _clock.ToString("O") };
            if (kind is not null) choice["kind"] = kind;
            if (workflow is not null) choice["workflow"] = workflow;
            held["workflowChoices"]!.AsArray().Add(choice);
        }
    }

    /// <summary>
    /// An ask proposed in <paramref name="workspace"/> and served by no intake yet, the newest (UX6d1): a look whose config
    /// names an intake's adapter starts one for it.
    /// </summary>
    public void Ask(string id, string workspace = "default")
    {
        lock (_gate)
        {
            _asks.Insert(0, new JsonObject
            {
                ["id"] = id, ["workspace"] = workspace, ["sentence"] = $"The words of ask #{id}.", ["state"] = "Proposed",
                ["tier"] = "named",
            });
        }
    }

    /// <summary>A quest now waiting on another repository's answer (D79), as the service lists it.</summary>
    public void Awaiting(string id, string repository)
    {
        lock (_gate) _quests.Single(q => q["id"]!.GetValue<string>() == id)["awaits"] = repository;
    }

    /// <summary>
    /// The person reopened the submission <paramref name="id"/> grew from (FABRIC-CLOSE1c), as the service then lists the step: its
    /// source named reopened and held for it.
    /// </summary>
    public void Reopened(string id, string machine, long sequence)
    {
        lock (_gate)
        {
            var quest = _quests.Single(q => q["id"]!.GetValue<string>() == id);
            quest["sourceReopened"] = new JsonObject { ["machine"] = machine, ["sequence"] = sequence };
            quest["held"] = true;
            quest["hold"] = "source-reopened";
        }
    }

    /// <summary>
    /// The person's go-on (FABRIC-CLOSE1c): the step rebound to the submission it grows from now, and its hold let go. Its own result,
    /// its submission and what was judged of it, is cleared (FABRIC-CLOSE1c10).
    /// </summary>
    /// <param name="moved">
    /// Whether the quest's <c>updated</c> moves to the go-on, as the service's does (FABRIC-CLOSE1c13 reads it). Off by default, as the
    /// tests before it model it. A look judges an unmarked take by <c>updated</c> only where the claim says no take time
    /// (<see cref="TakenAt"/>, CARRY3), as a host before it.
    /// </param>
    public void Rebound(string id, string machine, long sequence, bool moved = false)
    {
        lock (_gate)
        {
            var quest = _quests.Single(q => q["id"]!.GetValue<string>() == id);
            quest.Remove("sourceReopened");
            quest.Remove("held");
            quest.Remove("hold");
            quest.Remove("submission");
            quest.Remove("answers");
            quest.Remove("accepted");
            quest.Remove("evidence");
            quest["grewFrom"] = new JsonObject { ["machine"] = machine, ["sequence"] = sequence };
            if (!moved) return;

            _clock = _clock.AddSeconds(1);
            quest["updated"] = _clock.ToString("O");
        }
    }

    /// <summary>The taker's <c>ready</c> on <paramref name="id"/> (FABRIC-CLOSE1a): its current submission, said by <paramref name="session"/>.</summary>
    public void Submitted(string id, string machine, long sequence, string session)
    {
        lock (_gate)
        {
            var quest = _quests.Single(q => q["id"]!.GetValue<string>() == id);
            _clock = _clock.AddSeconds(1);
            quest["submission"] = new JsonObject
            {
                ["machine"] = machine, ["sequence"] = sequence, ["at"] = _clock.ToString("O"), ["note"] = "ready", ["session"] = session,
            };
            quest["updated"] = _clock.ToString("O");
        }
    }

    /// <summary>
    /// The person reopened <paramref name="id"/>'s own submission (FABRIC-CLOSE1c4, FABRIC-CLOSE1c8), as the service then lists the
    /// source: no submission, and its reopen by its machine and sequence, the submission it reopened, their words and the session that
    /// said the reopened ready, each left out where none.
    /// </summary>
    public void SourceReopened(string id, long sequence, long of, string? note, string? session, string machine = "desk")
    {
        lock (_gate)
        {
            var quest = _quests.Single(q => q["id"]!.GetValue<string>() == id);
            quest.Remove("submission");
            var reopen = new JsonObject
            {
                ["machine"] = machine, ["sequence"] = sequence, ["of"] = new JsonObject { ["machine"] = machine, ["sequence"] = of },
                ["at"] = _clock.ToString("O"),
            };
            if (note is not null) reopen["note"] = note;
            if (session is not null) reopen["session"] = session;
            quest["reopened"] = reopen;
        }
    }

    /// <summary>Move a quest, as its session's own connector would.</summary>
    public void Move(string id, string status)
    {
        lock (_gate) _quests.Single(q => q["id"]!.GetValue<string>() == id)["status"] = status;
    }

    // When each quest's take was made, as the claim's answer says it (CARRY3); a quest absent answers none, as a host before it.
    private readonly Dictionary<string, DateTimeOffset> _takenAt = new(StringComparer.Ordinal);

    /// <summary>The claim on <paramref name="id"/> says its take was made at <paramref name="at"/> (CARRY3).</summary>
    public void TakenAt(string id, DateTimeOffset at)
    {
        lock (_gate) _takenAt[id] = at;
    }

    /// <summary>
    /// Take a quest out of every list, as the service's rebase forgets a step that went with its source's lost submission (FABRIC-CLOSE1e):
    /// no list shows it, though this machine's claim on it is still answered.
    /// </summary>
    public void Forget(string id)
    {
        lock (_gate) _quests.RemoveAll(q => q["id"]!.GetValue<string>() == id);
    }

    /// <summary>
    /// A record's link to the session it continues (MIXAGENT1a-iii's <c>from</c>, <c>fromWhy</c>, <c>fromBy</c>), as the ledger
    /// answers it on loopback: what MIXAGENT1c's judge reads to know a part a hand-over started.
    /// </summary>
    public void Continues(string session, string from, string why, string by)
    {
        lock (_gate)
        {
            var record = _sessions.Single(s => s["id"]!.GetValue<string>() == session);
            record["from"] = from;
            record["fromWhy"] = why;
            record["fromBy"] = by;
        }
    }

    /// <summary>
    /// A driven session of this machine's that took <paramref name="quest"/> and parked to ask the person (D83), in its own
    /// tree, with no words on it yet (ANSWER2): what the say door keeps the person's answer on.
    /// </summary>
    /// <param name="entry">Its line of work's key (D167 point 2), where the test reads it: <c>ask:&lt;id&gt;/&lt;repository&gt;</c> has a channel.</param>
    public void Park(string session, string quest, string tree, string? entry = null)
    {
        lock (_gate)
        {
            var asked = _quests.Single(q => q["id"]!.GetValue<string>() == quest);
            asked["status"] = "Taken";
            _clock = _clock.AddSeconds(1);
            var parked = new JsonObject
            {
                ["id"] = session, ["quest"] = quest, ["repository"] = asked["to"]!.GetValue<string>(), ["state"] = "awaiting-person",
                ["kind"] = "driven", ["adapter"] = "stub", ["tree"] = tree, ["created"] = _clock.ToString("O"),
                ["updated"] = _clock.ToString("O"), ["note"] = "Which port should it listen on?", ["said"] = new JsonArray(),
            };
            if (entry is not null) parked["entry"] = entry;
            _sessions.Add(parked);
        }
    }

    /// <summary>
    /// A driven session of this machine's on <paramref name="quest"/>, which it took (MIXAGENT1a-ii): working, or in whatever state
    /// it ended, in its own tree, on the adapter and account it ran as.
    /// </summary>
    public void Driven(string session, string quest, string tree, string state = "working", string adapter = "stub", string? profile = null)
    {
        lock (_gate)
        {
            var served = _quests.Single(q => q["id"]!.GetValue<string>() == quest);
            served["status"] = "Taken";
            _clock = _clock.AddSeconds(1);
            _sessions.Add(new JsonObject
            {
                ["id"] = session, ["quest"] = quest, ["repository"] = served["to"]!.GetValue<string>(), ["state"] = state,
                ["kind"] = "driven", ["adapter"] = adapter, ["profile"] = profile, ["tree"] = tree, ["created"] = _clock.ToString("O"),
                ["updated"] = _clock.ToString("O"), ["took"] = true, ["said"] = new JsonArray(),
            });
        }
    }

    /// <summary>
    /// A session of this machine's in <paramref name="tree"/> with no quest (AUTOTIDY1): a conversation, as the ledger keeps one,
    /// which the active list answers while it is working, and the whole list once it ended.
    /// </summary>
    /// <param name="ask">The ask it answers, where it is an intake (CAPCOUNT1): kept on the record as the ledger keeps it.</param>
    /// <param name="serves">The session it answers, where it is a helper (CAPCOUNT1).</param>
    /// <param name="opinion">The second opinion it reads for, where it is a reviewer (CAPCOUNT1).</param>
    public void Working(
        string session, string repository, string tree, string state = "working", string? ask = null, string? serves = null,
        string? opinion = null)
    {
        lock (_gate)
        {
            _clock = _clock.AddSeconds(1);
            var record = new JsonObject
            {
                ["id"] = session, ["repository"] = repository, ["state"] = state, ["kind"] = "chat", ["adapter"] = "stub",
                ["tree"] = tree, ["created"] = _clock.ToString("O"),
            };
            // The marks the driver's own conversations carry, each only where it has one, as the ledger answers them on loopback.
            if (ask is not null) record["ask"] = ask;
            if (serves is not null) record["serves"] = serves;
            if (opinion is not null) record["opinion"] = opinion;
            _sessions.Add(record);
        }
    }

    /// <summary>
    /// <paramref name="quest"/> made a set-up step showing its chain's work in <paramref name="environment"/>, done, with one
    /// local set-up <paramref name="session"/> said and no verdict on it yet (REVIEWENV1d): a review waiting for the person.
    /// </summary>
    public void SetUpShown(string quest, string session, string environment = "local-dev")
    {
        lock (_gate)
        {
            var shown = _quests.Single(q => q["id"]!.GetValue<string>() == quest);
            shown["status"] = "Done";
            shown["setUpIn"] = environment;
            shown["setUps"] = new JsonArray(new JsonObject
            {
                ["commit"] = new string('a', 40), ["session"] = session, ["local"] = true, ["served"] = "dist",
                ["look"] = "http://localhost:5173/", ["machine"] = "m1", ["sequence"] = 1,
            });
        }
    }

    /// <summary>Every state a record was moved to through the state door, in order (ANSWER2).</summary>
    public IReadOnlyList<string> MovesOf(string id)
    {
        lock (_gate) return [.. _moves.GetValueOrDefault(id) ?? []];
    }

    private readonly Dictionary<string, List<string>> _moves = new(StringComparer.Ordinal);

    /// <summary>
    /// Where the ledger lets a running record move from each of its active states (D46 §4), for the sentence it refuses a
    /// move with. The stand-in refuses only a move to the state a running record is already in, the refusal a second run on
    /// the same record meets (ANSWER2); its own runs move a record queued → working, which the real ledger never takes.
    /// </summary>
    private static readonly Dictionary<string, string> From = new(StringComparer.Ordinal)
    {
        ["queued"] = "starting, stood-down, failed, stopped",
        ["starting"] = "working, stood-down, failed, stopped",
        ["working"] = "awaiting-person, completed, declined, stood-down, failed, stopped",
        ["awaiting-person"] = "working, completed, declined, stopped",
    };

    public JsonObject Session(string id)
    {
        lock (_gate) return (JsonObject)_sessions.Single(s => s["id"]!.GetValue<string>() == id).DeepClone();
    }

    /// <summary>
    /// The session an open says the new record continues, why and who asked (MIXAGENT1a-iii), and the access setup it started under
    /// (AUTONOMY1a), kept as the service answers them on loopback: each only where the open named it.
    /// </summary>
    internal static void Continues(JsonObject session, JsonObject open)
    {
        foreach (var name in new[] { "from", "fromWhy", "fromBy" })
        {
            if (open[name]?.GetValue<string>() is { } value) session[name] = value;
        }

        // The access setup a driven start resolved (AUTONOMY1a), kept as the service answers it on loopback.
        if (open["access"] is JsonObject access) session["access"] = access.DeepClone();
    }

    /// <summary>Every record, oldest first.</summary>
    public IReadOnlyList<JsonObject> Sessions
    {
        get { lock (_gate) return [.. _sessions.Select(s => (JsonObject)s.DeepClone())]; }
    }

    /// <summary>The records opened for one quest, oldest first.</summary>
    public IReadOnlyList<JsonObject> SessionsFor(string quest) =>
        [.. Sessions.Where(s => s["quest"]?.GetValue<string>() == quest)];

    /// <summary>How often a driver asked where this machine's claim on <paramref name="quest"/> stands.</summary>
    public int ClaimsAsked(string quest)
    {
        lock (_gate) return _claims.GetValueOrDefault(quest);
    }

    /// <summary>
    /// What a request waits on before it is answered, outside the gate, where a test holds one (PARALLEL1b): which requests are asked
    /// at once is what such a test reads. Null holds none.
    /// </summary>
    public Func<HttpRequestMessage, CancellationToken, Task>? Before { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.AbsolutePath;
        var all = request.RequestUri.Query.Contains("includeClosed=true", StringComparison.Ordinal);
        var body = request.Content is null ? null : JsonNode.Parse(await request.Content.ReadAsStringAsync(ct))?.AsObject();
        if (Before is { } before) await before(request, ct);

        if (ClaimHangs && request.Method == HttpMethod.Get && path.StartsWith("/api/quests/", StringComparison.Ordinal)
            && path.EndsWith("/claim", StringComparison.Ordinal))
        {
            var quest = path["/api/quests/".Length..^"/claim".Length];
            lock (_gate) _claims[quest] = _claims.GetValueOrDefault(quest) + 1;
            // Held outside the gate, until the client's token ends it: its timeout, or its caller's close.
            await Task.Delay(Timeout.Infinite, ct);
        }

        if (QuestsHang && request.Method == HttpMethod.Get && path == "/api/quests")
        {
            Interlocked.Increment(ref _questsAsked);
            await Task.Delay(Timeout.Infinite, ct);
        }

        if (request.Method == HttpMethod.Get && path == "/api/changes")
        {
            Interlocked.Increment(ref _changesAsked);
            if (Changes is not ChangeDoor.Absent) return await ChangesAsync(request.RequestUri.Query, ct);
        }

        lock (_gate)
        {
            if (Down && path == "/api/quests")
            {
                return Answer(HttpStatusCode.ServiceUnavailable, new JsonObject { ["error"] = "the stand-in is down" });
            }

            switch (request.Method.Method, path)
            {
                case ("GET", "/api/quests"):
                    return Answer(HttpStatusCode.OK, new JsonArray([.. _quests
                        .Where(q => all || q["status"]!.GetValue<string>() is "Open" or "Taken")
                        .Select(q =>
                        {
                            var row = q.DeepClone();
                            if (_holder is not null) { row["holder"] = _holder; row["here"] = "desk"; }
                            return row;
                        })]));

                case ("GET", "/api/registry"):
                    return Answer(HttpStatusCode.OK, new JsonArray([.. _registry.Select(r => r.DeepClone())]));

                // A circle's sync (DEV3b): this machine owes it no retires, and the host's pass answers where its remote stood.
                case ("GET", "/api/registry/retired"):
                    return Answer(HttpStatusCode.OK, new JsonObject { ["repositories"] = new JsonArray() });

                case ("POST", "/api/sync"):
                    Interlocked.Increment(ref _syncPasses);
                    return Answer(HttpStatusCode.OK, _syncProblem is { } wall
                        ? new JsonObject { ["wired"] = true, ["problem"] = wall }
                        : new JsonObject { ["wired"] = true });

                // Newest first, as the service answers them.
                case ("GET", "/api/asks"):
                    return Answer(HttpStatusCode.OK, new JsonArray([.. _asks.Select(a => a.DeepClone())]));

                case ("GET", "/api/sessions"):
                    return Answer(HttpStatusCode.OK, new JsonArray([.. _sessions
                        .Where(s => all || s["state"]!.GetValue<string>() is "queued" or "starting" or "working" or "awaiting-person")
                        .Select(s => s.DeepClone())]));

                case ("POST", "/api/sessions"):
                {
                    if (_openRefusal is { } refusal) return Answer(HttpStatusCode.Conflict, new JsonObject { ["error"] = refusal });
                    var quest = body!["quest"]!.GetValue<string>();
                    var to =_quests.Single(q => q["id"]!.GetValue<string>() == quest)["to"]!.GetValue<string>();
                    _clock = _clock.AddSeconds(1);
                    var session = new JsonObject
                    {
                        ["id"] = $"s{_sessions.Count + 1}", ["quest"] = quest, ["repository"] = to,
                        ["state"] = "queued", ["kind"] = "driven", ["adapter"] = body["adapter"]?.GetValue<string>(),
                        ["tree"] = body["tree"]?.GetValue<string>(), ["created"] = _clock.ToString("O"),
                        // The account it runs on, as the service answers it on loopback (TOOL4f reads it back).
                        ["profile"] = body["profile"]?.GetValue<string>(),
                    };
                    Continues(session, body);
                    _sessions.Add(session);
                    return Answer(HttpStatusCode.OK, new JsonObject { ["session"] = session.DeepClone(), ["message"] = "queued" });
                }

                case ("POST", _) when path.StartsWith("/api/sessions/", StringComparison.Ordinal) && path.EndsWith("/state", StringComparison.Ordinal):
                {
                    var id = path["/api/sessions/".Length..^"/state".Length];
                    var session = _sessions.Single(s => s["id"]!.GetValue<string>() == id);
                    var from = session["state"]!.GetValue<string>();
                    var to = body!["state"]!.GetValue<string>();
                    if (from == to && From.TryGetValue(from, out var onward))
                    {
                        // In the ledger's own words (SessionLedger.AdvanceAsync), which the driver's refusal quotes whole.
                        return Answer(HttpStatusCode.Conflict, new JsonObject
                        {
                            ["error"] = $"Session `{id}` cannot move {from} → {to}. From {from}: {onward}.",
                        });
                    }

                    session["state"] = to;
                    // The record moved now, as the ledger keeps it (`updated`), which a step's go-on is compared with (FABRIC-CLOSE1c13).
                    _clock = _clock.AddSeconds(1);
                    session["updated"] = _clock.ToString("O");
                    if (!_moves.TryGetValue(id, out var moves)) _moves[id] = moves = [];
                    moves.Add(to);
                    // A session that parks again asks anew (ANSWER1b): a move into a park clears the words waiting (MSG1a).
                    if (to == "awaiting-person" && session["said"] is not null)
                    {
                        session["said"] = new JsonArray();
                        session.Remove("answer");
                    }
                    if (body["note"] is { } note)
                    {
                        session["note"] = note.GetValue<string>();
                        // Its parts beside it, as the service keeps them (LANG1a): a note sent without them clears them. The
                        // strikes read an account's line by its code (ROSTER1b).
                        session["noteParts"] = body["noteParts"]?.DeepClone();
                    }

                    if (body["interrupted"] is { } interrupted) session["interrupted"] = interrupted.GetValue<bool>();
                    // Kept once said, as the service keeps it (TOOL4c).
                    if (body["limit"] is { } limit && limit.GetValue<bool>()) session["limit"] = true;
                    return Answer(HttpStatusCode.OK, new JsonObject { ["session"] = session.DeepClone(), ["message"] = "moved" });
                }

                // The say door (MSG1a), the one the screen's answer and a terminal's words reach through `ServiceClient.SayAsync`:
                // words kept on a parked or ended record, verbatim and in order, `answer` their join; a running record is
                // refused `running`, since its words reach it through the driver that runs it.
                case ("POST", _) when path.StartsWith("/api/sessions/", StringComparison.Ordinal) && path.EndsWith("/say", StringComparison.Ordinal):
                {
                    var id = path["/api/sessions/".Length..^"/say".Length];
                    var session = _sessions.Single(s => s["id"]!.GetValue<string>() == id);
                    var state = session["state"]!.GetValue<string>();
                    // COWORK1c: a running record keeps a word for its run where the driver that runs it asks so.
                    var forRun = state is "queued" or "starting" or "working";
                    if (forRun && body!["running"]?.GetValue<bool>() != true)
                    {
                        return Answer(HttpStatusCode.Conflict, new JsonObject
                        {
                            ["error"] = $"Session `{id}` is {state}: words to a running session reach it through the driver that runs it, not its record.",
                            ["refusal"] = "running",
                        });
                    }

                    if (session["said"] is not JsonArray said) session["said"] = said = [];
                    // COWORK1b: a word kept once by its producer's key, and Daoris's kept as Daoris's, never in the person's answer.
                    var key = body!["key"]?.GetValue<string>();
                    if (key is not null && said.FirstOrDefault(each => each!["key"]?.GetValue<string>() == key) is { } kept)
                    {
                        return Answer(HttpStatusCode.OK, new JsonObject
                        {
                            ["session"] = session.DeepClone(), ["message"] = $"Already kept for session `{id}` under key `{key}`.", ["said"] = kept.DeepClone(),
                        });
                    }

                    _clock = _clock.AddSeconds(1);
                    var word = new JsonObject
                    {
                        ["id"] = $"w{said.Count + 1}", ["text"] = body!["text"]!.GetValue<string>().Trim(), ["at"] = _clock.ToString("O"),
                        ["files"] = new JsonArray(), ["reopens"] = state != "awaiting-person" && !forRun,
                    };
                    if (body!["from"]?.GetValue<string>() is { } from) word["from"] = from;
                    if (key is not null) word["key"] = key;
                    if (body!["level"]?.GetValue<string>() is { } level) word["level"] = level;
                    said.Add(word);
                    var persons = said.Where(each => each!["from"] is null).Select(each => each!["text"]!.GetValue<string>()).ToList();
                    if (persons.Count > 0) session["answer"] = string.Join("\n\n", persons);
                    return Answer(HttpStatusCode.OK, new JsonObject
                    {
                        ["session"] = session.DeepClone(), ["message"] = $"Kept for session `{id}` to go on with.", ["said"] = word.DeepClone(),
                    });
                }

                // COWORK1c: the attempt door, the first moment kept on each word named that still waits.
                case ("POST", _) when path.StartsWith("/api/sessions/", StringComparison.Ordinal) && path.EndsWith("/attempt", StringComparison.Ordinal):
                {
                    var id = path["/api/sessions/".Length..^"/attempt".Length];
                    var session = _sessions.Single(s => s["id"]!.GetValue<string>() == id);
                    var named = body!["said"]!.AsArray().Select(word => word!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
                    _clock = _clock.AddSeconds(1);
                    foreach (var word in (session["said"] as JsonArray ?? []).OfType<JsonObject>())
                    {
                        if (named.Contains(word["id"]!.GetValue<string>()) && word["attempt"] is null) word["attempt"] = _clock.ToString("O");
                    }

                    return Answer(HttpStatusCode.OK, new JsonObject { ["session"] = session.DeepClone(), ["message"] = "marked", ["unmarked"] = new JsonArray() });
                }

                // SAIDBACK1a's take-back door, without a reword: a word waiting on a record that runs no more leaves it.
                case ("POST", _) when path.StartsWith("/api/sessions/", StringComparison.Ordinal) && path.EndsWith("/take-back", StringComparison.Ordinal):
                {
                    var id = path["/api/sessions/".Length..^"/take-back".Length];
                    var session = _sessions.Single(s => s["id"]!.GetValue<string>() == id);
                    var named = body!["word"]!.GetValue<string>();
                    var said = session["said"] as JsonArray ?? [];
                    if (said.FirstOrDefault(word => word!["id"]!.GetValue<string>() == named) is not { } waiting)
                    {
                        return Answer(HttpStatusCode.OK, new JsonObject { ["done"] = false, ["why"] = "taken", ["message"] = "no longer waits" });
                    }

                    said.Remove(waiting);
                    return Answer(HttpStatusCode.OK, new JsonObject { ["done"] = true, ["message"] = "taken back", ["session"] = session.DeepClone() });
                }

                // The taken door (MSG1a): the words a session took leave its record by their ids.
                case ("POST", _) when path.StartsWith("/api/sessions/", StringComparison.Ordinal) && path.EndsWith("/taken", StringComparison.Ordinal):
                {
                    var id = path["/api/sessions/".Length..^"/taken".Length];
                    var session = _sessions.Single(s => s["id"]!.GetValue<string>() == id);
                    var taken = body!["said"]!.AsArray().Select(word => word!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
                    var left = (session["said"] as JsonArray ?? []).Where(word => !taken.Contains(word!["id"]!.GetValue<string>()))
                        .Select(word => word!.DeepClone()).ToList();
                    session["said"] = new JsonArray([.. left]);
                    var persons = left.Where(word => word["from"] is null).Select(word => word["text"]!.GetValue<string>()).ToList();
                    if (persons.Count == 0) session.Remove("answer");
                    else session["answer"] = string.Join("\n\n", persons);
                    return Answer(HttpStatusCode.OK, new JsonObject { ["session"] = session.DeepClone(), ["message"] = "taken" });
                }

                // An ask's channel as one entry reads it (SESSONE2a), each message with that entry's acknowledgment (COWORK1d); an ask
                // with no channel here is not found, as a host without it answers.
                case ("GET", _) when path.StartsWith("/api/asks/", StringComparison.Ordinal) && path.EndsWith("/channel", StringComparison.Ordinal):
                {
                    var ask = path["/api/asks/".Length..^"/channel".Length];
                    var entry = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query)["entry"] ?? "";
                    if (!_channels.TryGetValue(ask, out var channel))
                    {
                        return Answer(HttpStatusCode.NotFound, new JsonObject { ["error"] = $"No ask `#{ask}`." });
                    }

                    return Answer(HttpStatusCode.OK, new JsonObject
                    {
                        ["ask"] = ask, ["entry"] = entry, ["unread"] = 0,
                        ["items"] = new JsonArray([.. channel.Select(message =>
                        {
                            var item = message.DeepClone().AsObject();
                            if (_acks.TryGetValue((ask, entry, message["id"]!.GetValue<string>()), out var ack))
                            {
                                item["ack"] = ack.State;
                                item["ackSession"] = ack.Session;
                            }

                            return item;
                        })]),
                    });
                }

                // An entry's acknowledgments by message id (COWORK1d): a claim only where the entry holds none or let one go, the rest
                // kept as said. Answers the ids it changed.
                case ("POST", _) when path.StartsWith("/api/asks/", StringComparison.Ordinal) && path.EndsWith("/channel/ack", StringComparison.Ordinal):
                {
                    var ask = path["/api/asks/".Length..^"/channel/ack".Length];
                    var (entry, session, state) = (body!["entry"]!.GetValue<string>(), body["session"]!.GetValue<string>(), body["state"]!.GetValue<string>());
                    var changed = new JsonArray();
                    foreach (var id in body["messages"]!.AsArray().Select(message => message!.GetValue<string>()))
                    {
                        var held = _acks.TryGetValue((ask, entry, id), out var ack) ? ack.State : null;
                        if (state == ChannelAcks.Taking && held is not null and not ChannelAcks.Released) continue;
                        _acks[(ask, entry, id)] = (state, session);
                        changed.Add(id);
                    }

                    return Answer(HttpStatusCode.OK, new JsonObject { ["acknowledged"] = changed });
                }

                // One ask, whole, as a session's start and resume read the go-aheads on it (KNOWUSE1a).
                case ("GET", _) when path.StartsWith("/api/asks/", StringComparison.Ordinal):
                    return _askedBy.TryGetValue(path["/api/asks/".Length..], out var whole)
                        ? Answer(HttpStatusCode.OK, whole.DeepClone())
                        : Answer(HttpStatusCode.NotFound, new JsonObject { ["error"] = $"No ask `#{path["/api/asks/".Length..]}`." });

                // The go-ahead door (KNOWUSE1a), with what the service does to a park on the answer that leaves none of its
                // go-aheads waiting (GOAHEAD2, SessionLedger.GoOnWithGoAheadsAsync): a parked record of a session that asked
                // it, holding no words yet, keeps the park's blank answer, unless the caller answers the park itself.
                case ("POST", _) when path.StartsWith("/api/asks/", StringComparison.Ordinal) && path.Contains("/go-aheads/", StringComparison.Ordinal):
                {
                    var named = path["/api/asks/".Length..].Split("/go-aheads/");
                    var held = _askedBy[named[0]];
                    var goAheads = held["goAheads"]!.AsArray().Select(each => each!.AsObject()).ToList();
                    var goAhead = goAheads.Single(each => each["number"]!.GetValue<int>().ToString(System.Globalization.CultureInfo.InvariantCulture) == named[1]);
                    var waited = goAhead["answer"] is null;
                    var approved = body!["answer"]!.GetValue<string>() == "approved";
                    _clock = _clock.AddSeconds(1);
                    goAhead["state"] = approved ? "approved" : "refused";
                    goAhead["answer"] = new JsonObject
                    {
                        ["approved"] = approved, ["words"] = body["words"]?.GetValue<string>(), ["at"] = _clock.ToString("O"),
                    };

                    static string Asker(JsonNode? request) => request!["session"]!.GetValue<string>();
                    if (waited && body["goesOn"]?.GetValue<bool>() != false)
                    {
                        foreach (var asker in goAhead["asked"]!.AsArray().Select(Asker).Distinct(StringComparer.Ordinal))
                        {
                            var parked = _sessions.SingleOrDefault(s => s["id"]!.GetValue<string>() == asker);
                            var open = goAheads.Any(each => each["answer"] is null && each["asked"]!.AsArray().Any(request => Asker(request) == asker));
                            if (parked?["state"]?.GetValue<string>() != "awaiting-person" || open || parked["said"] is JsonArray { Count: > 0 }) continue;
                            parked["said"] = new JsonArray(new JsonObject
                            {
                                ["id"] = $"w-{asker}", ["text"] = "carry on.", ["at"] = _clock.ToString("O"), ["files"] = new JsonArray(), ["reopens"] = false,
                            });
                            parked["answer"] = "carry on.";
                        }
                    }

                    return Answer(HttpStatusCode.OK, new JsonObject
                    {
                        ["ask"] = held.DeepClone(),
                        ["message"] = $"Go-ahead {named[1]} on ask `#{named[0]}` {(approved ? "approved" : "refused")}.",
                    });
                }

                case ("GET", _) when path.StartsWith("/api/quests/", StringComparison.Ordinal) && path.EndsWith("/claim", StringComparison.Ordinal):
                {
                    var quest = path["/api/quests/".Length..^"/claim".Length];
                    _claims[quest] = _claims.GetValueOrDefault(quest) + 1;
                    var claim = new JsonObject { ["claim"] = _claim, ["why"] = _claimWhy };
                    if (_takenAt.TryGetValue(quest, out var takenAt)) claim["taken"] = takenAt.ToString("O");
                    // SYNCMERGE1b0: written only where set, as the service answers it.
                    if (_claimForward is { } forward)
                    {
                        claim["forward"] = new JsonObject
                        {
                            ["machine"] = forward.Machine, ["sequence"] = forward.Sequence, ["at"] = forward.At.ToString("O"),
                            ["standing"] = forward.Standing,
                        };
                    }

                    return Answer(HttpStatusCode.OK, claim);
                }

                default:
                    return Answer(HttpStatusCode.NotFound, new JsonObject { ["error"] = $"the stand-in has no {request.Method} {path}" });
            }
        }
    }

    private static HttpResponseMessage Answer(HttpStatusCode status, JsonNode payload) => new(status)
    {
        Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
    };
}

/// <summary>
/// A start's run, standing in (DEV3): it opens its record through the real client, moves it to working,
/// says it opened, and holds its session until the test ends it. When the look's token is cancelled it
/// ends as the driver's shutdown ends a session: <c>stopped</c>, interrupted (D104), unless the driver
/// had stopped it first, whose stop is then the record's (FLAKE3, <see cref="Observation.Closed"/>).
/// </summary>
internal sealed class StandInRuns(ServiceClient service, StandInLedger ledger)
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, TaskCompletionSource<string>> _endings =
        new(StringComparer.Ordinal);

    private readonly List<string> _started = [];

    /// <summary>Run by the run the moment its record is open, before the look is told — a test's hook into that instant.</summary>
    public Action<string>? OnOpen { get; set; }

    /// <summary>Whether a session takes its quest as it opens, as a real one does first. Off, the quest stays open while it works.</summary>
    public bool Takes { get; set; } = true;

    /// <summary>
    /// The driver's running set, where a run says which quest it holds while it works, as the real run does —
    /// what the lost-claim stop asks about (D68 §5).
    /// </summary>
    public RunningSessions? Keeping { get; set; }

    /// <summary>The quests a session was opened for, in the order they opened.</summary>
    public IReadOnlyList<string> Started
    {
        get { lock (_started) return [.. _started]; }
    }

    /// <summary>The runner a driver is handed.</summary>
    public Func<Consideration, Action, CancellationToken, Task<StartRun>> Runner => RunAsync;

    /// <summary>
    /// The stop a driver is handed with the runner (DEV3b), which it calls for a take that lost as it ends a real session's
    /// process: true when a session of this stand-in's was running and not yet stopped, which then ends stood down for the
    /// driver's reason.
    /// </summary>
    public Func<string, Noted, bool> Stops => Stop;

    /// <summary>
    /// What a stopped run waits on before it writes how it ended, as a real run writes its record only once its process has
    /// exited: done at once unless a test holds it, to see what the driver says meanwhile.
    /// </summary>
    public Task StopLands { get; set; } = Task.CompletedTask;

    // Which quest each running session serves, and the driver's reason for each it stopped.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _serving = new(StringComparer.Ordinal);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Noted> _stoppedFor = new(StringComparer.Ordinal);

    /// <summary>End the session working on <paramref name="quest"/>: its quest closes done, and its record completes.</summary>
    public void End(string quest)
    {
        ledger.Move(quest, "Done");
        Ending(quest).TrySetResult("completed");
    }

    private bool Stop(string session, Noted reason)
    {
        // Once: a run already stopped and still writing how it ended is not stopped again, as the driver's own stop is not.
        if (!_serving.TryGetValue(session, out var quest) || !_stoppedFor.TryAdd(session, reason)) return false;
        Ending(quest).TrySetResult("stood-down");
        return true;
    }

    private TaskCompletionSource<string> Ending(string quest) =>
        _endings.GetOrAdd(quest, _ => new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously));
    private async Task<StartRun> RunAsync(Consideration start, Action opened, CancellationToken ct)
    {
        var quest = start.Quest;
        var (id, message) = await service.OpenSessionAsync(quest.Id, "stub", ct: ct);
        // In step with Driver.RunAsync's refusal (TICKSHOWN1): the person's words' refusal is the consideration's hold.
        if (id is null) return new StartRun($"refused  #{quest.Id} → {quest.To}: {message}", false, null, start.GoesOn ? $"the service refused the open: {message}" : null);

        await service.AdvanceAsync(id, "working", ct: ct);
        if (Takes) ledger.Move(quest.Id, "Taken");
        lock (_started) _started.Add(quest.Id);
        OnOpen?.Invoke(quest.Id);
        opened();

        Keeping?.Live.TryAdd(quest.Id, id);
        _serving[id] = quest.Id;
        try
        {
            string state;
            try
            {
                state = await Ending(quest.Id).Task.WaitAsync(ct);
                if (_stoppedFor.TryGetValue(id, out var reason))
                {
                    await StopLands.WaitAsync(ct);
                    await service.AdvanceAsync(id, state, reason, ct: CancellationToken.None);
                    return new StartRun(
                        $"{state}  session {id} (#{quest.Id} → {quest.To}): {reason.Note}", true,
                        new SessionEnded(id, quest.To, state, ByPerson: false, reason.Note, Quest: quest.Id));
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // A moment before the record is written, as ending a real process takes one: a caller that let go
                // without waiting for it would find the record still working.
                await Task.Delay(200, CancellationToken.None);

                // A stop the driver made before the close decided the end, as the driver's own close reads it (FLAKE3).
                var (closed, interrupted) = Observation.Closed(
                    _stoppedFor.TryGetValue(id, out var first) ? new SessionStop(first, Note: null) : null);
                if (!interrupted)
                {
                    await service.AdvanceAsync(id, closed.State, closed.AsNoted(), ct: CancellationToken.None);
                    return new StartRun(
                        $"{closed.State}  session {id} (#{quest.Id} → {quest.To}): {closed.Note}", true,
                        new SessionEnded(id, quest.To, closed.State, ByPerson: false, closed.Note, Quest: quest.Id));
                }

                await service.AdvanceAsync(
                    id, "stopped", note: "the driver was stopped while this ran.", ct: CancellationToken.None, interrupted: true);
                return new StartRun(
                    $"stopped  session {id} (#{quest.Id} → {quest.To}): the driver was stopped.", true,
                    new SessionEnded(id, quest.To, "stopped", ByPerson: true, Quest: quest.Id));
            }

            await service.AdvanceAsync(id, state, note: "the stand-in's work is done.", ct: CancellationToken.None);
            return new StartRun(
                $"{state}  session {id} (#{quest.Id} → {quest.To}): the stand-in's work is done.", true,
                new SessionEnded(id, quest.To, state, ByPerson: false, Quest: quest.Id));
        }
        finally
        {
            Keeping?.Live.TryRemove(quest.Id, out _);
            _serving.TryRemove(id, out _);
        }
    }
}
