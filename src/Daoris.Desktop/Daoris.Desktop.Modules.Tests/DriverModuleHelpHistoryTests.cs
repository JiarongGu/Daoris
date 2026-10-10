using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Ask Daoris's history over the bridge (ASKHIST1, `DriverModule.Help.cs`): its conversations listed and searched, named,
/// pinned, deleted through the one delete the window uses, and gone on in with the person's words through the one box's door.
/// Kept on this machine only: the records the local host already keeps, the words this machine's own record, and what the
/// person keeps of each a file beside them under the home. A local host stands in; nothing is spawned.
/// </summary>
public sealed class DriverModuleHelpHistoryTests : DriverModuleBridge
{
    private static JsonObject Record(string id, string state = "completed", string repository = "daoris:help") => new()
    {
        ["id"] = id, ["repository"] = repository, ["state"] = state, ["kind"] = "chat", ["adapter"] = "claude-code-acp",
        ["created"] = "2026-10-05T09:00:00Z", ["updated"] = "2026-10-05T09:05:00Z", ["deletable"] = state is not "working",
        ["said"] = new JsonArray(),
    };

    private async Task<(DriverLoop Loop, DriverModule Module, Ledger Ledger)> UpAsync(params JsonObject[] records)
    {
        var ledger = new Ledger(records);
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(ledger)));
        return (loop, new DriverModule(Bus, loop), ledger);
    }

    private static void Said(DriverLoop loop, string id, string question, string answer)
    {
        loop.Events.Append(id, new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = question });
        loop.Events.Append(id, new SessionEvent { Kind = SessionEventKind.Message, Text = answer });
    }

    /// <summary>
    /// The list: Ask Daoris's conversations the person spoke in, each with its title, its line and whether it goes on in itself;
    /// a repository's chat and one nobody spoke in are not among them. A search finds by words and says where.
    /// </summary>
    [Fact]
    public async Task The_list_holds_ask_daoris_conversations_and_a_search_finds_them_by_words()
    {
        var (loop, module, _) = await UpAsync(Record("h1"), Record("h2", "stopped"), Record("h3"), Record("c1", repository: "engine"));
        Said(loop, "h1", "what is a workspace?", "A circle of repositories that share a remote.");
        Said(loop, "h2", "how do I land on a branch?", "Repositories → Setup → Line and landing.");
        Said(loop, "c1", "fix the build", "Done.");
        new HarnessConversations(loop.Home).Keep("h1", "claude-code-acp", "conv-h1");

        var all = await AnswerAsync(module, "HELP_CONVERSATIONS", new { });
        var rows = all.GetProperty("conversations").EnumerateArray().ToDictionary(row => row.GetProperty("session").GetString()!);
        Assert.Equal(["h1", "h2"], rows.Keys.Order());
        Assert.Equal("what is a workspace?", rows["h1"].GetProperty("title").GetString());
        Assert.Equal("A circle of repositories that share a remote.", rows["h1"].GetProperty("about").GetString());
        Assert.True(rows["h1"].GetProperty("resumable").GetBoolean());
        Assert.False(rows["h2"].GetProperty("resumable").GetBoolean());
        Assert.False(all.GetProperty("cut").GetBoolean());

        var found = await AnswerAsync(module, "HELP_CONVERSATIONS", new { q = "remote" });
        var hit = Assert.Single(found.GetProperty("conversations").EnumerateArray());
        Assert.Equal("h1", hit.GetProperty("session").GetString());
        Assert.Contains("share a remote", hit.GetProperty("found").GetString());
    }

    /// <summary>The page's row is the terminal's <c>--json</c>, field for field, in order (D50: two doors, one answer).</summary>
    [Fact]
    public void A_row_is_the_terminals_json_field_for_field()
    {
        var camel = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var listing = new HelpListing([new HelpConversation("h1", "completed") { Opening = "what is a workspace?" }], Total: 1, Next: null);

        var answer = JsonSerializer.SerializeToElement(DriverModule.HistoryAnswer(listing), camel);

        Assert.Equal(HelpCommand.JsonFields, answer.GetProperty("conversations")[0].EnumerateObject().Select(field => field.Name));
        Assert.Equal(["conversations", "cut", "total", "next"], answer.EnumerateObject().Select(field => field.Name));
    }

    /// <summary>
    /// ASKHIST1d: the route answers a page, as the terminal does: <c>limit</c> how many, <c>offset</c> where it starts, with how
    /// many there are and where the next page starts, and <c>cut</c> read as it was, whether this answer left some out. A search
    /// pages the same way, each find with the line it found its words on.
    /// </summary>
    [Fact]
    public async Task The_list_is_answered_a_page_at_a_time()
    {
        var (loop, module, _) = await UpAsync(Record("h1"), Record("h2"), Record("h3"));
        Said(loop, "h1", "what is a workspace?", "A circle that shares a remote.");
        Said(loop, "h2", "how do I land on a branch?", "Setup, then a remote.");
        Said(loop, "h3", "which agent runs intake?", "Settings, and a remote.");

        var first = await AnswerAsync(module, "HELP_CONVERSATIONS", new { limit = 2 });
        var rest = await AnswerAsync(module, "HELP_CONVERSATIONS", new { offset = 2, limit = 2 });
        var found = await AnswerAsync(module, "HELP_CONVERSATIONS", new { q = "remote", offset = 1, limit = 1 });

        static (int, bool, int, JsonElement) Page(JsonElement answer) => (answer.GetProperty("conversations").GetArrayLength(),
            answer.GetProperty("cut").GetBoolean(), answer.GetProperty("total").GetInt32(), answer.GetProperty("next"));
        var (rows, cut, total, next) = Page(first);
        Assert.Equal((2, true, 3, 2), (rows, cut, total, next.GetInt32()));
        (rows, cut, total, next) = Page(rest);
        Assert.Equal((1, false, 3, JsonValueKind.Null), (rows, cut, total, next.ValueKind));
        (rows, cut, total, next) = Page(found);
        Assert.Equal((1, true, 3, 2), (rows, cut, total, next.GetInt32()));
        Assert.Equal(3, first.GetProperty("conversations").EnumerateArray().Concat(rest.GetProperty("conversations").EnumerateArray())
            .Select(row => row.GetProperty("session").GetString()).Distinct().Count());
        Assert.Contains("a remote.", found.GetProperty("conversations")[0].GetProperty("foundLine").GetString());
    }

    /// <summary>A name and a pin are the person's, kept beside the conversation's words and listed; a clear gives the question back.</summary>
    [Fact]
    public async Task Rename_and_pin_are_kept_on_this_machine_and_listed()
    {
        var (loop, module, _) = await UpAsync(Record("h1"), Record("h2"));
        Said(loop, "h1", "what is a workspace?", "A circle.");
        Said(loop, "h2", "how do I land?", "Setup.");

        var named = await AnswerAsync(module, "HELP_RENAME", new { id = "h1", name = " Workspaces " });
        var pinned = await AnswerAsync(module, "HELP_PIN", new { id = "h1", pinned = true });

        Assert.Equal("Workspaces", named.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.String, pinned.GetProperty("pinned").ValueKind);
        var first = (await AnswerAsync(module, "HELP_CONVERSATIONS", new { })).GetProperty("conversations")[0];
        Assert.Equal(("h1", "Workspaces"), (first.GetProperty("session").GetString(), first.GetProperty("title").GetString()));
        Assert.True(File.Exists(new HelpConversations(loop.Home).PathOf("h1")));

        await AnswerAsync(module, "HELP_PIN", new { id = "h1", pinned = false });
        var cleared = await AnswerAsync(module, "HELP_RENAME", new { id = "h1" });
        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("name").ValueKind);
        Assert.Equal(HelpKept.None, new HelpConversations(loop.Home).Read("h1"));
    }

    /// <summary>
    /// Only Ask Daoris's own conversations of this machine's are named, pinned or started from: a repository's chat, a teammate's
    /// record and one no record holds are refused in a sentence, and nothing is kept. A name that is not one short line is too.
    /// </summary>
    [Theory]
    [InlineData("HELP_RENAME", "c1", "is no Ask Daoris conversation of this machine's")]
    [InlineData("HELP_PIN", "laptop/h9", "is no Ask Daoris conversation of this machine's")]
    [InlineData("HELP_PIN", "n0b0dy00", "no conversation here is `n0b0dy00`")]
    public async Task Any_other_session_is_refused_and_nothing_is_kept(string route, string id, string why)
    {
        var (loop, module, _) = await UpAsync(Record("h1"), Record("c1", repository: "engine"), Record("laptop/h9"));

        var refusal = await RefusalAsync(module, route, new { id, name = "x", pinned = true });

        Assert.Contains(why, refusal);
        var sessions = Path.Combine(loop.Home, "sessions");
        Assert.True(!Directory.Exists(sessions) || !Directory.EnumerateFiles(sessions, "*" + HelpConversations.Suffix).Any());
    }

    [Fact]
    public async Task A_name_longer_than_a_title_is_refused_in_the_stores_words()
    {
        var (_, module, _) = await UpAsync(Record("h1"));

        var refusal = await RefusalAsync(module, "HELP_RENAME", new { id = "h1", name = new string('x', 81) });

        Assert.Contains("at most 80 characters", refusal);
    }

    /// <summary>
    /// Deleting one is the window's one delete (SESSUX1f): the ledger takes the record, and this machine's files go with it,
    /// its name and pin among them; another conversation's stay.
    /// </summary>
    [Fact]
    public async Task Deleting_one_takes_its_record_its_words_and_its_name()
    {
        var (loop, module, ledger) = await UpAsync(Record("h1"), Record("h2"));
        Said(loop, "h1", "what is a workspace?", "A circle.");
        Said(loop, "h2", "how do I land?", "Setup.");
        var kept = new HelpConversations(loop.Home);
        kept.Rename("h1", "Workspaces");
        kept.Rename("h2", "Landing");

        var deleted = await AnswerAsync(module, "SESSION_DELETE", new { id = "h1" });

        Assert.Equal("h1", deleted.GetProperty("deleted").GetString());
        Assert.Contains("help", deleted.GetProperty("removed").EnumerateArray().Select(name => name.GetString()));
        Assert.Equal(["DELETE /api/sessions/h1"], ledger.Deletes);
        Assert.False(File.Exists(kept.PathOf("h1")));
        Assert.False(File.Exists(loop.Events.PathOf("h1")));
        Assert.Equal("Landing", kept.Read("h2").Name);
    }

    /// <summary>
    /// 🔴 Going on in one is the person's words to it (D137 §2.2, ASKHIST1): an ended Ask Daoris conversation is no never any
    /// more, so its words are kept on its record through the say door, shown at once with the reach <c>resume</c>, and the loop
    /// is nudged, as a chat's are; its runner takes them up and resumes its own conversation.
    /// </summary>
    [Fact]
    public async Task Words_to_an_ended_ask_daoris_conversation_are_kept_on_its_record_to_go_on_in_it()
    {
        var (loop, module, ledger) = await UpAsync(Record("h1"));

        var sent = await AnswerAsync(module, "SESSION_INPUT", new { id = "h1", text = "and the remote?" });

        Assert.Equal((true, "resume"), (sent.GetProperty("sent").GetBoolean(), sent.GetProperty("reaches").GetString()));
        Assert.Equal(JsonValueKind.Null, sent.GetProperty("why").ValueKind);
        Assert.Equal("/api/sessions/h1/say", Assert.Single(ledger.Says));
        var shown = Assert.Single(loop.Events.Page("h1").Events);
        Assert.Equal(("person", "resume"), (shown.Origin, shown.Reaches));
    }

    /// <summary>Starting from an earlier one needs Ask Daoris's agent named, as its start does, and an Ask Daoris conversation.</summary>
    [Fact]
    public async Task Starting_from_an_earlier_one_needs_its_agent_and_an_ask_daoris_conversation()
    {
        var (loop, module, _) = await UpAsync(Record("h1"), Record("c1", repository: "engine"));

        Assert.Contains("Settings → AI features", await RefusalAsync(module, "HELP_START_FROM", new { id = "h1" }));

        File.WriteAllText(loop.ConfigPath, """{ "helperAdapter": "claude-code-acp" }""");
        Assert.Contains("is no Ask Daoris conversation of this machine's", await RefusalAsync(module, "HELP_START_FROM", new { id = "c1" }));
    }

    /// <summary>Before the loop's service answers, the history waits for it, as every route that reads the records does.</summary>
    [Fact]
    public async Task The_history_waits_for_the_loop()
    {
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "HELP_CONVERSATIONS", new { }));
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "HELP_PIN", new { id = "h1", pinned = true }));
    }

    /// <summary>
    /// The local host standing in: the records, the say door keeping words on an ended record, the ledger's delete judgement
    /// and its delete. Each say and delete is heard.
    /// </summary>
    private sealed class Ledger(params JsonObject[] records) : HttpMessageHandler
    {
        private readonly List<JsonObject> _records = [.. records];

        public List<string> Deletes { get; } = [];

        public List<string> Says { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.UnescapeDataString).ToArray();
            if (request.Content is not null) await request.Content.ReadAsStringAsync(ct);
            lock (_records)
            {
                return (request.Method.Method, parts) switch
                {
                    ("GET", ["api", "sessions"]) => Answer(HttpStatusCode.OK, new JsonArray([.. _records.Select(each => each.DeepClone())])),
                    ("GET", ["api", "sessions", var id, "deletable"]) => Answer(HttpStatusCode.OK, new JsonObject { ["deletable"] = Find(id) is not null }),
                    ("DELETE", ["api", "sessions", var id]) => Deleted(path, id),
                    ("POST", ["api", "sessions", var id, "say"]) => Kept(path, id),
                    _ => Answer(HttpStatusCode.NotFound, new JsonObject { ["error"] = $"the stand-in has no {request.Method} {path}" }),
                };
            }
        }

        private JsonObject? Find(string id) => _records.FirstOrDefault(each => (string?)each["id"] == id);

        private HttpResponseMessage Deleted(string path, string id)
        {
            Deletes.Add($"DELETE {path}");
            _records.Remove(Find(id)!);
            return Answer(HttpStatusCode.OK, new JsonObject { ["id"] = id, ["message"] = $"Deleted session `{id}`." });
        }

        private HttpResponseMessage Kept(string path, string id)
        {
            Says.Add(path);
            return Answer(HttpStatusCode.OK, new JsonObject
            {
                ["session"] = new JsonObject { ["id"] = id, ["state"] = "completed" },
                ["message"] = $"Kept for session `{id}` to go on with.",
                ["said"] = new JsonObject
                {
                    ["id"] = "w1", ["text"] = "and the remote?", ["at"] = "2026-10-05T10:00:00+00:00", ["files"] = new JsonArray(), ["reopens"] = true,
                },
            });
        }

        private static HttpResponseMessage Answer(HttpStatusCode status, JsonNode body) =>
            new(status) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
    }
}
