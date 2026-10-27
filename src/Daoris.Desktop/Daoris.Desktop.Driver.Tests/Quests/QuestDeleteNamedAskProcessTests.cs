using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// QUESTDELETE1: deleting the quest a named ask published never publishes the ask again, and never to a repository the ask did not name.
/// On the installed build the driver's look opened an intake for such an ask a minute after the delete, and the intake published it to the
/// three repositories of its routing proposal, two of which the person never named. Here the service is its own host in a process of its
/// own (<see cref="RealServiceHost"/>) and the driver's look runs over it, several times, with a stand-in intake that publishes to every
/// repository the ask's proposal lists and leaves a mark each time it runs — so a re-route is a mark and a quest, never a guess.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class QuestDeleteNamedAskProcessTests : IAsyncLifetime, IDisposable
{
    private const string Words = "retire the old gadget naming across every service";

    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-questdelete-" + Guid.NewGuid().ToString("N")[..8]);

    private RealServiceHost? _host;
    private ServiceClient? _client;

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _client?.Dispose();
        _host?.Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Marks => Path.Combine(_home, "intake-ran.log");

    private int Ran => File.Exists(Marks) ? File.ReadAllLines(Marks).Length : 0;

    /// <summary>The host over a scratch home, and three repositories that each declare the words of the ask: so its proposal lists all three.</summary>
    private async Task<ServiceClient> ServiceAsync()
    {
        Directory.CreateDirectory(_home);
        _host = await RealServiceHost.StartAsync(Path.Combine(_home, "service"));
        _client = new ServiceClient(_host.Url, null);
        foreach (var name in new[] { "Daoris", "Lyntai", "Shenora" })
        {
            var root = Path.Combine(_home, "checkouts", name);
            Directory.CreateDirectory(root);
            var (ok, message) = await _client.RegisterAsync(new JsonObject
            {
                ["repository"] = name,
                ["root"] = root,
                ["packs"] = new JsonArray(),
                ["domain"] = new JsonObject
                {
                    ["summary"] = "Owns the old gadget naming.",
                    ["owns"] = new JsonArray("old gadget naming", "every service"),
                    ["accepts"] = new JsonArray("a naming change"),
                },
            });
            Assert.True(ok, message);
        }

        return _client;
    }

    private Daoris.Driver.Driver Driver()
    {
        var config = (DriverConfig.Empty with
        {
            Adapter = "stub",
            TestBound = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", Agent()] },
        }).WithIntake("stub");
        var adapters = AdapterSet.Built();
        return new Daoris.Driver.Driver(
            new ServiceClient(_host!.Url, null), config, adapters, _home,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
    }

    /// <summary>
    /// The stand-in intake: a real process, no model. It leaves a mark, then does what the installed build's intake did — publishes the ask
    /// to every repository of its proposal — or, for an ask the words below mark as the control, to the one repository that ask's intake chose.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "intake-agent.mjs");
        File.WriteAllText(script, $$"""
            import { appendFileSync } from 'node:fs';
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }

            const ask = process.env.DAORIS_ASK_ID;
            const session = process.env.DAORIS_SESSION_ID;
            const service = process.env.DAORIS_SERVICE_URL;
            appendFileSync({{System.Text.Json.JsonSerializer.Serialize(Marks)}}, ask + '\n');

            const record = await (await fetch(service + '/api/asks/' + ask)).json();
            const proposed = (record.proposal ?? []).map((match) => match.repository);
            const routes = /control/.test(record.sentence) ? ['Lyntai'] : proposed;
            for (const to of routes) {
              const answer = await fetch(service + '/api/asks/' + ask + '/publish', {
                method: 'POST',
                headers: { 'content-type': 'application/json' },
                body: JSON.stringify({ to, title: 'Retire the naming', body: 'Read the ask.', session }),
              });
              console.log('stub: published to ' + to + ' -> ' + answer.status);
            }
            """);
        return script;
    }

    private async Task<IReadOnlyList<QuestView>> QuestsOfAsync(string ask) =>
        [.. (await _client!.EveryQuestAsync()).Where(quest => quest.From == $"ask #{ask}")];

    private async Task<AskView> StandingAsync(string ask) =>
        (await _client!.EveryAskAsync()).Single(each => each.Id == ask);

    /// <summary>
    /// 🔴 The ask the person sent to one repository by name: its only quest deleted, the look runs four times. No intake runs, no quest is
    /// published to any repository, and the ask stands with no quest and says so — over the real service's own doors, not a stand-in's.
    /// </summary>
    [Fact]
    public async Task A_named_asks_deleted_quest_is_not_published_again_over_several_looks()
    {
        var client = await ServiceAsync();
        var asked = await client.AskAsync("default", Words, [], [], to: "Lyntai");
        Assert.True(asked.Ok, asked.Message);
        var ask = asked.AskId!;
        var named = Assert.Single(await QuestsOfAsync(ask));
        Assert.Equal("Lyntai", named.To);
        // The exposure: its routing proposal lists repositories the person never named.
        var before = await StandingAsync(ask);
        Assert.Equal("named", before.Tier);
        Assert.Contains("Daoris", before.Proposed);
        Assert.Contains("Shenora", before.Proposed);

        var deleted = await client.DeleteQuestAsync(named.Id);

        // The terminal's answer says what happens to the ask.
        Assert.True(deleted.Ok, deleted.Message);
        Assert.Contains("Deleted quest", deleted.Message);
        Assert.Contains($"Ask `#{ask}` stands with no quest: you named its receiver", deleted.Message);
        Assert.Contains("not published again", deleted.Message);
        var driver = Driver();
        for (var look = 1; look <= 4; look++)
        {
            await driver.RunOnceAsync().WaitAsync(Poll.Patience);

            Assert.Equal(0, Ran);
            Assert.Empty(await QuestsOfAsync(ask));
            var standing = await StandingAsync(ask);
            Assert.Equal("Proposed", standing.State);
            Assert.Equal("named", standing.Tier);
            Assert.Null(standing.Intake);
            Assert.Empty(standing.Quests);
            Assert.Contains("stands with no quest", standing.Note);
        }

        // No quest anywhere for it, to the repository it named or to the ones its proposal lists.
        Assert.DoesNotContain(await client.EveryQuestAsync(), quest => quest.From == $"ask #{ask}");
    }

    /// <summary>
    /// An ask an intake routed is unchanged: its quest deleted, it is a proposal again with no note, and the intake that served it is not run
    /// a second time — over several looks, beside a named ask that is passed over in the same circle.
    /// </summary>
    [Fact]
    public async Task An_intake_routed_ask_is_unchanged_and_a_named_one_beside_it_is_passed_over()
    {
        var client = await ServiceAsync();
        var named = await client.AskAsync("default", Words, [], [], to: "Lyntai");
        Assert.True(named.Ok, named.Message);
        await client.DeleteQuestAsync(named.QuestId!);
        var routed = await client.AskAsync("default", $"{Words}, control", [], [], to: null);
        Assert.True(routed.Ok, routed.Message);
        var driver = Driver();

        // The look serves the ask nobody named, once: the intake routes it.
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.Equal(1, Ran);
        var published = Assert.Single(await QuestsOfAsync(routed.AskId!));
        Assert.Equal("Lyntai", published.To);
        Assert.Equal("intake", (await StandingAsync(routed.AskId!)).Tier);

        var deleted = await client.DeleteQuestAsync(published.Id);

        Assert.Contains($"Ask `#{routed.AskId}` stands with no quest: its intake session already served it", deleted.Message);
        for (var look = 1; look <= 3; look++)
        {
            await driver.RunOnceAsync().WaitAsync(Poll.Patience);

            Assert.Equal(1, Ran);
            Assert.Empty(await QuestsOfAsync(routed.AskId!));
            Assert.Empty(await QuestsOfAsync(named.AskId!));
            var standing = await StandingAsync(routed.AskId!);
            Assert.Equal("Proposed", standing.State);
            Assert.NotNull(standing.Intake);
            Assert.Null(standing.Note);
            Assert.Contains("stands with no quest", (await StandingAsync(named.AskId!)).Note);
        }
    }
}
