using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The local host's opinion doors as XAGENT1c built them, in process (XAGENT1e): an opinion read back with where its pass
/// stands, the hand door that keeps a first pass's findings on its working session's record once as a word whose <c>by</c> names
/// the opinion, and the session records that word waits on. The desk's refusals are modelled where the driver meets them: a
/// recheck, nothing said, nothing raised, handed already, and a working session that is running, parked or stood down.
/// </summary>
internal sealed class OpinionStandIn : HttpMessageHandler
{
    public const string Url = "http://opinions.test";

    public const string Base = "1111111111111111111111111111111111111111";

    public const string Tip = "2222222222222222222222222222222222222222";

    private readonly object _gate = new();
    private readonly Dictionary<string, JsonObject> _opinions = new(StringComparer.Ordinal);
    private readonly List<JsonObject> _sessions = [];
    private readonly List<JsonObject> _quests = [];

    /// <summary>A quest the service holds, closed ones included (XAGENT1f: the gate reads the chain from them).</summary>
    public OpinionStandIn Quest(string id, string status = "Done", string? parent = null, string? setUpIn = null, string to = "reports")
    {
        lock (_gate)
        {
            _quests.RemoveAll(quest => quest["id"]!.GetValue<string>() == id);
            _quests.Add(new JsonObject
            {
                ["id"] = id, ["from"] = "ask #a1", ["to"] = to, ["title"] = $"Work {id}", ["body"] = "", ["status"] = status,
                ["parent"] = parent, ["setUpIn"] = setUpIn,
            });
        }

        return this;
    }
    private int _words;

    /// <summary>Each hand asked, by opinion id, in order, whether it was taken or refused.</summary>
    public List<string> Hands { get; } = [];

    /// <summary>The record each hand named (CHATFLOW1e), in the order asked; null where it named none.</summary>
    public List<string?> HandsTo { get; } = [];

    /// <summary>Each helper's answer handed, by the helper's session, with the words as the driver composed them (MIXAGENT2a-ii).</summary>
    public List<(string Helper, string Text)> HelperHands { get; } = [];

    /// <summary>Each piece's answer handed, by the partner's session, with the record it went to and the words (COAGENT1b).</summary>
    public List<(string Partner, string To, string Text)> PartnerHands { get; } = [];

    /// <summary>Each move asked of a record's state door (HELPERWAIT1), in order: the record, the state, its note's first code.</summary>
    public List<(string Session, string State, string? Code)> Moves { get; } = [];

    /// <summary>How many times the records were read whole (HELPERWAIT1: a held hand reads none until its asker moves).</summary>
    public int RecordReads { get; private set; }

    /// <summary>How many times a helper's hand was asked, taken or refused (HELPERWAIT1: a hand the host refuses is not asked again until its asker moves).</summary>
    public int HelperHandsAsked { get; private set; }

    /// <summary>How many moves were asked of the state door, made or not (HELPERWAIT1).</summary>
    public int MovesAsked { get; private set; }

    /// <summary>Where set, the state door fails every move (HELPERWAIT1): <c>unreachable</c> as a transport failure, <c>refused</c> as a refusal.</summary>
    public string? StateFailure { get; set; }

    /// <summary>Where set, an ask's read fails (HELPERWAIT1c): <c>unreachable</c> as a transport failure, <c>refused</c> as a refusal.</summary>
    public string? AskFailure { get; set; }

    /// <summary>Where set, every helper's hand is refused with this reason (HELPERWAIT1), as an older or a failing desk refuses one.</summary>
    public string? HelperHandRefusal { get; set; }

    /// <summary>
    /// Where set, a helper's hand keeps its word and then its answer is lost (HELPERWAIT1b; Codex's round-3 #2 on COWORKUX1): <c>500</c> as
    /// the host's exception handler answers, <c>unreadable</c> as a success that does not read, <c>timeout</c> as a client that gave up.
    /// </summary>
    public string? HelperHandLost { get; set; }

    /// <summary>A client over this host, as a driver is handed one.</summary>
    public ServiceClient Client() => new(Url, null, new HttpClient(this, disposeHandler: false));

    /// <summary>A record of this machine's: its state, adapter, tree and kind; nothing waits on it.</summary>
    public OpinionStandIn Session(string id, string state, string? tree = null, string adapter = "claude-code-acp", string kind = "drive")
    {
        lock (_gate)
        {
            _sessions.Add(new JsonObject
            {
                ["id"] = id, ["quest"] = "q1", ["repository"] = "reports", ["state"] = state, ["kind"] = kind, ["adapter"] = adapter,
                ["tree"] = tree, ["said"] = new JsonArray(),
            });
        }

        return this;
    }

