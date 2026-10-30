using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// KNOWCENTRAL2b: the stand-in the memory tools and the coordinator's record are proved over repeats what Lyntai's engine does that
/// Daoris must settle around (the memory coordinator design §6 C), and a confirmation's revision bump, so a proof over it meets the
/// engine's effects rather than an engine that has none.
/// </summary>
public sealed class StandInMemoryTests
{
    /// <summary>
    /// 🔴 A same-content write advances the engine and bumps the entry's revision, a repeated relation adds weight, a confirmation bumps
    /// the revision, an end is guarded by the revision read and by the entry being unended, and a recall opened to reinforce touches what
    /// it returns while one opened with none changes nothing.
    /// </summary>
    [Fact]
    public async Task The_stand_in_repeats_the_engines_write_effects_and_a_reinforcing_recall()
    {
        var memory = new StandInMemory();
        var owner = memory.Engine("Owner");
        var writer = memory.Open("Owner", MemoryReinforcement.None)!;

        var first = await writer.RememberAsync("The map is drawn in tiles.");
        var advanced = owner.Advance;
        var again = await writer.RememberAsync("The map is drawn in tiles.");
        Assert.Equal(first.Id, again.Id);
        Assert.Equal(first.Revision + 1, again.Revision);
        Assert.True(owner.Advance > advanced);

        var other = await writer.RememberAsync("Tiles are sixteen pixels.");
        await writer.RelateAsync(other.Id, first.Id, "disputes");
        Assert.Equal(2, (await writer.RelateAsync(other.Id, first.Id, "disputes")).Weight);

        var confirmed = await writer.ConfirmAsync(first.Id);
        Assert.Equal(again.Revision + 1, confirmed!.Revision);
        Assert.Null(await writer.EndAsync(first.Id, again.Revision, "Read before the confirmation."));
        var ended = await writer.EndAsync(first.Id, confirmed.Revision, "The map is drawn in vectors now.");
        Assert.Equal("The map is drawn in vectors now.", ended!.Ended);
        Assert.Null(await writer.EndAsync(first.Id, ended.Revision, "Ended twice."));

        var before = memory.State();
        await writer.SearchAsync("tiles", 10);
        await writer.GetAsync(other.Id);
        await writer.ExpandAsync(other.Id);
        Assert.Equal(before, memory.State());
        await memory.Open("Owner", MemoryReinforcement.OnRecall)!.SearchAsync("tiles", 10);
        Assert.NotEqual(before, memory.State());
        Assert.Null(memory.Open("Nobody", MemoryReinforcement.None));
    }
}
