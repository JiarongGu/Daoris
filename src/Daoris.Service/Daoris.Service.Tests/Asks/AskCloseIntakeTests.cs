using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// An ask's close ends its intake waiting on the person in the same step, with the close's reason, whatever door closed it; an
/// intake still running is left to its own end, which the driver's look judges against the closed ask.
/// </summary>
public sealed class AskCloseIntakeTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-ask-close-" + Guid.NewGuid().ToString("N")[..8]);
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-12T18:57:00Z");

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private SessionStore _sessions = null!;
    private KnowledgeService _service = null!;
    private AskStore _asks = null!;
    private AskDesk _desk = null!;

    public async Task InitializeAsync()
    {
        var dir = Path.Combine(_root, "family", "lists");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "daoris.json"),
            """{ "source": "s", "packs": [], "domain": { "summary": "The lists app.", "owns": ["lists"], "accepts": ["a list bug"] } }""");
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
        _sessions = await SessionStore.OpenAsync(_connection);
        _asks = await AskStore.OpenAsync(_connection);
        var store = new InMemoryKnowledgeStore();
        _service = new KnowledgeService(store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(), DisclosurePolicy.LocalOnly, registry: new Registry());
        await _service.ImportAsync(Path.Combine(_root, "family"), Now);
        foreach (var row in await _service.RegistryAsync()) await _service.RegisterAsync(row with { Workspace = "work" }, Now);
        var files = new QuestFiles(Path.Combine(_root, "home"));
        _desk = new AskDesk(_service, _asks, new QuestExchange(_service, _quests, remotes: null, files, _sessions, _asks), files, _sessions);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private SessionLedger Ledger() => new(_quests, _sessions, _service, _asks);

    private async Task<(Ask Ask, string Intake)> IntakeAsync(params string[] moves)
    {
        var asked = (await _desk.AskAsync(new AskRequest("work", "a list feature"), Now)).Ask!;
        var intake = (await Ledger().OpenIntakeAsync(asked.Id, "stub", Path.Combine(_root, "room"), Now.AddSeconds(4))).Session!;
        foreach (var move in moves) await Ledger().AdvanceAsync(intake.Id, move, move == "awaiting-person" ? "it asks you" : null, null, null, Now.AddSeconds(20));
        return (asked, intake.Id);
    }

    [Fact]
    public async Task Closing_an_ask_ends_its_waiting_intake_with_the_closes_reason()
    {
        var (asked, intake) = await IntakeAsync("starting", "working", "awaiting-person");
        Assert.Equal(SessionState.AwaitingPerson, (await _sessions.FindAsync(intake))!.State);

        var closed = await _desk.CloseAsync(asked.Id, "built elsewhere", Now.AddMinutes(3));

        Assert.Equal(AskRefusal.None, closed.Refusal);
        var ended = (await _sessions.FindAsync(intake))!;
        Assert.Equal(SessionState.Stopped, ended.State);
        Assert.False(ended.Interrupted);
        Assert.Equal($"the person closed ask `#{asked.Id}`: built elsewhere", ended.Note);
        Assert.True(NoteParts.Carries(ended.NoteParts, LedgerNoteCodes.IntakeClosed.Code));
        Assert.Contains("\"words\":\"built elsewhere\"", ended.NoteParts);
    }

    [Fact]
    public async Task A_running_intake_is_left_to_its_own_end()
    {
        var (asked, intake) = await IntakeAsync("starting", "working");

        await _desk.CloseAsync(asked.Id, "built elsewhere", Now.AddMinutes(3));

        Assert.Equal(SessionState.Working, (await _sessions.FindAsync(intake))!.State);
    }
}
