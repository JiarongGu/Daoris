using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;

namespace Daoris.Service.Tests;

/// <summary>
/// KNOWCENTRAL2e (the memory coordinator design §4): a reader's proposal travels as a quest to the entry's owner. Proposals from one
/// repository join its open quest, each its own decision; the owner's session decides through its own operations or declines, and the
/// decline reaches each proposer; a target that moves leaves its proposal stale; closing the quest applies nothing, and no door of the
/// proposer's ends the entry.
/// </summary>
public sealed class McpMemoryProposalTests : McpToolsFixture
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

    /// <summary>The memory tools of a new session working in <paramref name="repository"/>, and its record.</summary>
    private async Task<(MemoryTools Tools, Session Record)> SessionInAsync(string repository)
    {
        await SeedAsync();
        var record = await _sessions.CreateAsync(null, repository, "claude-code", Now, workspace: "work", kind: SessionKind.Driven);
        return (new MemoryTools(_service, new AmbientWorkspace(Room(repository)), new IntakeScope(null, record.Id), _ledger, _memory,
            exchange: _exchange, quests: _quests), record);
    }

    /// <summary>The quest tools of a session working in the owner's repository.</summary>
    private async Task<QuestTools> OwnerQuestsAsync()
    {
        await SeedAsync();
        var record = await _sessions.CreateAsync(null, "Owner", "claude-code", Now, workspace: "work", kind: SessionKind.Driven);
        return new QuestTools(_quests, _exchange, In("Owner"), intake: new IntakeScope(null, record.Id), ledger: _ledger, engines: _memory);
    }

    private async Task<IReadOnlyList<Quest>> AskedOfOwnerAsync() =>
        [.. (await _quests.ListAsync("Owner", includeClosed: true)).Where(quest => quest.From == "Asker")];

    private Task<IReadOnlyList<MemoryOp>> ProposalsAsync() => _ledger.Memory.ProposalsAsync();

    /// <summary>
    /// 🔴 Two sessions of one repository proposing on the owner's entry publish one quest: the second joins the first while nobody has
    /// taken it, and the owner's list names both. Once it is taken, a further proposal is a quest of its own. Nothing of the memory moves.
    /// </summary>
    [Fact]
    public async Task Two_proposals_from_one_repository_travel_as_one_quest()
    {
        var (first, _) = await SessionInAsync("Asker");
        var (second, _) = await SessionInAsync("Asker");
        var before = _memory.State();

        var published = await first.ProposeAsync("Owner:m1", "disputes", "The map is drawn in vectors now, as the renderer's notes say.", 1);
        var joined = await second.ProposeAsync("Owner:m1", "amends", "The legend sits under the map.", 1);
        var quest = Assert.Single(await AskedOfOwnerAsync());

        Assert.Contains($"`#{quest.Id}`", published);
        Assert.Contains("published", published);
        Assert.Contains($"`#{quest.Id}`", joined);
        Assert.Contains("joined", joined);
        Assert.All(await ProposalsAsync(), op => Assert.Equal((MemoryOpState.Awaiting, quest.Id), (op.State, op.Carried)));
        var listed = await (await OwnerQuestsAsync()).ListQuestsAsync("Owner");
        Assert.Contains("disputes `Owner:m1` at revision 1", listed);
        Assert.Contains("amends `Owner:m1` at revision 1", listed);

        await _quests.MoveAsync(quest.Id, QuestStatus.Taken, null, Now);
        var after = await first.ProposeAsync("Owner:m1", "end", "The tiles were retired with the vector map.", 1);

        Assert.Equal(2, (await AskedOfOwnerAsync()).Count);
        Assert.DoesNotContain($"`#{quest.Id}`", after);
        Assert.Equal(before, _memory.State());
    }

    /// <summary>🔴 Proposals made at the same moment from one repository join one quest: the join is made under the store's lock, not in turns.</summary>
    [Fact]
    public async Task Proposals_made_at_the_same_moment_publish_one_quest()
    {
        var (_, record) = await SessionInAsync("Asker");
        // Each look waits a moment for the other to have looked: only a join under one lock keeps it from finding the other not yet there.
        var arrived = 0;
        var both = new TaskCompletionSource();
        Task Meet()
        {
            if (Interlocked.Increment(ref arrived) == 2) both.TrySetResult();
            return Task.WhenAny(both.Task, Task.Delay(TimeSpan.FromSeconds(1)));
        }

        var proposals = new MemoryProposals(_ledger.Memory, _quests, _exchange) { Looked = Meet };
        var by = new MemoryAuthor(record.Id, "work", null, "claude-code", null, null, null, Now);
        var change = new MemoryChange(MemoryOpKind.Propose, Text: "A note on the map.", Target: "m1", Revision: 1, Relation: "amends");

        var made = await Task.WhenAll(
            proposals.ProposeAsync("Asker", new MemoryReference("Owner", "m1"), change, by, Now),
            proposals.ProposeAsync("Asker", new MemoryReference("Owner", "m1"), change, by, Now));

        var quest = Assert.Single(await AskedOfOwnerAsync());
        Assert.Equal([false, true], made.Select(each => each.Joined).Order());
        Assert.All(await ProposalsAsync(), op => Assert.Equal(quest.Id, op.Carried));
    }

    /// <summary>
    /// 🔴 A dispute and a proposed end of one entry are two decisions on the quest: each its own row and its own numbered line in the
    /// owner's list, never folded into one.
    /// </summary>
    [Fact]
    public async Task A_dispute_and_a_proposed_end_are_two_decisions()
    {
        var (asker, _) = await SessionInAsync("Asker");

        await asker.ProposeAsync("Owner:m1", "disputes", "The map is drawn in vectors now.", 1);
        await asker.ProposeAsync("Owner:m1", "end", "The tiles were retired with the vector map.", 1);
        var proposals = await ProposalsAsync();
        var listed = await (await OwnerQuestsAsync()).ListQuestsAsync("Owner");

        Assert.Equal(["disputes", "end"], proposals.Select(op => op.Change.Relation));
        Assert.Single(proposals.Select(op => op.Carried).Distinct());
        Assert.Contains($"1. `{proposals[0].Id}` disputes `Owner:m1`", listed);
        Assert.Contains($"2. `{proposals[1].Id}` end `Owner:m1`", listed);
    }

    /// <summary>
    /// 🔴 The owner's decline reaches the proposer: its row is declined with the owner's reason, and its next memory call is told so,
    /// once. Nothing of the memory moves.
    /// </summary>
    [Fact]
    public async Task A_decline_reaches_the_proposer()
    {
        var (asker, _) = await SessionInAsync("Asker");
        await asker.ProposeAsync("Owner:m1", "end", "The tiles were retired with the vector map.", 1);
        var quest = Assert.Single(await AskedOfOwnerAsync());
        var before = _memory.State();

        var declined = await (await OwnerQuestsAsync()).RespondToQuestAsync(
            quest.Id, "decline", "The tiles stay: the vector map is a draft the renderer does not draw yet.");
        var told = await asker.SearchAsync("map");
        var again = await asker.SearchAsync("map");

        Assert.Contains("declined", declined, StringComparison.OrdinalIgnoreCase);
        var op = Assert.Single(await ProposalsAsync());
        Assert.Equal(MemoryOpState.Declined, op.State);
        Assert.Contains("The tiles stay", op.Answer);
        Assert.Contains($"`{op.Id}`", told);
        Assert.Contains("declined", told);
        Assert.Contains("The tiles stay", told);
        Assert.DoesNotContain("The tiles stay", again);
        Assert.Equal(before, _memory.State());
    }

    /// <summary>
    /// 🔴 A proposal whose target moves past the revision it read before the owner decides is stale, said so on the owner's list and
    /// told to the proposer; a proposal made at a revision already moved is refused at the call.
    /// </summary>
    [Fact]
    public async Task A_moved_target_leaves_its_proposal_stale()
    {
        var (asker, _) = await SessionInAsync("Asker");
        await asker.ProposeAsync("Owner:m1", "disputes", "The map is drawn in vectors now.", 1);

        await _memory.Open("Owner", MemoryReinforcement.None)!.ConfirmAsync("m1", 1);
        var listed = await (await OwnerQuestsAsync()).ListQuestsAsync("Owner");
        var told = await asker.GetAsync("Owner:m1");
        var late = await asker.ProposeAsync("Owner:m1", "end", "The tiles were retired with the vector map.", 1);

        var op = Assert.Single(await ProposalsAsync());
        Assert.Equal(MemoryOpState.Stale, op.State);
        Assert.Contains("stale", listed);
        Assert.Contains($"`{op.Id}`", told);
        Assert.Contains("stale", told);
        Assert.StartsWith("Refused", late);
        Assert.Contains("revision 2", late);
    }

    /// <summary>
    /// 🔴 The owner closing the quest done applies nothing: the service's applier runs and the memory stays as it was; each proposal
    /// is answered, never applied, and the proposer is told the owner's handback.
    /// </summary>
    [Fact]
    public async Task Closing_the_quest_applies_nothing()
    {
        var (asker, _) = await SessionInAsync("Asker");
        await asker.ProposeAsync("Owner:m1", "end", "The tiles were retired with the vector map.", 1);
        await asker.ProposeAsync("Owner:m1", "disputes", "The map is drawn in vectors now.", 1);
        var quest = Assert.Single(await AskedOfOwnerAsync());
        var owner = await OwnerQuestsAsync();
        var before = _memory.State();

        await owner.RespondToQuestAsync(quest.Id, "take");
        var done = await owner.RespondToQuestAsync(
            quest.Id, "done", "Wrote a disputing entry about the vector map and proposed the tiles' end, which waits for the person's approval.");
        await owner.ListQuestsAsync("Owner", includeClosed: true);
        await new MemoryApplier(_ledger.Memory, _memory).StartAsync();
        var told = await asker.SearchAsync("map");

        Assert.DoesNotContain("Refused", done);
        Assert.All(await ProposalsAsync(), op => Assert.Equal(MemoryOpState.Answered, op.State));
        Assert.Contains("applying nothing", told);
        Assert.Contains("Wrote a disputing entry", told);
        Assert.Equal(before, _memory.State());
    }

    /// <summary>
    /// 🔴 No door of the proposer's ends the entry: its own end of another repository's entry is refused, the decision a row's approver
    /// makes is refused on a proposal, the person's included, and the applier leaves the entry current.
    /// </summary>
    [Fact]
    public async Task No_proposers_door_ends_the_entry()
    {
        var (asker, record) = await SessionInAsync("Asker");
        await asker.ProposeAsync("Owner:m1", "end", "The tiles were retired with the vector map.", 1);
        var op = Assert.Single(await ProposalsAsync());
        var before = _memory.State();
        var desk = new MemoryDesk(_ledger.Memory);

        var ended = await asker.EndAsync("Owner:m1", 1, "The tiles were retired with the vector map.");
        var bySession = await desk.DecideAsync(op.Id, record.Id, approve: true, Now);
        var byPerson = await desk.DecideAsync(op.Id, session: null, approve: true, Now);
        await new MemoryApplier(_ledger.Memory, _memory).StartAsync();

        Assert.StartsWith("Refused", ended);
        Assert.NotEqual(MemoryDecisionRefusal.None, bySession.Refusal);
        Assert.Equal(MemoryDecisionRefusal.NotFound, byPerson.Refusal);
        Assert.Equal(MemoryOpState.Awaiting, Assert.Single(await ProposalsAsync()).State);
        Assert.Null((await _memory.Open("Owner", MemoryReinforcement.None)!.GetAsync("m1"))!.Ended);
        Assert.Equal(before, _memory.State());
    }
}
