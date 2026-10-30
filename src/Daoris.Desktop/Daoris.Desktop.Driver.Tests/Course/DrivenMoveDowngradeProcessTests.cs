using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1f-iv part b, sitting 2 (D212; the foundation design §13.5's downgrade table, §19.4, row f-iv's proof): the driven carry-on's move
/// across a downgrade, as <c>CurveMoveDowngradeProcessTests</c> proves the curve's. A loop is killed at each boundary the move passes
/// (<see cref="DrivenMoveCrashProcessTests"/>), then an older build comes up on the home and takes nothing up, then this build returns and acts
/// once, then comes up again and finds nothing to do.
/// </summary>
/// <remarks>
/// <para>An older build's come-up is the real watch's first look while the killed run still reads as alive, so its take-up takes no run's move
/// up, as a build before the store takes none. The standing move then also holds its line and its destination, as this build holds them while
/// a live run's move stands, so the older come-up here carries nothing on except where the destination is not yet named (<c>answered</c>),
/// which its own go-on carries on to a part of its own and moves the word once. What an older build's go-on does to a destination it was not
/// held from is <c>DrivenMoveOlderBuildProcessTests</c>'.</para>
/// <para>Each row after this build's return is the crash row of the same boundary, since the older come-up changed nothing the return reads
/// differently; the return's take-up of a move the older come-up left standing under its dead run is a claim of its own.</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class DrivenMoveDowngradeProcessTests : IAsyncLifetime
{
    private readonly DrivenMoveHarness _h = new("driven-move-downgrade");

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _h.DisposeAsync();

    public static TheoryData<string, bool> Boundaries
    {
        get
        {
            var data = new TheoryData<string, bool>();
            foreach (var cut in Cuts.All.Where(cut => cut != Cuts.OldEnded))
            {
                data.Add(cut, true);
                data.Add(cut, false);
            }

            return data;
        }
    }

    /// <summary>🔴 The census beside the theory: every boundary the driven move passes has a row, and the downgrade runs the same ones.</summary>
    [Fact]
    public void Every_boundary_the_driven_move_passes_has_a_downgrade_row()
    {
        foreach (var cut in Cuts.All.Where(cut => cut != Cuts.OldEnded)) Assert.NotNull(DrivenMoveCrashProcessTests.Row(cut));
        Assert.Equal(Cuts.All.Count - 1, Boundaries.Select(row => (string)row[0]).Distinct().Count());
    }

    /// <summary>
    /// 🔴 A loop killed at each boundary, an older build's come-up, then this build's return, which acts once: the older come-up leaves the move
    /// standing under its dead run and sends nothing it was not held from, the return ends it by its phase as a crash does, the word reaches an
    /// agent once, and a second come-up of this build changes nothing.
    /// </summary>
    [Theory]
    [MemberData(nameof(Boundaries))]
    public async Task A_downgrade_at_each_boundary_delivers_the_word_once_and_the_returning_build_acts_once(string cut, bool word)
    {
        var expect = DrivenMoveCrashProcessTests.Row(cut);
        await _h.SeedAsync(word);
        await _h.KillAtAsync(cut);
        var left = _h.MoveFrom("s1")!;
        await _h.EndLiveAsync();
        var before = _h.SnapshotOf();

        // The older build comes up: it takes no move up.
        _h.ComeUp();
        // Its look, run a fixed while: where the line is held by the standing move it asks its resume again at every look and changes nothing
        // (an absence the time states); where the destination is not yet named it carries it on, and the part it opens completes.
        if (cut == Cuts.Answered) await Poll.Until(() => _h.Service.SessionCount == 3 && _h.Service.State("s3") == "completed", _h.Seen);
        await Task.Delay(TimeSpan.FromSeconds(8));
        await _h.StopAsync();
        var standing = _h.MoveFrom("s1")!;
        var seen = _h.Seen();
        _h.Trail.Add($"after the older come-up: {seen}");
        Assert.True(standing.Stands, $"an older build's come-up leaves the move standing under its dead run: {seen}");
        Assert.Equal((left.PhaseWord, left.ClaimRun, left.ClaimGeneration), (standing.PhaseWord, standing.ClaimRun, standing.ClaimGeneration));
        if (cut != Cuts.Answered) Assert.Equal(before, _h.SnapshotOf());
        else Assert.Equal(word ? 1 : 0, _h.Prompts().Count(prompt => prompt.Text.Contains(DrivenMoveHarness.Word, StringComparison.Ordinal)));

        // This build returns: its take-up reads what the older one left, and acts once.
        _h.Die(DrivenMoveHarness.Killed);
        _h.ComeUp();
        await _h.SettleAsync(ready: () => expect.First.SequenceEqual(_h.Shapes("s1")) && expect.Second.SequenceEqual(_h.Shapes("s2")), idle: false);
        await _h.StopAsync();

        seen = _h.Seen();
        Assert.True(expect.First.SequenceEqual(_h.Shapes("s1")), $"s1's moves {string.Join(" | ", _h.Shapes("s1"))}: {seen}");
        Assert.True(expect.Second.SequenceEqual(_h.Shapes("s2")), $"s2's moves {string.Join(" | ", _h.Shapes("s2"))}: {seen}");
        Assert.All(_h.Store("run-probe").Of("s1"), move => Assert.False(move.Stands, seen));
        Assert.True(_h.Store("run-probe").Of("s1")[0].ClaimGeneration > left.ClaimGeneration, "the return's take-up is a claim of its own");
        var hearing = _h.Prompts().Where(prompt => prompt.Text.Contains(DrivenMoveHarness.Word, StringComparison.Ordinal)).ToList();
        if (word)
        {
            var heard = Assert.Single(hearing);
            Assert.Equal(expect.Hears, heard.Session);
            Assert.Equal(expect.Waits is null ? [] : [$"{expect.Waits}:w1"], _h.Waiting());
        }
        else
        {
            Assert.Empty(hearing);
            Assert.Empty(_h.Waiting());
        }

        var keys = _h.Service.Opened.Select(opened => opened.Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
        for (var at = 1; at <= _h.Service.SessionCount; at++)
        {
            Assert.DoesNotContain(_h.Events.After($"s{at}", 0).Events, e => e.Code == SessionEventCodes.Lost);
        }

        // A second come-up of this build finds nothing to do: the return acted once.
        var shape = _h.SnapshotOf();
        _h.ComeUp();
        await _h.SettleAsync(idle: false);
        await _h.StopAsync();
        Assert.Equal(shape, _h.SnapshotOf());
        await _h.ReleaseAsync();
        Assert.Equal(shape, _h.SnapshotOf());
    }
}
