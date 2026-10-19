using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// Ask Daoris's session (HELP1a, D89): a conversation about Daoris itself, opened in a room under the
/// driver's home the way an intake's is — a chat serving no quest and no ask, in no repository, one
/// running per room. The harness carries the model; nothing here names one.
/// </summary>
public sealed class HelpSessionTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-help-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private SessionStore _sessions = null!;
    private SessionLedger _ledger = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-29T10:00:00Z");

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        var quests = await QuestStore.OpenAsync(_connection);
        _sessions = await SessionStore.OpenAsync(_connection);
        _ledger = new SessionLedger(quests, _sessions);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string Room() => Path.Combine(_root, "home", "help");

    [Fact]
    public async Task A_help_session_is_a_conversation_in_its_room_serving_nothing()
    {
        var opened = await _ledger.OpenHelpAsync("stub", Room(), Now, harnessVersion: "1.2.3", profile: "work");

        Assert.Equal(SessionOpenRefusal.None, opened.Refusal);
        var session = opened.Session!;
        // A conversation (SES2), the kind every build already reads as one nothing plans from.
        Assert.Equal(SessionKind.Chat, session.Kind);
        Assert.Null(session.Quest);
        Assert.Null(session.Ask);
        // Its own "repository", which no folder can be called: a colon is in no folder name.
        Assert.Equal("daoris:help", session.Repository);
        Assert.Equal(SessionLedger.HelpRepository, session.Repository);
        Assert.Equal("default", session.Workspace);
        Assert.Equal(Trees.Normalize(Room()), session.Tree);
        // The room is no repository, and git asked about it would walk up.
        Assert.Null(session.BaseCommit);
        Assert.Equal("1.2.3", session.HarnessVersion);
        Assert.Equal("work", session.Profile);
        Assert.Contains(session.Id, opened.Message);
    }

    /// <summary>One conversation per machine: a second open while one runs is refused, naming it.</summary>
    [Fact]
    public async Task One_help_session_runs_at_a_time_and_an_ended_one_leaves_the_room()
    {
        var holding = (await _ledger.OpenHelpAsync("stub", Room(), Now)).Session!;

        var busy = await _ledger.OpenHelpAsync("stub", Room(), Now);

        Assert.Equal(SessionOpenRefusal.RepositoryBusy, busy.Refusal);
        Assert.Contains(holding.Id, busy.Message);
        Assert.Null(busy.Session);

        await _sessions.SetStateAsync(holding.Id, SessionState.Completed, null, null, null, Now);
        Assert.Equal(SessionOpenRefusal.None, (await _ledger.OpenHelpAsync("stub", Room(), Now)).Refusal);
    }

    [Fact]
    public async Task A_help_session_needs_its_room()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _ledger.OpenHelpAsync("stub", " ", Now));
    }

    // ——— ASKSCOPE1a (D158's ASKSCOPE1 note, points 1-3): a conversation keeps the workspace it started in, one runs per workspace.

    /// <summary>
    /// 🔴 One running per workspace, not per machine: two workspaces' conversations open at once and both run, in the one room
    /// folder they share until ASKSCOPE1b; a second in a workspace is refused naming that workspace's, never the other's.
    /// </summary>
    [Fact]
    public async Task Two_workspaces_conversations_run_at_once_and_a_second_in_one_is_refused_naming_its_own()
    {
        var alpha = await _ledger.OpenHelpAsync("stub", Room(), Now, workspace: "alpha");
        var beta = await _ledger.OpenHelpAsync("stub", Room(), Now, workspace: "beta");

        Assert.Equal((SessionOpenRefusal.None, SessionOpenRefusal.None), (alpha.Refusal, beta.Refusal));
        Assert.Equal(("alpha", "beta"), (alpha.Session!.Workspace, beta.Session!.Workspace));
        Assert.Equal(alpha.Session.Tree, beta.Session.Tree);

        var again = await _ledger.OpenHelpAsync("stub", Room(), Now, workspace: "ALPHA");

        Assert.Equal(SessionOpenRefusal.RepositoryBusy, again.Refusal);
        Assert.Contains(alpha.Session.Id, again.Message);
        Assert.Contains("`alpha`", again.Message);
        Assert.DoesNotContain(beta.Session.Id, again.Message);
        Assert.Null(again.Session);
    }

    /// <summary>
    /// A record from before has no workspace of its own and is read as <c>default</c>, as it was recorded (point 1): it is the
    /// default workspace's running conversation, so a start with none named is refused naming it and one in a named workspace
    /// runs beside it.
    /// </summary>
    [Fact]
    public async Task A_record_from_before_reads_as_default_and_holds_only_the_default_workspace()
    {
        // As OpenHelpAsync recorded one before ASKSCOPE1a: no workspace named.
        var before = await _sessions.CreateAsync(null, SessionLedger.HelpRepository, "stub", Now, kind: SessionKind.Chat, tree: Room());

        Assert.Equal("default", (await _sessions.FindAsync(before.Id))!.Workspace);
        var unnamed = await _ledger.OpenHelpAsync("stub", Room(), Now);
        Assert.Equal(SessionOpenRefusal.RepositoryBusy, unnamed.Refusal);
        Assert.Contains(before.Id, unnamed.Message);
        Assert.Equal(SessionOpenRefusal.RepositoryBusy, (await _ledger.OpenHelpAsync("stub", Room(), Now, workspace: "default")).Refusal);
        Assert.Equal(SessionOpenRefusal.None, (await _ledger.OpenHelpAsync("stub", Room(), Now, workspace: "alpha")).Refusal);
    }

    /// <summary>
    /// Going on with an ended conversation holds its own workspace's slot (point 3): another workspace's running one does not
    /// stop it, and its own workspace's does, named.
    /// </summary>
    [Fact]
    public async Task An_ended_conversation_goes_on_beside_another_workspace_s_and_waits_for_its_own()
    {
        var ended = (await _ledger.OpenHelpAsync("stub", Room(), Now, workspace: "alpha")).Session!;
        await _sessions.SetStateAsync(ended.Id, SessionState.Completed, null, null, null, Now.AddMinutes(1));
        await _ledger.SayAsync(ended.Id, "One more thing.", null, Now.AddMinutes(2));
        var beta = (await _ledger.OpenHelpAsync("stub", Room(), Now.AddMinutes(3), workspace: "beta")).Session!;
        var alpha = (await _ledger.OpenHelpAsync("stub", Room(), Now.AddMinutes(3), workspace: "alpha")).Session!;

        var busy = await _ledger.AdvanceAsync(ended.Id, "working", null, null, null, Now.AddMinutes(4));

        Assert.Equal(SessionAdvanceRefusal.Busy, busy.Refusal);
        Assert.Contains(alpha.Id, busy.Message);
        Assert.DoesNotContain(beta.Id, busy.Message);

        await _sessions.SetStateAsync(alpha.Id, SessionState.Completed, null, null, null, Now.AddMinutes(5));
        var went = await _ledger.AdvanceAsync(ended.Id, "working", null, null, null, Now.AddMinutes(6));

        Assert.Equal(SessionAdvanceRefusal.None, went.Refusal);
        Assert.Equal("alpha", went.Session!.Workspace);
    }

    /// <summary>
    /// A workspace this machine does not hold is refused, saying which it holds: <c>default</c> always, and each registered
    /// repository's. A ledger with no registry cannot judge it, so it takes the name as told.
    /// </summary>
    [Fact]
    public async Task A_workspace_this_machine_does_not_hold_is_refused_naming_the_ones_it_does()
    {
        var ledger = await RegisteredAsync(("engine", "alpha"), ("site", "beta"));

        var unknown = await ledger.OpenHelpAsync("stub", Room(), Now, workspace: "gamma");

        Assert.Equal(SessionOpenRefusal.WorkspaceUnknown, unknown.Refusal);
        Assert.Null(unknown.Session);
        Assert.Contains("`gamma`", unknown.Message);
        Assert.Contains("alpha, beta, default", unknown.Message);
        Assert.Equal(SessionOpenRefusal.None, (await ledger.OpenHelpAsync("stub", Room(), Now, workspace: "Beta")).Refusal);
        Assert.Equal(SessionOpenRefusal.None, (await ledger.OpenHelpAsync("stub", Room(), Now)).Refusal);
    }

    // ——— HELPMODE1a2 (D183 point 3): the record keeps the mode its start got, the effective one, apart from the mode chosen.

    /// <summary>The open keeps the mode the driver says its start got, spelled as the ledger spells it; none told is restricted.</summary>
    [Fact]
    public async Task A_help_record_keeps_the_mode_its_start_got()
    {
        var full = (await _ledger.OpenHelpAsync("stub", Room(), Now, workspace: "alpha", mode: "FULL")).Session!;
        var unsaid = (await _ledger.OpenHelpAsync("stub", Room(), Now, workspace: "beta")).Session!;

        Assert.Equal("full", full.HelpMode);
        Assert.Equal("full", (await _sessions.FindAsync(full.Id))!.HelpMode);
        Assert.Equal("restricted", unsaid.HelpMode);
        Assert.Equal("restricted", (await _sessions.FindAsync(unsaid.Id))!.HelpMode);
    }

    /// <summary>
    /// 🔴 A record from before the mode has none kept and reads as restricted, which is what every start before HELPMODE1a1 ran
    /// under; the stored row is not rewritten. A record that is not Ask Daoris's has no mode at all.
    /// </summary>
    [Fact]
    public async Task A_help_record_from_before_the_mode_reads_restricted_and_no_other_record_has_one()
    {
        var before = await _sessions.CreateAsync(null, SessionLedger.HelpRepository, "stub", Now, kind: SessionKind.Chat, tree: Room());
        var chat = await _sessions.CreateAsync(null, "Engine", "stub", Now, kind: SessionKind.Chat);

        Assert.Equal("restricted", (await _sessions.FindAsync(before.Id))!.HelpMode);
        Assert.Null((await _sessions.FindAsync(chat.Id))!.HelpMode);
    }

    /// <summary>A mode the ledger does not know is no mode: the open is refused, and nothing is recorded.</summary>
    [Fact]
    public async Task A_mode_the_ledger_does_not_know_is_refused()
    {
        var refused = await Assert.ThrowsAsync<ArgumentException>(() => _ledger.OpenHelpAsync("stub", Room(), Now, mode: "sometimes"));

        Assert.Contains("`sometimes`", refused.Message);
        Assert.Empty(await _sessions.ListAsync(includeClosed: true));
    }

    /// <summary>
    /// A conversation going on after the mode changed records the mode it goes on under (D183 point 3), on Ask Daoris's records of
    /// this machine's alone; any other record, or an unknown mode, is refused and nothing moves.
    /// </summary>
    [Fact]
    public async Task A_conversation_going_on_records_the_mode_it_goes_on_under()
    {
        var help = (await _ledger.OpenHelpAsync("stub", Room(), Now)).Session!;
        var chat = await _sessions.CreateAsync(null, "Engine", "stub", Now, kind: SessionKind.Chat);

        var kept = await _ledger.KeepHelpModeAsync(help.Id, "full");

        Assert.Equal("full", kept!.HelpMode);
        Assert.Equal("full", (await _sessions.FindAsync(help.Id))!.HelpMode);
        // Its state and its last move are untouched: the mode is a fact about its run, not a move.
        Assert.Equal((help.State, help.Updated), (kept.State, kept.Updated));
        Assert.Null(await _ledger.KeepHelpModeAsync(chat.Id, "full"));
        Assert.Null((await _sessions.FindAsync(chat.Id))!.HelpMode);
        Assert.Null(await _ledger.KeepHelpModeAsync("nothere", "full"));
        await Assert.ThrowsAsync<ArgumentException>(() => _ledger.KeepHelpModeAsync(help.Id, "sometimes"));
        Assert.Equal("full", (await _sessions.FindAsync(help.Id))!.HelpMode);
    }

    /// <summary>A ledger over this test's sessions whose registry holds each repository in its workspace.</summary>
    private async Task<SessionLedger> RegisteredAsync(params (string Repository, string Workspace)[] repositories)
    {
        foreach (var (repository, _) in repositories)
        {
            var dir = Path.Combine(_root, "family", repository);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "daoris.json"), """
                { "source": "s", "packs": [], "domain": { "summary": "A repository.", "owns": ["it"], "accepts": [] } }
                """);
        }

        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(), DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.ImportAsync(Path.Combine(_root, "family"), Now);
        foreach (var row in await service.RegistryAsync())
        {
            var workspace = repositories.Single(each => each.Repository == row.Repository).Workspace;
            await service.RegisterAsync(row with { Workspace = workspace }, Now);
        }

        return new SessionLedger(await QuestStore.OpenAsync(_connection), _sessions, service);
    }
}
