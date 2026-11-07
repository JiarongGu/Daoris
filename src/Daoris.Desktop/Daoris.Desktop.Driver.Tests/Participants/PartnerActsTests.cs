using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Which state takes which act, what the person's press judges and writes, the
/// read of whether a handed piece has been taken in, and an uncertain piece's hand-over in the look: its job ended, the keeper's moment written
/// with the end <c>stopped</c>, and the work kept and answered as any other end's is. The keeper is a stand-in here (the git half holds
/// the real one), so nothing starts a process.
/// </summary>
public sealed class PartnerActsTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-partner-acts-" + Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-22T10:00:00Z");

    public PartnerActsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private PartnerFiles Files => new(_home);

    private void Piece(string state, string? copy = null)
    {
        Files.WriteAsk(new PartnerAsk("p1", "s1", "Make the window seven days.", ["src"], "codex-acp", OpinionStandIn.Tip, [], PartnerAsk.ByPerson, At));
        Files.WriteState(new PartnerState("p1", state, state == PartnerStates.Uncertain ? PartnerWhy.JobUnknown : null, At)
        {
            Agent = "codex-acp", Copy = copy, Commit = state is PartnerStates.Handed or PartnerStates.NotTakenIn ? new string('a', 40) : null,
        });
    }

    [Theory]
    [InlineData("discard", PartnerStates.Held, true)]
    [InlineData("discard", PartnerStates.Imported, true)]
    [InlineData("discard", PartnerStates.Answered, true)]
    [InlineData("discard", PartnerStates.Handed, true)]
    [InlineData("discard", PartnerStates.NotTakenIn, true)]
    [InlineData("discard", PartnerStates.Uncertain, true)]
    [InlineData("discard", PartnerStates.TakenIn, false)]
    [InlineData("discard", PartnerStates.Started, false)]
    [InlineData("discard", PartnerStates.Refused, false)]
    [InlineData("discard", PartnerStates.Discarded, false)]
    [InlineData("hand-over", PartnerStates.Uncertain, true)]
    [InlineData("hand-over", PartnerStates.Held, false)]
    [InlineData("hand-over", PartnerStates.Handed, false)]
    [InlineData("nothing", PartnerStates.Uncertain, false)]
    public void Each_state_takes_only_its_acts(string act, string state, bool allowed) =>
        Assert.Equal(allowed, PartnerPersonActs.Allows(act, state));

    [Fact]
    public void The_press_refuses_what_a_reader_of_the_file_cannot_know_and_writes_nothing()
    {
        Assert.Equal(PartnerPersonActs.Why.NoPiece, PartnerPersonAct.Ask(_home, "none", PartnerPersonActs.Discard, At).Why);
        Piece(PartnerStates.Started);
        Assert.Equal(PartnerPersonActs.Why.State, PartnerPersonAct.Ask(_home, "p1", PartnerPersonActs.Discard, At).Why);
        Piece(PartnerStates.Uncertain, copy: Path.Combine(_home, "gone"));
        Assert.Equal(PartnerPersonActs.Why.NoCopy, PartnerPersonAct.Ask(_home, "p1", PartnerPersonActs.HandOver, At).Why);
        Assert.Equal(PartnerPersonActs.Why.Unknown, PartnerPersonAct.Ask(_home, "p1", "burn", At).Why);
        Assert.False(new PartnerActFiles(_home).Waits("p1"));
    }

    [Fact]
    public void The_press_writes_the_act_for_the_look_and_a_later_press_replaces_it()
    {
        Piece(PartnerStates.Uncertain, copy: _home);

        Assert.True(PartnerPersonAct.Ask(_home, "p1", PartnerPersonActs.Discard, At).Acted);
        Assert.True(PartnerPersonAct.Ask(_home, "p1", PartnerPersonActs.HandOver, At.AddSeconds(1)).Acted);

        Assert.Equal(new PartnerActAsk("p1", PartnerPersonActs.HandOver, At.AddSeconds(1)), new PartnerActFiles(_home).Read("p1"));
    }

    [Fact]
    public void A_piece_is_taken_in_where_its_commit_is_an_ancestor_and_never_by_a_landing_alone()
    {
        var handed = new PartnerState("p1", PartnerStates.Handed, null, At) { Commit = new string('a', 40) };

        Assert.Equal(PartnerStates.TakenIn, PartnerTakenIn.Next(handed, "tip", ancestor: true, landed: false, At)!.State);
        Assert.Equal("tip", PartnerTakenIn.Next(handed, "tip", ancestor: true, landed: false, At)!.TakenBy);
        Assert.Equal(PartnerStates.NotTakenIn, PartnerTakenIn.Next(handed, "tip", ancestor: false, landed: true, At)!.State);
        Assert.Null(PartnerTakenIn.Next(handed, "tip", ancestor: false, landed: false, At));
        Assert.Null(PartnerTakenIn.Next(handed, null, ancestor: false, landed: false, At));
        // A not-taken-in piece the lead merges later is read as taken in; a taken-in one is never read again.
        Assert.Equal(PartnerStates.TakenIn, PartnerTakenIn.Next(handed with { State = PartnerStates.NotTakenIn }, "tip", true, true, At)!.State);
        Assert.Null(PartnerTakenIn.Next(handed with { State = PartnerStates.TakenIn }, "tip", true, true, At));
    }

    private sealed class Keeper
    {
        public List<PartnerState> Handed { get; } = [];

        public Task<PieceKept> KeepAsync(PartnerAsk ask, PartnerState state, CancellationToken ct)
        {
            Handed.Add(state);
            return Task.FromResult(new PieceKept(PartnerStates.Imported, "c0ffee0000000000000000000000000000000000", null, null, null));
        }
    }

    [Fact]
    public async Task Handing_over_what_an_uncertain_piece_left_keeps_its_work_with_the_end_stopped_and_answers_it_once()
    {
        var copy = Path.Combine(_home, "copy");
        Directory.CreateDirectory(copy);
        Piece(PartnerStates.Uncertain, copy);
        Assert.True(PartnerPersonAct.Ask(_home, "p1", PartnerPersonActs.HandOver, At).Acted);
        var keeper = new Keeper();
        var host = new OpinionStandIn().Session("s1", "working", tree: "trees/default/reports/s-1");
        var driver = new Daoris.Driver.Driver(
            host.Client(), DriverConfig.Empty with { Actors = StaffedRoles.On("claude-code-acp"), Cap = 3 }, ProviderRegistry.Built(), _home)
        {
            PartnerKeeps = keeper.KeepAsync,
            PartnerTrees = (_, _) => Task.FromResult<string?>("D:/checkouts/reports"),
        };

        var events = new List<string>();
        await driver.PiecesAsync(new Snapshot([], [], []) { Pieces = Files.Reservations() }, 0, events, CancellationToken.None);

        var kept = Assert.Single(keeper.Handed);
        Assert.Equal(PartnerStates.Ending, kept.State);
        Assert.Equal(PartnerEnds.Stopped, kept.End);
        Assert.NotNull(kept.Kept);
        Assert.Null(kept.Code);
        Assert.Equal(PartnerStates.Answered, Files.ReadState("p1")!.State);
        Assert.False(new PartnerActFiles(_home).Waits("p1"));

        // Nothing is done again at the next look.
        await driver.PiecesAsync(new Snapshot([], [], []) { Pieces = Files.Reservations() }, 0, [], CancellationToken.None);
        Assert.Single(keeper.Handed);
    }
}
