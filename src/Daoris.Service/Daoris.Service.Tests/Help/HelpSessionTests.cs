using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// Ask Daoris's session: a conversation about Daoris itself, opened in a room under the
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
        var opened = await _ledger.OpenHelpAsync("stub", Room(), Now, doorVersion: "1.2.3", profile: "work");

        Assert.Equal(SessionOpenRefusal.None, opened.Refusal);
        var session = opened.Session!;
        // A conversation, the kind every build reads as one nothing plans from.
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
        Assert.Equal("1.2.3", session.DoorVersion);
        Assert.Equal("work", session.Profile);
        Assert.Contains(session.Id, opened.Message);
    }

    /// <summary>
    /// 🔴 Each conversation its own: a second opens while the first runs, in the same workspace and the same room folder, and
    /// neither names the other.
    /// </summary>
    [Fact]
    public async Task A_second_conversation_opens_while_one_runs_in_its_workspace()
    {
        var first = (await _ledger.OpenHelpAsync("stub", Room(), Now)).Session!;

        var second = await _ledger.OpenHelpAsync("stub", Room(), Now);

        Assert.Equal(SessionOpenRefusal.None, second.Refusal);
        Assert.NotEqual(first.Id, second.Session!.Id);
        Assert.Equal(first.Tree, second.Session.Tree);
        Assert.DoesNotContain(first.Id, second.Message);
    }

    [Fact]
    public async Task A_help_session_needs_its_room()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _ledger.OpenHelpAsync("stub", " ", Now));
    }

    // ——— A conversation keeps the workspace it started in; none waits on another.

    /// <summary>Two workspaces' conversations open at once, each keeping its own workspace, in the one room folder they share.</summary>
    [Fact]
    public async Task Two_workspaces_conversations_run_at_once_each_keeping_its_workspace()
    {
        var alpha = await _ledger.OpenHelpAsync("stub", Room(), Now, workspace: "alpha");
        var beta = await _ledger.OpenHelpAsync("stub", Room(), Now, workspace: "beta");

        Assert.Equal((SessionOpenRefusal.None, SessionOpenRefusal.None), (alpha.Refusal, beta.Refusal));
        Assert.Equal(("alpha", "beta"), (alpha.Session!.Workspace, beta.Session!.Workspace));
        Assert.Equal(alpha.Session.Tree, beta.Session.Tree);
    }

    /// <summary>A record from before has no workspace of its own and is read as <c>default</c>.</summary>
    [Fact]
    public async Task A_record_from_before_reads_as_default()
    {
        // A record with no workspace named.
        var before = await _sessions.CreateAsync(null, SessionLedger.HelpRepository, "stub", Now, kind: SessionKind.Chat, tree: Room());

        Assert.Equal("default", (await _sessions.FindAsync(before.Id))!.Workspace);
    }

    /// <summary>
    /// 🔴 An ended conversation goes on with the person's words while another of its own workspace answers a turn: one
    /// conversation's turn never holds another's words.
    /// </summary>
    [Fact]
    public async Task An_ended_conversation_goes_on_while_another_of_its_workspace_answers()
    {
        var ended = (await _ledger.OpenHelpAsync("stub", Room(), Now, workspace: "alpha")).Session!;
        await _sessions.SetStateAsync(ended.Id, SessionState.Stopped, null, null, null, Now.AddMinutes(1));
        await _ledger.SayAsync(ended.Id, "stuck", null, Now.AddMinutes(2));
        var answering = (await _ledger.OpenHelpAsync("stub", Room(), Now.AddMinutes(3), workspace: "alpha")).Session!;
        await _sessions.SetStateAsync(answering.Id, SessionState.Working, null, null, null, Now.AddMinutes(3));

        var went = await _ledger.AdvanceAsync(ended.Id, "working", null, null, null, Now.AddMinutes(4));

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

    // ——— The record keeps the mode its start got, the effective one, apart from the mode chosen.

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
    /// A conversation going on after the mode changed records the mode it goes on under, on Ask Daoris's records of
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
