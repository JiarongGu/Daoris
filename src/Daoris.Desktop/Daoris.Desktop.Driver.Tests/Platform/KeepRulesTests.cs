using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// KEEP1d (D195 point 2; the cleanup design §2.2, §3.1, §9): the person's keep rules in <c>driver.json</c>'s <c>keep</c>, judged by the
/// one judgement every door makes (<see cref="KeepRules"/>), read by the one effective policy and through it by every reader of the
/// set-aside days, and the look's daily prune of the machine log at the person's days. Files only, in a home under <c>_fixtures/</c>; the
/// report's two readers are <see cref="BranchTidyingTests"/>', the card's <see cref="SystemSpaceTests"/>', the terminal's text
/// <c>fixtures/driver-choices-text.json</c>'s and the file's keeping <c>fixtures/driver-config-keeps.json</c>'s.
/// </summary>
public sealed class KeepRulesTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "keep-rules", Guid.NewGuid().ToString("N")[..8]);

    public KeepRulesTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { TreeSetAside.DeleteNeverFollowing(_home); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Config => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    private void Set(params KeepSet[] sets)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Config)!);
        var config = DriverConfig.Load(Config);
        foreach (var set in sets) config = config.WithKeep(set);
        config.Save(Config);
    }

    /// <summary>§2.1's bounds, each way a rule is set: what each kind takes, and the refusal of what it does not, in its code.</summary>
    public static TheoryData<string, int?, bool, bool?, bool, string?> Rules => new()
    {
        // kind, days, never, whole, default: null where it is taken, else the refusal's check.
        { "set-aside", 1, false, null, false, null },
        { "set-aside", 90, false, null, false, null },
        { "set-aside", 0, false, null, false, ChoiceRefusal.KeepOutOfRange },
        { "set-aside", 91, false, null, false, ChoiceRefusal.KeepOutOfRange },
        { "set-aside", null, false, true, false, null },
        { "set-aside", 30, false, false, false, null },
        { "set-aside", null, true, null, false, ChoiceRefusal.KeepOutOfRange },
        { "downloads", 0, false, null, false, null },
        { "downloads", 365, false, null, false, null },
        { "downloads", 366, false, null, false, ChoiceRefusal.KeepOutOfRange },
        { "downloads", -1, false, null, false, ChoiceRefusal.KeepOutOfRange },
        { "downloads", null, true, null, false, null },
        { "downloads", 7, true, null, false, ChoiceRefusal.KeepOutOfRange },
        { "downloads", null, false, true, false, ChoiceRefusal.KeepOutOfRange },
        { "logs", 7, false, null, false, null },
        { "logs", 30, false, null, false, null },
        { "logs", 6, false, null, false, ChoiceRefusal.KeepOutOfRange },
        { "logs", 31, false, null, false, ChoiceRefusal.KeepOutOfRange },
        { "logs", null, true, null, false, ChoiceRefusal.KeepOutOfRange },
        { "logs", null, false, null, true, null },
        { "logs", 14, false, null, true, ChoiceRefusal.KeepOutOfRange },
    };

    [Theory]
    [MemberData(nameof(Rules))]
    public void Each_kind_takes_its_bounds_and_refuses_the_rest_in_its_code(string kind, int? days, bool never, bool? whole, bool back, string? refused)
    {
        var set = new KeepSet(kind, days, never, whole, back);
        if (refused is null)
        {
            var policy = KeepPolicy.From(DriverConfig.Empty.WithKeep(set).Keep);
            Assert.Equal(back, DriverConfig.Empty.WithKeep(set).Keep.IsDefault(kind));
            if (kind == "set-aside" && days is { } kept) Assert.Equal(TimeSpan.FromDays(kept), policy.SetAside.Days);
            if (kind == "set-aside" && whole is { } held) Assert.Equal(held, policy.SetAside.Whole);
            if (kind == "downloads") Assert.Equal(never ? null : TimeSpan.FromDays(days!.Value), policy.Downloads.Days);
            if (kind == "logs" && days is { } logs) Assert.Equal(TimeSpan.FromDays(logs), policy.Logs.Days);
            return;
        }

        var refusal = Assert.Throws<ChoiceRefusal>(() => DriverConfig.Empty.WithKeep(set));
        Assert.Equal((refused, ChoiceCodes.KeepOutOfRange), (refusal.Check, ChoiceCodes.Of(refusal)));
        var bounds = refusal.Facts.ToDictionary(fact => fact.Key, fact => fact.Value);
        Assert.Equal((kind, KeepKinds.Named(kind)!.MinDays.ToString(System.Globalization.CultureInfo.InvariantCulture)), (bounds["kind"], bounds["min"]));
    }

    /// <summary>A kind no rule of the person's names, and a set naming nothing, are refused in words, not in the bounds' code.</summary>
    [Fact]
    public void A_kind_the_person_does_not_set_and_a_set_of_nothing_are_refused()
    {
        Assert.IsNotType<ChoiceRefusal>(Assert.ThrowsAny<DriverException>(() => DriverConfig.Empty.WithKeep(new KeepSet("history", Days: 30))));
        Assert.IsNotType<ChoiceRefusal>(Assert.ThrowsAny<DriverException>(() => DriverConfig.Empty.WithKeep(new KeepSet("logs"))));
    }

    /// <summary>
    /// §2.2: the one effective policy reads <c>keep</c> from the home's <c>driver.json</c>: each kind as set, a value out of its bounds as
    /// its default, and a file that does not read as what keeps the most (§6 point 7).
    /// </summary>
    [Fact]
    public void The_policy_reads_the_persons_rules_and_keeps_the_most_where_the_file_does_not_read()
    {
        Assert.Equal(KeepPolicy.Default, KeepPolicy.Of(_home));

        Set(new KeepSet("set-aside", Days: 30, Whole: true), new KeepSet("downloads", Never: true), new KeepSet("logs", Days: 14));
        var policy = KeepPolicy.Of(_home);
        Assert.Equal(new SetAsidePolicy(TimeSpan.FromDays(30), Whole: true), policy.SetAside);
        Assert.Equal(new DownloadsPolicy(null), policy.Downloads);
        Assert.Equal(new LogsPolicy(TimeSpan.FromDays(14)), policy.Logs);

        File.WriteAllText(Config, """{ "keep": { "setAside": { "days": 400 }, "downloads": { "days": "soon" }, "logs": { "days": 3 } } }""");
        Assert.Equal(KeepPolicy.Default, KeepPolicy.Of(_home));

        File.WriteAllText(Config, "{ not json");
        Assert.Same(KeepPolicy.Unread, KeepPolicy.Of(_home));
        Assert.Equal((TimeSpan.FromDays(90), true, (TimeSpan?)null), (KeepPolicy.Unread.SetAside.Days, KeepPolicy.Unread.SetAside.Whole, KeepPolicy.Unread.Downloads.Days));
    }

    /// <summary>
    /// §9's proof, the readers of the set-aside days: the purge, the work inventory's folder and its purge date, and the terminal's trees
    /// list each read the days the person set (the report's two, <see cref="BranchTidyingTests"/>).
    /// </summary>
    [Fact]
    public async Task The_purge_the_inventory_and_the_trees_list_read_the_days_the_person_set()
    {
        Set(new KeepSet("set-aside", Days: 30));
        var moved = DateTimeOffset.UtcNow.AddDays(-20);
        var folder = DaorisHome.Entry(_home, HomeEntry.Trees, ".tidied", "default", "engine",
            $"s-1-{moved.ToString("yyyyMMdd'T'HHmmssfff'Z'", System.Globalization.CultureInfo.InvariantCulture)}");
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "old.txt");
        File.WriteAllText(file, "written before the move\n");
        foreach (var made in new FileSystemInfo[] { new FileInfo(file), new DirectoryInfo(folder) })
        {
            made.CreationTimeUtc = moved.AddDays(-1).UtcDateTime;
            made.LastWriteTimeUtc = moved.AddDays(-1).UtcDateTime;
        }

        var trees = new SessionTrees(_home);
        var now = DateTimeOffset.UtcNow;
        Assert.Empty(trees.KeepSetAside(KeepPolicy.Of(_home), now, TimeSpan.FromMinutes(1), CancellationToken.None).Purged);
        Assert.True(Directory.Exists(folder));

        var facts = await WorkInventories.GatherAsync(_home, new Dictionary<string, string>(), new HashSet<string>(), new List<string>(), now: now);
        Assert.Equal(TimeSpan.FromDays(30), facts.SetAsideFor);
        var entry = Assert.Single(WorkInventory.Recoveries(facts), each => each.Resource == folder);
        Assert.Equal(TreeSetAside.MovedAt(Path.GetFileName(folder))!.Value.AddDays(30), entry.Until);

        Assert.True(Assert.Single(trees.KeepSetAside(KeepPolicy.Of(_home), now.AddDays(11), TimeSpan.FromMinutes(1), CancellationToken.None).Purged).Purged);

        // The terminal's `trees list` is the host's (no test reaches its console): it says the policy's days, never a constant.
        var console = File.ReadAllText(Path.Combine(GitSeed.RepoRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Host", "TreesConsole.cs"));
        Assert.Contains("var days = KeepPolicy.Of(home).SetAside.Days;", console, StringComparison.Ordinal);
        Assert.Contains("deleted once it is left untouched for {days.TotalDays:0} days", console, StringComparison.Ordinal);
    }

    /// <summary>
    /// §3.1 step 5: the look's pass prunes the machine log once a day at the person's days, today's file never, writes <c>last.logs</c> and
    /// says it; another look the same day prunes nothing more, and a rule changed runs at once.
    /// </summary>
    [Fact]
    public async Task The_look_prunes_the_machine_log_daily_at_the_persons_days()
    {
        Set(new KeepSet("logs", Days: 14));
        var logs = DaorisHome.Entry(_home, HomeEntry.Logs);
        Directory.CreateDirectory(logs);
        string Day(int ago) => Path.Combine(logs, $"{Now.AddDays(-ago):yyyy-MM-dd}.driver.jsonl");
        foreach (var ago in new[] { 0, 10, 20 }) File.WriteAllText(Day(ago), "{}\n");
        using var ledger = new StandInLedger();
        var now = Now;
        var tidying = new BranchTidying(_home) { Pace = TimeSpan.Zero, Clock = () => now };

        var said = await tidying.LookAsync(ledger.Client(), null, CancellationToken.None);

        Assert.Equal("tidy  removed 1 file(s) of the machine log older than 14 days (0.0 KB).", Assert.Single(said));
        Assert.Equal((false, true, true), (File.Exists(Day(20)), File.Exists(Day(10)), File.Exists(Day(0))));
        var last = KeepingRecord.Read(_home).Last["logs"];
        Assert.Equal((KeepingRun.Look, 1), (last.Door, last.Removed));

        now = Now.AddHours(1);
        File.WriteAllText(Path.Combine(logs, $"{Now.AddDays(-25):yyyy-MM-dd}.desktop.jsonl"), "{}\n");
        Assert.Empty(await tidying.LookAsync(ledger.Client(), null, CancellationToken.None));

        Set(new KeepSet("logs", Days: 7));
        Assert.Equal("tidy  removed 2 file(s) of the machine log older than 7 days (0.0 KB).",
            Assert.Single(await tidying.LookAsync(ledger.Client(), null, CancellationToken.None)));
        Assert.True(File.Exists(Day(0)));
    }
}
