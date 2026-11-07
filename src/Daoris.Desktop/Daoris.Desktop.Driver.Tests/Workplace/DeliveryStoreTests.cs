using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The delivery record, a desktop-store family over no git and no plugin. A delivery kept
/// whole with its frozen intent and authorization under its key; concurrent starts for one unit making one; an unreconciled delivery blocking
/// a new one for its unit until it settles; each phase written ahead and its outcome after, in order, an entry without its outcome read as such
/// after a reopen; a take only from a dead run; a rollback refused while any delivery is unresolved, and no delivery started in the start's
/// rollback window; an older build's every write refused under the raised stamp.
/// </summary>
/// <remarks>Each case has its own scratch home under <c>_fixtures/</c>; "another process" is a second store under a run of its own, and a dead run is the liveness the case hands in.</remarks>
public sealed class DeliveryStoreTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "delivery-store", Guid.NewGuid().ToString("N")[..8]);

    public DeliveryStoreTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private DeliveryStore Store(string run, Func<StoreRun, bool>? alive = null, Func<StoreSettled>? settled = null) =>
        new(_home, settled ?? (() => StoreSettled.Yes), alive, run, StoreProcess.Shell);

    private static Func<StoreRun, bool> DeadAre(params string[] runs) => run => !runs.Contains(run.Run);

    private static DeliveryIntent Intent(string tip = "a1b2c3d4e5", string method = DeliveryMethods.Branch, string unit = "unit-1") => new(
        unit, "engine", tip, method, "main", "main",
        method == DeliveryMethods.Merge ? null : "feature/ENG-12-tidy", method == DeliveryMethods.Merge ? null : "ENG-12",
        "Tidy the readme (ENG-12)", method is DeliveryMethods.Push or DeliveryMethods.PullRequest ? "forge" : null, "epoch-7");

    private static DeliveryAuthorization Accepted() =>
        DeliveryAuthorization.Person("accept-3", "r3", "graph-digest", """{"method":"branch","target":"main"}""", """["opinion:o1","look:l2"]""");

    private string Db => DesktopStore.LocationIn(_home);

    private long Count(string table)
    {
        using var reader = DesktopStoreReader.Open(Db)!;
        return reader.Query($"SELECT COUNT(*) FROM {table}", row => row.GetInt64(0))[0];
    }

    /// <summary>A delivery walked to its method's reach: prepared, the ref, the record and, for a plugin's method, its dispatch and answer.</summary>
    private static void Reach(DeliveryStore store, DeliveryClaim claim, bool plugin = false)
    {
        string[] phases = plugin
            ? [DeliveryPhases.Prepared, DeliveryPhases.RefWritten, DeliveryPhases.Recorded, DeliveryPhases.PluginDispatched, DeliveryPhases.PluginAnswered]
            : [DeliveryPhases.Prepared, DeliveryPhases.RefWritten, DeliveryPhases.Recorded];
        foreach (var phase in phases)
        {
            store.Begin(claim, phase, $"{phase} intended");
            store.End(claim, phase, DeliveryOutcomes.Done, $"{phase} read", $"{phase} done");
        }
    }

    // ——— The record and its key.

    [Fact]
    public void A_delivery_is_kept_whole_with_its_frozen_intent_and_the_person_s_accept_under_its_key()
    {
        var store = Store("run-a");

        var start = store.Start(Intent(), Accepted());

        Assert.True(start.Started);
        Assert.Null(start.Refused);
        var key = DeliveryStore.KeyOf("unit-1", "a1b2c3d4e5", DeliveryMethods.Branch, "main");
        Assert.Equal(new DeliveryClaim(key, "run-a", 1), start.Claim);
        var kept = store.Delivery(key)!;
        Assert.Equal(Intent(), kept.Intent);
        Assert.Equal(Accepted(), kept.Authorization);
        Assert.Equal((DeliveryStore.Format, "run-a", 1L, true, false), (kept.Format, kept.ClaimRun, kept.ClaimGeneration, kept.Unresolved, kept.Held));
        Assert.Null(kept.Settled);
        Assert.Equal(kept, start.Delivery);
    }

    [Fact]
    public void The_key_is_a_digest_of_the_unit_the_tip_the_method_and_the_target_and_of_nothing_else()
    {
        var key = DeliveryStore.KeyOf("unit-1", "tip", "branch", "main");

        Assert.Matches("^[0-9a-f]{64}$", key);
        Assert.Equal(key, (Intent("tip") with { Message = "another message", Line = "release", Plugin = "forge", Epoch = "epoch-8", Branch = null }).Key);
        Assert.Equal(4, new[]
        {
            DeliveryStore.KeyOf("unit-2", "tip", "branch", "main"), DeliveryStore.KeyOf("unit-1", "tip2", "branch", "main"),
            DeliveryStore.KeyOf("unit-1", "tip", "push", "main"), DeliveryStore.KeyOf("unit-1", "tip", "branch", "release"),
        }.Where(each => each != key).Distinct().Count());

        // Each part is length-prefixed, so no two parts joined read as another pair.
        Assert.NotEqual(DeliveryStore.KeyOf("ab", "c", "branch", "main"), DeliveryStore.KeyOf("a", "bc", "branch", "main"));
    }

    [Fact]
    public void The_standing_automatic_authority_is_kept_with_its_scope()
    {
        var store = Store("run-a");
        var automatic = DeliveryAuthorization.Automatic("repository:engine", "r3", "graph-digest", "{}", "[]");

        var start = store.Start(Intent(), automatic);

        var kept = store.Delivery(start.Claim!.Key)!.Authorization;
        Assert.Equal((DeliveryAuthorities.Automatic, "repository:engine", (string?)null), (kept.Authority, kept.Scope, kept.Accept));
        Assert.Equal(automatic, kept);
    }

    [Fact]
    public void A_start_that_does_not_hold_is_refused_and_writes_nothing()
    {
        var store = Store("run-a");

        Assert.Contains("person's Accept", Assert.Throws<DeliveryRefusedException>(() => store.Start(Intent(), Accepted() with { Accept = null })).Why);
        Assert.Contains("scope", Assert.Throws<DeliveryRefusedException>(() => store.Start(Intent(), Accepted() with { Authority = DeliveryAuthorities.Automatic, Accept = null })).Why);
        Assert.Contains("`someone`", Assert.Throws<DeliveryRefusedException>(() => store.Start(Intent(), Accepted() with { Authority = "someone" })).Why);
        Assert.Contains("`rebase`", Assert.Throws<DeliveryRefusedException>(() => store.Start(Intent(method: "rebase"), Accepted())).Why);
        Assert.Contains("plugin", Assert.Throws<DeliveryRefusedException>(() => store.Start(Intent(method: DeliveryMethods.Push) with { Plugin = null }, Accepted())).Why);
        Assert.Contains("tip", Assert.Throws<DeliveryRefusedException>(() => store.Start(Intent(tip: " "), Accepted())).Why);

        Assert.Empty(store.Unresolved());
        Assert.False(File.Exists(Db));
    }

    [Fact]
    public void The_methods_are_the_land_node_s()
    {
        // The land node lands by three methods; the store also reads the two a delivery recorded as a plugin's push or pull request.
        Assert.Equal(LandNodes.Methods, DeliveryMethods.All.Where(method => method is not (DeliveryMethods.Push or DeliveryMethods.PullRequest)));
        Assert.Equal(DeliveryPhases.PluginAnswered, DeliveryPhases.ReachOf(DeliveryMethods.Push));
        Assert.Equal(DeliveryPhases.PluginAnswered, DeliveryPhases.ReachOf(DeliveryMethods.PullRequest));
        Assert.All(new[] { DeliveryMethods.Merge, DeliveryMethods.Branch, DeliveryMethods.Integrate }, method => Assert.Equal(DeliveryPhases.Recorded, DeliveryPhases.ReachOf(method)));
    }

    // ——— One delivery per unit at a time (the fence's step 1).

    [Fact]
    public async Task Concurrent_starts_for_one_unit_make_one_delivery()
    {
        var gate = new ManualResetEventSlim();
        var starts = Enumerable.Range(0, 6).Select(each => Task.Run(() =>
        {
            gate.Wait();
            return Store($"run-{each}").Start(Intent(), Accepted());
        })).ToArray();

        gate.Set();
        var answers = await Task.WhenAll(starts);

        var started = Assert.Single(answers, each => each.Started);
        Assert.All(answers, each => Assert.Equal(started.Claim!.Key, each.Delivery!.Key));
        Assert.All(answers.Where(each => !each.Started), each => Assert.Null(each.Claim));
        Assert.Equal(1, Count("deliveries"));
        Assert.Equal(started.Claim!.Run, Store("run-x").Delivery(started.Claim.Key)!.ClaimRun);
    }

    [Fact]
    public async Task Concurrent_starts_of_two_tips_for_one_unit_make_one_delivery_and_refuse_the_other()
    {
        var gate = new ManualResetEventSlim();
        var starts = new[] { "tip-one", "tip-two" }.Select((tip, each) => Task.Run(() =>
        {
            gate.Wait();
            return Store($"run-{each}").Start(Intent(tip), Accepted());
        })).ToArray();

        gate.Set();
        var answers = await Task.WhenAll(starts);

        var started = Assert.Single(answers, each => each.Started);
        var refused = Assert.Single(answers, each => !each.Started);
        Assert.Contains("is not settled", refused.Refused);
        Assert.Equal(started.Delivery!.Key, refused.Delivery!.Key);
        Assert.Equal(1, Count("deliveries"));
    }

    [Fact]
    public void An_unreconciled_delivery_blocks_a_new_one_for_its_unit_and_a_new_tip_or_method_starts_only_once_it_settles()
    {
        var store = Store("run-a");
        var first = store.Start(Intent("tip-one"), Accepted()).Claim!;

        var newTip = store.Start(Intent("tip-two"), Accepted());
        var newMethod = store.Start(Intent("tip-one", DeliveryMethods.Push), Accepted());

        Assert.False(newTip.Started);
        Assert.Contains("`unit-1`", newTip.Refused);
        Assert.Contains("is not settled", newTip.Refused);
        Assert.Equal(first.Key, newTip.Delivery!.Key);
        Assert.False(newMethod.Started);
        Assert.True(store.Start(Intent("tip-one", unit: "unit-2"), Accepted()).Started);

        // Its own start again is the same delivery, its claim handed back to the run that holds it.
        var again = store.Start(Intent("tip-one"), Accepted());
        Assert.Equal((false, (string?)null, first), (again.Started, again.Refused, again.Claim));

        Reach(store, first);
        store.Settle(first, DeliverySettlements.Delivered, "landed on feature/ENG-12-tidy");

        Assert.True(store.Start(Intent("tip-two"), Accepted()).Started);

        // The settled key is answered as it settled: one key, one delivery, never repeated.
        var repeat = store.Start(Intent("tip-one"), Accepted());
        Assert.Equal((false, (string?)null, (DeliveryClaim?)null, DeliverySettlements.Delivered), (repeat.Started, repeat.Refused, repeat.Claim, repeat.Delivery!.Settled));
        Assert.Equal(3, Count("deliveries"));
    }

    // ——— The phases: each effect written ahead, its outcome after.

    [Fact]
    public void Phase_entries_are_written_ahead_and_their_outcomes_after_in_order_and_round_trip()
    {
        var store = Store("run-a");
        var claim = store.Start(Intent(), Accepted()).Claim!;

        var prepared = store.Begin(claim, DeliveryPhases.Prepared, null);
        Assert.True(prepared.Open);
        Assert.Equal((claim.Key, 0, DeliveryPhases.Prepared, (string?)null, (string?)null), (prepared.Delivery, prepared.Ordinal, prepared.Phase, prepared.Intended, prepared.Outcome));
        Assert.Contains("has no outcome", Assert.Throws<DeliveryRefusedException>(() => store.Begin(claim, DeliveryPhases.RefWritten, "tip")).Why);
        store.End(claim, DeliveryPhases.Prepared, DeliveryOutcomes.Done, null, "the intent prepared");

        Assert.Contains("after `ref-written`", Assert.Throws<DeliveryRefusedException>(() => store.Begin(claim, DeliveryPhases.Recorded, null)).Why);
        Assert.Contains("`landed`", Assert.Throws<DeliveryRefusedException>(() => store.Begin(claim, "landed", null)).Why);
        store.Begin(claim, DeliveryPhases.RefWritten, "refs/heads/feature/ENG-12-tidy a1b2c3d4e5");
        Assert.Contains("is `ref-written`", Assert.Throws<DeliveryRefusedException>(() => store.End(claim, DeliveryPhases.Prepared, DeliveryOutcomes.Done, null, "again")).Why);
        var written = store.End(claim, DeliveryPhases.RefWritten, DeliveryOutcomes.Done, "a1b2c3d4e5", "the branch holds the tip");
        Assert.Equal(written.EndedAt, store.End(claim, DeliveryPhases.RefWritten, DeliveryOutcomes.Unknown, "other", "again").EndedAt);
        store.Begin(claim, DeliveryPhases.Recorded, "landings.json");
        store.End(claim, DeliveryPhases.Recorded, DeliveryOutcomes.Done, "landing-1", "the landing recorded");

        // A branch landing that names no plugin takes no plugin phase.
        Assert.Contains("names no plugin", Assert.Throws<DeliveryRefusedException>(() => store.Begin(claim, DeliveryPhases.PluginDispatched, "{}")).Why);

        var read = Store("run-b").Phases(claim.Key);
        Assert.Equal(
            [
                (0, DeliveryPhases.Prepared, (string?)null, DeliveryOutcomes.Done, (string?)null, "the intent prepared"),
                (1, DeliveryPhases.RefWritten, "refs/heads/feature/ENG-12-tidy a1b2c3d4e5", DeliveryOutcomes.Done, "a1b2c3d4e5", "the branch holds the tip"),
                (2, DeliveryPhases.Recorded, "landings.json", DeliveryOutcomes.Done, "landing-1", "the landing recorded"),
            ],
            read.Select(each => (each.Ordinal, each.Phase, each.Intended, each.Outcome, each.Evidence, each.Said)));
        Assert.All(read, each => Assert.True(each.EndedAt >= each.BeganAt));
        Assert.All(read, each => Assert.False(each.Open));
    }

    [Fact]
    public void A_withdrawn_phase_is_written_ahead_again_a_done_one_never_and_an_unknown_one_holds_every_later_phase()
    {
        var store = Store("run-a");
        var claim = store.Start(Intent(method: DeliveryMethods.PullRequest), Accepted()).Claim!;
        store.Begin(claim, DeliveryPhases.Prepared, null);
        store.End(claim, DeliveryPhases.Prepared, DeliveryOutcomes.Done, null, "prepared");

        store.Begin(claim, DeliveryPhases.RefWritten, "tip");
        store.End(claim, DeliveryPhases.RefWritten, DeliveryOutcomes.Withdrawn, null, "the branch holds nothing of it");
        var again = store.Begin(claim, DeliveryPhases.RefWritten, "tip");
        Assert.Equal(2, again.Ordinal);
        store.End(claim, DeliveryPhases.RefWritten, DeliveryOutcomes.Done, "tip", "written");
        Assert.Contains("is done", Assert.Throws<DeliveryRefusedException>(() => store.Begin(claim, DeliveryPhases.RefWritten, "tip")).Why);

        store.Begin(claim, DeliveryPhases.Recorded, null);
        store.End(claim, DeliveryPhases.Recorded, DeliveryOutcomes.Done, null, "recorded");
        store.Begin(claim, DeliveryPhases.PluginDispatched, "{\"key\":\"k\"}");
        store.End(claim, DeliveryPhases.PluginDispatched, DeliveryOutcomes.Unknown, null, "the plugin's answer was lost and work/state did not say");

        Assert.Contains("unknown", Assert.Throws<DeliveryRefusedException>(() => store.Begin(claim, DeliveryPhases.PluginDispatched, "{}")).Why);
        Assert.Contains("unknown", Assert.Throws<DeliveryRefusedException>(() => store.Begin(claim, DeliveryPhases.PluginAnswered, null)).Why);
        Assert.True(store.Delivery(claim.Key)!.Unresolved);
    }

    [Fact]
    public void An_entry_without_its_outcome_reads_as_such_after_a_reopen()
    {
        var claim = Store("run-a").Start(Intent(), Accepted()).Claim!;
        var writer = Store("run-a");
        writer.Begin(claim, DeliveryPhases.Prepared, null);
        writer.End(claim, DeliveryPhases.Prepared, DeliveryOutcomes.Done, null, "prepared");
        writer.Begin(claim, DeliveryPhases.RefWritten, "refs/heads/feature/ENG-12-tidy a1b2c3d4e5");

        // The process ends between its write-ahead and its effect's outcome: every connection is gone, and another reads the file.
        SqliteConnection.ClearAllPools();
        var reader = Store("run-b");

        var standing = reader.Phases(claim.Key)[^1];
        Assert.Equal((DeliveryPhases.RefWritten, "refs/heads/feature/ENG-12-tidy a1b2c3d4e5", true), (standing.Phase, standing.Intended, standing.Open));
        Assert.Equal(((DateTimeOffset?)null, (string?)null, (string?)null), (standing.EndedAt, standing.Outcome, standing.Evidence));
        Assert.Equal(claim.Key, Assert.Single(reader.Unresolved()).Key);
        Assert.Equal(claim.Key, reader.UnresolvedFor("unit-1")!.Key);
        Assert.Null(reader.UnresolvedFor("unit-2"));
    }

    // ——— Settling.

    [Fact]
    public void A_delivery_is_delivered_only_once_its_method_s_effects_are_recorded_and_settles_with_no_entry_standing()
    {
        var store = Store("run-a");
        var push = store.Start(Intent(method: DeliveryMethods.Push), Accepted()).Claim!;

        store.Begin(push, DeliveryPhases.Prepared, null);
        Assert.Contains("has no outcome", Assert.Throws<DeliveryRefusedException>(() => store.Settle(push, DeliverySettlements.Stopped, "stopped")).Why);
        store.End(push, DeliveryPhases.Prepared, DeliveryOutcomes.Done, null, "prepared");
        foreach (var phase in new[] { DeliveryPhases.RefWritten, DeliveryPhases.Recorded })
        {
            store.Begin(push, phase, null);
            store.End(push, phase, DeliveryOutcomes.Done, null, phase);
        }

        // A push is reached by the plugin's answer recorded, never by the landing record (§3, *When land is reached*).
        Assert.Contains("`plugin-answered`", Assert.Throws<DeliveryRefusedException>(() => store.Settle(push, DeliverySettlements.Delivered, "landed")).Why);
        Assert.Contains("stands", Assert.Throws<DeliveryRefusedException>(() => store.Settle(push, DeliverySettlements.Withdrawn, "nothing stands")).Why);
        foreach (var phase in new[] { DeliveryPhases.PluginDispatched, DeliveryPhases.PluginAnswered })
        {
            store.Begin(push, phase, null);
            store.End(push, phase, DeliveryOutcomes.Done, "{\"state\":\"pushed\"}", phase);
        }

        store.Settle(push, DeliverySettlements.Delivered, "pushed");
        store.Settle(push, DeliverySettlements.Delivered, "again");
        var settled = store.Delivery(push.Key)!;
        Assert.Equal((DeliverySettlements.Delivered, "pushed", false), (settled.Settled, settled.Said, settled.Unresolved));
        Assert.NotNull(settled.SettledAt);
        Assert.Contains("settled", Assert.Throws<DeliveryRefusedException>(() => store.Settle(push, DeliverySettlements.Stopped, "late")).Why);
        Assert.Contains("settled", Assert.Throws<DeliveryRefusedException>(() => store.Begin(push, DeliveryPhases.Prepared, null)).Why);
    }

    [Fact]
    public void A_delivery_whose_effects_never_stood_is_withdrawn_and_a_stop_keeps_what_was_done()
    {
        var store = Store("run-a");
        var withdrawn = store.Start(Intent(), Accepted()).Claim!;
        store.Begin(withdrawn, DeliveryPhases.Prepared, null);
        store.End(withdrawn, DeliveryPhases.Prepared, DeliveryOutcomes.Done, null, "prepared");
        store.Begin(withdrawn, DeliveryPhases.RefWritten, "tip");
        store.End(withdrawn, DeliveryPhases.RefWritten, DeliveryOutcomes.Withdrawn, null, "the tip moved");

        store.Settle(withdrawn, DeliverySettlements.Withdrawn, "a new tip came");

        var stopped = store.Start(Intent("tip-two"), Accepted()).Claim!;
        Reach(store, stopped);
        store.Settle(stopped, DeliverySettlements.Stopped, "the person stopped it");
        Assert.Equal(DeliverySettlements.Stopped, store.Delivery(stopped.Key)!.Settled);
        Assert.Equal(3, store.Phases(stopped.Key).Count(each => each.Outcome == DeliveryOutcomes.Done));
        Assert.Contains("`someday`", Assert.Throws<DeliveryRefusedException>(() => store.Settle(stopped, "someday", "?")).Why);
    }

    // ——— The claim (the move's and the act's).

    [Fact]
    public void A_delivery_is_taken_only_from_a_dead_run_and_the_claim_it_lost_writes_nothing_more()
    {
        var first = Store("run-a");
        var claim = first.Start(Intent(), Accepted()).Claim!;

        Assert.Equal("the Daoris process that delivers it still runs", Store("run-b").Take(claim.Key, 1).Refused);
        Assert.Equal("it is this run's own", Store("run-a").Take(claim.Key, 1).Refused);
        var taken = Store("run-b", DeadAre("run-a")).Take(claim.Key, 1);

        Assert.Equal(new DeliveryClaim(claim.Key, "run-b", 2), taken.Claim);
        Assert.Contains("run-b holds it", Assert.Throws<DeliveryRefusedException>(() => first.Begin(claim, DeliveryPhases.Prepared, null)).Why);
        Assert.Empty(first.Phases(claim.Key));
        Assert.Equal("another run took it first", Store("run-c", DeadAre("run-a", "run-b")).Take(claim.Key, 1).Refused);
        Assert.Contains("writes under run run-c", Assert.Throws<DeliveryRefusedException>(() => Store("run-c").Begin(taken.Claim!, DeliveryPhases.Prepared, null)).Why);
        Store("run-b").Begin(taken.Claim!, DeliveryPhases.Prepared, null);
    }

    [Fact]
    public async Task Two_takers_at_once_leave_one_winner()
    {
        var key = Store("run-a").Start(Intent(), Accepted()).Claim!.Key;
        var dead = DeadAre("run-a");

        var takes = await Task.WhenAll(Enumerable.Range(0, 4).Select(each => Task.Run(() => Store($"run-{each}x", dead).Take(key, 1))));

        Assert.Single(takes, each => each.Claim is not null);
        Assert.Equal(2, Store("run-a").Delivery(key)!.ClaimGeneration);
    }

    // ——— Older builds and the rollback window (§4; the foundation design §13.3, §13.5).

    [Fact]
    public void A_rollback_is_refused_while_any_delivery_is_unresolved_and_allowed_once_all_are_settled()
    {
        var store = Store("run-a");
        Assert.True(store.Rollback().Allowed);

        var one = store.Start(Intent(), Accepted()).Claim!;
        var two = store.Start(Intent(unit: "unit-2", method: DeliveryMethods.Merge), Accepted()).Claim!;

        var refused = store.Rollback();
        Assert.False(refused.Allowed);
        Assert.Equal(new[] { one.Key, two.Key }.Order(), refused.Unresolved.Select(each => each.Key).Order());
        Assert.Contains("2 deliveries", refused.Refused);
        Assert.Contains("`unit-1` by branch at a1b2c3d4", refused.Refused);
        Assert.Contains("`unit-2` by merge", refused.Refused);

        // A phase held unknown is unresolved too: the person settles it.
        Reach(store, one);
        store.Settle(one, DeliverySettlements.Delivered, "landed");
        store.Begin(two, DeliveryPhases.Prepared, null);
        store.End(two, DeliveryPhases.Prepared, DeliveryOutcomes.Unknown, null, "read neither way");
        var still = store.Rollback();
        Assert.False(still.Allowed);
        Assert.Contains("1 delivery", still.Refused);
        Assert.Equal(two.Key, Assert.Single(still.Unresolved).Key);

        store.Settle(two, DeliverySettlements.Stopped, "the person settled it");

        var allowed = store.Rollback();
        Assert.True(allowed.Allowed);
        Assert.Null(allowed.Refused);
        Assert.Empty(allowed.Unresolved);
    }

    [Fact]
    public void No_delivery_starts_in_the_start_s_rollback_window_and_the_store_s_raise_waits_with_it()
    {
        var settled = StoreSettled.Not("the install's swap journal is at `started`");
        var store = Store("run-a", settled: () => settled);

        var held = store.Start(Intent(), Accepted());

        Assert.False(held.Started);
        Assert.Null(held.Delivery);
        Assert.Contains("`started`", held.Held);
        Assert.False(File.Exists(Db));
        Assert.True(store.Rollback().Allowed);

        // A store the build opened meanwhile, for another family, stops before the raise: no deliveries table, and an older build writes on.
        using (var other = DesktopStore.Open(Db, new DesktopStoreOptions { Settled = () => settled }))
        {
            Assert.Equal((DeliveryStore.Since - 1, 1), other.Stamp);
            Assert.Contains($"migration {DeliveryStore.Since} raises the store's writer stamp from 1 to 2", other.Held);
        }

        settled = StoreSettled.Yes;

        Assert.True(store.Start(Intent(), Accepted()).Started);
        using var reader = DesktopStoreReader.Open(Db)!;
        Assert.Equal((DesktopStore.Steps.Count, DesktopStore.WriterStamp), (reader.Version, reader.Writer));
    }

    [Fact]
    public void An_older_build_reads_the_store_and_its_every_write_is_refused_under_the_raised_stamp()
    {
        var key = Store("run-a").Start(Intent(), Accepted()).Claim!.Key;

        using var older = DesktopStore.Open(Db, new DesktopStoreOptions { Writer = 1, Steps = [.. DesktopStore.Steps.Take(DeliveryStore.Since - 1)], Settled = () => StoreSettled.Yes });

        Assert.True(older.Newer);
        Assert.Equal((DesktopStore.Steps.Count, DesktopStore.WriterStamp), older.Stamp);
        Assert.Equal(1L, older.Query("SELECT COUNT(*) FROM deliveries", row => row.GetInt64(0))[0]);
        var refused = Assert.Throws<DesktopStoreNewerException>(() => older.Write("moves", write => write.Execute(
            """
            INSERT INTO moves (record_left, executor, how, from_why, from_by, phase, claim_run, claim_generation, decided_at)
            VALUES ('s1', 'conversation', 'brief', 'limit', 'daoris', 'decided', 'run-a', 1, '2026-11-01T00:00:00.0000000+00:00')
            """)));
        Assert.Equal((DesktopStore.WriterStamp, 1), (refused.Stamped, refused.Own));
        Assert.Throws<DesktopStoreNewerException>(older.Enter);
        Assert.Equal(0L, older.Query("SELECT COUNT(*) FROM moves", row => row.GetInt64(0))[0]);
        Assert.True(Store("run-a").Delivery(key)!.Unresolved);
    }

    [Fact]
    public void An_older_build_that_opened_the_store_before_the_raise_is_refused_at_its_next_write()
    {
        using var older = DesktopStore.Open(Db, new DesktopStoreOptions { Writer = 1, Steps = [.. DesktopStore.Steps.Take(DeliveryStore.Since - 1)], Settled = () => StoreSettled.Yes });
        older.Write("probe", _ => { });

        Assert.True(Store("run-a").Start(Intent(), Accepted()).Started);

        // It kept its connection and its idea of the file: its next write meets the stamp inside its own transaction.
        Assert.Throws<DesktopStoreNewerException>(() => older.Write("probe", _ => { }));
        Assert.Equal(1, older.Revisions()["probe"]);
    }

    [Fact]
    public void A_record_a_newer_build_wrote_is_read_as_held_and_never_written()
    {
        var store = Store("run-a");
        var claim = store.Start(Intent(), Accepted()).Claim!;
        using (var raw = DesktopStore.Open(Db, new DesktopStoreOptions { Settled = () => StoreSettled.Yes }))
        {
            raw.Write(DeliveryStore.Family, write => write.Execute("UPDATE deliveries SET format = 2 WHERE key = $key", ("$key", claim.Key)));
        }

        Assert.True(store.Delivery(claim.Key)!.Held);
        Assert.Contains("newer Daoris", Assert.Throws<DeliveryRefusedException>(() => store.Begin(claim, DeliveryPhases.Prepared, null)).Why);
        Assert.Contains("newer Daoris", store.Take(claim.Key, 1).Refused);
        Assert.False(store.Rollback().Allowed);
    }

    [Fact]
    public void A_home_with_no_store_reads_as_holding_no_delivery_and_is_left_without_one()
    {
        var store = Store("run-a");

        Assert.Empty(store.Unresolved());
        Assert.Null(store.Delivery("no-such-key"));
        Assert.Empty(store.Phases("no-such-key"));
        Assert.True(store.Rollback().Allowed);
        Assert.False(File.Exists(Db));
    }
}
