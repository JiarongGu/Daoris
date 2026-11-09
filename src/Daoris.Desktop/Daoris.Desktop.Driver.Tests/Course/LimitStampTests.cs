using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The limit wait's stamp carries everything the look decides from (the design's §35.2, §48, §58): the choices' effective version, each
/// family of the store the move reads, and the ledger's holds, so a change to any of them makes the next look run with
/// <c>driver.json</c> untouched; a staffing store that failed leaves the stamp equal to nothing.
/// </summary>
public sealed class LimitStampTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "limit-stamp", Guid.NewGuid().ToString("N")[..8]);

    private string File_ => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    public LimitStampTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File_)!);
        File.WriteAllText(File_, """{ "cap": 3, "actors": { "build": [ { "agent": "claude-code-acp" } ] } }""");
    }

    public void Dispose()
    {
        RoleStaffingPort.Unwire(File_);
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private StaffLedger Ledger(TimeProvider? clock = null) => new(_home, () => StoreSettled.Yes, clock);

    private RoleStaffing Imported()
    {
        var staffing = RoleStaffing.Wire(_home, () => StoreSettled.Yes);
        Assert.Equal(StaffingImported.Done, staffing.ImportFile().How);
        return staffing;
    }

    private LimitStamp Stamp(StaffLedger ledger) => LimitStamp.Of(_home, DriverConfig.Load(File_), ledger);

    [Fact]
    public void A_staffing_change_moves_the_stamp_and_the_next_load_answers_the_new_actors_with_driver_json_untouched()
    {
        var staffing = Imported();
        var ledger = Ledger();
        var before = Stamp(ledger);
        var written = File.GetLastWriteTimeUtc(File_);
        Assert.True(LimitStamp.Same(before, Stamp(ledger)));

        staffing.Set("build", [new ActorChoice("codex-acp")]);

        Assert.Equal(written, File.GetLastWriteTimeUtc(File_));
        Assert.False(LimitStamp.Same(before, Stamp(ledger)));
        Assert.Equal(["codex-acp"], DriverConfig.Load(File_).Actors["build"].Select(choice => choice.Agent));
    }

    [Fact]
    public void A_staffing_store_that_throws_once_answers_the_files_staffing_unversioned_and_the_next_load_the_stores()
    {
        Imported().Set("build", [new ActorChoice("codex-acp")]);
        var store = RoleStaffingPort.Of(File_)!;
        var once = new ThrowingOnce(store);
        RoleStaffingPort.Wire(File_, once);
        var ledger = Ledger();

        var failed = DriverConfig.Load(File_);
        Assert.Equal(["claude-code-acp"], failed.Actors["build"].Select(choice => choice.Agent));
        var stamp = LimitStamp.Of(_home, failed, ledger);
        Assert.False(LimitStamp.Same(stamp, stamp));

        var next = DriverConfig.Load(File_);
        Assert.Equal(["codex-acp"], next.Actors["build"].Select(choice => choice.Agent));
        Assert.True(LimitStamp.Same(LimitStamp.Of(_home, next, ledger), Stamp(ledger)));
    }

    [Fact]
    public void A_write_of_each_family_the_move_reads_moves_the_stamp_and_a_family_it_does_not_read_leaves_it()
    {
        Imported();
        var ledger = Ledger();
        Assert.Equal(
            new[] { RoleStaffing.Family, RoleCatalog.Family, StaffPolicies.Family, StaffLedger.Family, SupervisorRuleStore.Family, StaffMoves.Family, BindingStore.Family },
            LimitMove.Reads.ToArray());
        foreach (var family in LimitMove.Reads)
        {
            var before = Stamp(ledger);
            Moved(family);
            Assert.False(LimitStamp.Same(before, Stamp(ledger)), $"a write of {family} left the stamp as it was");
        }

        var unread = Stamp(ledger);
        Moved("news");
        Assert.True(LimitStamp.Same(unread, Stamp(ledger)));
    }

    [Fact]
    public void A_mark_its_clearing_a_reservation_and_its_release_each_move_the_stamp_and_an_expiry_moves_nothing()
    {
        var clock = new Seam(DateTimeOffset.Parse("2026-11-10T09:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        var ledger = Ledger(clock);
        var reservation = new StaffReservation("build", new StaffKey("claude-code-acp", null, null), clock.Now);

        var stamps = new List<LimitStamp> { Stamp(ledger) };
        ledger.MarkNotAnswering("claude-code-acp");
        stamps.Add(Stamp(ledger));
        ledger.Answered("claude-code-acp");
        stamps.Add(Stamp(ledger));
        ledger.Holds.Reserve(reservation);
        stamps.Add(Stamp(ledger));
        ledger.Release(reservation);
        stamps.Add(Stamp(ledger));
        for (var at = 1; at < stamps.Count; at++) Assert.False(LimitStamp.Same(stamps[at - 1], stamps[at]), $"write {at} left the stamp");

        ledger.MarkNotAnswering("codex-acp");
        var marked = Stamp(ledger);
        clock.Now += StaffLedger.Mark + TimeSpan.FromMinutes(1);
        Assert.Empty(ledger.Preview("build", "chat", []).Facts.NotAnswering);
        Assert.True(LimitStamp.Same(marked, Stamp(ledger)));
    }

    [Fact]
    public void The_ledgers_next_change_is_the_earliest_end_of_a_mark_a_reservation_and_a_move_counted_for_its_day()
    {
        var start = DateTimeOffset.Parse("2026-11-10T09:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var clock = new Seam(start);
        var ledger = Ledger(clock);
        Assert.Null(ledger.NextChange(start));

        ledger.MarkNotAnswering("claude-code-acp");
        Assert.Equal(start + StaffLedger.Mark, ledger.NextChange(start));

        ledger.Holds.Reserve(new StaffReservation("build", new StaffKey("codex-acp", null, null), start));
        Assert.Equal(start + StaffLedger.ReservationLife, ledger.NextChange(start));

        clock.Now = start - StaffMoves.Window + TimeSpan.FromMinutes(5);
        Assert.True(ledger.Moves.Record("c0", RoleWork.Chat, "c0", StaffConditions.Limit, SupervisorRules.Next, "talk"));
        clock.Now = start;
        Assert.Equal(start + TimeSpan.FromMinutes(5), ledger.NextChange(start));
    }

    private void Moved(string family)
    {
        using var store = DesktopStore.Open(DesktopStore.LocationIn(_home), StoreProcess.Options(() => StoreSettled.Yes));
        store.Write(family, _ => 0);
    }

    private sealed class Seam(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class ThrowingOnce(IRoleStaffing store) : IRoleStaffing
    {
        private int _thrown;

        public StaffingHeld? Held() =>
            Interlocked.Exchange(ref _thrown, 1) == 0 ? throw new IOException("the staffing store is held") : store.Held();
    }
}
