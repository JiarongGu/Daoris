using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A native-door conversation whose harness never answers its first word fails its opening past the bound, with the reason said and the
/// word kept to continue; one that answers in time is not touched.
/// </summary>
/// <remarks>A real process (node) speaking the native door, and the in-process ledger of <see cref="ChatGoOnTests"/>. No model, no account.</remarks>
[Trait(Category.Name, Category.Process)]
public sealed class NativeOpeningProcessTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-native-opening-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    public NativeOpeningProcessTests()
    {
        _root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_root);
        SeededRules.In(_home);
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "claude-code", "daoris"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task A_native_harness_that_never_answers_its_first_word_fails_its_opening_with_the_reason_said()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["claude-code"] = ["node", Silent()] },
        };
        config.Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        using var client = ledger.Client();
        var adapters = ProviderRegistry.Built();
        using var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), events: events,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            OpeningBound = TimeSpan.FromSeconds(2),
        };
        string Seen(string id) => $"state {ledger.State(id)}, record [{string.Join(" | ", events.After(id, 0).Events.Select(e => $"{e.Kind}:{e.Text}"))}]";

        var id = (await runner.StartAsync("engine", "claude-code", config)).SessionId!;
        await Poll.Until(() => ledger.State(id) == "working", () => Seen(id));
        Assert.True(runner.Say(id, "are you there?"));

        await Poll.Until(
            () => ledger.State(id) == "failed" && events.After(id, 0).Events.Any(e => e.Parts?.Any(part => part.Code == "chat.not-up") == true),
            () => Seen(id));
        Assert.Single(events.After(id, 0).Events, e => e.Parts?.Any(part => part.Code == "chat.not-up") == true);
        Assert.Contains(events.After(id, 0).Events, e => e.Text?.Contains("did not come up within 2 s") == true);
        Assert.Contains(events.After(id, 0).Events, e => e.Kind == SessionEventKind.User && e.Text == "are you there?");
    }

    /// <summary>A native harness that takes its words and says nothing: no event ever comes of them.</summary>
    private string Silent()
    {
        var script = Path.Combine(_home, "claude-silent.mjs");
        File.WriteAllText(script, StubPlace.Js + """
            import { createInterface } from 'node:readline';
            const argv = process.argv.slice(2);
            if (argv.includes('--version')) { console.log('2.1.0 (Claude Code)'); process.exit(0); }
            if (argv[0] === 'auth' && argv[1] === 'status') { console.log('{"loggedIn": true}'); process.exit(0); }
            if (!argv.includes('-p')) process.exit(0);
            createInterface({ input: process.stdin }).on('line', () => {});
            setTimeout(() => process.exit(3), 120000);
            """);
        return script;
    }
}