    /// <summary>
    /// A conversation in a tree of its own (CHATFLOW1e): no quest, a line of its own unless <paramref name="from"/> names the record it continues,
    /// made at <paramref name="minute"/> past nine, so a line reads its records in order.
    /// </summary>
    public OpinionStandIn Chat(string id, string state, string tree, string? from = null, int minute = 0, string adapter = "claude-code-acp")
    {
        Session(id, state, tree, adapter, kind: "chat");
        lock (_gate)
        {
            var record = Find(id)!;
            record["quest"] = null;
            var first = from is null ? id : (Find(from)!["entry"]!.GetValue<string>().Split('/')[0])["session:".Length..];
            record["entry"] = $"session:{first}/reports";
            record["from"] = from;
            record["created"] = $"2026-10-09T09:{minute:00}:00Z";
        }

        return this;
    }

    /// <summary>Move a record, as its run would.</summary>
    public void Move(string id, string state)
    {
        lock (_gate) Find(id)!["state"] = state;
    }

    /// <summary>Change a record's field, a teammate's id or an intake's ask among them.</summary>
    public void Set(string id, string field, JsonNode? value)
    {
        lock (_gate) Find(id)![field] = value;
    }

    /// <summary>A record's note as it now reads (HELPERWAIT1).</summary>
    public string? Note(string id)
    {
        lock (_gate) return Find(id)!["note"]?.GetValue<string>();
    }

    /// <summary>The words waiting on a record, as the wire answers them.</summary>
    public JsonArray Said(string id)
    {
        lock (_gate) return (JsonArray)Find(id)!["said"]!.DeepClone();
    }

    /// <summary>Take a record's words off by their ids, as a resumed run's conclusion does.</summary>
    public void Take(string id)
    {
        lock (_gate) Find(id)!["said"] = new JsonArray();
    }

    /// <summary>
    /// A pass on <paramref name="working"/>'s work by <paramref name="adapter"/>, where its pass stands, and its findings by
    /// weight once given (<paramref name="weights"/>; empty for a pass that raised nothing, null for one that said nothing).
    /// </summary>
    public OpinionStandIn Opinion(
        string id, string working, string state = "given", string[]? weights = null, string pass = "first", string? rechecks = null,
        string? why = null, string adapter = "codex-acp", string? tip = null, string? @base = null)
    {
        var opinion = new JsonObject
        {
            ["id"] = id, ["occasion"] = "landing", ["pass"] = pass, ["working"] = working, ["session"] = "r-" + id, ["rechecks"] = rechecks,
            ["candidate"] = new JsonObject
            {
                ["repository"] = "reports", ["base"] = @base ?? Base, ["tip"] = tip ?? Tip, ["commits"] = new JsonArray(tip ?? Tip),
            },
            ["reviewer"] = new JsonObject { ["adapter"] = adapter, ["label"] = "another-maker", ["product"] = "Codex", ["maker"] = "OpenAI" },
            ["families"] = new JsonArray("claude-code"), ["posture"] = "copy-alone", ["minutes"] = 20, ["tier"] = "agent",
            ["asked"] = "2026-10-09T09:00:00Z", ["due"] = "2026-10-09T09:20:00Z", ["state"] = state, ["why"] = why,
            ["given"] = weights is null ? null : new JsonObject
            {
                ["findings"] = new JsonArray([.. weights.Select((weight, at) => (JsonNode)new JsonObject
                {
                    ["number"] = at + 1, ["weight"] = weight, ["where"] = $"src/report.ts:{at + 10}", ["claim"] = $"claim {at + 1}",
                    ["consequence"] = $"consequence {at + 1}", ["reproduce"] = $"reproduce {at + 1}", ["sure"] = "likely",
                })]),
                ["read"] = "src/report.ts and the two commits", ["limits"] = "ran nothing", ["rechecked"] = new JsonArray(),
                ["at"] = "2026-10-09T09:10:00Z",
            },
            ["handed"] = null,
            ["answers"] = new JsonArray(),
        };
        lock (_gate) _opinions[id] = opinion;
        return this;
    }

    /// <summary>A working session's answer to one finding, as its connector's <c>opinion_answer</c> keeps it.</summary>
    public void Answer(string opinion, int finding, string said, string? commit = null, string? evidence = null, string? why = null)
    {
        lock (_gate)
        {
            _opinions[opinion]["answers"]!.AsArray().Add(new JsonObject
            {
                ["finding"] = finding, ["said"] = said, ["commit"] = commit, ["evidence"] = evidence, ["why"] = why, ["at"] = "2026-10-09T09:30:00Z",
            });
        }
    }

