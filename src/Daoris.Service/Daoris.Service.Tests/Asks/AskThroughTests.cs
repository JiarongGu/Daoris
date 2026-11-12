using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// The person's words an ask carries when it is made: requirements whose quotes are kept on the ask as words the person said,
/// so the exchange's own quote judge reads them, and through words, the person's asking for delivery with no press of theirs, kept on
/// the ask with when, the latest standing and a take-back clearing them.
/// </summary>
public sealed class AskThroughTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-ask-through-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private AskStore _asks = null!;
    private AskDesk _desk = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-12T09:00:00Z");

    private const string Sentence = "write the guide for the export button";

    public async Task InitializeAsync()
    {
        var dir = Path.Combine(_root, "family", "reports");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "daoris.json"),
            """{ "source": "s", "packs": [], "domain": { "summary": "The reports.", "owns": ["report pages"], "accepts": ["a report"] } }""");

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        var quests = await QuestStore.OpenAsync(_connection);
        _asks = await AskStore.OpenAsync(_connection);

        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(), DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.ImportAsync(Path.Combine(_root, "family"), Now);
        foreach (var row in await service.RegistryAsync()) await service.RegisterAsync(row with { Workspace = "work" }, Now);

        var files = new QuestFiles(Path.Combine(_root, "home"));
        _desk = new AskDesk(service, _asks, new QuestExchange(service, quests, files: files, asks: _asks), files);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task A_new_asks_requirements_travel_on_its_quest_and_their_quotes_are_kept_as_the_persons_words()
    {
        var asked = await _desk.AskAsync(new AskRequest("work", Sentence)
        {
            To = "reports",
            Requirements = [new QuestRequirement("merge all changes to remote too", "the work reaches its pull request")],
        }, Now);

        Assert.Equal(AskRefusal.None, asked.Refusal);
        Assert.Equal("merge all changes to remote too", Assert.Single(asked.Quest!.Requirements).Quote);
        var kept = (await _desk.FindAsync(asked.Ask!.Id))!;
        Assert.Equal(AskState.Published, kept.State);
        var quoted = Assert.Single(kept.Later);
        Assert.Equal((AskWordKind.Quoted, "merge all changes to remote too"), (quoted.Kind, quoted.Text));
        Assert.DoesNotContain("carries no requirement", asked.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_requirement_on_a_new_ask_with_no_receiver_is_refused_and_nothing_is_kept()
    {
        var asked = await _desk.AskAsync(new AskRequest("work", Sentence)
        {
            Requirements = [new QuestRequirement("merge all changes to remote too", "the work reaches its pull request")],
        }, Now);

        Assert.Equal(AskRefusal.BadRequirement, asked.Refusal);
        Assert.Contains("--to", asked.Message, StringComparison.Ordinal);
        Assert.Empty(await _desk.ListAsync("work", includeClosed: true));
    }

    [Fact]
    public async Task Through_words_are_kept_on_a_new_ask_the_latest_standing_and_a_take_back_clears_them()
    {
        var asked = await _desk.AskAsync(new AskRequest("work", Sentence) { Through = "  please do without my accept  " }, Now);
        Assert.Equal(AskRefusal.None, asked.Refusal);
        Assert.Equal("please do without my accept", asked.Ask!.Through?.Words);

        var again = await _desk.ChooseThroughAsync(asked.Ask.Id, "pr merge should be automated", Now.AddMinutes(1));
        Assert.Equal(AskRefusal.None, again.Refusal);
        Assert.Equal("pr merge should be automated", (await _desk.FindAsync(asked.Ask.Id))!.Through?.Words);

        var taken = await _desk.ChooseThroughAsync(asked.Ask.Id, null, Now.AddMinutes(2));
        Assert.Equal(AskRefusal.None, taken.Refusal);
        var read = (await _desk.FindAsync(asked.Ask.Id))!;
        Assert.Null(read.Through);
        Assert.Equal(3, read.ThroughWords.Count);

        var none = await _desk.ChooseThroughAsync(asked.Ask.Id, null, Now.AddMinutes(3));
        Assert.Equal(AskRefusal.None, none.Refusal);
        Assert.Contains("no through words", none.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Through_words_past_their_bound_are_refused_and_a_closed_ask_takes_none()
    {
        var ask = (await _desk.AskAsync(new AskRequest("work", Sentence), Now)).Ask!;

        Assert.Equal(AskRefusal.BadThrough, (await _desk.ChooseThroughAsync(ask.Id, new string('x', AskThroughs.WordsLimit + 1), Now)).Refusal);
        await _desk.CloseAsync(ask.Id, "not needed", Now);
        Assert.Equal(AskRefusal.Closed, (await _desk.ChooseThroughAsync(ask.Id, "please do without my accept", Now)).Refusal);
    }
}
