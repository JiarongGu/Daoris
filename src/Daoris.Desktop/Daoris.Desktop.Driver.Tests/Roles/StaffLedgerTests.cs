using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// STAFFPOLICY1a (D226; the staff policy design §2.1, §3.2, §4.2, §11): a role's policy in <c>role_policy</c>, revisioned and bound to its
/// staffing by the digest, and the ledger of its new starts, its reservations under one lock and the health mark. Nothing reads them at
/// a start yet.
/// </summary>
/// <remarks>
/// Every case has its own scratch home under <c>_fixtures/</c>, its staffing imported from a <c>driver.json</c> as a first start imports it,
/// and a clock the test moves. "An older build" is a store opened with the fifteen steps before the policy and their writer stamp, running
/// that build's own statements of the staffing.
/// </remarks>
public sealed class StaffLedgerTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "staff-ledger", Guid.NewGuid().ToString("N")[..8]);

    private readonly MovedClock _clock = new();

    private static readonly ActorChoice Sonnet = new("claude-code", "sonnet", "high");

    private static readonly ActorChoice Opus = new("claude-code", "opus", "high");

    private static readonly ActorChoice Codex = new("codex", null, "medium");

    private static readonly IReadOnlyList<ActorEffective> Order = [new(Sonnet.Agent, Sonnet.Model, Sonnet.Effort, "1"), new(Opus.Agent, Opus.Model, Opus.Effort, "2")];

    public StaffLedgerTests() => Directory.CreateDirectory(_home);

    /// <summary>Build staffed by sonnet then opus, imported from the home's <c>driver.json</c> as a first start imports it.</summary>
    private void Import()
    {
        var file = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file,
            """
            { "adapter": "claude-code", "actors": { "build": [ { "agent": "claude-code", "model": "sonnet", "effort": "high" }, { "agent": "claude-code", "model": "opus", "effort": "high" } ] } }
            """);
        Assert.Equal(StaffingImported.Done, new RoleStaffing(_home, () => StoreSettled.Yes).ImportFile().How);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private string Db => DesktopStore.LocationIn(_home);

    private StaffLedger Ledger(TimeZoneInfo? zone = null) => new(_home, () => StoreSettled.Yes, _clock, zone ?? TimeZoneInfo.Utc);

    private string Now => _clock.GetUtcNow().ToString("o", System.Globalization.CultureInfo.InvariantCulture);

    private static StaffPolicy Weighted(int one, int two, IReadOnlyDictionary<string, StaffMove>? moves = null) => new(
        StaffRules.Weight, [new StaffChoice(StaffKey.Of(Sonnet), one), new StaffChoice(StaffKey.Of(Opus), two)], moves ?? new Dictionary<string, StaffMove>());

    private long Save(StaffPolicy policy, long read, IReadOnlyList<ActorChoice>? choices = null)
    {
        if (!File.Exists(DaorisHome.Entry(_home, HomeEntry.DriverConfig))) Import();
        using var store = DesktopStore.Open(Db, new DesktopStoreOptions { Settled = () => StoreSettled.Yes });
        return store.Write(StaffPolicies.Family, write => StaffPolicies.Save(write, "build", policy, choices ?? [Sonnet, Opus], read, Now));
    }

    private void Start(string session, ActorChoice choice, StaffLedger? ledger = null) =>
        Assert.True((ledger ?? Ledger()).Record(new StaffStart(session, "build", StaffKey.Of(choice), StaffBy.Weight)));

    /// <summary>The staffing's own statements of a role's choices, as the projection writes them: no re-key of the policy.</summary>
    private static void Staff(DesktopStore store, params ActorChoice[] choices) => store.Write(RoleStaffing.Family, write =>
    {
        write.Execute("DELETE FROM role_staffing WHERE role = 'build'");
        write.Execute(
            "INSERT INTO role_staffing (role, state, choices, why, first_agent, at) VALUES ('build', 'set', $choices, NULL, 'claude-code', '2026-11-02T00:00:00.0000000+00:00')",
            ("$choices", RoleStaffing.Write(choices)));
    });

    // ——— The policy's record in the store (§2.1).

    [Fact]
    public void A_role_with_no_row_reads_default_and_a_saved_policy_reads_set_with_its_revision_and_since()
    {
        Assert.Equal((StaffPolicyRead.Default, 0L), (Ledger().Policy("build").State, Ledger().Policy("build").Revision));
        Import();
        Assert.Equal((StaffPolicyRead.Default, 0L, (string?)null), (Ledger().Policy("build").State, Ledger().Policy("build").Revision, Ledger().Policy("build").Since));

        var saved = Weighted(3, 1);
        Assert.Equal(1, Save(saved, read: 0));

        var read = Ledger().Policy("build");
        Assert.Equal((StaffPolicyRead.Set, 1L, Now), (read.State, read.Revision, read.Since));
        Assert.Equal(saved.Write(), read.Policy!.Write());
        Assert.Same(read.Policy, read.Applied);
        using var reader = DesktopStoreReader.Open(Db)!;
        Assert.Equal(
            [(1L, saved.Write(), StaffPolicy.Digest([Sonnet, Opus]))],
            reader.Query("SELECT revision, policy, staffing FROM role_policy_revisions WHERE role = 'build'", row => (row.GetInt64(0), row.GetString(1), row.GetString(2))));
    }

    [Fact]
    public void A_save_against_a_revision_that_moved_since_it_was_read_is_refused_and_writes_nothing()
    {
        Save(Weighted(3, 1), read: 0);

        var refused = Assert.Throws<RoleRefusedException>(() => Save(Weighted(1, 1), read: 0));

        Assert.Equal(StaffPolicy.Code, refused.Code);
        Assert.Contains("changed since you read it", refused.Message);
        Assert.Equal(Weighted(3, 1).Write(), Ledger().Policy("build").Policy!.Write());
        Assert.Equal(1, Ledger().Policy("build").Revision);
    }

    [Fact]
    public void A_save_of_a_weight_or_the_rule_starts_the_window_afresh_and_one_of_a_limit_or_a_move_does_not()
    {
        Save(Weighted(1, 1), read: 0);
        var first = Ledger().Policy("build").Since;
        _clock.Advance(TimeSpan.FromMinutes(1));
        Start("s1", Sonnet);
        Start("s2", Sonnet);
        Start("s3", Opus);

        _clock.Advance(TimeSpan.FromMinutes(1));
        Save(Weighted(1, 1, new Dictionary<string, StaffMove> { [StaffConditions.Limit] = new(StaffActions.Weighted) }) with { Most = 2 }, read: 1);
        Save(new StaffPolicy(StaffRules.Weight, [new StaffChoice(StaffKey.Of(Sonnet), Share: 60), new StaffChoice(StaffKey.Of(Opus))], new Dictionary<string, StaffMove>()), read: 2);
        Assert.Equal(first, Ledger().Policy("build").Since);
        Assert.Equal(3, Ledger().Take("build", RoleWork.Build, Order).Facts.Window.Count);

        _clock.Advance(TimeSpan.FromMinutes(1));
        Save(Weighted(2, 1), read: 3);
        Assert.Equal(Now, Ledger().Policy("build").Since);
        var afresh = Ledger().Take("build", RoleWork.Build, Order);
        Assert.Empty(afresh.Facts.Window);
        Assert.Equal("1", afresh.Arrangement.Walk[0].Entry.From);

        _clock.Advance(TimeSpan.FromMinutes(1));
        Save(new StaffPolicy(StaffRules.Order, Weighted(2, 1).Choices, new Dictionary<string, StaffMove>()), read: 4);
        Assert.Equal(Now, Ledger().Policy("build").Since);
    }

    [Fact]
    public void Deleting_a_policy_keeps_its_revisions_and_reads_default_at_the_deletion_s_revision()
    {
        Save(Weighted(3, 1), read: 0);
        using (var store = DesktopStore.Open(Db, new DesktopStoreOptions { Settled = () => StoreSettled.Yes }))
        {
            Assert.True(store.Write(StaffPolicies.Family, write => StaffPolicies.Delete(write, "build", Now)));
            Assert.False(store.Write(StaffPolicies.Family, write => StaffPolicies.Delete(write, "build", Now)));
        }

        var read = Ledger().Policy("build");
        Assert.Equal((StaffPolicyRead.Default, 2L), (read.State, read.Revision));
        Assert.Null(read.Applied);
        Assert.Equal(3, Save(Weighted(1, 1), read: 2));
    }

    // ——— The digest: Daoris's writes that leave the choices as they were keep the policy; a change to them leaves it stale (§2.1, §11).

    [Fact]
    public void The_staffing_written_again_unchanged_keeps_the_policy_and_a_change_to_the_choices_leaves_it_stale_and_unapplied()
    {
        Save(Weighted(3, 1), read: 0);
        using var store = DesktopStore.Open(Db, new DesktopStoreOptions { Settled = () => StoreSettled.Yes });

        Staff(store, Sonnet, Opus);
        Assert.Equal(StaffPolicyRead.Set, Ledger().Policy("build").State);

        Staff(store, Opus, Sonnet);
        var stale = Ledger().Policy("build");
        Assert.Equal(StaffPolicyRead.Stale, stale.State);
        Assert.Contains("set for other choices", stale.Why);
        Assert.Equal(Weighted(3, 1).Write(), stale.Policy!.Write());
        Assert.Null(stale.Applied);
        var walked = Ledger().Take("build", RoleWork.Build, Order);
        Assert.Equal([("1", StaffBy.First), ("2", StaffBy.First)], walked.Arrangement.Walk.Select(each => (each.Entry.From, each.By)));
        Assert.Null(walked.Reservation);

        Staff(store, Sonnet, Opus, Codex);
        Assert.Equal(StaffPolicyRead.Stale, Ledger().Policy("build").State);
        store.Write(RoleStaffing.Family, write => write.Execute("UPDATE role_staffing SET state = 'before', choices = NULL WHERE role = 'build'"));
        Assert.Contains("no choices of its own", Ledger().Policy("build").Why);
    }

    [Fact]
    public void An_older_build_reads_the_staffing_under_a_policy_and_its_every_write_of_it_is_refused_leaving_the_policy_set()
    {
        Save(Weighted(3, 1), read: 0);

        using var older = DesktopStore.Open(Db, new DesktopStoreOptions { Writer = DesktopStore.DeliveryWriter, Steps = [.. DesktopStore.Steps.Take(15)], Settled = () => StoreSettled.Yes });

        Assert.True(older.Newer);
        Assert.Equal(RoleStaffing.Write([Sonnet, Opus]), older.Query("SELECT choices FROM role_staffing WHERE role = 'build'", row => row.GetString(0))[0]);
        var refused = Assert.Throws<DesktopStoreNewerException>(() => Staff(older, Opus, Sonnet));
        Assert.Equal((DesktopStore.PolicyWriter, DesktopStore.DeliveryWriter), (refused.Stamped, refused.Own));
        Assert.Equal(StaffPolicyRead.Set, Ledger().Policy("build").State);
    }

    [Fact]
    public void An_older_build_that_opened_the_store_before_the_policy_s_step_is_refused_at_its_next_write_of_the_staffing()
    {
        using var older = DesktopStore.Open(Db, new DesktopStoreOptions { Writer = DesktopStore.DeliveryWriter, Steps = [.. DesktopStore.Steps.Take(15)], Settled = () => StoreSettled.Yes });
        Assert.Equal((15, 2), older.Stamp);
        Assert.Equal(StaffPolicyRead.Default, Ledger().Policy("build").State);

        using (DesktopStore.Open(Db, new DesktopStoreOptions { Settled = () => StoreSettled.Yes })) { }

        Assert.Throws<DesktopStoreNewerException>(() => older.Write(RoleStaffing.Family, _ => { }));
    }

    [Fact]
    public void A_store_before_the_policy_s_step_reads_default_and_records_no_start()
    {
        using (var older = DesktopStore.Open(Db, new DesktopStoreOptions { Writer = DesktopStore.DeliveryWriter, Steps = [.. DesktopStore.Steps.Take(15)], Settled = () => StoreSettled.Yes }))
        {
            older.Write("probe", _ => { });
        }

        var unsettled = new StaffLedger(_home, () => StoreSettled.Not("the swap journal is at `started`"), _clock, TimeZoneInfo.Utc);

        Assert.Equal(StaffPolicyRead.Default, unsettled.Policy("build").State);
        Assert.False(unsettled.Record(new StaffStart("s1", "build", StaffKey.Of(Sonnet), StaffBy.First)));
        using var reader = DesktopStoreReader.Open(Db)!;
        Assert.Equal((15, 2), (reader.Version, reader.Writer));
    }

    // ——— The ledger: new starts, the window, the day and the reservations (§3.2).

    [Fact]
    public void The_facts_hold_the_new_starts_since_the_policy_in_order_and_a_session_is_counted_once()
    {
        Import();
        Start("before", Opus);
        _clock.Advance(TimeSpan.FromSeconds(1));
        Save(Weighted(1, 1), read: 0);
        for (var i = 0; i < 22; i++)
        {
            _clock.Advance(TimeSpan.FromSeconds(1));
            Start($"s{i}", i < 2 ? Opus : Sonnet);
        }

        Start("s0", Sonnet);

        var facts = Ledger().Take("build", RoleWork.Build, Order).Facts;
        // The sequence the balances are replayed from (§11 point 9); a share reads its last twenty.
        Assert.Equal(22, facts.Window.Count);
        Assert.Equal([StaffKey.Of(Opus), StaffKey.Of(Opus)], facts.Window.Take(2));
        Assert.All(facts.Window.Skip(2), key => Assert.Equal(StaffKey.Of(Sonnet), key));
        using var reader = DesktopStoreReader.Open(Db)!;
        Assert.Equal(23L, reader.Query("SELECT COUNT(*) FROM role_starts", row => row.GetInt64(0))[0]);
        Assert.Equal(("build", StaffKey.Of(Opus).Text, StaffBy.Weight), reader.Query(
            "SELECT role, choice, by FROM role_starts WHERE session = 's0'", row => (row.GetString(0), row.GetString(1), row.GetString(2)))[0]);
    }

    [Fact]
    public void Today_s_starts_are_counted_in_the_machine_s_local_day_and_an_edit_never_refills_them()
    {
        var east = TimeZoneInfo.CreateCustomTimeZone("staff-east", TimeSpan.FromHours(8), "staff-east", "staff-east");
        Save(Weighted(1, 1) with { Choices = [new StaffChoice(StaffKey.Of(Sonnet), Budget: new StaffBudget(Starts: 2)), new StaffChoice(StaffKey.Of(Opus))] }, read: 0);

        // 09:00 UTC is 17:00 in the east: a start at 15:59 UTC is the east's 23:59, yesterday's by 16:00 UTC.
        _clock.Advance(TimeSpan.FromHours(6) + TimeSpan.FromMinutes(59));
        Start("late", Sonnet);
        _clock.Advance(TimeSpan.FromMinutes(2));
        Start("early", Sonnet);

        var ledger = Ledger(east);
        Assert.Equal(1, ledger.Preview("build", RoleWork.Build, Order).Facts.Today[StaffKey.Of(Sonnet)]);

        Save(Weighted(1, 1) with { Choices = [new StaffChoice(StaffKey.Of(Sonnet), Weight: 3, Budget: new StaffBudget(Starts: 2)), new StaffChoice(StaffKey.Of(Opus))] }, read: 1);
        Start("again", Sonnet, ledger);
        var over = ledger.Take("build", RoleWork.Build, Order);
        Assert.Equal(2, over.Facts.Today[StaffKey.Of(Sonnet)]);
        Assert.Equal([("1", StaffPasses.OverBudget)], over.Arrangement.Passed.Select(each => (each.Entry.From, each.Code)));
    }

    [Fact]
    public void Two_reservations_in_one_look_land_on_different_choices_under_1_to_1_and_a_preview_reserves_nothing()
    {
        Save(Weighted(1, 1), read: 0);
        var ledger = Ledger();

        var preview = ledger.Preview("build", RoleWork.Build, Order);
        var first = ledger.Take("build", RoleWork.Build, Order);
        var second = ledger.Take("build", RoleWork.Build, Order);

        Assert.Null(preview.Reservation);
        Assert.Equal("1", preview.Arrangement.Walk[0].Entry.From);
        Assert.Equal((StaffKey.Of(Sonnet), StaffKey.Of(Opus)), (first.Reservation!.Choice, second.Reservation!.Choice));
        Assert.Equal(1, second.Facts.Reserved[StaffKey.Of(Sonnet)]);
        Assert.Equal(2, ledger.Preview("build", RoleWork.Build, Order).Facts.Reserved.Values.Sum());
    }

    [Fact]
    public void A_refusal_gives_its_reservation_back_a_settled_selection_moves_it_and_an_open_records_the_start_in_its_place()
    {
        Save(Weighted(1, 1), read: 0);
        var ledger = Ledger();

        var refused = ledger.Take("build", RoleWork.Build, Order);
        ledger.Release(refused.Reservation!);
        ledger.Release(refused.Reservation!);
        Assert.Empty(ledger.Preview("build", RoleWork.Build, Order).Facts.Reserved);

        // The top pick's accounts all cool: the walk takes choice 2, and the reservation moves with it.
        var taken = ledger.Take("build", RoleWork.Build, Order);
        Assert.Equal(StaffKey.Of(Sonnet), taken.Reservation!.Choice);
        ledger.Settle(taken.Reservation, Order[1]);
        Assert.Equal(StaffKey.Of(Opus), taken.Reservation.Choice);
        Assert.Equal(1, Assert.Single(ledger.Preview("build", RoleWork.Build, Order).Facts.Reserved, each => each.Key == StaffKey.Of(Opus)).Value);
        Assert.Single(ledger.Preview("build", RoleWork.Build, Order).Facts.Reserved);

        Assert.True(ledger.Record(new StaffStart("s1", "build", taken.Reservation.Choice, StaffBy.Weight), taken.Reservation));
        var after = ledger.Preview("build", RoleWork.Build, Order);
        Assert.Empty(after.Facts.Reserved);
        Assert.Equal([StaffKey.Of(Opus)], after.Facts.Window);
    }

    [Fact]
    public void A_restart_drops_the_reservations_and_keeps_the_starts()
    {
        Save(Weighted(1, 1), read: 0);
        var before = Ledger();
        before.Take("build", RoleWork.Build, Order);
        Start("s1", Opus, before);

        var after = Ledger();

        var facts = after.Preview("build", RoleWork.Build, Order).Facts;
        Assert.Empty(facts.Reserved);
        Assert.Equal([StaffKey.Of(Opus)], facts.Window);
    }

    // ——— The health mark (§4.2).

    [Fact]
    public void An_agent_marked_not_answering_is_held_fifteen_minutes_or_until_a_prompt_on_it_answers_and_a_restart_forgets_it()
    {
        var ledger = Ledger();

        ledger.MarkNotAnswering("codex");
        Assert.Equal(_clock.GetUtcNow() + StaffLedger.Mark, ledger.Preview("build", RoleWork.Build, Order).Facts.NotAnswering["codex"]);
        _clock.Advance(StaffLedger.Mark);
        Assert.Empty(ledger.Preview("build", RoleWork.Build, Order).Facts.NotAnswering);

        ledger.MarkNotAnswering("Codex");
        ledger.Answered("codex");
        Assert.Empty(ledger.Preview("build", RoleWork.Build, Order).Facts.NotAnswering);

        ledger.MarkNotAnswering("codex");
        Assert.Empty(Ledger().Preview("build", RoleWork.Build, Order).Facts.NotAnswering);
    }

    [Fact]
    public void The_line_s_own_facts_are_kept_and_the_ledger_s_read_over_them()
    {
        Save(Weighted(1, 1, new Dictionary<string, StaffMove> { [StaffConditions.Transient] = new(StaffActions.Weighted) }), read: 0);

        var take = Ledger().Take("build", RoleWork.Build, Order, new StaffFacts
        {
            MovedOff = new Dictionary<StaffKey, DateTimeOffset> { [StaffKey.Of(Sonnet)] = _clock.GetUtcNow().AddMinutes(-5) },
            Window = [StaffKey.Of(Opus), StaffKey.Of(Opus)],
        });

        Assert.Empty(take.Facts.Window);
        Assert.Equal(_clock.GetUtcNow(), take.Facts.Now);
        Assert.Equal([("1", StaffPasses.MovedOff)], take.Arrangement.Passed.Select(each => (each.Entry.From, each.Code)));
        Assert.Equal(StaffKey.Of(Opus), take.Reservation!.Choice);
    }
}
