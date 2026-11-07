using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The swap's journal holds each move before it is made, so the next start undoes a swap a launcher died in.</summary>
public sealed class SwapJournalTests : ScratchInstall
{
    // A move made just before a launcher died is journalled, or the undo deletes update/previous/, the only copy.
    [Theory]
    [InlineData("app", "update/previous/app")]
    [InlineData("update/staged/app", "app")]
    public void A_launcher_that_died_in_the_moment_after_a_move_is_still_undone_to_the_build_before(string from, string to)
    {
        Stage();
        var swap = new InstallSwap(_install, start: _ => 1, alive: _ => true)
        {
            Mover = (source, target, overwrite) =>
            {
                InstallSwap.MoveOnDisk(source, target, overwrite);
                if (Is(source, from) && Is(target, to)) throw new Stop();
            },
        };
        Assert.Throws<Stop>(() => swap.Run([]));
        Assert.Equal(SwapPhase.Swapping, StagedBuild.ReadJournal(_install)!.Phase);

        var put = new InstallSwap(_install, start: _ => 1, alive: _ => true).Recover(appRunning: false);

        Assert.Equal(SwapPhase.RolledBack, put);
        AssertTheBuildBefore();
    }
}
