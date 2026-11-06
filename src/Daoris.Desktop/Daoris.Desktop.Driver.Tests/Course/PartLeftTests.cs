using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COAGENT1g: what the old part's line says of what it left running at a hand-over, by its code and count, and that a part with no job
/// to read says nothing. The wait itself, on real processes, is <see cref="HandOverLeftProcessTests"/>.
/// </summary>
public sealed class PartLeftTests
{
    [Fact]
    public void A_part_that_left_nothing_says_nothing()
    {
        Assert.Null(new PartLeft(0, Emptied: true).Note(PartLeftovers.Bound));
    }

    [Fact]
    public void What_was_ended_and_waited_for_is_said_with_its_count()
    {
        var note = new PartLeft(2, Emptied: true).Note(PartLeftovers.Bound)!;

        var part = Assert.Single(note.Parts);
        Assert.Equal(("chat.left-ended", (object?)2), (part.Code, part.Value("count")));
        Assert.Contains("2 in all", note.Note);
    }

    [Fact]
    public void What_still_ran_at_the_bound_is_said_with_the_bound()
    {
        var note = new PartLeft(1, Emptied: false).Note(TimeSpan.FromSeconds(30))!;

        var part = Assert.Single(note.Parts);
        Assert.Equal(("chat.left-running", (object?)1, (object?)30), (part.Code, part.Value("count"), part.Value("seconds")));
        Assert.Contains("after 30 seconds", note.Note);
    }

    [Fact]
    public async Task No_job_held_is_no_reading()
    {
        Assert.Null(await PartLeftovers.EndAsync(null, TimeSpan.FromSeconds(1)));
    }
}
