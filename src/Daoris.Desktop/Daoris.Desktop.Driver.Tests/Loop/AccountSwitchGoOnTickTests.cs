using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Through real looks: a session that waits on its workflow's step (<c>awaiting-step</c>) is an active record, and a host
/// refuses to leave an active record for a new part (<c>cannot be left; its head is … and its state is awaiting-step</c>, the install's own
/// word for two sessions on a switched-off account). So its words, which a fall-back carries to a new part on a ready account, never moved
/// and were taken up again at every look. The fall-back now ends the record first, as an answered park's is, so the part opens once.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Needs, Needed.Harness)]
public sealed class AccountSwitchGoOnTickTests : IDisposable
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");

    private static readonly DateTimeOffset Seen = new(2026, 10, 1, 14, 0, 0, TimeSpan.FromMinutes(345));

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-switch-goon-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public AccountSwitchGoOnTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        GitSeed.StartingPoint(_repository, "switch@example.com", "TOOL4f");

        foreach (var account in new[] { "account-1", "account-2" })
        {
            Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "stub", account));
        }

        new HarnessSettings().WithDefault("stub", "account-1").WithRotation("stub", ["account-1", "account-2"])
            .Save(DaorisHome.Entry(_home, HomeEntry.Harnesses));
    }

    public void Dispose()
    {
        GitSeed.Discard(_home);
    }

    private string Log => Path.Combine(_home, "agent.log");

    private string ResumeLog => Path.Combine(_home, "resume.log");

    private static string Profile(SessionMessagesTickTests.SaidStandIn service, string session) =>
        JsonNode.Parse(service.Record(session))!["profile"]!.GetValue<string>();

    private DriverConfig Resuming(string mode) => StaffedRoles.Config("acp-stub") with
    {
        Drivable = ["engine"],
        TestBound = 1,
        PollSeconds = 1,
        Commands = new Dictionary<string, IReadOnlyList<string>>
        {
            ["acp-stub"] = ["node", RotationStubs.ResumingAgent(_home, Zone), ResumeLog, mode],
            ["stub"] = ["node", RotationStubs.Agent(_home), Log, "-"],
        },
    };

    /// <summary>
    /// The person's words (a terminal say with no author is theirs) to a session waiting on its step, whose account they switched off while
    /// another is ready, on a quest still taken: the part opens once on the ready account, taking the words, and nothing is reopened after.
    /// </summary>
    [Fact]
    public async Task A_session_waiting_on_its_step_on_a_switched_off_account_moves_once_to_the_ready_account_with_its_words()
    {
        await using var service = SessionMessagesTickTests.SaidStandIn.Start(_repository);
        service.Opens = 2;
        var adapters = ProviderRegistry.Built();
        var roster = new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)) { Clock = () => Seen, Zone = Zone };
        using var client = new ServiceClient(service.Url, null);
        var lines = new List<AccountLine>();
        client.AccountLined += line => { lock (lines) lines.Add(line); };
        var driver = new Daoris.Driver.Driver(client, Resuming("fails"), adapters, _home, processes: new SessionProcesses(), harnesses: roster);

        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.Equal("account-1", Profile(service, "s1"));
        AccountSwitch.Set(_home, "stub", HarnessSettings.Profiles(_home, "stub"), "account-1", off: true);
        await service.SayAsync("s1", "The steward asks: is the check green?");
        await client.AdvanceAsync("s1", "working");
        await client.AdvanceAsync("s1", "awaiting-step");

        var seen = new List<string>();
        for (var look = 0; look < 3; look++)
        {
            var round = await driver.RunOnceAsync().WaitAsync(Poll.Patience);
            seen.Add($"look {look}: {string.Join(" | ", round.Events)}");
        }

        int reopened;
        lock (lines) reopened = lines.Count(line => line.Event == "session.reopened");
        var said = string.Join("\n", seen) + "\nafter: " + service.Record("s1");
        Assert.True(service.SessionCount == 2, said);
        Assert.Equal("account-2", Profile(service, "s2"));
        Assert.Equal(1, reopened);
        Assert.Equal("completed", service.State("s1"));
        Assert.Contains("is the check green?", File.ReadAllText(ResumeLog), StringComparison.Ordinal);
    }

    /// <summary>
    /// The same session on a quest that is done goes on through the shell's runner, which moves only an ended record: the record is ended
    /// before the runner is asked, once.
    /// </summary>
    [Fact]
    public async Task A_session_waiting_on_its_step_of_a_closed_quest_is_ended_before_the_runner_is_asked_to_move_it()
    {
        await using var service = SessionMessagesTickTests.SaidStandIn.Start(_repository);
        var adapters = ProviderRegistry.Built();
        var roster = new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)) { Clock = () => Seen, Zone = Zone };
        using var client = new ServiceClient(service.Url, null);
        var asked = new List<(string Session, string StateThen)>();
        Task<StartedFrom> GoesOn(string session, ContinueReason why, CancellationToken _)
        {
            lock (asked) asked.Add((session, service.State(session)));
            return Task.FromResult(new StartedFrom("c9", true, null, $"`{session}` went on in conversation `c9`."));
        }

        var driver = new Daoris.Driver.Driver(client, Resuming("closes"), adapters, _home, processes: new SessionProcesses(), harnesses: roster)
        {
            GoesOnLost = GoesOn,
        };

        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        AccountSwitch.Set(_home, "stub", HarnessSettings.Profiles(_home, "stub"), "account-1", off: true);
        await service.SayAsync("s1", "The steward asks: is the check green?");
        await client.AdvanceAsync("s1", "working");
        await client.AdvanceAsync("s1", "awaiting-step");

        var look = await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        lock (asked) Assert.Equal([("s1", "completed")], asked);
        Assert.Contains(look.Events, line => line.Contains("went on in conversation `c9`", StringComparison.Ordinal));
    }
}
