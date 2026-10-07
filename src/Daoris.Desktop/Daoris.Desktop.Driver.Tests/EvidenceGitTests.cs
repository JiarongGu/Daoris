using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// EVID1b (D144 §3): a done's evidence read from a real repository through the git Daoris runs: a path found with git's own
/// object id and whether the work changed it, a folder found as its tree, a path the commit spells only in another case, one
/// written and never committed, one missing; the commit a check reads placed against the done's on its history; and the
/// commits since a base, as the sweep's record says them. <c>EvidenceReaderTests</c> holds the reads with git standing in.
/// </summary>
/// <remarks>
/// Fixtures live under the repository's gitignored <c>_fixtures/</c>, never OS temp. 🔴 <b>Nothing here reaches a network</b>,
/// and nothing is written to the repository but by the fixture's own steps: the read asks git and the disk, and writes nothing.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class EvidenceGitTests : IDisposable
{
    private readonly string _scratch;

    public EvidenceGitTests()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "evidence", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_scratch);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_scratch, recursive: true);
        }
        catch (IOException) { /* a straggling git handle; the next run's folder is its own */ }
        catch (UnauthorizedAccessException) { /* read-only pack files under .git */ }
    }

    private sealed record History(string Root, string Base, string Done, string Later);

    private async Task<History> HistoryAsync()
    {
        var root = Path.Combine(_scratch, "reports");
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await GitAsync(root, "config", "user.email", "fixture@example.test");
        await GitAsync(root, "config", "user.name", "Fixture");
        await GitAsync(root, "config", "core.autocrlf", "false");
        var before = await CommitAsync(root, "base", ("README.md", "r\n"), ("docs/old.md", "old\n"));
        var done = await CommitAsync(root, "done", ("README.md", "r2\n"), ("Report.md", "report\n"));
        var later = await CommitAsync(root, "later", ("late.md", "late\n"));
        await GitAsync(root, "reset", "--quiet", "--hard", done);
        // Written and never committed, exactly as the requirement names it.
        Directory.CreateDirectory(Path.Combine(root, "notes"));
        await File.WriteAllTextAsync(Path.Combine(root, "notes", "draft.md"), "draft\n");
        return new History(root, before, done, later);
    }

    [Fact]
    public async Task Each_path_is_read_in_the_commit_as_git_holds_it()
    {
        var history = await HistoryAsync();
        QuestEvidenceItem P(string path) => new(path);
        var wanted = new[]
        {
            new WantedEvidence(1, P("README.md")), new WantedEvidence(1, P("docs/old.md")), new WantedEvidence(1, P("docs")),
            new WantedEvidence(2, P("report.md")), new WantedEvidence(2, P("notes/draft.md")), new WantedEvidence(2, P("gone.md")),
        };

        var reading = await EvidenceReader.ReadAsync(
            new EvidenceAt(history.Root, EvidenceCodes.SessionEnd) { Base = history.Base, Session = "s1" }, wanted);

        Assert.Null(reading.Unread);
        var verdict = reading.Verdict!;
        Assert.Equal(history.Done, verdict.Commit);
        Assert.Equal(
            [
                new EvidenceRead(1, "README.md", null, EvidenceCodes.Found) { Object = await RevAsync(history.Root, $"{history.Done}:README.md"), Changed = true },
                new EvidenceRead(1, "docs/old.md", null, EvidenceCodes.Found) { Object = await RevAsync(history.Root, $"{history.Done}:docs/old.md"), Changed = false },
                new EvidenceRead(1, "docs", null, EvidenceCodes.Found) { Object = await RevAsync(history.Root, $"{history.Done}:docs"), Changed = false },
                new EvidenceRead(2, "report.md", null, EvidenceCodes.Case) { Spelled = "Report.md", Changed = false },
                new EvidenceRead(2, "notes/draft.md", null, EvidenceCodes.Uncommitted) { Changed = false },
                new EvidenceRead(2, "gone.md", null, EvidenceCodes.Missing) { Changed = false },
            ],
            verdict.Items);
        // It read and wrote nothing: the tree is as the fixture left it.
        Assert.Equal("?? notes/", (await GitAsync(history.Root, "status", "--porcelain")).Trim());
    }

    [Fact]
    public async Task A_later_commit_on_the_same_history_is_read_and_one_before_the_done_is_not()
    {
        var history = await HistoryAsync();

        Assert.Equal(EvidencePlace.Same, await EvidenceReader.PlaceAsync(history.Root, history.Done, history.Done[..10]));
        Assert.Equal(EvidencePlace.After, await EvidenceReader.PlaceAsync(history.Root, history.Done, history.Later));
        Assert.Equal(EvidencePlace.Elsewhere, await EvidenceReader.PlaceAsync(history.Root, history.Done, history.Base));
        Assert.Equal(EvidencePlace.Unknown, await EvidenceReader.PlaceAsync(history.Root, history.Done, new string('f', 40)));

        var later = await EvidenceReader.ReadAsync(
            new EvidenceAt(history.Root, EvidenceCodes.Terminal) { Commit = history.Later }, [new WantedEvidence(1, new QuestEvidenceItem("late.md"))]);
        Assert.Equal((history.Later, true), (later.Verdict!.Commit, later.Verdict.Found));
    }

    [Fact]
    public async Task The_commits_since_a_base_are_read_through_the_seam_as_the_record_says_them()
    {
        var history = await HistoryAsync();

        var commits = await WorkingTree.CommitsSinceAsync(history.Root, history.Base, WorkingTree.ReadGitAsync, CancellationToken.None);

        Assert.Equal(await WorkingTree.CommitsSinceAsync(history.Root, history.Base), commits);
        Assert.StartsWith("commits landed:\n", commits);
        Assert.EndsWith(" done", commits);
    }

    private static async Task<string> CommitAsync(string root, string message, params (string File, string Content)[] files)
    {
        foreach (var (file, content) in files)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(root, file))!);
            await File.WriteAllTextAsync(Path.Combine(root, file), content);
        }

        await GitAsync(root, "add", "-A");
        await GitAsync(root, "commit", "-q", "-m", message);
        return await RevAsync(root, "HEAD");
    }

    private static async Task<string> RevAsync(string root, string revision) => (await GitAsync(root, "rev-parse", revision)).Trim();

    /// <summary>git through the fixture's one runner (TESTGIT1), its stdout once it has exited cleanly.</summary>
    private static async Task<string> GitAsync(string cwd, params string[] arguments)
    {
        var run = await GitFixture.RunAsync(cwd, arguments);
        Assert.True(run.ExitCode == 0, $"git {string.Join(' ', arguments)} failed: {run.Stderr}");
        return run.Stdout;
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
