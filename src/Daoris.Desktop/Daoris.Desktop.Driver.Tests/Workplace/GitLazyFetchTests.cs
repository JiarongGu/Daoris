using System.Diagnostics;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Lazy fetch off by a proof, not a version: the proof read from two traces, a build that ignores the variable placed outside Daoris
/// from its start and never told it, and the one parser of a <c>cat-file --batch</c> answer. No process: the proof is the seam's.
/// </summary>
public sealed class GitLazyFetchTests : IDisposable
{
    private readonly string _root = Path.Combine(TestHomes.Root, "daoris-lazy-" + Guid.NewGuid().ToString("N")[..8]);

    private const string Fetched = "12:00:00.000000 run-command.c:673 trace: run_command: git -c fetch.negotiationAlgorithm=noop fetch origin --no-tags --no-write-fetch-head --recurse-submodules=no --filter=blob:none --stdin\n";

    private const string Read = "12:00:00.000000 git.c:476 trace: built-in: git cat-file -t da0fda0f\n";

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Repository(string config)
    {
        var folder = Path.Combine(_root, Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(Path.Combine(folder, ".git", "objects"));
        Directory.CreateDirectory(Path.Combine(folder, ".git", "refs"));
        File.WriteAllText(Path.Combine(folder, ".git", "HEAD"), "ref: refs/heads/main\n");
        File.WriteAllText(Path.Combine(folder, ".git", "config"), config);
        return folder;
    }

    private const string Partial = "[core]\n\trepositoryformatversion = 1\n[remote \"origin\"]\n\turl = file:///nowhere\n\tpromisor = true\n[extensions]\n\tpartialclone = origin\n";

    private static ProcessStartInfo Start(string folder) => new() { FileName = "git", WorkingDirectory = folder };

    [Fact]
    public void The_variable_is_proved_only_where_the_plain_run_fetched_and_the_told_run_did_not()
    {
        Assert.True(GitLazyFetch.Proves(Read + Fetched, Read));
        // 2.44.0 and 2.45.0 fetch whatever they are told.
        Assert.False(GitLazyFetch.Proves(Read + Fetched, Read + Fetched));
        // A run that fetched nothing either way proves nothing.
        Assert.False(GitLazyFetch.Proves(Read, Read));
    }

    [Fact]
    public async Task An_unsupported_builds_read_in_a_partial_clone_is_never_told_the_variable_and_waits_outside_from_its_start()
    {
        var folder = Repository(Partial);
        GitLazyFetch.Proof.Value = _ => Task.FromResult(false);
        var info = Start(folder);

        await GitLazyFetch.PrepareAsync(info, ["cat-file", "--batch"], CancellationToken.None);
        var ask = GitStartAsks.For(info, ["cat-file", "--batch"], GitRunner.Bytes, TimeSpan.FromSeconds(30), shares: false);

        Assert.False(info.Environment.ContainsKey(GitLazyFetch.Variable));
        Assert.Equal(GitWait.Outside, ask.Wait);
        Assert.False(ask.Endable);
    }

    [Fact]
    public async Task A_supported_builds_read_in_a_partial_clone_is_told_the_variable_and_holds_an_ordinary_place()
    {
        var folder = Repository(Partial);
        GitLazyFetch.Proof.Value = _ => Task.FromResult(true);
        var info = Start(folder);

        await GitLazyFetch.PrepareAsync(info, ["cat-file", "--batch"], CancellationToken.None);
        var ask = GitStartAsks.For(info, ["cat-file", "--batch"], GitRunner.Bytes, TimeSpan.FromSeconds(30), shares: false);

        Assert.Equal("1", info.Environment[GitLazyFetch.Variable]);
        Assert.Equal(GitWait.Read, ask.Wait);
        // The one remote start of a read that answered missing may fetch, so it waits outside.
        var again = Start(folder);
        using (GitLazyFetch.Fetching()) await GitLazyFetch.PrepareAsync(again, ["cat-file", "--batch"], CancellationToken.None);
        Assert.Equal(GitWait.Outside, GitStartAsks.For(again, ["cat-file", "--batch"], GitRunner.Bytes, TimeSpan.FromSeconds(30), shares: false).Wait);
    }

    [Fact]
    public async Task A_repository_with_no_promisor_asks_no_proof_and_its_reads_are_as_before()
    {
        var folder = Repository("[core]\n\trepositoryformatversion = 0\n");
        var asked = 0;
        GitLazyFetch.Proof.Value = _ => { asked++; return Task.FromResult(true); };
        var info = Start(folder);

        await GitLazyFetch.PrepareAsync(info, ["cat-file", "--batch"], CancellationToken.None);

        Assert.Equal(0, asked);
        Assert.False(info.Environment.ContainsKey(GitLazyFetch.Variable));
        Assert.Equal(GitWait.Read, GitStartAsks.For(info, ["cat-file", "--batch"], GitRunner.Bytes, TimeSpan.FromSeconds(30), shares: false).Wait);
    }

    [Fact]
    public void A_batch_answer_is_its_headers_size_and_the_bytes_after_it_or_missing()
    {
        var bytes = Encoding.UTF8.GetBytes("3b18e512dba79e4c8300dd08aeb37f8e728b8dad blob 12\nexport a = 1\n\n");
        var answer = GitObjectAnswer.Parse(bytes, bytes.Length)!;
        Assert.Equal(("blob", 12L, 12), (answer.Type, answer.Size, answer.Count));
        Assert.Equal("export a = 1", Encoding.UTF8.GetString(bytes, answer.Start, answer.Count));

        // Read short of the whole: the size is still the object's.
        Assert.Equal((5000L, 4), (GitObjectAnswer.Parse(Encoding.UTF8.GetBytes("ab blob 5000\nabcd"), 17)!.Size, GitObjectAnswer.Parse(Encoding.UTF8.GetBytes("ab blob 5000\nabcd"), 17)!.Count));
        Assert.True(GitObjectAnswer.Parse(Encoding.UTF8.GetBytes("3b18e5 missing\n"), 15)!.Missing);
        Assert.Null(GitObjectAnswer.Parse(Encoding.UTF8.GetBytes("3b18e5 ambiguous\n"), 17));
        Assert.Null(GitObjectAnswer.Parse(Encoding.UTF8.GetBytes("3b18e5 blob BAD\n"), 16));
        Assert.Null(GitObjectAnswer.Parse(Encoding.UTF8.GetBytes("3b18e5 blob 12"), 14));
    }
}
