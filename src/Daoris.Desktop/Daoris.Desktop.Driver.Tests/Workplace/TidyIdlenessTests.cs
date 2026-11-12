using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A repository whose last tidy pass did nothing is left alone while git's own files stand as they were, and read again when a ref
/// moves, a working tree comes or goes, a folder appears under its trees, or <see cref="TidyIdleness.Idle"/> has passed.
/// </summary>
public sealed class TidyIdlenessTests : IDisposable
{
    private readonly string _scratch = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "tidy-idle", Guid.NewGuid().ToString("N")[..8]);
    private readonly string _root;
    private readonly string _trees;
    private DateTimeOffset _now = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);

    public TidyIdlenessTests()
    {
        _root = Path.Combine(_scratch, "engine");
        _trees = Path.Combine(_scratch, "trees");
        Directory.CreateDirectory(Path.Combine(_root, ".git", "refs", "heads"));
        File.WriteAllText(Path.Combine(_root, ".git", "HEAD"), "ref: refs/heads/main\n");
        File.WriteAllText(Path.Combine(_root, ".git", "refs", "heads", "main"), new string('a', 40) + "\n");
    }

    public void Dispose() => GitSeed.Discard(_scratch);

    private TidyIdleness Idleness() => new(Path.Combine(_scratch, "home"), () => _now);

    private string? Stamp(TidyIdleness idleness) => idleness.Stamp(_root, _trees, "default", "engine");

    [Fact]
    public void A_quiet_pass_is_skipped_while_nothing_moved()
    {
        var idleness = Idleness();
        var stamp = Stamp(idleness);
        Assert.NotNull(stamp);
        Assert.False(idleness.Skips(_root, stamp));
        idleness.Remember(_root, stamp, new TidyPass([]));
        Assert.True(idleness.Skips(_root, Stamp(idleness)));
    }

    [Fact]
    public void A_ref_that_moves_a_tree_that_comes_or_a_folder_that_appears_ends_the_skip()
    {
        var idleness = Idleness();
        idleness.Remember(_root, Stamp(idleness), new TidyPass([]));

        File.WriteAllText(Path.Combine(_root, ".git", "refs", "heads", "daoris-s-1"), new string('b', 40) + "\n");
        Assert.False(idleness.Skips(_root, Stamp(idleness)));

        idleness.Remember(_root, Stamp(idleness), new TidyPass([]));
        Assert.True(idleness.Skips(_root, Stamp(idleness)));
        Directory.CreateDirectory(Path.Combine(_root, ".git", "worktrees", "s-1"));
        Assert.False(idleness.Skips(_root, Stamp(idleness)));

        idleness.Remember(_root, Stamp(idleness), new TidyPass([]));
        Assert.True(idleness.Skips(_root, Stamp(idleness)));
        Directory.CreateDirectory(Path.Combine(_trees, "default", "engine", "s-stray"));
        Assert.False(idleness.Skips(_root, Stamp(idleness)));
    }

    [Fact]
    public void A_quiet_repository_is_read_again_after_the_idle_time_whatever_the_files_say()
    {
        var idleness = Idleness();
        idleness.Remember(_root, Stamp(idleness), new TidyPass([]));
        _now += TidyIdleness.Idle - TimeSpan.FromSeconds(1);
        Assert.True(idleness.Skips(_root, Stamp(idleness)));
        _now += TimeSpan.FromSeconds(2);
        Assert.False(idleness.Skips(_root, Stamp(idleness)));
    }

    [Fact]
    public void A_pass_that_held_or_said_anything_is_never_skipped_and_forgets_an_earlier_quiet_one()
    {
        var idleness = Idleness();
        idleness.Remember(_root, Stamp(idleness), new TidyPass([]));
        idleness.Remember(_root, Stamp(idleness), new TidyPass([], TidyKept.Starting, "a start holds it"));
        Assert.False(idleness.Skips(_root, Stamp(idleness)));

        idleness.Remember(_root, Stamp(idleness), new TidyPass([]));
        idleness.Forget(_root);
        Assert.False(idleness.Skips(_root, Stamp(idleness)));
    }

    [Fact]
    public void A_checkout_whose_files_cannot_say_where_its_refs_are_has_no_stamp()
    {
        Assert.Null(Idleness().Stamp(Path.Combine(_scratch, "nowhere"), _trees, "default", "engine"));
    }
}
