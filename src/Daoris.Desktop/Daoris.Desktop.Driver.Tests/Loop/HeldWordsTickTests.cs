using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Through REAL ticks: words held on a record because its own account could not carry them (a <c>cannot</c> mark, why
/// <c>account</c>) are read again at each look (HELDWORDS1, D348). Once the account is usable again they go on in the record's own
/// conversation; where it stays unusable past the bound they go on in a new session on a ready account, in its tree.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class HeldWordsTickTests : IDisposable
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");

    private static readonly DateTimeOffset Seen = new(2026, 10, 1, 14, 0, 0, TimeSpan.FromMinutes(345));

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-held-words-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public HeldWordsTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        GitSeed.StartingPoint(_repository, "held@example.com", "HELDWORDS1");
        foreach (var account in new[] { "account-1", "account-2" }) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "stub", account));
        new HarnessSettings().WithDefault("stub", "account-1").WithRotation("stub", ["account-1", "account-2"])
            .Save(DaorisHome.Entry(_home, HomeEntry.Harnesses));
    }

    public void Dispose()
    {
        GitSeed.Discard(_home);
    }

    private string ResumeLog => Path.Combine(_home, "resume.log");

    private JsonObject[] Frames() => [.. StubFile.Lines(ResumeLog).Select(line => JsonNode.Parse(line)!.AsObject())];

    private DriverConfig Resuming(string signedOut = "-") => StaffedRoles.Config("acp-stub") with
    {
        Drivable = ["engine"],
        TestBound = 1,
        PollSeconds = 1,
        Commands = new Dictionary<string, IReadOnlyList<string>>
        {
            ["acp-stub"] = ["node", RotationStubs.ResumingAgent(_home, Zone), ResumeLog, "closes"],
            ["stub"] = ["node", RotationStubs.Agent(_home), Path.Combine(_home, "agent.log"), signedOut],
        },
    };

    private HarnessRoster Roster(ProviderRegistry adapters) =>
        new(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)) { Clock = () => Seen, Zone = Zone };

    private static string Profile(SessionMessagesTickTests.SaidStandIn service, string session) =>
        JsonNode.Parse(service.Record(session))!["profile"]!.GetValue<string>();

    /// <summary>Hold the words as the install held them: marked by their ids, why <c>account</c>, this long ago.</summary>
    private void Held(string session, string word, TimeSpan ago) =>
        new GoOnMarks(_home).Mark(session, [word], ContinueWhy.Of(ContinueWhy.Account), DateTimeOffset.UtcNow - ago);

    /// <summary>
    /// 🔴 The install's case: a record's words were held because its account was not on this machine at the time, and the
    /// account is back and ready. The next look takes them up again and the record goes on in its own conversation, on its own
    /// account, handed them, and the mark judges nothing any more.
    /// </summary>
    [Fact]
    public async Task Words_held_for_an_account_usable_again_go_on_in_their_own_conversation_at_the_next_look()
    {
        await using var service = SessionMessagesTickTests.SaidStandIn.Start(_repository);
        var adapters = ProviderRegistry.Built();
        using var client = new ServiceClient(service.Url, null);
        var driver = new Daoris.Driver.Driver(client, Resuming(), adapters, _home, processes: new SessionProcesses(), harnesses: Roster(adapters));
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.Equal(("completed", "Done", "account-1"), (service.State("s1"), service.Status("q1"), Profile(service, "s1")));
        await service.SayAsync("s1", "Also log the port.");
        Held("s1", "w1", TimeSpan.FromHours(1));

        var look = await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        var seen = $"look: {string.Join(" | ", look.Events)}\nafter: {service.Record("s1")}";
        Assert.True(service.SessionCount == 1, seen);
        var resumed = Assert.Single(Frames(), frame => frame["method"]!.GetValue<string>() == "session/resume");
        Assert.Equal("account-1", resumed["account"]!.GetValue<string>());
        Assert.Equal([("s1", "w1", (string?)null)], service.Taken);
    }

    /// <summary>
    /// 🔴 Words held on a closed quest's session whose own account stays signed out past the bound do not wait on it any longer:
    /// the look hands the session to the shell's runner, the reason <c>account</c>, which goes on as a conversation in its tree on
    /// the ready account. Before the bound the same words still wait.
    /// </summary>
    [Fact]
    public async Task Words_held_past_the_bound_for_a_signed_out_account_go_on_in_a_new_session_on_a_ready_account()
    {
        await using var service = SessionMessagesTickTests.SaidStandIn.Start(_repository);
        var adapters = ProviderRegistry.Built();
        using var client = new ServiceClient(service.Url, null);
        await new Daoris.Driver.Driver(client, Resuming(), adapters, _home, processes: new SessionProcesses(), harnesses: Roster(adapters))
            .RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.Equal(("completed", "Done", "account-1"), (service.State("s1"), service.Status("q1"), Profile(service, "s1")));
        await service.SayAsync("s1", "Also log the port.");

        // account-1 signs out, and the person's press reads it so.
        var roster = Roster(adapters);
        await roster.ReportAsync("stub", Resuming(signedOut: "account-1"), refresh: true);
        var handed = new List<(string Session, string Why)>();
        Task<StartedFrom> GoesOn(string session, ContinueReason why, CancellationToken _)
        {
            lock (handed) handed.Add((session, why.Code));
            return Task.FromResult(new StartedFrom("c9", true, null, $"`{session}` went on in conversation `c9` on the next account."));
        }

        var driver = new Daoris.Driver.Driver(
            client, Resuming(signedOut: "account-1"), adapters, _home, processes: new SessionProcesses(), harnesses: roster) { GoesOnLost = GoesOn };

        // Inside the bound (HeldAccountWords.Bound, half an hour) they wait; past it they go on.
        Held("s1", "w1", TimeSpan.FromMinutes(25));
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        lock (handed) Assert.Empty(handed);

        Held("s1", "w1", TimeSpan.FromMinutes(35));
        var look = await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        var seen = $"look: {string.Join(" | ", look.Events)}\nafter: {service.Record("s1")}";
        lock (handed) Assert.True(handed.SequenceEqual([("s1", ContinueWhy.Account)]), seen);
        Assert.Contains(look.Events, line => line.Contains("went on in conversation `c9`", StringComparison.Ordinal));
        Assert.DoesNotContain(Frames(), frame => frame["method"]!.GetValue<string>() == "session/resume");
    }
}
