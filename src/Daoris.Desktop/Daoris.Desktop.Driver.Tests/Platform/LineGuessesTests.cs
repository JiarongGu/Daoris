using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The checkout's guess at its line, asked of git once for as long as what git reads for it stays as it was (IDLECPU1). The
/// screen's LINES asked git of every checkout with no line set at each ask, one to three starts each, at every tick and every
/// return to the window, on a machine whose every program start costs a quarter of a second or more (DEVDRIVE1).
/// </summary>
/// <remarks>
/// No git here: the guess is counted, and the files git reads are laid out by hand, as the files backend keeps them.
/// </remarks>
public sealed class LineGuessesTests : IDisposable
{
    private readonly string _root;
    private readonly string _git;
    private int _asked;

    public LineGuessesTests()
    {
        _root = Path.Combine(RepoRoot(), "_fixtures", "line-guesses", Guid.NewGuid().ToString("N")[..8]);
        _git = Path.Combine(_root, ".git");
        Directory.CreateDirectory(Path.Combine(_git, "refs", "heads"));
        Write("HEAD", "ref: refs/heads/main\n");
        Write("refs/heads/main", "0123456789abcdef0123456789abcdef01234567\n");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException) { /* a straggling handle */ }
    }

    // The checkout's root, a worktree's included (its `.git` is a file): scratch stays in the repository (`_fixtures`).
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Path.Exists(Path.Combine(directory.FullName, ".git"))) directory = directory.Parent;
        return directory?.FullName ?? AppContext.BaseDirectory;
    }

    private void Write(string name, string text)
    {
        var path = Path.Combine(_git, name.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private LineGuesses Counted() => new((_, _) =>
    {
        Interlocked.Increment(ref _asked);
        return Task.FromResult<string?>("main");
    });

    [Fact]
    public async Task Git_is_asked_once_while_nothing_it_reads_moved()
    {
        var guesses = Counted();

        Assert.Equal("main", await guesses.GuessAsync(_root, CancellationToken.None));
        Assert.Equal("main", await guesses.GuessAsync(_root, CancellationToken.None));
        Assert.Equal("main", await guesses.GuessAsync(_root, CancellationToken.None));

        Assert.Equal(1, _asked);
    }

    [Fact]
    public async Task A_ref_it_reads_moved_is_asked_again()
    {
        var guesses = Counted();
        await guesses.GuessAsync(_root, CancellationToken.None);

        // origin's HEAD set, as a clone or `git remote set-head` writes it.
        Write("refs/remotes/origin/HEAD", "ref: refs/remotes/origin/trunk\n");
        await guesses.GuessAsync(_root, CancellationToken.None);
        // The branches packed, as `git pack-refs` or a gc leaves them.
        Write("packed-refs", "# pack-refs with: peeled fully-peeled sorted\n0123456789abcdef0123456789abcdef01234567 refs/heads/master\n");
        await guesses.GuessAsync(_root, CancellationToken.None);
        // A loose branch gone.
        File.Delete(Path.Combine(_git, "refs", "heads", "main"));
        await guesses.GuessAsync(_root, CancellationToken.None);

        Assert.Equal(4, _asked);
    }

    [Fact]
    public async Task What_the_files_cannot_tell_is_asked_every_time()
    {
        // A reftable store keeps no loose refs to read.
        Directory.CreateDirectory(Path.Combine(_git, "reftable"));
        var guesses = Counted();
        await guesses.GuessAsync(_root, CancellationToken.None);
        await guesses.GuessAsync(_root, CancellationToken.None);
        Assert.Equal(2, _asked);

        // A linked worktree's or a submodule's `.git` is a file naming the folder elsewhere.
        var linked = Path.Combine(_root, "linked");
        Directory.CreateDirectory(linked);
        File.WriteAllText(Path.Combine(linked, ".git"), "gitdir: ../.git/worktrees/linked\n");
        await guesses.GuessAsync(linked, CancellationToken.None);
        await guesses.GuessAsync(linked, CancellationToken.None);
        Assert.Equal(4, _asked);
    }

    [Fact]
    public async Task A_failed_guess_is_not_kept()
    {
        var fails = true;
        var guesses = new LineGuesses((_, _) =>
        {
            Interlocked.Increment(ref _asked);
            return fails ? Task.FromException<string?>(new IOException("git did not start")) : Task.FromResult<string?>("main");
        });

        await Assert.ThrowsAsync<IOException>(() => guesses.GuessAsync(_root, CancellationToken.None));
        fails = false;
        Assert.Equal("main", await guesses.GuessAsync(_root, CancellationToken.None));
        Assert.Equal(2, _asked);
    }
}
