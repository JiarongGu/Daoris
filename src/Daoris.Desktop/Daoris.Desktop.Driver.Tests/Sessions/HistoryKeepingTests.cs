using System.Globalization;
using System.Text.Json.Nodes;
using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.HistoryStandIn;
using StandIn = Daoris.Desktop.Driver.Tests.HistoryStandIn;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// KEEP1g (D195 point 7, decided by the owner; the cleanup design §3.6, §9): finished history by an age the person sets. Once a day the
/// look's Keeping pass lists each workspace through the clear's first press and sends exactly the units whose last move is older than the
/// person's days, every refusal holding; what went is the log's <c>history.cleared</c> with <c>door: rule</c> and <c>last.history</c>,
/// what stays is said. With no age set, the default, nothing is cleared.
/// </summary>
/// <remarks>A home under <c>_fixtures/</c> and the in-process stand-in for the service (<see cref="HistoryStandIn"/>).</remarks>
public sealed class HistoryKeepingTests : IDisposable
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "history-keeping", Guid.NewGuid().ToString("N")[..8]);

    public HistoryKeepingTests() => Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Sessions));

    public void Dispose()
    {
        try { TreeSetAside.DeleteNeverFollowing(_home); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Sessions => DaorisHome.Entry(_home, HomeEntry.Sessions);

    private string Config => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    private void Age(int days) => DriverConfig.Empty.WithKeep(new KeepSet("history", Days: days)).Save(Config);

    private static string Ago(int days) => (Now - TimeSpan.FromDays(days)).ToString("O", CultureInfo.InvariantCulture);

    private static JsonObject Moved(JsonObject row, int daysAgo)
    {
        row["updated"] = Ago(daysAgo);
        return row;
    }

    private BranchTidying Tidying(Func<DateTimeOffset>? clock = null) => new(_home)
    {
        Pace = TimeSpan.Zero,
        Clock = clock ?? (() => Now),
        Processes = new SessionProcesses(Sessions),
    };

    /// <summary>
    /// Three closed quests of one workspace, at 90 days: one moved 200 days ago that may go, one as old that waits on the person, and one
    /// moved 10 days ago. The service is sent exactly the first; the second is said with its word; the third is not listed as due.
    /// </summary>
    private StandIn Service()
    {
        File.WriteAllText(Path.Combine(Sessions, "s1.events.jsonl"), "{\"seq\":1}\n");
        var service = new StandIn
        {
            Records = [Moved(Record("s1", "q1"), 200), Moved(Record("s2", "q2"), 200), Moved(Record("s3", "q3"), 10)],
            Quests = [Moved(Quest("q1"), 200), Moved(Quest("q2"), 200), Moved(Quest("q3"), 10)],
            Home = _home,
        };
        service.Listings["workspace=default"] =
        [
            Unit("quest", "q1", quests: ["q1"], sessions: ["s1"]),
            Unit("quest", "q2", quests: ["q2"], sessions: ["s2"], refusal: Refusal(HistoryWords.NeedsYou, quest: "q2", waits: HistoryWaits.Held)),
            Unit("quest", "q3", quests: ["q3"], sessions: ["s3"]),
        ];
        return service;
    }

    /// <summary>§9's proof: a unit past its days cleared, one with a refusal kept and said; the line and the record say the rule's door.</summary>
    [Fact]
    public async Task A_unit_past_the_persons_age_is_cleared_and_one_a_refusal_holds_is_kept_and_said()
    {
        Age(90);
        var service = Service();
        var log = new MachineLog(_home, "desktop");

        var said = await Tidying().LookAsync(service.Client(), log, CancellationToken.None);
        log.Dispose();

        Assert.Equal(["quest:q1"], service.Pressed);
        Assert.False(File.Exists(Path.Combine(Sessions, "s1.events.jsonl")));
        Assert.Contains(said, line => line.StartsWith("tidy  cleared 1 unit(s) of finished history whose last move was over 90 days ago", StringComparison.Ordinal));
        Assert.Contains(said, line => line.StartsWith("tidy  quest #q2's finished history stays past 90 days:", StringComparison.Ordinal));
        Assert.DoesNotContain(said, line => line.Contains("#q3", StringComparison.Ordinal));

        var line = Assert.Single(
            Directory.GetFiles(DaorisHome.Entry(_home, HomeEntry.Logs)).SelectMany(StubFile.Lines),
            each => each.Contains("\"history.cleared\"", StringComparison.Ordinal));
        Assert.Contains("\"door\":\"rule\"", line);
        Assert.Contains("\"scope\":\"workspace\"", line);
        Assert.Contains("\"quests\":1", line);

        var last = KeepingRecord.Read(_home).Last[KeepKinds.History.Id];
        Assert.Equal((KeepingRun.Look, 1), (last.Door, last.Removed));
        Assert.Equal(1, last.Kept[HistoryWords.NeedsYou]);
    }

    /// <summary>Once a day: a second look the same day sends nothing, and a changed age runs at once.</summary>
    [Fact]
    public async Task The_clear_runs_once_a_day_and_at_once_when_the_age_changes()
    {
        Age(90);
        var service = Service();
        var now = Now;
        var tidying = Tidying(() => now);

        await tidying.LookAsync(service.Client(), null, CancellationToken.None);
        Assert.Equal(["quest:q1"], service.Pressed);

        now = Now.AddHours(1);
        Assert.Empty(await tidying.LookAsync(service.Client(), null, CancellationToken.None));
        Assert.Equal(["quest:q1"], service.Pressed);

        service.Records.Add(Moved(Record("s4", "q4"), 40));
        service.Quests.Add(Moved(Quest("q4"), 40));
        service.Listings["workspace=default"] = [Unit("quest", "q4", quests: ["q4"], sessions: ["s4"])];
        Age(31);
        await tidying.LookAsync(service.Client(), null, CancellationToken.None);
        Assert.Equal(["quest:q1", "quest:q4"], service.Pressed);
    }

    /// <summary>§9's proof: <c>never</c>, the default, clears nothing and asks the service nothing of history.</summary>
    [Fact]
    public async Task Never_the_default_clears_nothing()
    {
        var service = Service();

        Assert.Empty(await Tidying().LookAsync(service.Client(), null, CancellationToken.None));
        Assert.Empty(service.Pressed);
        Assert.False(KeepingRecord.Read(_home).Last.ContainsKey(KeepKinds.History.Id));

        DriverConfig.Empty.WithKeep(new KeepSet("history", Never: true)).Save(Config);
        Assert.Equal(HistoryKeepingPass.None, await HistoryKeeping.PassAsync(
            new HistoryWorld(service.Client(), _home, Config, new SessionProcesses(Sessions)), KeepPolicy.Of(_home).History));
        Assert.Empty(service.Pressed);
    }

    /// <summary>A unit whose last move is not known, a quest the host answers no moment for, is not past its days: the unknown keeps.</summary>
    [Fact]
    public async Task A_unit_whose_last_move_is_not_known_stays()
    {
        Age(30);
        var service = Service();
        service.Quests[0].Remove("updated");

        var pass = await HistoryKeeping.PassAsync(
            new HistoryWorld(service.Client(), _home, Config, new SessionProcesses(Sessions)) { Clock = () => Now }, KeepPolicy.Of(_home).History);

        Assert.Equal(0, pass.Cleared);
        Assert.Empty(service.Pressed);
    }
}
