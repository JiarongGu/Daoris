using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// EVID1b (D144 §3): what Daoris reads of a done's evidence in one commit, through the review's git seam with git standing
/// in: each path found with its object, in another case, in the tree uncommitted, or missing; whether this work changed it
/// since the tree's base; and a gate answered <c>no-queue</c> until the landing queue reads gates (EVID1d). It reads and
/// never writes, and each path reaches git as one argument, judged again first. <c>EvidenceGitTests</c> holds the same reads
/// against real git.
/// </summary>
/// <remarks>Reads and writes files only (the tree's own, for what is uncommitted), so it is in the fast half (MOD8).</remarks>
public sealed class EvidenceReaderTests : IDisposable
{
    private readonly string _tree = Path.Combine(Path.GetTempPath(), "daoris-evidence-" + Guid.NewGuid().ToString("N")[..8]);

    public EvidenceReaderTests() => Directory.CreateDirectory(_tree);

    public void Dispose()
    {
        try { Directory.Delete(_tree, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static QuestEvidenceItem Path_(string path) => new(path);

    /// <summary>A done that answered four requirements: 1, 2 and 4 met, 3 departed, each naming evidence.</summary>
    private static QuestView Done() =>
        new("q1", "ask #a1", "reports", "Build the report", "b", "Done")
        {
            Requirements =
            [
                new("use the common-report module", "a common-report entry") { Evidence = [Path_("README.md"), Path_("docs/old.md"), Path_("docs")] },
                new("write it up", "a page in docs") { Evidence = [Path_("docs/report.md"), Path_("notes/draft.md")] },
                new("no new types", "none added") { Evidence = [Path_("types.md")] },
                new("keep it tested", "the web gate passes") { Evidence = [Path_("missing.md"), new QuestEvidenceItem(null, "web")] },
            ],
            Answers =
            [
                new(1, "it is one", null, null),
                new(2, "written", null, null),
                new(3, null, "a type was needed", "no new types"),
                new(4, "tested", null, null),
            ],
        };

    private (StandInGit Git, string Base, string Head) History()
    {
        var git = new StandInGit(_tree);
        var before = git.Commit("base", ("README.md", "r"), ("docs/old.md", "old"));
        var head = git.Commit("done", ("README.md", "r2"), ("docs/old.md", "old"), ("Docs/Report.md", "report"));
        // Written but never committed, exactly as named; and one in another case on disk, which is not the path named.
        Directory.CreateDirectory(Path.Combine(_tree, "notes"));
        File.WriteAllText(Path.Combine(_tree, "notes", "draft.md"), "draft");
        File.WriteAllText(Path.Combine(_tree, "MISSING.md"), "not the one");
        return (git, before, head);
    }

    [Fact]
    public void A_done_waits_on_each_item_of_each_requirement_it_met_once_and_none_of_a_departure()
    {
        var quest = Done() with
        {
            Requirements = [.. Done().Requirements.Select((r, i) => i == 0 ? r with { Evidence = [.. r.Evidence, Path_("README.md")] } : r)],
        };

        Assert.Equal(
            [
                new WantedEvidence(1, Path_("README.md")), new WantedEvidence(1, Path_("docs/old.md")), new WantedEvidence(1, Path_("docs")),
                new WantedEvidence(2, Path_("docs/report.md")), new WantedEvidence(2, Path_("notes/draft.md")),
                new WantedEvidence(4, Path_("missing.md")), new WantedEvidence(4, new QuestEvidenceItem(null, "web")),
            ],
            EvidenceReader.Wanted(quest));
        // Not done, nothing waits; an answer numbering no requirement waits on nothing.
        Assert.Empty(EvidenceReader.Wanted(Done() with { Status = "Taken" }));
        Assert.Empty(EvidenceReader.Wanted(Done() with { Answers = [new(9, "met", null, null)] }));
    }

    [Fact]
    public async Task Each_path_is_found_in_another_case_uncommitted_or_missing_and_a_gate_waits_on_the_queue()
    {
        var (git, before, head) = History();

        var reading = await EvidenceReader.ReadAsync(
            new EvidenceAt(_tree, EvidenceCodes.SessionEnd) { Base = before, Session = "s1" }, EvidenceReader.Wanted(Done()), git.Read,
            CancellationToken.None);

        Assert.Null(reading.Unread);
        var verdict = reading.Verdict!;
        Assert.Equal((head, EvidenceCodes.SessionEnd, "s1"), (verdict.Commit, verdict.How, verdict.Session));
        Assert.Equal(
            [
                new EvidenceRead(1, "README.md", null, EvidenceCodes.Found) { Object = StandInGit.Blob("r2"), Changed = true },
                new EvidenceRead(1, "docs/old.md", null, EvidenceCodes.Found) { Object = StandInGit.Blob("old"), Changed = false },
                new EvidenceRead(1, "docs", null, EvidenceCodes.Found) { Object = verdict.Items[2].Object, Changed = false },
                new EvidenceRead(2, "docs/report.md", null, EvidenceCodes.Case) { Spelled = "Docs/Report.md", Changed = false },
                new EvidenceRead(2, "notes/draft.md", null, EvidenceCodes.Uncommitted) { Changed = false },
                new EvidenceRead(4, "missing.md", null, EvidenceCodes.Missing) { Changed = false },
                new EvidenceRead(4, null, "web", EvidenceCodes.NoQueue),
            ],
            verdict.Items);
        // A folder is found as its tree.
        Assert.True(EvidenceCodes.IsObjectId(verdict.Items[2].Object));
        Assert.False(verdict.Found);
    }

    [Fact]
    public async Task Each_path_reaches_git_as_one_argument_beside_the_commit_and_nothing_is_written()
    {
        var (git, _, head) = History();

        await EvidenceReader.ReadAsync(new EvidenceAt(_tree, EvidenceCodes.SessionEnd), EvidenceReader.Wanted(Done()), git.Read, CancellationToken.None);

        Assert.Contains(git.Calls, call => call.SequenceEqual(["rev-parse", "--verify", "--quiet", $"{head}:docs/report.md"]));
        Assert.Contains(git.Calls, call => call.SequenceEqual(["rev-parse", "--verify", "--quiet", $"{head}:notes/draft.md"]));
        // Reads only: every verb is one of git's readers.
        Assert.All(git.Calls, call => Assert.Contains(call[0], new[] { "rev-parse", "ls-tree" }));
        // No base named: every path is asked of the commit read alone.
        Assert.All(
            git.CallsTo("rev-parse", "--verify", "--quiet").Where(call => call[3].Contains(':')),
            call => Assert.StartsWith(head + ":", call[3]));
    }

    [Fact]
    public async Task With_no_base_whether_the_work_changed_it_is_not_read()
    {
        var (git, _, _) = History();

        var reading = await EvidenceReader.ReadAsync(
            new EvidenceAt(_tree, EvidenceCodes.SessionEnd), [new WantedEvidence(1, Path_("README.md"))], git.Read, CancellationToken.None);

        Assert.Null(Assert.Single(reading.Verdict!.Items).Changed);
    }

    [Fact]
    public async Task The_commit_read_is_the_trees_HEAD_by_its_full_id_unless_one_is_named()
    {
        var (git, before, head) = History();
        var wanted = new[] { new WantedEvidence(1, Path_("docs/old.md")) };

        var atHead = await EvidenceReader.ReadAsync(new EvidenceAt(_tree, EvidenceCodes.Terminal), wanted, git.Read, CancellationToken.None);
        var named = await EvidenceReader.ReadAsync(
            new EvidenceAt(_tree, EvidenceCodes.Terminal) { Commit = before[..9].ToUpperInvariant() }, wanted, git.Read, CancellationToken.None);

        Assert.Equal(head, atHead.Verdict!.Commit);
        Assert.Equal(before, named.Verdict!.Commit);
        Assert.True(named.Verdict.Found);
    }

    [Theory]
    [InlineData("--output=x")]
    [InlineData("HEAD")]
    [InlineData("abc")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0")]
    public async Task A_named_commit_that_is_not_a_commit_id_is_never_handed_to_git(string named)
    {
        var (git, _, _) = History();

        var reading = await EvidenceReader.ReadAsync(
            new EvidenceAt(_tree, EvidenceCodes.Terminal) { Commit = named }, [new WantedEvidence(1, Path_("README.md"))], git.Read,
            CancellationToken.None);

        Assert.Null(reading.Verdict);
        Assert.Contains("is not a commit id", reading.Unread);
        Assert.DoesNotContain(git.Calls, call => call.Any(argument => argument.Contains(named, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_commit_git_does_not_hold_is_unread_and_says_so()
    {
        var (git, _, _) = History();

        var reading = await EvidenceReader.ReadAsync(
            new EvidenceAt(_tree, EvidenceCodes.Terminal) { Commit = "deadbeefdeadbeef" }, [new WantedEvidence(1, Path_("README.md"))],
            git.Read, CancellationToken.None);

        Assert.Null(reading.Verdict);
        Assert.Contains("`deadbeefdeadbeef`", reading.Unread);
    }

    /// <summary>A path the service would refuse (a twin's judge, read again here) is never a git argument; nothing is read.</summary>
    [Theory]
    [InlineData("../outside.md")]
    [InlineData("-c")]
    [InlineData(".git/config")]
    [InlineData("C:/work/x.md")]
    [InlineData("docs\\x.md")]
    public async Task A_path_the_judge_refuses_is_read_by_nobody(string path)
    {
        var (git, _, _) = History();

        var reading = await EvidenceReader.ReadAsync(
            new EvidenceAt(_tree, EvidenceCodes.SessionEnd), [new WantedEvidence(2, Path_(path))], git.Read, CancellationToken.None);

        Assert.Null(reading.Verdict);
        Assert.StartsWith($"requirement 2 names `{path}`, which is not a path Daoris asks git for:", reading.Unread);
        Assert.Empty(git.Calls);
    }

    /// <summary>🔴 git walks UP: a tree that is gone, or is not the top of its own repository, is unread, and git is asked nothing more.</summary>
    [Fact]
    public async Task A_tree_that_is_not_the_top_of_its_own_repository_is_unread()
    {
        var git = new StandInGit(Path.GetDirectoryName(_tree)!);
        git.Commit("done", ("README.md", "r"));

        var reading = await EvidenceReader.ReadAsync(
            new EvidenceAt(_tree, EvidenceCodes.SessionEnd), [new WantedEvidence(1, Path_("README.md"))], git.Read, CancellationToken.None);
        var gone = await EvidenceReader.ReadAsync(
            new EvidenceAt(Path.Combine(_tree, "gone"), EvidenceCodes.SessionEnd), [new WantedEvidence(1, Path_("README.md"))], git.Read,
            CancellationToken.None);

        Assert.Null(reading.Verdict);
        Assert.Contains("not the top of a repository of its own", reading.Unread);
        Assert.Equal(reading.Unread, gone.Unread);
        Assert.Equal([["rev-parse", "--show-toplevel"]], git.Calls);
    }

    [Fact]
    public async Task A_git_that_does_not_start_reads_nothing()
    {
        var git = new StandInGit(_tree) { Broken = true };

        var reading = await EvidenceReader.ReadAsync(
            new EvidenceAt(_tree, EvidenceCodes.SessionEnd), [new WantedEvidence(1, Path_("README.md"))], git.Read, CancellationToken.None);

        Assert.Null(reading.Verdict);
        Assert.NotNull(reading.Unread);
    }

    [Fact]
    public async Task Nothing_wanted_is_nothing_to_read()
    {
        var (git, _, _) = History();

        var reading = await EvidenceReader.ReadAsync(new EvidenceAt(_tree, EvidenceCodes.SessionEnd), [], git.Read, CancellationToken.None);

        Assert.Null(reading.Verdict);
        Assert.Contains("name none", reading.Unread);
        Assert.Empty(git.Calls);
    }

    /// <summary>The commit a check reads must be the done's or come after it on the same history (D144 §3).</summary>
    [Fact]
    public async Task A_commit_is_placed_against_the_dones_on_its_history()
    {
        var git = new StandInGit(_tree);
        var first = git.Commit("first", ("a", "1"));
        var done = git.Commit("done", ("a", "2"));
        var later = git.Commit("later", ("a", "3"));

        Assert.Equal(EvidencePlace.Same, await EvidenceReader.PlaceAsync(_tree, done, done[..8], git.Read, CancellationToken.None));
        Assert.Equal(EvidencePlace.After, await EvidenceReader.PlaceAsync(_tree, done, later, git.Read, CancellationToken.None));
        Assert.Equal(EvidencePlace.Elsewhere, await EvidenceReader.PlaceAsync(_tree, done, first, git.Read, CancellationToken.None));
        Assert.Equal(EvidencePlace.Unknown, await EvidenceReader.PlaceAsync(_tree, done, "feedfacefeedface", git.Read, CancellationToken.None));
        Assert.Equal(EvidencePlace.Unknown, await EvidenceReader.PlaceAsync(_tree, done, "--all", git.Read, CancellationToken.None));
    }

    /// <summary>The service's judge, row for row (<c>QuestEvidence.JudgePath</c>): the two share no code.</summary>
    [Theory]
    [InlineData("docs/report.md", null)]
    [InlineData("a b/c.d", null)]
    [InlineData("", "it is empty")]
    [InlineData("a\tb", "it holds a control character")]
    [InlineData("docs\\x", "it holds a backslash, and a path is written with forward slashes")]
    [InlineData("/etc/x", "it starts with `/`, and a path is relative to the repository's root")]
    [InlineData("C:/x", "it names a drive, and a path is relative to the repository's root")]
    [InlineData("-x", "it starts with `-`, which git would read as an option")]
    [InlineData("a//b", "it holds an empty segment (a doubled or trailing `/`), and a folder is named without one")]
    [InlineData("a/", "it holds an empty segment (a doubled or trailing `/`), and a folder is named without one")]
    [InlineData("./a", "it holds a `.` segment, and a path is named one way, from the repository's root")]
    [InlineData("a/../b", "it holds a `..` segment, which reaches outside the path it names")]
    [InlineData("a/.GIT/b", "it reaches into `.git`, which is git's, never the work's")]
    public void A_path_is_judged_as_the_service_judges_it(string path, string? why) => Assert.Equal(why, EvidencePaths.Judge(path));

    [Fact]
    public void A_path_longer_than_the_bound_is_refused() =>
        Assert.Equal("it is longer than 300 characters", EvidencePaths.Judge(new string('a', 301)));
}
