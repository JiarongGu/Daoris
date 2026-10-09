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

    /// <summary>How many sync passes a driver asked this host for.</summary>
    public int SyncPasses => Volatile.Read(ref _syncPasses);

    private int _syncPasses;

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
    public void Publish(string id, string to, string? title = null, string from = "game")
    {
        lock (_gate)
        {
            _quests.Add(new JsonObject
            {
                ["id"] = id, ["from"] = from, ["to"] = to, ["title"] = title ?? $"The work of #{id}",
                ["body"] = "Stand-in work.", ["status"] = "Open",
            });
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

    /// <summary>Move a quest, as its session's own connector would.</summary>
    public void Move(string id, string status)
    {
        lock (_gate) _quests.Single(q => q["id"]!.GetValue<string>() == id)["status"] = status;
    }

    /// <summary>
    /// A driven session of this machine's that took <paramref name="quest"/> and parked to ask the person (D83), in its own
    /// tree, with no words on it yet (ANSWER2): what the say door keeps the person's answer on.
    /// </summary>
    public void Park(string session, string quest, string tree)
    {
        lock (_gate)
        {
            var asked = _quests.Single(q => q["id"]!.GetValue<string>() == quest);
            asked["status"] = "Taken";
            _clock = _clock.AddSeconds(1);
            _sessions.Add(new JsonObject
            {
                ["id"] = session, ["quest"] = quest, ["repository"] = asked["to"]!.GetValue<string>(), ["state"] = "awaiting-person",
                ["kind"] = "driven", ["adapter"] = "stub", ["tree"] = tree, ["created"] = _clock.ToString("O"),
                ["note"] = "Which port should it listen on?", ["said"] = new JsonArray(),
            });
        }
    }

    /// <summary>
    /// A session of this machine's working in <paramref name="tree"/> with no quest (AUTOTIDY1): a conversation, as the ledger
    /// keeps one, which the active list answers.
    /// </summary>
    public void Working(string session, string repository, string tree)
    {
        lock (_gate)
        {
            _clock = _clock.AddSeconds(1);
            _sessions.Add(new JsonObject
            {
                ["id"] = session, ["repository"] = repository, ["state"] = "working", ["kind"] = "chat", ["adapter"] = "stub",
                ["tree"] = tree, ["created"] = _clock.ToString("O"),
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

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.AbsolutePath;
        var all = request.RequestUri.Query.Contains("includeClosed=true", StringComparison.Ordinal);
        var body = request.Content is null ? null : JsonNode.Parse(await request.Content.ReadAsStringAsync(ct))?.AsObject();

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
                        .Select(q => q.DeepClone())]));

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
                    var quest = body!["quest"]!.GetValue<string>();
                    var to = _quests.Single(q => q["id"]!.GetValue<string>() == quest)["to"]!.GetValue<string>();
                    _clock = _clock.AddSeconds(1);
                    var session = new JsonObject
                    {
                        ["id"] = $"s{_sessions.Count + 1}", ["quest"] = quest, ["repository"] = to,
                        ["state"] = "queued", ["kind"] = "driven", ["adapter"] = body["adapter"]?.GetValue<string>(),
                        ["tree"] = body["tree"]?.GetValue<string>(), ["created"] = _clock.ToString("O"),
                        // The account it runs on, as the service answers it on loopback (TOOL4f reads it back).
                        ["profile"] = body["profile"]?.GetValue<string>(),
                    };
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
                    if (state is "queued" or "starting" or "working")
                    {
                        return Answer(HttpStatusCode.Conflict, new JsonObject
                        {
                            ["error"] = $"Session `{id}` is {state}: words to a running session reach it through the driver that runs it, not its record.",
                            ["refusal"] = "running",
                        });
                    }

                    if (session["said"] is not JsonArray said) session["said"] = said = [];
                    _clock = _clock.AddSeconds(1);
                    var word = new JsonObject
                    {
                        ["id"] = $"w{said.Count + 1}", ["text"] = body!["text"]!.GetValue<string>().Trim(), ["at"] = _clock.ToString("O"),
                        ["files"] = new JsonArray(), ["reopens"] = state != "awaiting-person",
                    };
                    said.Add(word);
                    session["answer"] = string.Join("\n\n", said.Select(each => each!["text"]!.GetValue<string>()));
                    return Answer(HttpStatusCode.OK, new JsonObject
                    {
                        ["session"] = session.DeepClone(), ["message"] = $"Kept for session `{id}` to go on with.", ["said"] = word.DeepClone(),
                    });
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
                    if (left.Count == 0) session.Remove("answer");
                    else session["answer"] = string.Join("\n\n", left.Select(word => word["text"]!.GetValue<string>()));
                    return Answer(HttpStatusCode.OK, new JsonObject { ["session"] = session.DeepClone(), ["message"] = "taken" });
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
                    return Answer(HttpStatusCode.OK, new JsonObject { ["claim"] = _claim });
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
/// ends as the driver's shutdown ends a session: <c>stopped</c>, interrupted (D104).
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
        if (id is null) return new StartRun($"refused  #{quest.Id} → {quest.To}: {message}", false);

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