    /// <summary>A recheck's word on a first-pass finding.</summary>
    public void Rechecked(string opinion, int finding, string says)
    {
        lock (_gate) _opinions[opinion]["given"]!["rechecked"]!.AsArray().Add(new JsonObject { ["finding"] = finding, ["says"] = says });
    }

    /// <summary>Where a first pass's findings went, once handed.</summary>
    public JsonNode? Handed(string opinion)
    {
        lock (_gate) return _opinions[opinion]["handed"]?.DeepClone();
    }

    private JsonObject? Find(string id) => _sessions.SingleOrDefault(session => session["id"]!.GetValue<string>() == id);

    private static readonly HashSet<string> Running = ["queued", "starting", "working"];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.AbsolutePath;
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
        lock (_gate)
        {
            switch (request.Method.Method, path)
            {
                case ("GET", "/api/sessions"):
                    RecordReads++;
                    return Answer(HttpStatusCode.OK, new JsonArray([.. _sessions.Select(session => session.DeepClone())]));

                case ("GET", "/api/quests"):
                    return Answer(HttpStatusCode.OK, new JsonArray([.. _quests.Select(quest => quest.DeepClone())]));

                // HELPERWAIT1c: an ask's read the service does not answer, or refuses; with neither, the stand-in holds no ask.
                case ("GET", _) when AskFailure is not null && path.StartsWith("/api/asks/", StringComparison.Ordinal):
                    if (AskFailure == "unreachable") throw new HttpRequestException("the service did not answer");
                    return Refuse(HttpStatusCode.InternalServerError, "the asks could not be read");

                // HELPERWAIT1: the ledger's moves as far as a helper's asker reaches them: a live record moves as the ledger lets it, and a
                // finished one only where it ended waiting on its own helper, back to the person; the note and its parts kept.
                case ("POST", _) when path.StartsWith("/api/sessions/", StringComparison.Ordinal) && path.EndsWith("/state", StringComparison.Ordinal):
                {
                    var id = Uri.UnescapeDataString(path["/api/sessions/".Length..^"/state".Length]);
                    MovesAsked++;
                    // HELPERWAIT1 (Codex's round-2 #5): a door that cannot be reached, or one that refuses the move.
                    if (StateFailure == "unreachable") throw new HttpRequestException("the service did not answer");
                    if (StateFailure == "refused") return Refuse(HttpStatusCode.Conflict, "this service does not make that move");
                    var record = Find(id);
                    if (record is null) return Refuse(HttpStatusCode.NotFound, "no such session");
                    var asked = JsonNode.Parse(body!)!;
                    var from = record["state"]!.GetValue<string>();
                    var to = asked["state"]!.GetValue<string>();
                    var waited = from == "completed"
                                 && record["noteParts"] is JsonArray kept && kept.Any(part => part?["code"]?.GetValue<string>() == "ended.awaits-helper");
                    string[] moves = from switch
                    {
                        "queued" or "starting" or "working" => ["awaiting-person", "completed", "declined", "stood-down", "failed", "stopped", "working"],
                        "awaiting-person" => ["working", "completed", "declined", "stopped"],
                        _ when waited => ["awaiting-person"],
                        _ => [],
                    };
                    if (!moves.Contains(to)) return Refuse(HttpStatusCode.Conflict, $"Session `{id}` cannot move {from} → {to}");

                    record["state"] = to;
                    if (asked["note"] is { } note) record["note"] = note.GetValue<string>();
                    if (asked["note"] is not null) record["noteParts"] = asked["noteParts"]?.DeepClone();
                    Moves.Add((id, to, asked["noteParts"]?[0]?["code"]?.GetValue<string>()));
                    return Answer(HttpStatusCode.OK, new JsonObject { ["session"] = record.DeepClone(), ["message"] = "moved" });
                }

                case ("GET", _) when path.StartsWith("/api/opinions/", StringComparison.Ordinal):
                {
                    var id = Uri.UnescapeDataString(path["/api/opinions/".Length..]);
                    return _opinions.TryGetValue(id, out var opinion)
                        ? Answer(HttpStatusCode.OK, opinion.DeepClone())
                        : Answer(HttpStatusCode.NotFound, new JsonObject { ["error"] = $"There is no second opinion `{id}` on this machine." });
                }

                case ("POST", _) when path.StartsWith("/api/opinions/", StringComparison.Ordinal) && path.EndsWith("/hand", StringComparison.Ordinal):
                {
                    var id = Uri.UnescapeDataString(path["/api/opinions/".Length..^"/hand".Length]);
                    Hands.Add(id);
                    if (!_opinions.TryGetValue(id, out var opinion)) return Refuse(HttpStatusCode.NotFound, "no such opinion");
                    if (opinion["pass"]!.GetValue<string>() == "recheck") return Refuse(HttpStatusCode.Conflict, "a recheck's findings go to the person");
                    if (opinion["given"] is not JsonObject given) return Refuse(HttpStatusCode.Conflict, "it has said nothing yet");
                    if (given["findings"]!.AsArray().Count == 0) return Refuse(HttpStatusCode.Conflict, "it raised nothing");
                    // CHATFLOW1e: a chat run's record of the line the driver names, live between turns or ended; handed again once, after a hand-over.
                    var to = string.IsNullOrWhiteSpace(body) ? null : JsonNode.Parse(body)?["to"]?.GetValue<string>();
                    HandsTo.Add(to);
                    if (opinion["handed"] is JsonObject earlier)
                    {
                        if (to is null || earlier["session"]!.GetValue<string>() == to || earlier["from"] is not null) return Refuse(HttpStatusCode.Conflict, "they go once");
                        if (opinion["answers"]!.AsArray().Count > 0) return Refuse(HttpStatusCode.Conflict, "they were answered");
                    }

                    var working = Find(to ?? opinion["working"]!.GetValue<string>());
                    var state = working?["state"]!.GetValue<string>();
                    if (working is null) return Refuse(HttpStatusCode.Conflict, "there is no such session: they go to the person instead");
                    if (state == "stood-down") return Refuse(HttpStatusCode.Conflict, "it stood down");
                    if (state == "awaiting-person") return Refuse(HttpStatusCode.Conflict, "it waits on the person's answer");
                    if (to is not null && working["kind"]?.GetValue<string>() != "chat") return Refuse(HttpStatusCode.Conflict, "it is no conversation of a chat run's");
                    if (Running.Contains(state!) && !(to is not null && state == "working"))
                    {
                        return Refuse(HttpStatusCode.Conflict, "another agent's findings reach a running session at its turn's end");
                    }

                    var word = new JsonObject
                    {
                        ["id"] = $"op-w{++_words}", ["text"] = $"Another agent, Codex by OpenAI, read your work at `{Tip}` and claims what follows.",
                        ["at"] = "2026-10-09T09:15:00Z", ["files"] = new JsonArray(), ["reopens"] = true, ["by"] = id,
                    };
                    if (to is not null) word["level"] = "ordinary";
                    working["said"]!.AsArray().Add(word);
                    opinion["handed"] = new JsonObject
                    {
                        ["session"] = working["id"]!.GetValue<string>(), ["word"] = word["id"]!.GetValue<string>(), ["at"] = "2026-10-09T09:15:00Z",
                        ["from"] = (opinion["handed"] as JsonObject)?["session"]?.GetValue<string>(),
                    };
                    return Answer(HttpStatusCode.OK, new JsonObject
                    {
                        ["opinion"] = opinion.DeepClone(), ["message"] = "Handed.", ["session"] = working.DeepClone(), ["said"] = word.DeepClone(),
                    });
                }

                // MIXAGENT2a-ii: a helper's answer, handed to the session its record serves as a word marked `helper`, once, and refused
                // as the desk refuses it where the asker is gone, stood down, parked or running.
                case ("POST", _) when path.StartsWith("/api/helpers/", StringComparison.Ordinal) && path.EndsWith("/hand", StringComparison.Ordinal):
                {
                    var id = Uri.UnescapeDataString(path["/api/helpers/".Length..^"/hand".Length]);
                    HelperHandsAsked++;
                    if (HelperHandRefusal is { } refusal) return Refuse(HttpStatusCode.Conflict, refusal);
                    var helper = Find(id);
                    if (helper?["serves"]?.GetValue<string>() is not { } served) return Refuse(HttpStatusCode.NotFound, $"There is no helper session `{id}`");
                    var asker = Find(served);
                    var state = asker?["state"]!.GetValue<string>();
                    if (asker is null) return Refuse(HttpStatusCode.Conflict, "there is no such session to hand the answer to");
                    if (state == "stood-down") return Refuse(HttpStatusCode.Conflict, "it stood down");
                    // HELPERWAIT1: a park the person answered takes it beside their answer, as the desk lets it.
                    if (state == "awaiting-person" && asker["answer"] is null) return Refuse(HttpStatusCode.Conflict, "it waits on the person's answer");
                    // An open conversation between turns (MIXAGENT2a-iii) takes it, as the desk lets it; and a driven asker working, where
                    // the driver that runs it asks with `running` (COWORK1d), its answer kept with its level.
                    var asked = JsonNode.Parse(body!)!;
                    var open = state == "working" && (asker["kind"]?.GetValue<string>() == "chat" || asked["running"]?.GetValue<bool>() == true);
                    if (Running.Contains(state!) && !open) return Refuse(HttpStatusCode.Conflict, "a helper's answer reaches a running session at its turn's end");
                    if (asker["said"]!.AsArray().Any(each => each!["helper"]?.GetValue<string>() == id)) return Refuse(HttpStatusCode.Conflict, "it goes once");

                    var text = asked["text"]!.GetValue<string>();
                    var word = new JsonObject
                    {
                        ["id"] = $"hp-w{++_words}", ["text"] = text, ["at"] = "2026-10-12T10:15:00Z", ["files"] = new JsonArray(), ["reopens"] = true, ["helper"] = id,
                    };
                    if (asked["level"]?.GetValue<string>() is { } level) word["level"] = level;
                    asker["said"]!.AsArray().Add(word);
                    HelperHands.Add((id, text));
                    switch (HelperHandLost)
                    {
                        case "500": return Answer(HttpStatusCode.InternalServerError, new JsonObject { ["error"] = "An unexpected error." });
                        case "unreadable": return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>", Encoding.UTF8, "text/html") };
                        case "timeout": throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.");
                    }

                    return Answer(HttpStatusCode.OK, new JsonObject { ["message"] = "Handed.", ["session"] = asker.DeepClone(), ["said"] = word.DeepClone() });
                }

                // COAGENT1b: a piece's answer, kept under the piece's key on its lead (the stand-in knows no successor), once per key,
                // and refused as the desk refuses it where the lead is gone, stood down, parked or running.
                case ("POST", _) when path.StartsWith("/api/partners/", StringComparison.Ordinal) && path.EndsWith("/hand", StringComparison.Ordinal):
                {
                    var id = Uri.UnescapeDataString(path["/api/partners/".Length..^"/hand".Length]);
                    var partner = Find(id);
                    if (partner?["serves"]?.GetValue<string>() is not { } served || partner["piece"]?.GetValue<string>() is not { } piece)
                    {
                        return Refuse(HttpStatusCode.NotFound, $"There is no partner session `{id}`");
                    }

                    var lead = Find(served);
                    var state = lead?["state"]!.GetValue<string>();
                    if (lead is null) return Refuse(HttpStatusCode.Conflict, "there is no such session to hand the answer to");
                    if (state == "stood-down") return Refuse(HttpStatusCode.Conflict, "it stood down");
                    if (state == "awaiting-person") return Refuse(HttpStatusCode.Conflict, "it waits on the person's answer");
                    if (Running.Contains(state!)) return Refuse(HttpStatusCode.Conflict, "a partner's answer reaches a running session at its turn's end");
                    var key = $"partner:{piece}";
                    if (lead["said"]!.AsArray().FirstOrDefault(each => each!["key"]?.GetValue<string>() == key) is { } waiting)
                    {
                        return Answer(HttpStatusCode.OK, new JsonObject { ["message"] = "Waits already.", ["session"] = lead.DeepClone(), ["said"] = waiting.DeepClone() });
                    }

                    var text = JsonNode.Parse(body!)!["text"]!.GetValue<string>();
                    var word = new JsonObject
                    {
                        ["id"] = $"pt-w{++_words}", ["text"] = text, ["at"] = "2026-10-22T10:15:00Z", ["files"] = new JsonArray(), ["reopens"] = true,
                        ["helper"] = id, ["key"] = key,
                    };
                    lead["said"]!.AsArray().Add(word);
                    PartnerHands.Add((id, served, text));
                    return Answer(HttpStatusCode.OK, new JsonObject { ["message"] = "Handed.", ["session"] = lead.DeepClone(), ["said"] = word.DeepClone() });
                }

                default:
                    return Answer(HttpStatusCode.NotFound, new JsonObject { ["error"] = $"the stand-in has no {request.Method} {path}" });
            }
        }
    }

    private static HttpResponseMessage Refuse(HttpStatusCode status, string why) =>
        Answer(status, new JsonObject { ["error"] = $"{why}. Nothing was kept." });

    private static HttpResponseMessage Answer(HttpStatusCode status, JsonNode payload) => new(status)
    {
        Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
    };
}
