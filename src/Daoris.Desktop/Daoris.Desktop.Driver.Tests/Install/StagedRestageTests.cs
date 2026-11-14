using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A build refused <c>busy</c> is staged again by itself once nothing holds <c>app/</c>; any other set-aside build stays aside.
/// Real folders in a scratch install, nothing started.
/// </summary>
public sealed class StagedRestageTests : ScratchInstall
{
    private InstallSwap Swap() => new(_install, start: _ => 1, alive: _ => true);

    [Fact]
    public void A_build_refused_busy_is_staged_again_whole_and_passes_the_check()
    {
        Stage();
        Swap().Held("something still ran from app/ after the application closed, so nothing was replaced.");
        Assert.False(StagedBuild.IsStaged(_install));

        Assert.True(StagedRestage.Again(_install));

        Assert.True(StagedBuild.IsStaged(_install));
        Assert.Null(StagedBuild.Check(_install));
        Assert.False(File.Exists(Path.Combine(_install, "update", "failed", "build.json")));
    }

    [Fact]
    public void Nothing_is_staged_again_while_a_build_is_staged_or_for_a_refusal_that_was_not_busy()
    {
        Stage();
        Swap().Held("the launcher met an error.", code: "error");
        Assert.False(StagedRestage.Again(_install));
        Assert.False(StagedBuild.IsStaged(_install));

        Stage();
        Assert.False(StagedRestage.Again(_install));
        Assert.True(File.Exists(Path.Combine(_install, "update", "failed", "build.json")));
    }

    [Fact]
    public void A_set_aside_build_that_is_not_the_one_refused_is_left_aside()
    {
        Stage();
        Swap().Held("a process still runs from app/.");
        StagedBuild.WriteJournal(_install, StagedBuild.ReadJournal(_install)! with { Id = "another" });

        Assert.False(StagedRestage.Again(_install));
        Assert.False(StagedBuild.IsStaged(_install));
    }

    [Fact]
    public void A_restage_that_stopped_part_way_moves_the_rest_at_the_next_start()
    {
        Stage();
        Swap().Held("a process still runs from app/.");
        var staged = StagedBuild.StagedOf(_install);
        Directory.CreateDirectory(staged);
        File.Move(Path.Combine(_install, "update", "failed", "Daoris.exe"), Path.Combine(staged, "Daoris.exe"));
        Assert.False(StagedBuild.IsStaged(_install));

        Assert.True(StagedRestage.Again(_install));
        Assert.Null(StagedBuild.Check(_install));
    }
}
