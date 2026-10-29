using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;

namespace Daoris.Service.Tests;

/// <summary>
/// HANDBACK1 (D219): a session's handback is the note it closes its quest with through the connector, and anyone entitled to the work
/// reads it whole: the steward, another session of the workspace, a terminal. `quest_respond` refuses a note that only points elsewhere;
/// `handback_read` answers one quest's handback, by the quest's id or the session's, in any state, from the session's own workspace.
/// </summary>
public sealed class McpHandbackToolTests : McpToolsFixture
{
    private SessionStore _sessions = null!;

    private async Task<SessionLedger> LedgerAsync()
    {
        _sessions ??= await SessionStore.OpenAsync(_connection);
        return new SessionLedger(_quests, _sessions, _service, await AskStore.OpenAsync(_connection));
    }

    /// <summary>The Owner's connector for a session the driver started.</summary>
    private QuestTools Owner(string session = "s0a1b2c3") =>
        new(_quests, new QuestExchange(_service, _quests, files: _files), In("Owner"), intake: new IntakeScope(null, session));

    /// <summary>The read as a steward's session of the workspace holds it.</summary>
    private async Task<HandbackTools> StewardAsync(WorkspaceScope? circle = null) => new(_quests, circle ?? In("Asker"), await LedgerAsync());

    private Task<Quest> PublishAsync(string title = "Draw the minimap") =>
        _quests.PublishAsync("Asker", "Owner", title, "The map needs one.", DateTimeOffset.UtcNow);

    /// <summary>
    /// 🔴 The connector's `quest_respond` refuses a done whose note only points at the session's last message, in the refusal's words,
    /// and the quest stays taken; the same session's real handback then closes it.
    /// </summary>
    [Fact]
    public async Task Quest_respond_refuses_a_note_that_only_points_elsewhere_and_takes_the_handback_itself()
    {
        var quest = await PublishAsync();
        var owner = Owner();
        await owner.RespondToQuestAsync(quest.Id, "take");

        var refused = await owner.RespondToQuestAsync(quest.Id, "done", "Details are in my last message");

        Assert.Equal(HandbackNotes.Refusal("Details are in my last message"), refused);
        Assert.Equal(QuestStatus.Taken, (await _quests.FindAsync(quest.Id))!.Status);

        Assert.Contains("is now Done", await owner.RespondToQuestAsync(quest.Id, "done", "Drew the minimap in 4f3e2d1; its render test passes."));
    }

    /// <summary>
    /// The steward's closed-quest read, as this tree answers it: `quest_list` is scoped by workspace alone and reaches a closed quest's
    /// note whole with `includeClosed`, so the gap Lyntai's steward met is not its reach. What it lacks is a read of one quest: its
    /// default leaves every closed quest out, and with them in it answers every closed quest of the workspace, each note whole, with no
    /// way to ask for one. `handback_read` answers the one.
    /// </summary>
    [Fact]
    public async Task The_list_reaches_a_closed_note_only_among_every_closed_quest_and_the_read_answers_the_one()
    {
        var owner = Owner();
        var closed = new List<Quest>();
        for (var at = 0; at < 30; at++)
        {
            var quest = await PublishAsync($"Draw map number {at}");
            await owner.RespondToQuestAsync(quest.Id, "take");
            await owner.RespondToQuestAsync(quest.Id, "done", $"Drew map {at} in 4f3e2d{at:00}; its render test passes. " + new string('x', 400));
            closed.Add(quest);
        }

        var steward = new QuestTools(_quests, new QuestExchange(_service, _quests, files: _files), In("Asker"), intake: new IntakeScope(null, "s9steward"));
        var outstanding = await steward.ListQuestsAsync();
        var everything = await steward.ListQuestsAsync(includeClosed: true);
        var one = await (await StewardAsync()).ReadAsync(closed[7].Id);

        Assert.DoesNotContain($"#{closed[7].Id}", outstanding);
        Assert.Contains("Drew map 7 in 4f3e2d07", everything);
        Assert.All(closed, quest => Assert.Contains($"`#{quest.Id}`", everything));
        Assert.Contains("Drew map 7 in 4f3e2d07", one);
        Assert.DoesNotContain("Drew map 8 in", one);
        Assert.True(one.Length * 10 < everything.Length, $"one quest's read ({one.Length}) against the list's ({everything.Length})");
    }

