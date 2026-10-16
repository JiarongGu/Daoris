using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// STRAYOTHERS1 (D88's note): what a pass a kill cut off left under a repository's trees, an opinion's copy or a replay's tree,
/// is moved aside by the next pass of its kind once nothing running holds it and nothing in it was written for ten minutes.
/// These hold the halves no git or process reaches: the holder's words, and the copies' judgement over plain folders with a
/// stand-in for which processes run. The real clones and replays are <c>PassLeftoversProcessTests</c>.
/// </summary>
public sealed class PassLeftoversTests : IDisposable
{
    private const int Ended = 41;
    private const int Runs = 42;

    private readonly string _scratch = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "pass-leftovers", Guid.NewGuid().ToString("N")[..8]);
    private string Home => Path.Combine(_scratch, "daoris-home");
    private string Engine => DaorisHome.Entry(Home, HomeEntry.Trees,"default", "engine");

    public PassLeftoversTests() => Directory.CreateDirectory(Engine);

    public void Dispose() => Directory.Delete(_scratch, recursive: true);

    [Fact]
    public void The_holder_is_read_back_from_the_words_a_pass_wrote_and_from_no_other()
    {
        Assert.Equal(1234, PassLeftovers.HolderOf(PassLeftovers.HeldBy(1234)));
        Assert.Equal("Daoris is using this for one pass (process 1234)", PassLeftovers.HeldBy(1234));
        Assert.Null(PassLeftovers.HolderOf(null));
        Assert.Null(PassLeftovers.HolderOf(""));
        Assert.Null(PassLeftovers.HolderOf("a person's"));
        Assert.Null(PassLeftovers.HolderOf(TreeOpenings.GitsOwn));
        Assert.Null(PassLeftovers.HolderOf(TreeOpenings.LockReason(1234)));
        Assert.Null(PassLeftovers.HolderOf("Daoris is using this for one pass (process -1)"));
        Assert.Null(PassLeftovers.HolderOf("Daoris is using this for one pass (process 12a)"));
    }

    /// <summary>
    /// 🔴 The row's case for an opinion's copy: one whose pass ended without removing it, its process gone and nothing written in it
    /// for ten minutes, goes aside whole; so does one from before the copies named their process, by its quiet alone. A copy a
    /// running process holds stays, however quiet; so does one written in lately, one naming a holder Daoris did not write, and a
    /// folder that is no copy.
    /// </summary>
    [Fact]
    public void A_copy_no_running_pass_holds_and_left_quiet_goes_aside_and_every_other_stays()
    {
        var killed = Copy("o-0dead001", PassLeftovers.HeldBy(Ended));
        var older = Copy("o-0de1d002", holder: null);
        var held = Copy("o-11fe0003", PassLeftovers.HeldBy(Runs));
        var strange = Copy("o-5a1d0004", "written by someone else");
        var session = Copy("s-1a2b3c4d", holder: null);
        var written = Copy("o-f2e50005", PassLeftovers.HeldBy(Ended));
        var now = DateTimeOffset.UtcNow + TreeStrays.Quiet + TimeSpan.FromMinutes(1);
        File.WriteAllText(Path.Combine(written, "late.txt"), "written by the reviewer a moment ago\n");
        File.SetLastWriteTimeUtc(Path.Combine(written, "late.txt"), now.UtcDateTime.AddMinutes(-1));

        var moved = new SessionTrees(Home).ClearPassCopies(Engine, now, process => process == Runs);

        var aside = DaorisHome.Entry(Home, HomeEntry.Trees,".tidied", "default", "engine");
        Assert.Equal(["o-0de1d002", "o-0dead001"], moved.Select(to => Path.GetFileName(to)[..10]).Order(StringComparer.Ordinal));
        Assert.All(moved, to => Assert.Equal(aside, Path.GetDirectoryName(to)));
        Assert.All(moved, to => Assert.NotNull(TreeSetAside.MovedAt(Path.GetFileName(to))));
        Assert.All(moved, to => Assert.True(File.Exists(Path.Combine(to, "README.md"))));
        Assert.False(Directory.Exists(killed));
        Assert.False(Directory.Exists(older));
        Assert.All([held, strange, session, written], folder => Assert.True(Directory.Exists(folder), folder));
    }

    /// <summary>A repository with no trees folder, or none of its copies, is read and left alone.</summary>
    [Fact]
    public void A_repository_with_nothing_of_a_pass_s_moves_nothing()
    {
        var trees = new SessionTrees(Home);

        Assert.Empty(trees.ClearPassCopies(DaorisHome.Entry(Home, HomeEntry.Trees,"default", "absent"), DateTimeOffset.UtcNow.AddHours(1), _ => false));
        Assert.Empty(trees.ClearPassCopies(Engine, DateTimeOffset.UtcNow.AddHours(1), _ => false));
        Assert.False(Directory.Exists(DaorisHome.Entry(Home, HomeEntry.Trees,".tidied")));
    }

    /// <summary>A copy's folder as a pass makes one: its files, and the file in its own <c>.git</c> naming who holds it.</summary>
    private string Copy(string name, string? holder)
    {
        var folder = Path.Combine(Engine, name);
        Directory.CreateDirectory(Path.Combine(folder, ".git"));
        File.WriteAllText(Path.Combine(folder, "README.md"), "# engine\n");
        if (holder is not null) File.WriteAllText(Path.Combine(folder, ".git", PassLeftovers.CopyHolder), holder + "\n");
        return folder;
    }
}
