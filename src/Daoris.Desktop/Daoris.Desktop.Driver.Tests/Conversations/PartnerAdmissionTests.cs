using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COAGENT1b (D199 point 4; the co-agent design §2.3 step 4, §5): a piece reserved and not yet opened holds a slot in the count every
/// starter reads, counted once when its record opens; the reservation is judged and written in one step, so two looks never both take
/// the last slot, nor a look and a quest's start; it is released at every failure and end; a lead's two open pieces are counted across
/// its <c>from</c> chain, so a hand-over does not reset them.
/// </summary>
public sealed class PartnerAdmissionTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyDictionary<string, string?> NoChain = new Dictionary<string, string?>();

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-partner-admission-" + Guid.NewGuid().ToString("N")[..8]);

    public PartnerAdmissionTests() => Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Partners));

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static PieceReservation Piece(string id, string state, string lead = "lead") => new(id, lead, state);

    private static SessionView Driven(string id, string repository) => new(id, repository, "working") { Kind = "driven", Quest = $"q-{id}" };

    private PartnerFiles Asked(params (string Id, string Lead)[] asks)
    {
        var files = new PartnerFiles(_home);
        foreach (var (id, lead) in asks)
        {
            files.WriteAsk(new PartnerAsk(id, lead, "Write the piece.", [], "stub-b", "abc123", [], PartnerAsk.ByPerson, At));
        }

        return files;
    }

    [Fact]
    public void A_reserved_piece_holds_a_slot_beside_the_active_records_until_its_record_opens()
    {
        var active = new[] { Driven("s1", "A") };
        var pieces = new[] { Piece("p1", PartnerStates.Approved), Piece("p2", PartnerStates.Preparing), Piece("p3", PartnerStates.Asked) };

        Assert.Equal(3, new Snapshot([], [], active) { Pieces = pieces }.SlotsHeld);

        // Its record open, carrying its piece and serving its lead, holds the slot instead: counted once, not twice.
        var opened = new SessionView("s2", "A", "working") { Kind = "chat", Serves = "lead", Piece = "p2" };
        Assert.Equal(3, new Snapshot([], [], [.. active, opened]) { Pieces = pieces }.SlotsHeld);
        Assert.True(ActiveSessions.HoldsSlot(opened));
    }

    [Fact]
    public void A_reservation_is_released_at_every_failure_and_end()
    {
        foreach (var state in PartnerStates.All.Where(state => state is not (PartnerStates.Approved or PartnerStates.Preparing)))
        {
            Assert.Equal(0, PartnerAdmission.Reserving([Piece("p1", state)], []));
        }
    }

    [Fact]
    public void A_quest_waits_for_the_last_slot_a_piece_reserved()
    {
        var config = DriverConfig.Empty with { Drivable = ["A"], Cap = 1, Trees = ["A"] };
        var snapshot = new Snapshot([new QuestView("q1", "Asker", "A", "Ask q1", "Why.", "Open")], [new RepoView("A", true, "D:/fam/A")], [])
        {
            Pieces = [Piece("p1", PartnerStates.Approved)],
        };

        Assert.NotEqual(StartVerdict.Start, Assert.Single(Planner.Plan(snapshot, config)).Verdict);
        Assert.Equal(StartVerdict.Start, Assert.Single(Planner.Plan(snapshot with { Pieces = [] }, config)).Verdict);
    }

    [Fact]
    public void Two_looks_at_the_last_slot_reserve_one_piece_and_the_other_waits()
    {
        var files = Asked(("p1", "lead-a"), ("p2", "lead-b"));

        var results = new PieceAdmitted[2];
        Parallel.For(0, 2, at => results[at] = PartnerReservations.TryReserve(files, $"p{at + 1}", [], NoChain, cap: 1, taken: 0, At));

        Assert.Equal([PieceAdmitted.Reserved, PieceAdmitted.CapFull], results.Order());
        Assert.Single(files.Reservations(), piece => piece.State == PartnerStates.Approved);
    }

    [Fact]
    public void A_quests_start_this_look_took_leaves_the_piece_waiting_and_it_reserves_once_a_slot_frees()
    {
        var files = Asked(("p1", "lead"));

        Assert.Equal(PieceAdmitted.CapFull, PartnerReservations.TryReserve(files, "p1", [Driven("s1", "A")], NoChain, cap: 2, taken: 1, At));
        Assert.Null(files.ReadState("p1"));

        Assert.Equal(PieceAdmitted.Reserved, PartnerReservations.TryReserve(files, "p1", [Driven("s1", "A")], NoChain, cap: 2, taken: 0, At));
        Assert.Equal(PartnerStates.Approved, files.ReadState("p1")!.State);
        // Reserved once: a second look finds it past judging and takes nothing.
        Assert.Equal(PieceAdmitted.Already, PartnerReservations.TryReserve(files, "p1", [], NoChain, cap: 9, taken: 0, At));
    }

    [Fact]
    public void Two_open_pieces_are_counted_from_approved_on_and_a_third_is_refused_whatever_the_cap()
    {
        var files = Asked(("p1", "lead"), ("p2", "lead"), ("p3", "lead"));

        Assert.Equal(PieceAdmitted.Reserved, PartnerReservations.TryReserve(files, "p1", [], NoChain, cap: 9, taken: 0, At));
        Assert.Equal(PieceAdmitted.Reserved, PartnerReservations.TryReserve(files, "p2", [], NoChain, cap: 9, taken: 0, At));
        Assert.Equal(PieceAdmitted.TooMany, PartnerReservations.TryReserve(files, "p3", [], NoChain, cap: 9, taken: 0, At));

        // An uncertain piece stays open until the person's act; a final one frees its place.
        files.WriteState(new PartnerState("p1", PartnerStates.Uncertain, PartnerWhy.JobUnknown, At));
        Assert.Equal(PieceAdmitted.TooMany, PartnerReservations.TryReserve(files, "p3", [], NoChain, cap: 9, taken: 0, At));
        files.WriteState(new PartnerState("p1", PartnerStates.Handed, null, At));
        Assert.Equal(PieceAdmitted.Reserved, PartnerReservations.TryReserve(files, "p3", [], NoChain, cap: 9, taken: 0, At));
    }

    [Fact]
    public void A_hand_over_does_not_reset_its_leads_two_open_pieces()
    {
        // The lead `old` asked two, then handed over to `new`, which a later part `newer` continues: one chain.
        var files = Asked(("p1", "old"), ("p2", "old"), ("p3", "newer"));
        files.WriteState(new PartnerState("p1", PartnerStates.Started, null, At));
        files.WriteState(new PartnerState("p2", PartnerStates.Preparing, null, At));
        var fromOf = new Dictionary<string, string?> { ["old"] = null, ["new"] = "old", ["newer"] = "new", ["other"] = null };

        Assert.Equal(["new", "newer", "old"], PartnerAdmission.Chain("newer", fromOf).Order());
        Assert.Equal(["new", "newer", "old"], PartnerAdmission.Chain("old", fromOf).Order());
        Assert.Equal(PieceAdmitted.TooMany, PartnerReservations.TryReserve(files, "p3", [], fromOf, cap: 9, taken: 0, At));
        Assert.Equal(PartnerStates.Asked, files.Reservations().Single(piece => piece.Id == "p3").State);
    }

    [Fact]
    public void An_unreadable_state_is_read_as_an_open_uncertain_piece()
    {
        var files = Asked(("p1", "lead"));
        File.WriteAllText(files.PathOf("p1", "state"), "{ not json");

        Assert.Equal(PartnerStates.Uncertain, Assert.Single(files.Reservations()).State);
        Assert.Equal(PieceAdmitted.Already, PartnerReservations.TryReserve(files, "p1", [], NoChain, cap: 9, taken: 0, At));
    }
}