    /// <summary>
    /// 🔴 By the quest's id: the handback whole, the act it was written at, the session that wrote it, how it answered each of the
    /// person's requirements, and that it is that session's claim.
    /// </summary>
    [Fact]
    public async Task A_closed_quests_handback_is_read_whole_by_its_id_naming_its_session()
    {
        var quest = await PublishAsync();
        var owner = Owner();
        await owner.RespondToQuestAsync(quest.Id, "take");
        var note = "Drew the minimap in 4f3e2d1.\n\nProved: its render test passes. Not proved: the zoom at 4K, which needs a display.";
        await owner.RespondToQuestAsync(quest.Id, "done", note);

        var read = await (await StewardAsync()).ReadAsync(quest.Id);

        Assert.Contains($"`#{quest.Id}`", read);
        Assert.Contains("Done", read);
        Assert.Contains("session `s0a1b2c3`", read);
        Assert.Contains("closed it done", read);
        Assert.Contains(note, read);
        Assert.Contains("claim", read);
    }

    /// <summary>🔴 By the session's id: the quest it served, and its handback; a ready is read as the submission it is, the quest still taken.</summary>
    [Fact]
    public async Task A_sessions_handback_is_read_by_its_id_and_a_ready_says_the_quest_stays_taken()
    {
        var quest = await PublishAsync();
        await LedgerAsync();
        var session = await _sessions.CreateAsync(quest.Id, "Owner", "claude-code", DateTimeOffset.UtcNow);
        var exchange = new QuestExchange(_service, _quests, files: _files);
        var owner = new QuestTools(_quests, exchange, In("Owner"), intake: new IntakeScope(null, session.Id));
        await owner.RespondToQuestAsync(quest.Id, "take");
        Assert.Equal(QuestRespondRefusal.None, (await exchange.BindAsync(quest.Id, "sha256:0f1e2d3c4b5a6978", DateTimeOffset.UtcNow)).Refusal);
        await owner.RespondToQuestAsync(quest.Id, "ready", "Drew the minimap in 4f3e2d1; its render test passes.");

        var read = await (await StewardAsync()).ReadAsync(session.Id);

        Assert.Contains($"Session `{session.Id}` served quest `#{quest.Id}`", read);
        Assert.Contains("said it ready", read);
        Assert.Contains("Drew the minimap in 4f3e2d1; its render test passes.", read);
        Assert.Contains("stays taken until its graph closes it", read);
    }

    /// <summary>
    /// A quest closed before Daoris kept who wrote a handback, or through a door that names none, still answers its closing note whole,
    /// said as the quest's note with no session recorded as its writer, never as a session's handback.
    /// </summary>
    [Fact]
    public async Task A_close_that_names_no_session_answers_its_note_as_the_quests_own()
    {
        var quest = await PublishAsync();
        var exchange = new QuestExchange(_service, _quests, files: _files);
        await exchange.RespondAsync(quest.Id, "take", null, DateTimeOffset.UtcNow);
        await exchange.RespondAsync(quest.Id, "decline", "The map belongs to the engine; ask it there.", DateTimeOffset.UtcNow);

        var read = await (await StewardAsync()).ReadAsync(quest.Id);

        Assert.Contains("No session's handback is recorded on it", read);
        Assert.Contains("The map belongs to the engine; ask it there.", read);
        Assert.DoesNotContain("session `", read);
    }

    /// <summary>An open quest has no handback yet, and the read says what one will be; a conversation that served no quest has none.</summary>
    [Fact]
    public async Task An_open_quest_and_a_session_that_served_none_have_no_handback()
    {
        var quest = await PublishAsync();
        await LedgerAsync();
        var chat = await _sessions.CreateAsync(null, "Owner", "claude-code", DateTimeOffset.UtcNow, kind: SessionKind.Chat);
        var steward = await StewardAsync();

        Assert.Contains("No handback yet", await steward.ReadAsync(quest.Id));
        Assert.Contains($"Session `{chat.Id}` served no quest, so it has no handback", await steward.ReadAsync(chat.Id));
        Assert.Contains("No quest or session `nothing00` here", await steward.ReadAsync("nothing00"));
    }

    /// <summary>The read answers the session's own workspace alone, as `quest_list` does: another workspace's quest is refused, naming both.</summary>
    [Fact]
    public async Task Another_workspaces_quest_is_refused()
    {
        var quest = await PublishAsync();

        var read = await (await StewardAsync(Circle(Path.Combine(_root, "family", "Asker"), "elsewhere"))).ReadAsync(quest.Id);

        Assert.Contains($"workspace `{quest.Workspace}`'s", read);
        Assert.Contains("workspace `elsewhere`", read);
        Assert.DoesNotContain("The map needs one.", read);
    }
}
