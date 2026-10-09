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
        if (request.Content is not null) await request.Content.ReadAsStringAsync(ct);
        lock (_gate)
        {
            switch (request.Method.Method, path)
            {
                case ("GET", "/api/sessions"):
                    return Answer(HttpStatusCode.OK, new JsonArray([.. _sessions.Select(session => session.DeepClone())]));

                case ("GET", "/api/quests"):
                    return Answer(HttpStatusCode.OK, new JsonArray([.. _quests.Select(quest => quest.DeepClone())]));

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
                    if (opinion["handed"] is not null) return Refuse(HttpStatusCode.Conflict, "they go once");

                    var working = Find(opinion["working"]!.GetValue<string>());
                    var state = working?["state"]!.GetValue<string>();
                    if (working is null) return Refuse(HttpStatusCode.Conflict, "there is no such session: they go to the person instead");
                    if (state == "stood-down") return Refuse(HttpStatusCode.Conflict, "it stood down");
                    if (state == "awaiting-person") return Refuse(HttpStatusCode.Conflict, "it waits on the person's answer");
                    if (Running.Contains(state!)) return Refuse(HttpStatusCode.Conflict, "another agent's findings reach a running session at its turn's end");

                    var word = new JsonObject
                    {
                        ["id"] = $"op-w{++_words}", ["text"] = $"Another agent, Codex by OpenAI, read your work at `{Tip}` and claims what follows.",
                        ["at"] = "2026-10-09T09:15:00Z", ["files"] = new JsonArray(), ["reopens"] = true, ["by"] = id,
                    };
                    working["said"]!.AsArray().Add(word);
                    opinion["handed"] = new JsonObject
                    {
                        ["session"] = working["id"]!.GetValue<string>(), ["word"] = word["id"]!.GetValue<string>(), ["at"] = "2026-10-09T09:15:00Z",
                    };
                    return Answer(HttpStatusCode.OK, new JsonObject
                    {
                        ["opinion"] = opinion.DeepClone(), ["message"] = "Handed.", ["session"] = working.DeepClone(), ["said"] = word.DeepClone(),
                    });
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
