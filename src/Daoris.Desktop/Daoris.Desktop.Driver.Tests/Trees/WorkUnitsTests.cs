using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-UNIT1a (the fabric events design §D2): one <see cref="WorkUnit"/> projection over every kind of work. The branch adapter is
/// MILESTONE1a's inventory read as units, and the milestone is read from the unit; the quest and ask adapters read the service's
/// records as units of the same shape; a quest links the branch units its sessions worked, and its ask, by id, never by a copy. Every
/// adapter reads: no store, no file, no write.
/// </summary>
public sealed class WorkUnitsTests : IDisposable
{
    private static readonly DateTimeOffset Then = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);
    private static readonly string Trees = Path.GetFullPath(Path.Combine("unit-trees", "default", "engine"));

    private readonly string _home = Path.Combine(Directory.GetCurrentDirectory(), "_units-" + Guid.NewGuid().ToString("N")[..8]);

    public WorkUnitsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static HeldFact Held() => new(Then, Then, EndBy.Merged, "a1b2c3d4e5f6", "main", "main", "m1", "repo-id");

    private static GrownBranch Grown(string branch = "daoris/s1", string unit = "unit-a", HeldFact? held = null, DateTimeOffset? gone = null) =>
        new("engine", "default", branch, "main", "f0", null, Then) { Unit = unit, Held = held, GoneAt = gone };

    private static LandedBranch Landed(string[] units, string? quest = null, string session = "s1") =>
        new("engine", "default", "feature/x", "main", "t1", session, quest, null, Then) { Units = units };

    private static SessionTree Tree(string name = "s1") => new(Path.Combine(Trees, name), "default", "engine", $"daoris/{name}");

    private static InventoryFacts Facts(
        GrownBranch[]? branches = null, LandedBranch[]? landings = null, SessionTree[]? trees = null, string[]? inUse = null) =>
        new(branches ?? [], landings ?? [], trees ?? [], [], [], (inUse ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase), _ => false, Then.AddDays(1));

    private static QuestView Quest(string id, string status, string to = "engine", string from = "game") =>
        new(id, from, to, $"The work of #{id}", "Body.", status) { Updated = Then };

    private static SessionRecord Session(string id, string? quest, string? tree, string repository = "engine") =>
        new(id, repository, "completed") { Quest = quest, Tree = tree, Created = Then };

    // ---- the branch adapter: the inventory read as units

    /// <summary>The branch adapter is the inventory, not a second derivation: the milestone is read from each unit, field for field.</summary>
    [Fact]
    public void The_branch_adapter_is_the_inventory_read_as_units_and_the_milestone_is_read_from_each()
    {
        var facts = Facts(branches: [Grown(held: Held())], landings: [Landed(["unit-a"])], trees: [Tree()], inUse: [Tree().Path]);

        var unit = Assert.Single(WorkInventory.Units(facts));
        var milestone = Assert.Single(WorkInventory.Derive(facts));

        Assert.Equal(("unit-a", WorkUnitKind.Branch), (unit.Id, unit.Kind));
        Assert.Equal(new WorkOwner("engine", "default", SessionBranches.FileName, WorkRecord.DriverWriter), unit.Owner);
        Assert.Equal(new WorkSubject("daoris/s1", "a1b2c3d4e5f6"), unit.Subject);
        Assert.Equal(["s1"], unit.Executors);
        var read = WorkMilestone.Of(unit);
        Assert.Equal((milestone.Unit, milestone.Repository, milestone.Branch, milestone.Session), (read.Unit, read.Repository, read.Branch, read.Session));
        Assert.Equal(milestone.End, read.End);
        Assert.Equal(milestone.Entries, read.Entries);
        Assert.Equal(milestone.Entries, unit.Entries);
        Assert.Equal(milestone.Result, read.Result);
        Assert.Equal(milestone.Recovery, read.Recovery);
        Assert.Equal(milestone.Cleanup.State, read.Cleanup.State);
        Assert.Equal(milestone.Cleanup.Waits, read.Cleanup.Waits);
        Assert.Equal(milestone.Cleanup.Gone, read.Cleanup.Gone);
    }

    /// <summary>
    /// A unit retires only once its end is reached and every entry has a terminal disposition (§D3): merged and cleaned is retired;
    /// merged with its tree busy is open; a tree no record names has no owner, and is open, never retired by a guess.
    /// </summary>
    [Fact]
    public void A_branch_unit_retires_only_ended_with_every_entry_terminal_and_an_unknown_owner_never_does()
    {
        var cleaned = Assert.Single(WorkInventory.Units(Facts(branches: [Grown(held: Held(), gone: Then.AddHours(2))])));
        var busy = Assert.Single(WorkInventory.Units(Facts(branches: [Grown(held: Held())], trees: [Tree()], inUse: [Tree().Path])));
        var stray = Assert.Single(WorkInventory.Units(Facts(trees: [Tree("stray")])));

        Assert.Equal(WorkUnitState.Retired, cleaned.State);
        Assert.Equal(WorkUnitState.Open, busy.State);
        Assert.Equal(WorkUnitState.Open, stray.State);
        Assert.False(stray.OwnerKnown);
        Assert.Null(stray.Owner.Record);
        Assert.Equal(WorkEndState.Unknown, stray.End.State);
        Assert.True(cleaned.OwnerKnown);
        Assert.Contains(cleaned.Results, entry => entry.Kind == InventoryKind.Line && entry.Resource == "main");
    }

    // ---- the quest adapter

    /// <summary>
    /// A quest is a unit read from its record (§D2: the service's quest store, its writer the service): its end from its status, a held
    /// done not yet an end, a decline dropped, a status this reader does not know unknown; its executors the sessions that served it.
    /// </summary>
    [Fact]
    public void A_quest_is_a_unit_whose_end_is_read_from_its_status_and_a_held_done_is_no_end_yet()
    {
        var units = QuestUnits.Read(
            [
                Quest("q1", "Open"), Quest("q2", "Taken"), Quest("q3", "Done"), Quest("q4", "Done") with { Held = true, Hold = "evidence-missing" },
                Quest("q5", "Declined") with { Note = "Not ours." }, Quest("q6", "Wandering"),
            ],
            [Session("s2", "q3", null), Session("s1", "q3", null), Session("s9", "q2", null)]);

        string End(string id) => units.Single(unit => unit.Id == $"quest:{id}").End.State;
        Assert.Equal(WorkEndState.NotEnded, End("q1"));
        Assert.Equal(WorkEndState.NotEnded, End("q2"));
        Assert.Equal(WorkEndState.Ended, End("q3"));
        Assert.Equal(WorkEndState.NotEnded, End("q4"));
        Assert.Contains("evidence-missing", units.Single(unit => unit.Id == "quest:q4").End.Said);
        Assert.Equal(WorkEndState.Dropped, End("q5"));
        Assert.Contains("Not ours.", units.Single(unit => unit.Id == "quest:q5").End.Said);
        Assert.Equal(WorkEndState.Unknown, End("q6"));

        var done = units.Single(unit => unit.Id == "quest:q3");
        Assert.Equal(WorkUnitKind.Quest, done.Kind);
        Assert.Equal(new WorkOwner("engine", "default", WorkRecord.Quests, WorkRecord.ServiceWriter), done.Owner);
        Assert.Equal(("done", Then), (done.End.By, done.End.At));
        Assert.Equal(["s2", "s1"], done.Executors);
        Assert.Equal(WorkUnitState.Retired, done.State);
        Assert.Equal(WorkUnitState.Open, units.Single(unit => unit.Id == "quest:q4").State);
        Assert.Equal(WorkUnitState.Retired, units.Single(unit => unit.Id == "quest:q5").State);
    }

    /// <summary>A quest's evidence is pointers: its record and each attachment by name, kept; never a path, and nothing to clean.</summary>
    [Fact]
    public void A_quests_evidence_is_pointers_to_its_record_and_attachments_with_no_path()
    {
        var unit = Assert.Single(QuestUnits.Read(
            [Quest("q1", "Done") with { Attachments = [new QuestFileView("shot.png", "abcdef0123456789", 10, Path.Combine(Trees, "shot.png"))] }], []));

        Assert.Equal([InventoryKind.Quest, InventoryKind.Attachment], unit.Evidence.Select(entry => entry.Kind));
        Assert.All(unit.Entries, entry => Assert.Equal((InventoryRole.Evidence, InventoryDisposition.Kept), (entry.Role, entry.Disposition)));
        Assert.Equal(["#q1", "shot.png"], unit.Evidence.Select(entry => entry.Resource));
        Assert.DoesNotContain(unit.Entries, entry => entry.Detail.Contains(Trees, StringComparison.OrdinalIgnoreCase));
        Assert.Empty(unit.Results);
    }

    // ---- the ask adapter

    /// <summary>An ask is a unit read from its record (§D2, the quest's space): done is its end, the person's close drops it.</summary>
    [Fact]
    public void An_ask_is_a_unit_whose_end_is_its_state_and_whose_executor_is_its_intake()
    {
        var units = AskUnits.Read(
        [
            new AskView("a1", "default", "Make it faster.", "Published", "named") { Intake = "i1", Quests = ["q1", "q2"] },
            new AskView("a2", "default", "Done one.", "Done", "named"),
            new AskView("a3", "team", "Closed one.", "Closed", "named") { Note = "Changed my mind." },
        ]);

        var published = units.Single(unit => unit.Id == "ask:a1");
        Assert.Equal(WorkUnitKind.Ask, published.Kind);
        Assert.Equal(new WorkOwner(null, "default", WorkRecord.Asks, WorkRecord.ServiceWriter), published.Owner);
        Assert.Equal(WorkEndState.NotEnded, published.End.State);
        Assert.Equal(["i1"], published.Executors);
        Assert.Equal([new WorkLink(WorkRelation.Produced, "quest:q1"), new WorkLink(WorkRelation.Produced, "quest:q2")], published.Links);
        Assert.Equal(WorkEndState.Ended, units.Single(unit => unit.Id == "ask:a2").End.State);
        var closed = units.Single(unit => unit.Id == "ask:a3");
        Assert.Equal(WorkEndState.Dropped, closed.End.State);
        Assert.Contains("Changed my mind.", closed.End.Said);
        Assert.Equal("team", closed.Owner.Workspace);
    }

    // ---- links: a quest and its ask relate to the branch units by id

    /// <summary>
    /// The quest links the branch unit its session worked (by the session's tree), and the ask it came from; each link has its inverse;
    /// a quest's chain step and its D79 question are links too. Nothing of the branch unit is copied onto the quest.
    /// </summary>
    [Fact]
    public void A_quest_links_the_branch_units_its_sessions_worked_and_its_ask_by_id_never_by_a_copy()
    {
        var branches = WorkInventory.Units(Facts(branches: [Grown(held: Held())], trees: [Tree()]));
        var sessions = new[] { Session("s1", "q1", Tree().Path) };
        var quests = QuestUnits.Read(
            [Quest("q1", "Done", from: "ask #a1"), Quest("q2", "Taken") with { Parent = "q1", Awaits = "q9" }], sessions);
        var asks = AskUnits.Read([new AskView("a1", "default", "Make it faster.", "Published", "named") { Quests = ["q1"] }]);

        var all = WorkUnits.Linked(branches, quests, asks, sessions);

        WorkUnit Of(string id) => all.Single(unit => unit.Id == id);
        Assert.Contains(new WorkLink(WorkRelation.ProducedBy, "quest:q1"), Of("unit-a").Links);
        Assert.Contains(new WorkLink(WorkRelation.Produced, "unit-a"), Of("quest:q1").Links);
        Assert.Contains(new WorkLink(WorkRelation.ProducedBy, "ask:a1"), Of("quest:q1").Links);
        Assert.Equal([new WorkLink(WorkRelation.Produced, "quest:q1")], Of("ask:a1").Links);
        Assert.Contains(new WorkLink(WorkRelation.Follows, "quest:q1"), Of("quest:q2").Links);
        Assert.Contains(new WorkLink(WorkRelation.Awaits, "quest:q9"), Of("quest:q2").Links);
        Assert.Equal(Of("quest:q1").Links.Distinct(), Of("quest:q1").Links);
        Assert.DoesNotContain(Of("quest:q1").Entries, entry => entry.Unit == "unit-a");
        Assert.Equal(branches.Single().Entries, Of("unit-a").Entries);
    }

    /// <summary>
    /// A landing names a quest for the unit it carries alone; where it carries several units, which of them served the quest is not
    /// read from it, so none is linked by it: never a guess.
    /// </summary>
    [Fact]
    public void A_landing_links_its_quest_to_the_one_unit_it_carries_and_to_none_where_it_carries_several()
    {
        var alone = Assert.Single(WorkInventory.Units(Facts(branches: [Grown()], landings: [Landed(["unit-a"], quest: "q1")])));
        var several = WorkInventory.Units(Facts(branches: [Grown(), Grown("daoris/s2", "unit-b")], landings: [Landed(["unit-a", "unit-b"], quest: "q1")]));

        Assert.Equal([new WorkLink(WorkRelation.ProducedBy, "quest:q1")], alone.Links);
        Assert.All(several, unit => Assert.Empty(unit.Links));
    }

    /// <summary>Every kind's milestone is read the same way: a done quest has nothing of its own to clean; an open one waits for its end.</summary>
    [Fact]
    public void A_quests_milestone_is_read_as_a_branch_units_is()
    {
        var units = QuestUnits.Read([Quest("q1", "Done"), Quest("q2", "Open"), Quest("q3", "Declined")], []);

        Assert.Equal("completed", WorkMilestone.Of(units[0]).Cleanup.State);
        var open = WorkMilestone.Of(units[1]);
        Assert.Equal("pending", open.Cleanup.State);
        Assert.Equal(InventoryKind.End, Assert.Single(open.Cleanup.Waits).Kind);
        Assert.Equal("completed", WorkMilestone.Of(units[2]).Cleanup.State);
        Assert.Null(open.Branch);
    }

    // ---- read-only

    /// <summary>
    /// Reading every kind asks the service only by GET and writes nothing in the home; a service that cannot answer is said, never
    /// read as no quests, and the branch units are still read.
    /// </summary>
    [Fact]
    public async Task Reading_every_kind_asks_the_service_only_by_GET_and_writes_nothing()
    {
        new SessionBranches(_home).Record(Grown());
        var before = Directory.EnumerateFiles(_home, "*", SearchOption.AllDirectories).Order().Select(file => (file, File.ReadAllBytes(file))).ToList();
        var ledger = new ReadingLedger();
        var unread = new List<string>();

        var units = await WorkUnits.ReadAsync(_home, new Dictionary<string, string>(), new HashSet<string>(), ledger.Client(), unread);

        Assert.Equal(["unit-a", "quest:q1", "ask:a1"], units.Select(unit => unit.Id));
        Assert.Contains(new WorkLink(WorkRelation.ProducedBy, "quest:q1"), units[0].Links);
        Assert.NotEmpty(ledger.Asked);
        Assert.All(ledger.Asked, asked => Assert.StartsWith("GET ", asked));
        var after = Directory.EnumerateFiles(_home, "*", SearchOption.AllDirectories).Order().Select(file => (file, File.ReadAllBytes(file))).ToList();
        Assert.Equal(before.Select(each => each.file), after.Select(each => each.file));
        Assert.All(before.Zip(after), pair => Assert.Equal(pair.First.Item2, pair.Second.Item2));

        var down = new List<string>();
        var alone = await WorkUnits.ReadAsync(_home, new Dictionary<string, string>(), new HashSet<string>(), new ReadingLedger { Down = true }.Client(), down);
        Assert.Equal(["unit-a"], alone.Select(unit => unit.Id));
        Assert.Contains(down, said => said.Contains("quests and asks were not read", StringComparison.Ordinal));

        var none = new List<string>();
        Assert.Equal(["unit-a"], (await WorkUnits.ReadAsync(_home, new Dictionary<string, string>(), new HashSet<string>(), null, none)).Select(unit => unit.Id));
        Assert.Contains(none, said => said.Contains("without the service", StringComparison.Ordinal));
    }

    /// <summary>The service's three reads, standing in: one quest, one ask, one session in the unit's tree; every request is kept.</summary>
    private sealed class ReadingLedger : HttpMessageHandler
    {
        public List<string> Asked { get; } = [];

        public bool Down { get; init; }

        public ServiceClient Client() => new("http://units.test", null, new HttpClient(this, disposeHandler: false));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            lock (Asked) Asked.Add($"{request.Method} {request.RequestUri!.PathAndQuery}");
            JsonNode? answer = Down ? null : request.RequestUri!.AbsolutePath switch
            {
                "/api/quests" => new JsonArray(new JsonObject
                {
                    ["id"] = "q1", ["from"] = "ask #a1", ["to"] = "engine", ["title"] = "The work", ["body"] = "Body.", ["status"] = "Taken",
                }),
                "/api/asks" => new JsonArray(new JsonObject
                {
                    ["id"] = "a1", ["workspace"] = "default", ["sentence"] = "Make it.", ["state"] = "Published", ["tier"] = "named",
                    ["quests"] = new JsonArray("q1"),
                }),
                "/api/sessions" => new JsonArray(new JsonObject
                {
                    ["id"] = "s1", ["quest"] = "q1", ["repository"] = "engine", ["state"] = "working", ["kind"] = "driven",
                    ["tree"] = Tree().Path, ["created"] = Then.ToString("O"),
                }),
                _ => null,
            };
            return Task.FromResult(answer is null
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("{\"error\":\"down\"}", Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer.ToJsonString(), Encoding.UTF8, "application/json") });
        }
    }
}
