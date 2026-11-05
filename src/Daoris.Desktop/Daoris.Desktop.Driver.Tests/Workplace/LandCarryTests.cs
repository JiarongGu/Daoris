using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDBACK1 (D87's note): the carry-on's words and the kept yes. A done quest's carry-on in the run is no Accept, and the kept yes
/// counts, restarts and is taken back as the performer's carry-on needs.
/// </summary>
/// <remarks>Real git in a scratch repository.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class LandCarryTests : LandedFixture
{
    private static readonly DateTimeOffset Pressed = new(2026, 10, 30, 1, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// LANDBACK1 part 2: a done quest's carry-on in the run is no Accept. It keeps no <see cref="CarriedAccepts"/> entry, which is the
    /// person's yes and would make the later landing read accepted by them; its words are its own.
    /// </summary>
    [Fact]
    public async Task A_done_quests_carry_on_in_the_run_keeps_no_accept()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "left.txt"), "not committed\n");

        var look = await TurnCarries.LookAsync(closed: true, () => false, tree.Path, CancellationToken.None);

        Assert.True(look.Dirty);
        Assert.Equal(1, look.Paths);
        Assert.Empty(new CarriedAccepts(Home).All());
        Assert.False(File.Exists(new CarriedAccepts(Home).FilePath));
        Assert.DoesNotContain("accepted", look.Words, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("Your work is accepted", LandCarry.DirtyWords(1), StringComparison.Ordinal);
        Assert.Contains(LandCarry.CommitWords, LandCarry.DirtyWords(1), StringComparison.Ordinal);
        Assert.Contains(LandCarry.CommitWords, look.Words, StringComparison.Ordinal);
    }

    [Fact]
    public void A_new_press_starts_the_count_again_and_a_take_back_removes_the_yes()
    {
        var accepts = new CarriedAccepts(Home);
        accepts.Keep("s1", "X:/daoris/trees/a", Pressed);
        accepts.CarriedOn("s1");
        accepts.TurnEnded("s1");
        accepts.CarriedOn("s1");
        Assert.Equal(new CarriedAccept("s1", "X:/daoris/trees/a", Pressed, 2, true), accepts.Of("s1"));

        accepts.Keep("s1", "X:/daoris/trees/a", Pressed.AddMinutes(5));
        Assert.Equal(new CarriedAccept("s1", "X:/daoris/trees/a", Pressed.AddMinutes(5), 0, false), accepts.Of("s1"));

        accepts.Take("s1");
        Assert.Null(accepts.Of("s1"));

        // A deleted session's record takes its yes with it (SessionHomeFiles): a tree that is gone is never landed.
        accepts.Keep("s1", "X:/daoris/trees/a", Pressed);
        accepts.Keep("s2", "X:/daoris/trees/b", Pressed);
        Assert.Equal(["s1"], accepts.Forget(["s1", "s3"]));
        Assert.Null(accepts.Of("s1"));
        Assert.NotNull(accepts.Of("s2"));
    }
}
