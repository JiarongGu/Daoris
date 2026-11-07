using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The read's host half is the roster's around the layer's read as around today's. An account
/// that is a key is never read, an account a session of Daoris's runs on is never read, a held probe lock holds the read off, and a ready
/// account's read is spawned under its own home, its windows kept.
/// </summary>
public sealed class LayerUsageHostTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-usagehost-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly List<UsageServerRunner> _reads = [];

    public LayerUsageHostTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private IUsageRead Read() => new LayerUsageRead(CodexUsage.Question, CodexUsageLayer.Vendor, (_, _) =>
    {
        var runner = new UsageServerRunner(new UsageServerStandIn(claude: false, CodexUsageTests.Recorded(3.5, 100.25)));
        lock (_reads) _reads.Add(runner);
        return runner;
    });

    private HarnessRoster Roster() =>
        new(TestDoors.Registry(
            TestDoors.Door(
                "fake",
                new TestAgent(
                    Binary: [Path.Combine(_home, "agent-here")], VersionArguments: ["--version"], ProfileVariable: "CODEX_HOME",
                    ProbeByPresence: true, Usage: Read()),
                transport: Transport.Protocol)), DaorisHome.Entry(_home, HomeEntry.Harnesses))
        {
            Clock = () => CodexUsageTests.Now,
        };

    private string[] Homes()
    {
        lock (_reads) return [.. _reads.SelectMany(read => read.Spawns).Select(spawn => spawn.Environment?.GetValueOrDefault("CODEX_HOME") ?? "")];
    }

    /// <summary>
    /// Where the driver carries the layer, both agents' questions are the layer's read from the moment the driver loads: Claude Code's
    /// again since Lyntai's reader keeps the installed server's model scope with no id.
    /// </summary>
    [Fact]
    public void Both_agents_questions_are_bound_to_the_layer_s_read()
    {
        Assert.IsType<LayerUsageRead>(UsageReads.Of(CodexUsage.Question));
        Assert.IsType<LayerUsageRead>(UsageReads.Of(ClaudeUsage.Question));
    }

    [Fact]
    public async Task A_key_a_busy_account_and_a_held_lock_are_never_read_and_a_ready_account_is_read_under_its_own_home()
    {
        foreach (var name in new[] { "listed", "busy", "locked" }) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "fake", name));
        var keyed = HarnessKeys.Add(_home, "fake", "sk-test-0000");
        var roster = Roster();
        roster.Look([new SessionStarted("fake", "busy", CodexUsageTests.Now.AddMinutes(-3), Running: true)], roster.Mark());

        Assert.Null(await roster.AskUsageAsync("fake", "fake", keyed, DriverConfig.Empty, CancellationToken.None));
        Assert.Null(await roster.AskUsageAsync("fake", "fake", "busy", DriverConfig.Empty, CancellationToken.None));
        await using (await ProbeLock.TakeAsync(ProbeLock.PathOf(_home, "fake", "locked")))
        {
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => roster.AskUsageAsync("fake", "fake", "locked", DriverConfig.Empty, stop.Token));
        }

        Assert.Empty(Homes());

        var read = await roster.AskUsageAsync("fake", "fake", "listed", DriverConfig.Empty, CancellationToken.None);

        Assert.Equal(["session", "weekly"], read!.Select(window => window.Window));
        Assert.Equal([HarnessSettings.ProfileHome(_home, "fake", "listed")], Homes());
        Assert.Equal("layer:" + CodexUsage.Method, Assert.IsType<UsageAnswer>(read).Source);
    }
}
