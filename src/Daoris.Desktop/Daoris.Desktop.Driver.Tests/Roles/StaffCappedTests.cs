using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A role's policy skips a choice whose agent's accounts a limit or a spend cap holds, until the reset the account's cool-off says, and
/// leaves its weight as written. The judge is pure (the fact in, the pass and its reason out); the ledger reads the fact from the one
/// record of cool-offs, <c>cooling.json</c>, never a store of its own.
/// </summary>
public sealed class StaffCappedTests : IDisposable
{
    private static readonly ActorChoice Claude = new("claude-code", "opus", "high");

    private static readonly ActorChoice Codex = new("codex-acp", "gpt", null);

    private static readonly DateTimeOffset Now = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Until = Now.AddHours(5);

    private static readonly IReadOnlyList<ActorEffective> Order =
        [new(Claude.Agent, Claude.Model, Claude.Effort, "1"), new(Codex.Agent, Codex.Model, Codex.Effort, "2")];

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "staff-capped", Guid.NewGuid().ToString("N")[..8]);

    private readonly MovedClock _clock = new();

    public StaffCappedTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private static StaffPolicy Weights(int claude, int codex) => new(
        StaffRules.Weight, [new StaffChoice(StaffKey.Of(Claude), claude), new StaffChoice(StaffKey.Of(Codex), codex)], new Dictionary<string, StaffMove>());

    private static StaffFacts Facts(params (string Agent, DateTimeOffset Until)[] capped) =>
        new() { Now = Now, Capped = capped.ToDictionary(each => each.Agent, each => each.Until) };

    [Fact]
    public void A_capped_choice_is_passed_with_its_reset_said_and_the_walk_goes_to_the_next_with_the_skip_in_its_reason()
    {
        var arranged = StaffJudge.Arrange(Weights(2, 1), RoleWork.Build, Order, Facts((Claude.Agent, Until)));

        Assert.Equal([("1", StaffPasses.Capped)], arranged.Passed.Select(each => (each.Entry.From, each.Code)));
        Assert.Contains("capped until 2026-11-16 17:00", arranged.Passed[0].Why, StringComparison.Ordinal);
        Assert.Equal("2", Assert.Single(arranged.Walk).Entry.From);
        Assert.StartsWith("choice 1 skipped: its account is capped until 2026-11-16 17:00", arranged.Walk[0].Said, StringComparison.Ordinal);
    }

    [Fact]
    public void A_reset_that_has_passed_or_no_cap_passes_nothing_and_the_weights_decide_as_written()
    {
        Assert.Empty(StaffJudge.Arrange(Weights(2, 1), RoleWork.Build, Order, Facts((Claude.Agent, Now.AddMinutes(-1)))).Passed);
        var arranged = StaffJudge.Arrange(Weights(2, 1), RoleWork.Build, Order, Facts());
        Assert.Empty(arranged.Passed);
        Assert.Equal("1", arranged.Walk[0].Entry.From);
    }

    [Fact]
    public void Every_choice_capped_leaves_the_first_in_the_walk_for_the_account_walk_to_say_so()
    {
        var arranged = StaffJudge.Arrange(Weights(2, 1), RoleWork.Build, Order, Facts((Claude.Agent, Until), (Codex.Agent, Until)));

        Assert.Single(arranged.Walk);
        Assert.Single(arranged.Passed);
    }

    /// <summary>The home's one cooling record, with the policy saved the way the role door saves it.</summary>
    private StaffLedger Staffed(StaffPolicy policy)
    {
        var file = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, """{ "adapter": "claude-code", "actors": { "build": [ { "agent": "claude-code", "model": "opus", "effort": "high" }, { "agent": "codex-acp", "model": "gpt" } ] } }""");
        Assert.Equal(StaffingImported.Done, new RoleStaffing(_home, () => StoreSettled.Yes).ImportFile().How);
        using var store = DesktopStore.Open(DesktopStore.LocationIn(_home), new DesktopStoreOptions { Settled = () => StoreSettled.Yes });
        store.Write(StaffPolicies.Family, write => StaffPolicies.Save(write, "build", policy, [Claude, Codex], 0, Now.ToString("o", System.Globalization.CultureInfo.InvariantCulture)));
        return new StaffLedger(_home, () => StoreSettled.Yes, _clock, TimeZoneInfo.Utc);
    }

    [Fact]
    public void The_ledger_reads_a_capped_agent_from_the_account_cool_offs_and_skips_it_until_the_reset()
    {
        var ledger = Staffed(Weights(2, 1));
        var now = _clock.GetUtcNow();
        AccountCooling.Cool(_home, new CoolingEntry("claude-code", null, now.AddHours(5), true, "weekly", now, "s-1"), now);

        var take = ledger.Preview("build", RoleWork.Build, Order);

        Assert.Equal([("1", StaffPasses.Capped)], take.Arrangement.Passed.Select(each => (each.Entry.From, each.Code)));
        Assert.Equal("2", Assert.Single(take.Arrangement.Walk).Entry.From);
        // The weights stay as written.
        Assert.Equal(Weights(2, 1).Write(), ledger.Policy("build").Policy!.Write());

        _clock.Advance(TimeSpan.FromHours(6));
        var after = ledger.Preview("build", RoleWork.Build, Order);
        Assert.Empty(after.Arrangement.Passed);
        Assert.Equal("1", after.Arrangement.Walk[0].Entry.From);
    }
}
