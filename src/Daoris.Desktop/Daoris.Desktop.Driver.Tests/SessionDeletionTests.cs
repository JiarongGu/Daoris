using System.Net;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SESSUX1f (D126 §5.4): deleting a conversation that served no quest, the machine's half. The ledger judges the record's
/// half and its refusal is said first, in its own words and with its word; then the machine's half, a tree still here or
/// a landing naming it; then the ledger deletes, and only after its yes are the files this machine kept of it removed.
/// The line in the machine log holds no words.
/// </summary>
/// <remarks>Files and an in-process stand-in for the service only: the suite's fast half.</remarks>
public sealed class SessionDeletionTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-delete-" + Guid.NewGuid().ToString("N")[..8]);

    public SessionDeletionTests() => Directory.CreateDirectory(Path.Combine(_home, "sessions"));

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private string Sessions => Path.Combine(_home, "sessions");

    private static JsonObject Record(string id, string state = "completed", string kind = "chat", string repository = "engine",
        string? quest = null, string? tree = null, string? ask = null, bool deletable = true) => new()
        {
            ["id"] = id, ["repository"] = repository, ["state"] = state, ["kind"] = kind, ["quest"] = quest,
            ["tree"] = tree, ["ask"] = ask, ["deletable"] = deletable,
            ["created"] = "2026-10-03T09:00:00Z", ["updated"] = "2026-10-03T09:05:00Z",
        };

    /// <summary>Everything this machine keeps of a session: its words, its transcript, its files, its conversation id, its mark.</summary>
    private void Kept(string id)
    {
        File.WriteAllText(Path.Combine(Sessions, $"{id}.events.jsonl"), "{\"seq\":1,\"kind\":\"user\",\"text\":\"a secret plan\"}\n");
        File.WriteAllText(Path.Combine(Sessions, $"{id}.log"), "the transcript\n");
        Directory.CreateDirectory(Path.Combine(Sessions, id, "files"));
        File.WriteAllText(Path.Combine(Sessions, id, "files", "shot.png"), "png");
        File.WriteAllText(Path.Combine(Sessions, id + HarnessConversations.Suffix), "{}");
        // Every id a test keeps files for is a known record, so one mark's write keeps the others'.
        new SessionArchive(_home).Archive([id], [new SessionGrouping(id, SessionGroup.Ended, "completed")], ["c1", "c2", id], DateTimeOffset.UtcNow);
    }

    private bool AnyKept(string id) =>
        File.Exists(Path.Combine(Sessions, $"{id}.events.jsonl")) || File.Exists(Path.Combine(Sessions, $"{id}.log"))
        || Directory.Exists(Path.Combine(Sessions, id)) || File.Exists(Path.Combine(Sessions, id + HarnessConversations.Suffix))
        || new SessionArchive(_home).Marks().ContainsKey(id);

    [Fact]
    public async Task A_conversation_the_ledger_takes_goes_with_what_this_machine_kept_of_it_and_a_line_with_no_words()
    {
        Kept("c1");
        Kept("c2");
        var ledger = new Ledger(Record("c1"), Record("c2"));
        using var service = ledger.Client();
        var log = new LogLines(_home);

        var outcome = await new SessionDeletion(_home).DeleteAsync(service, "c1", PluginEvents.Screen, log.Log);

        Assert.Equal(DeleteVerdict.Deleted, outcome.Verdict);
        Assert.Equal(["DELETE /api/sessions/c1"], ledger.Deletes);
        Assert.False(AnyKept("c1"));
        Assert.True(AnyKept("c2"));
        Assert.Equal(["record", "conversation", "transcript", "files", "harness", "archived"], outcome.Removed);
        var line = Assert.Single(log.Lines(), each => each.Contains("\"session.deleted\"", StringComparison.Ordinal));
        Assert.Contains("\"session\":\"c1\"", line);
        Assert.Contains("\"kind\":\"chat\"", line);
        Assert.Contains("\"door\":\"screen\"", line);
        Assert.DoesNotContain("secret", line);
    }

    /// <summary>
    /// HIST1c (D153 point 6, the history-clearing design §2.2): the delete and the clear take a session's files through one
    /// helper, so the four a delete left behind go too: its go-on mark, its choice of a new session, its held words and its
    /// closed automatic landing, with the spawn files a crash left. Another session's are untouched.
    /// </summary>
    [Fact]
    public async Task A_delete_takes_the_marks_the_held_words_the_closed_landing_and_the_spawn_files_too()
    {
        foreach (var id in new[] { "c1", "c2" })
        {
            Kept(id);
            File.WriteAllText(Path.Combine(Sessions, id + GoOnMarks.Suffix), "{\"said\":[\"w1\"],\"why\":\"closed\",\"at\":\"2026-10-03T09:00:00Z\"}\n");
            File.WriteAllText(Path.Combine(Sessions, id + NewSessionChoices.Suffix), "{\"said\":[\"w1\"],\"at\":\"2026-10-03T09:00:00Z\"}\n");
            Directory.CreateDirectory(Path.Combine(_home, SpawnServers.Folder));
            File.WriteAllText(Path.Combine(_home, SpawnServers.Folder, $"{id}.mcp.json"), "{}");
            File.WriteAllText(Path.Combine(_home, SpawnServers.Folder, $"{id}.settings.json"), "{}");
            var landings = new AutoLandings(_home);
            landings.Due(new AutoLanding(id, "q1", "engine", "default", "a tree", DateTimeOffset.UtcNow));
            landings.Tried(id, new AutoTry(DateTimeOffset.UtcNow, AutoLandingCode.Landed), close: true);
        }

        File.WriteAllText(HeldWordsFile.PathOf(_home), """
            {
              "held": [
                { "session": "c1", "text": "a word said as it wound up", "files": [], "door": "screen" },
                { "session": "c2", "text": "another", "files": [], "door": "terminal" }
              ]
            }
            """);
        var ledger = new Ledger(Record("c1"), Record("c2"));
        using var service = ledger.Client();

        var outcome = await new SessionDeletion(_home).DeleteAsync(service, "c1", PluginEvents.Screen, log: null);

        Assert.Equal(DeleteVerdict.Deleted, outcome.Verdict);
        Assert.Equal(["record", "conversation", "transcript", "files", "harness", "mark", "choice", "spawn", "held", "landing", "archived"], outcome.Removed);
        Assert.False(File.Exists(Path.Combine(Sessions, "c1" + GoOnMarks.Suffix)));
        Assert.False(File.Exists(Path.Combine(Sessions, "c1" + NewSessionChoices.Suffix)));
        Assert.False(File.Exists(Path.Combine(_home, SpawnServers.Folder, "c1.mcp.json")));
        Assert.False(File.Exists(Path.Combine(_home, SpawnServers.Folder, "c1.settings.json")));
        Assert.Null(new AutoLandings(_home).Of("c1"));
        Assert.DoesNotContain("c1", File.ReadAllText(HeldWordsFile.PathOf(_home)));

        Assert.True(AnyKept("c2"));
        Assert.True(File.Exists(Path.Combine(Sessions, "c2" + GoOnMarks.Suffix)));
        Assert.True(File.Exists(Path.Combine(Sessions, "c2" + NewSessionChoices.Suffix)));
        Assert.True(File.Exists(Path.Combine(_home, SpawnServers.Folder, "c2.settings.json")));
        Assert.NotNull(new AutoLandings(_home).Of("c2"));
        Assert.Contains("\"c2\"", File.ReadAllText(HeldWordsFile.PathOf(_home)));
    }

    /// <summary>An automatic landing still trying is no leftover: the helper leaves an open entry, which a clear refuses before it.</summary>
    [Fact]
    public void The_helper_leaves_an_automatic_landing_still_open()
    {
        new AutoLandings(_home).Due(new AutoLanding("c1", "q1", "engine", "default", "a tree", DateTimeOffset.UtcNow));

        var removed = new SessionHomeFiles(_home).Remove(["c1"], events: null);

        Assert.DoesNotContain("landing", removed.Went["c1"]);
        Assert.NotNull(new AutoLandings(_home).Of("c1"));
    }

    /// <summary>Ask Daoris's conversation is said as its own kind in the log, as its open is (LOG1b).</summary>
    [Fact]
    public async Task Ask_daoris_is_logged_as_help()
    {
        var ledger = new Ledger(Record("h1", repository: "daoris:help"));
        using var service = ledger.Client();
        var log = new LogLines(_home);

        await new SessionDeletion(_home).DeleteAsync(service, "h1", PluginEvents.Terminal, log.Log);

        var line = Assert.Single(log.Lines(), each => each.Contains("\"session.deleted\"", StringComparison.Ordinal));
        Assert.Contains("\"kind\":\"help\"", line);
        Assert.Contains("\"door\":\"terminal\"", line);
    }

    /// <summary>
    /// A conversation with a tree of its own still here (§5.4): the tree is discarded or cleaned up first, and the ledger is
    /// never asked to delete, so its record and every file stay.
    /// </summary>
    [Fact]
    public async Task Its_tree_still_here_refuses_before_the_ledger_is_asked_to_delete()
    {
        var tree = Path.Combine(_home, "trees", "default", "engine", "s-1a2b3c4d");
        Directory.CreateDirectory(tree);
        File.WriteAllText(Path.Combine(tree, "work.txt"), "work");
        Kept("c1");
        var ledger = new Ledger(Record("c1", tree: tree));
        using var service = ledger.Client();

        var outcome = await new SessionDeletion(_home).DeleteAsync(service, "c1", PluginEvents.Screen, log: null);

        Assert.Equal(DeleteVerdict.TreeHere, outcome.Verdict);
        Assert.Contains("discard the tree or clean it up first", outcome.Message);
        Assert.Empty(ledger.Deletes);
        Assert.True(AnyKept("c1"));
    }

    /// <summary>A tree a tidy took, or one in the repository's own checkout, is no tree of its own here.</summary>
    [Fact]
    public async Task A_tree_gone_or_a_checkout_does_not_refuse()
    {
        var gone = Path.Combine(_home, "trees", "default", "engine", "s-gone0000");
        var checkout = Path.Combine(_home, "checkouts", "engine");
        Directory.CreateDirectory(checkout);
        File.WriteAllText(Path.Combine(checkout, "README.md"), "a checkout");
        var ledger = new Ledger(Record("c1", tree: gone), Record("c2", tree: checkout));
        using var service = ledger.Client();

        Assert.Equal(DeleteVerdict.Deleted, (await new SessionDeletion(_home).DeleteAsync(service, "c1", PluginEvents.Screen, null)).Verdict);
        Assert.Equal(DeleteVerdict.Deleted, (await new SessionDeletion(_home).DeleteAsync(service, "c2", PluginEvents.Screen, null)).Verdict);
        Assert.True(Directory.Exists(checkout));
    }

    [Fact]
    public async Task A_landing_that_names_it_refuses()
    {
        new LandedBranches(_home).Record(new LandedBranch(
            "engine", "default", "daoris/s-1a2b3c4d", "main", "abc123", "c1", null, "a chat's work", DateTimeOffset.UtcNow));
        var ledger = new Ledger(Record("c1"));
        using var service = ledger.Client();

        var outcome = await new SessionDeletion(_home).DeleteAsync(service, "c1", PluginEvents.Screen, null);

        Assert.Equal((DeleteVerdict.Named, SessionDeletion.ByLanding), (outcome.Verdict, outcome.NamedBy));
        Assert.Empty(ledger.Deletes);
    }

    public static TheoryData<string, DeleteVerdict> LedgerRefusals => new()
    {
        { "live", DeleteVerdict.Live },
        { "served-quest", DeleteVerdict.ServedQuest },
        { "named", DeleteVerdict.Named },
        { "on-remote", DeleteVerdict.OnRemote },
        { "not-ours", DeleteVerdict.NotOurs },
    };

    /// <summary>
    /// The ledger's refusal comes first, in its words and with its facts, before the machine's half: a driven session with a
    /// tree here is told it served a quest, not to discard a tree it would then still not be deleted for.
    /// </summary>
    [Theory]
    [MemberData(nameof(LedgerRefusals))]
    public async Task The_ledgers_refusal_is_said_first_with_its_word_and_facts(string word, DeleteVerdict verdict)
    {
        var tree = Path.Combine(_home, "trees", "default", "engine", "s-9f8e7d6c");
        Directory.CreateDirectory(tree);
        File.WriteAllText(Path.Combine(tree, "work.txt"), "work");
        var ledger = new Ledger(Record("s1", kind: "driven", quest: "q1", tree: tree, deletable: false))
        {
            Refusal = new JsonObject
            {
                ["deletable"] = false, ["error"] = $"the ledger's {word} sentence", ["refusal"] = word,
                ["quest"] = "q1", ["ask"] = "a1", ["origin"] = "laptop", ["workspace"] = "aurora",
            },
        };
        using var service = ledger.Client();

        var outcome = await new SessionDeletion(_home).DeleteAsync(service, "s1", PluginEvents.Screen, null);

        Assert.Equal(verdict, outcome.Verdict);
        Assert.Equal($"the ledger's {word} sentence", outcome.Message);
        Assert.Equal(("q1", "a1", "laptop", "aurora"), (outcome.Quest, outcome.Ask, outcome.Machine, outcome.Workspace));
        Assert.Empty(ledger.Deletes);
    }

    /// <summary>Named by an ask, or by a quest it published: which, so the sentence names it.</summary>
    [Fact]
    public async Task A_name_from_the_ledger_says_whether_an_ask_or_a_quest_named_it()
    {
        var ledger = new Ledger(Record("i1", repository: "ask #a1", ask: "a1", deletable: false))
        {
            Refusal = new JsonObject { ["deletable"] = false, ["error"] = "named by its ask", ["refusal"] = "named", ["ask"] = "a1" },
        };
        using var service = ledger.Client();

        var outcome = await new SessionDeletion(_home).DeleteAsync(service, "i1", PluginEvents.Screen, null);

        Assert.Equal((DeleteVerdict.Named, SessionDeletion.ByAsk), (outcome.Verdict, outcome.NamedBy));
    }

    /// <summary>
    /// A teammate's record (SYNC4) is refused from the record itself, naming the machine: its id carries the machine, which
    /// the ledger's doors cannot address, and nothing this machine does reaches their copy.
    /// </summary>
    [Fact]
    public async Task A_teammates_record_is_refused_naming_its_machine_without_asking_the_ledger()
    {
        var ledger = new Ledger(Record("laptop/c1", deletable: false));
        using var service = ledger.Client();

        var outcome = await new SessionDeletion(_home).DeleteAsync(service, "laptop/c1", PluginEvents.Screen, null);

        Assert.Equal((DeleteVerdict.NotOurs, "laptop"), (outcome.Verdict, outcome.Machine));
        Assert.Empty(ledger.Judged);
    }

    [Fact]
    public async Task An_id_no_record_has_is_unknown()
    {
        var ledger = new Ledger(Record("c1"));
        using var service = ledger.Client();

        var outcome = await new SessionDeletion(_home).DeleteAsync(service, "nope", PluginEvents.Screen, null);

        Assert.Equal(DeleteVerdict.Unknown, outcome.Verdict);
        Assert.Empty(ledger.Deletes);
    }

    /// <summary>A record that moved between the judgement and the delete is judged again by the ledger: its refusal stands, and no file goes.</summary>
    [Fact]
    public async Task A_refusal_at_the_delete_itself_keeps_every_file()
    {
        Kept("c1");
        var ledger = new Ledger(Record("c1"))
        {
            DeleteRefusal = new JsonObject { ["deletable"] = false, ["error"] = "it is running again", ["refusal"] = "live" },
        };
        using var service = ledger.Client();

        var outcome = await new SessionDeletion(_home).DeleteAsync(service, "c1", PluginEvents.Screen, null);

        Assert.Equal(DeleteVerdict.Live, outcome.Verdict);
        Assert.True(AnyKept("c1"));
    }

    /// <summary>
    /// What the list reads for <c>deletable</c>: the sessions the ledger would delete whose tree or landing this machine
    /// still holds. Only those the ledger would delete are looked at.
    /// </summary>
    [Fact]
    public void Kept_names_the_sessions_whose_tree_or_landing_this_machine_holds()
    {
        var tree = Path.Combine(_home, "trees", "default", "engine", "s-1a2b3c4d");
        Directory.CreateDirectory(tree);
        File.WriteAllText(Path.Combine(tree, "work.txt"), "work");
        new LandedBranches(_home).Record(new LandedBranch(
            "engine", "default", "daoris/s-landed00", "main", "abc123", "landed", null, "work", DateTimeOffset.UtcNow));
        var records = SessionRecords.Parse(new JsonArray(
            Record("treed", tree: tree), Record("landed"), Record("free"), Record("driven", kind: "driven", quest: "q1", tree: tree, deletable: false))
            .ToJsonString());

        var kept = new SessionDeletion(_home).Kept(records);

        Assert.Equal(["landed", "treed"], kept.Order(StringComparer.Ordinal));
    }

    /// <summary>The service's answer per record, read as the list reads it.</summary>
    [Fact]
    public void A_records_deletable_is_read_as_the_service_answers_it()
    {
        var records = SessionRecords.Parse(new JsonArray(Record("c1"), Record("c2", deletable: false),
            new JsonObject { ["id"] = "c3", ["repository"] = "engine", ["state"] = "completed" }).ToJsonString());

        Assert.Equal([true, false, false], records.Select(record => record.Deletable));
    }

    /// <summary>The machine log's lines this test's home holds, as written.</summary>
    private sealed class LogLines(string home)
    {
        public MachineLog Log { get; } = new(home, "desktop");

        public IReadOnlyList<string> Lines()
        {
            Log.Dispose();
            var folder = Path.Combine(home, MachineLog.Folder);
            return Directory.Exists(folder) ? [.. Directory.GetFiles(folder).SelectMany(StubFile.Lines)] : [];
        }
    }

    /// <summary>
    /// The service's session doors, in-process: the records, the ledger's judgement, and its delete. It deletes what it is
    /// asked to unless told to refuse, and keeps what was asked.
    /// </summary>
    private sealed class Ledger(params JsonObject[] records) : HttpMessageHandler
    {
        private readonly List<JsonObject> _records = [.. records];

        /// <summary>What the judgement answers when the record says it may not be deleted.</summary>
        public JsonObject? Refusal { get; init; }

        /// <summary>What the delete answers, where it refuses although the judgement said yes.</summary>
        public JsonObject? DeleteRefusal { get; init; }

        public List<string> Deletes { get; } = [];

        public List<string> Judged { get; } = [];

        public ServiceClient Client() => new("http://ledger.test", null, new HttpClient(this, disposeHandler: false));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path == "/api/sessions")
            {
                return Answer(HttpStatusCode.OK, new JsonArray([.. _records.Select(record => record.DeepClone())]));
            }

            var id = Uri.UnescapeDataString(path["/api/sessions/".Length..].Replace("/deletable", "", StringComparison.Ordinal));
            var record = _records.FirstOrDefault(each => (string?)each["id"] == id);
            if (record is null)
            {
                return Answer(HttpStatusCode.NotFound, new JsonObject { ["deletable"] = false, ["error"] = $"No session `{id}`.", ["refusal"] = "not-found" });
            }

            if (request.Method == HttpMethod.Get)
            {
                Judged.Add(id);
                return (bool?)record["deletable"] == true
                    ? Answer(HttpStatusCode.OK, new JsonObject { ["deletable"] = true })
                    : Answer(HttpStatusCode.OK, Refusal!.DeepClone());
            }

            Deletes.Add($"DELETE {path}");
            if (DeleteRefusal is not null) return Answer(HttpStatusCode.Conflict, DeleteRefusal.DeepClone());
            _records.Remove(record);
            return Answer(HttpStatusCode.OK, new JsonObject { ["id"] = id, ["message"] = $"Deleted session `{id}`." });
        }

        private static Task<HttpResponseMessage> Answer(HttpStatusCode status, JsonNode body) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json"),
            });
    }
}
