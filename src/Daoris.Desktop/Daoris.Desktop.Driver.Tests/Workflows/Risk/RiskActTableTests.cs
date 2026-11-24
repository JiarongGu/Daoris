using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests.Risk;

/// <summary>
/// The act table: per kind of the census its class at a place, where its rule comes from, who answers by the cast, the
/// passes and gates of the last seven days at that place, <c>policy.change</c> locked; one model the window's row and <c>daoris settings risk</c>
/// both print, so the two agree on a home.
/// </summary>
public sealed class RiskActTableTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "risk-act-table", Guid.NewGuid().ToString("N")[..8]);
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 9, 0, 0, TimeSpan.Zero);

    public void Dispose() => GitSeed.Discard(_home);

    private string ConfigPath => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    private DriverConfig Written(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        File.WriteAllText(ConfigPath, json);
        return DriverConfig.Load(ConfigPath);
    }

    private void Journaled(string kind, string outcome, DateTimeOffset at, string repository = "Aurora", string workspace = "work") =>
        RiskJournal.Append(_home, new RiskJudged(
            at, RiskJudged.Opened, $"k-{Guid.NewGuid():N}"[..8], "hold", kind, null, "person", workspace, repository, RiskPlace.RestrictedMode, "q1", "step", "subject",
            outcome, outcome == RiskVerdict.Pass ? RiskClasses.None : RiskClasses.Role, null, RiskVerdict.Seed, "r1", "reason", "today", true));

    [Fact]
    public void Each_kind_has_its_class_its_source_who_answers_and_seven_days_of_passes_and_gates_at_the_place()
    {
        var config = Written("""{"risk":{"repository/Aurora":[{"kind":"spend.money","class":"owner"},{"kind":"line.land","class":"role","by":"reviewer"}]}}""");
        Journaled(RiskActs.LineLand, RiskVerdict.Pass, Now.AddDays(-1));
        Journaled(RiskActs.LineLand, RiskVerdict.Pass, Now.AddDays(-6));
        Journaled(RiskActs.LineLand, RiskVerdict.Gate, Now.AddDays(-2));
        Journaled(RiskActs.LineLand, RiskVerdict.Pass, Now.AddDays(-8));
        Journaled(RiskActs.LineLand, RiskVerdict.Pass, Now.AddDays(-1), repository: "Other");

        var rows = RiskActTable.Rows(config, _home, Now, "work", "Aurora");

        Assert.Equal(RiskActs.All, rows.Select(row => row.Kind));
        var money = rows.Single(row => row.Kind == RiskActs.SpendMoney);
        Assert.Equal((RiskClasses.Owner, "repository/Aurora"), (money.Class, money.Source));
        Assert.Null(money.Answers);
        var land = rows.Single(row => row.Kind == RiskActs.LineLand);
        Assert.Equal((RiskClasses.Role, "reviewer", 2, 1), (land.Class, land.By, land.Passes, land.Gates));
        Assert.Equal("reviewer", land.Answers!.Role);
        var seeded = rows.Single(row => row.Kind == RiskActs.WorkDone);
        Assert.Equal((RiskClasses.None, RiskVerdict.Seed, 0, 0), (seeded.Class, seeded.Source, seeded.Passes, seeded.Gates));
    }

    [Fact]
    public void Policy_change_is_locked_and_the_cast_says_who_holds_a_role_row()
    {
        var config = Written("""{"risk":{"machine":[{"kind":"spend.money","class":"role"}]}}""");
        Assert.Null(new PlaceCastFiles(_home, () => true).Cast("machine", "supervisor", "agent:codex", "test", Now));

        var rows = RiskActTable.Rows(config, _home, Now, null, null);

        Assert.True(rows.Single(row => row.Kind == RiskActs.PolicyChange).Locked);
        Assert.All(rows.Where(row => row.Kind != RiskActs.PolicyChange), row => Assert.False(row.Locked));
        var money = rows.Single(row => row.Kind == RiskActs.SpendMoney);
        Assert.Equal(("supervisor", "agent:codex"), (money.Answers!.Role, money.Answers.Holder));
    }

    [Fact]
    public void A_policy_that_does_not_read_says_so_and_reads_every_act_at_the_owner()
    {
        var config = Written("""{"risk":{"nowhere":[]}}""");

        var table = RiskActTable.Of(config, _home, Now, null, null);

        Assert.NotNull(table.Fault);
        Assert.All(table.Rows, row => Assert.Equal(RiskClasses.Owner, row.Class));
    }

    [Fact]
    public void The_terminal_prints_the_rows_the_window_reads_and_names_the_waits_a_change_opens()
    {
        var config = Written("""{"risk":{"repository/Aurora":[{"kind":"spend.money","class":"owner"}]}}""");
        var table = RiskActTable.Of(config, _home, Now, "work", "Aurora");

        var lines = RiskActTable.Lines(table);

        foreach (var row in table.Rows) Assert.Contains(lines, line => line.Contains(row.Kind, StringComparison.Ordinal) && line.Contains(row.Class, StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("spend.money", StringComparison.Ordinal) && line.Contains("repository/Aurora", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("policy.change", StringComparison.Ordinal) && line.Contains("locked", StringComparison.Ordinal));
        _ = JsonSerializer.Serialize(table.Rows);
    }

    [Fact]
    public void A_pending_edit_previews_the_waits_it_opens_and_releases_beside_the_table()
    {
        var config = Written("{}");

        var shifts = RiskActTable.Preview(config, "repository/Aurora", "work", [new PolicyEdit("repository/Aurora", "release.start", RiskClasses.Owner, null, null)]);

        Assert.Contains(shifts, shift => shift is { Act: "release.start", Effect: PolicyShift.Opens });
    }
}
