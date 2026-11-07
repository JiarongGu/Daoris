using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;

namespace Daoris.Service.Tests;

/// <summary>
/// A proposal from an intake, Ask Daoris or a person's own agent travels as a
/// quest to the entry's owner, as a session's does, and the owner's quest says who proposed it. Each proposes as the sender its record or
/// its place gives it, in its own workspace; a caller with no repository at all is refused; and none of them writes the owner's memory.
/// </summary>
public sealed class McpMemoryProposerTests : McpToolsFixture
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);

    private readonly StandInMemory _memory = new();
    private SessionStore _sessions = null!;
    private SessionLedger _ledger = null!;
    private QuestExchange _exchange = null!;

    private async Task SeedAsync()
    {
        if (_ledger is not null) return;
        foreach (var row in await _service.RegistryAsync())
        {
            await _service.RegisterAsync(row with { Workspace = row.Repository == "Checker" ? "elsewhere" : "work" }, Now);
        }

        _sessions = await SessionStore.OpenAsync(_connection);
        _ledger = new SessionLedger(_quests, _sessions, _service, await AskStore.OpenAsync(_connection));
        _exchange = new QuestExchange(_service, _quests, files: _files, sessions: _sessions);
        foreach (var name in new[] { "Asker", "Owner", "Checker" }) _memory.Engine(name).Keep($"{name} draws the map in tiles.");
    }

    private string Room(string repository) => Path.Combine(_root, "family", repository);

    private MemoryTools Tools(string room, IntakeScope? intake = null, string? circle = null) =>
        new(_service, new AmbientWorkspace(room, circle), intake, _ledger, _memory, exchange: _exchange, quests: _quests);

    private async Task<IReadOnlyList<Quest>> OfOwnerAsync() => await _quests.ListAsync("Owner", includeClosed: true);

    private async Task<QuestTools> OwnerQuestsAsync()
    {
        var record = await _sessions.CreateAsync(null, "Owner", "claude-code", Now, workspace: "work", kind: SessionKind.Driven);
        return new QuestTools(_quests, _exchange, In("Owner"), intake: new IntakeScope(null, record.Id), ledger: _ledger, engines: _memory);
    }

    /// <summary>
    /// 🔴 An intake proposes as its ask, the sender every quest it publishes has, in the ask's workspace: one quest to the owner, saying
    /// the ask and the intake session that proposed, and a row naming that session.
    /// </summary>
    [Fact]
    public async Task An_intake_proposes_as_its_ask()
    {
        await SeedAsync();
        var record = await _sessions.CreateAsync(
            null, AskDesk.SenderOf("a1b2c3"), "claude-code", Now, workspace: "work", kind: SessionKind.Chat, ask: "a1b2c3");
        var intake = Tools(Path.Combine(_root, "intake-room"), new IntakeScope("a1b2c3", record.Id));
        var before = _memory.State();

        var said = await intake.ProposeAsync("Owner:m1", "disputes", "The map is drawn in vectors now, as the renderer's notes say.", 1);

        var quest = Assert.Single(await OfOwnerAsync());
        Assert.DoesNotContain("Refused", said);
        Assert.Contains($"`#{quest.Id}`", said);
        Assert.Equal("ask #a1b2c3", quest.From);
        Assert.Contains("ask #a1b2c3", quest.Body);
        Assert.Contains($"`{record.Id}`", quest.Body);
        var op = Assert.Single(await _ledger.Memory.ProposalsAsync());
        Assert.Equal((MemoryOpState.Awaiting, quest.Id, record.Id), (op.State, op.Carried, op.By?.Session));
        Assert.Contains("ask #a1b2c3", await (await OwnerQuestsAsync()).ListQuestsAsync("Owner"));
        Assert.Equal(before, _memory.State());
    }

    /// <summary>
    /// 🔴 Ask Daoris proposes as Daoris's own sender, <c>daoris:help</c>, the place its record is kept, in the workspace it is started
    /// in: a second proposal from it joins the first while nobody has taken it. The owner's quest names Ask Daoris.
    /// </summary>
    [Fact]
    public async Task Ask_Daoris_proposes_as_its_own_sender()
    {
        await SeedAsync();
        var record = await _sessions.CreateAsync(
            null, SessionLedger.HelpRepository, "claude-code", Now, workspace: "work", kind: SessionKind.Chat, helpMode: SessionLedger.HelpRestricted);
        var help = Tools(Path.Combine(_root, "help-room"), new IntakeScope(null, record.Id));

        var first = await help.ProposeAsync("Owner:m1", "end", "The tiles were retired with the vector map.", 1);
        var second = await help.ProposeAsync("Owner:m1", "amends", "The legend sits under the map.", 1);

        var quest = Assert.Single(await OfOwnerAsync());
        Assert.DoesNotContain("Refused", first);
        Assert.Contains("joined", second);
        Assert.Equal(SessionLedger.HelpRepository, quest.From);
        Assert.Contains("Ask Daoris", quest.Body);
        Assert.Contains($"`{record.Id}`", quest.Body);
        Assert.All(await _ledger.Memory.ProposalsAsync(), op => Assert.Equal((quest.Id, record.Id), (op.Carried, op.By?.Session)));
        Assert.Contains("Ask Daoris", await (await OwnerQuestsAsync()).ListQuestsAsync("Owner"));
    }

    /// <summary>
    /// 🔴 A person's own agent, no session Daoris started, proposes as the repository it runs in: one quest to the owner, saying it is a
    /// person's own agent in that repository, and a row with no session to name. It is told it will not hear how it settles.
    /// </summary>
    [Fact]
    public async Task A_persons_own_agent_proposes_as_the_repository_it_runs_in()
    {
        await SeedAsync();
        var agent = Tools(Room("Asker"));

        var said = await agent.ProposeAsync("Owner:m1", "amends", "The legend sits under the map.", 1);

        var quest = Assert.Single(await OfOwnerAsync());
        Assert.DoesNotContain("Refused", said);
        Assert.Contains($"`#{quest.Id}`", said);
        Assert.Contains("quest_list", said);
        Assert.Equal("Asker", quest.From);
        Assert.Contains("person's own agent", quest.Body);
        var op = Assert.Single(await _ledger.Memory.ProposalsAsync());
        Assert.Equal((MemoryOpState.Awaiting, quest.Id, ""), (op.State, op.Carried, op.By?.Session));
        Assert.Contains("person's own agent", await (await OwnerQuestsAsync()).ListQuestsAsync("Owner"));
    }

    /// <summary>
    /// 🔴 A caller with no repository at all is refused in a sentence, and nothing is published or kept: a person's own agent in a room
    /// no registered repository holds, and a session whose record names a repository no registry holds.
    /// </summary>
    [Fact]
    public async Task A_caller_with_no_repository_is_refused()
    {
        await SeedAsync();
        var stray = await _sessions.CreateAsync(null, "Nowhere", "claude-code", Now, workspace: "work", kind: SessionKind.Driven);

        var unplaced = await Tools(Path.Combine(_root, "elsewhere-room"), circle: "work")
            .ProposeAsync("Owner:m1", "end", "The tiles were retired with the vector map.", 1);
        var unregistered = await Tools(Path.Combine(_root, "elsewhere-room"), new IntakeScope(null, stray.Id))
            .ProposeAsync("Owner:m1", "end", "The tiles were retired with the vector map.", 1);

        foreach (var refused in new[] { unplaced, unregistered })
        {
            Assert.StartsWith("Refused", refused);
            Assert.Contains("no repository", refused);
            Assert.Contains("Nothing was published", refused);
        }

        Assert.Empty(await OfOwnerAsync());
        Assert.Empty(await _ledger.Memory.ProposalsAsync());
    }

    /// <summary>
    /// 🔴 Never wider than a request: none of the three changes the memory, and none of their other doors is opened by it — an
    /// intake's and a person's own agent's <c>memory_write</c> and <c>memory_end</c> stay refused.
    /// </summary>
    [Fact]
    public async Task A_proposal_widens_no_door()
    {
        await SeedAsync();
        var record = await _sessions.CreateAsync(
            null, AskDesk.SenderOf("a1b2c3"), "claude-code", Now, workspace: "work", kind: SessionKind.Chat, ask: "a1b2c3");
        var intake = Tools(Path.Combine(_root, "intake-room"), new IntakeScope("a1b2c3", record.Id));
        var agent = Tools(Room("Asker"));
        var before = _memory.State();

        var written = new[]
        {
            await intake.WriteAsync("Owner draws the map in vectors."), await agent.WriteAsync("Owner draws the map in vectors."),
            await intake.EndAsync("Owner:m1", 1, "The tiles were retired."), await agent.EndAsync("Owner:m1", 1, "The tiles were retired."),
        };

        Assert.All(written, said => Assert.StartsWith("Refused", said));
        Assert.Empty(await _ledger.Memory.ListAsync(MemoryOpState.Staged));
        Assert.Equal(before, _memory.State());
    }
}
