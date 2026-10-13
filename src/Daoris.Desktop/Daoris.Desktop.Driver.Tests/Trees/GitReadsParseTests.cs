using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// GIT1b (D147 §4.1): the machine formats the reads behind a page ask, and the pure readers of what git answers: a page of
/// history, a commit, a file's history, a blame and the branches holding a commit. No process starts here: each answer is
/// git's own bytes, written out in the shape Git 2.53 wrote them over a fixture.
/// </summary>
public sealed class GitReadsParseTests
{
    private const string Root = "1111111111111111111111111111111111111111";
    private const string Side = "2222222222222222222222222222222222222222";
    private const string Line = "3333333333333333333333333333333333333333";
    private const string Merge = "4444444444444444444444444444444444444444";

    /// <summary>One commit as the list's format writes it: seven NUL-separated fields, and <c>-z</c>'s NUL ending the record.</summary>
    private static string Listed(
        string commit, string parents, string subject = "work", string author = "Fixture", string email = "fixture@example.test",
        string authored = "2026-10-04T06:13:35+11:00", string committed = "2026-10-04T06:13:36+11:00", string? mark = null) =>
        (mark is null ? "" : mark + "\0") + string.Join('\0', commit, parents, author, email, authored, committed, subject) + "\0";

    [Fact]
    public void A_page_of_history_asks_a_machine_format_from_its_tips_a_page_at_a_time()
    {
        Assert.Equal(
            ["log", "--no-color", "--no-show-signature", "--encoding=UTF-8", "-z", "--date-order",
             "--format=%H%x00%P%x00%an%x00%ae%x00%aI%x00%cI%x00%s", "--skip=400", "--max-count=201", Line, Side, "--"],
            GitFormats.GraphArguments([Line, Side], sinceParted: false, page: 2));
    }

    /// <summary>A branch's page: what it and its line hold since they parted, <c>A...B</c>, each commit marked with its side.</summary>
    [Fact]
    public void A_walk_since_two_parted_asks_both_sides_and_marks_each()
    {
        Assert.Equal(
            ["log", "--no-color", "--no-show-signature", "--encoding=UTF-8", "-z", "--date-order", "--left-right",
             "--format=%m%x00%H%x00%P%x00%an%x00%ae%x00%aI%x00%cI%x00%s", "--skip=0", "--max-count=201", $"{Side}...{Line}", "--"],
            GitFormats.GraphArguments([Side, Line], sinceParted: true, page: 0));
    }

    [Fact]
    public void Each_commit_is_read_whole_with_its_parents_its_author_and_its_times()
    {
        var commits = GitFormats.ParseListed(
            Listed(Merge, $"{Line} {Side}", subject: "Merge topic")
            + Listed(Side, Root, subject: "a subject with\ttabs and ünïcode", author: "", email: "")
            + Listed(Root, "", subject: "first", authored: ""),
            marked: false);

        Assert.Equal(3, commits.Count);
        var merge = commits[0];
        Assert.Equal((Merge, "Fixture", "fixture@example.test", "Merge topic"), (merge.Commit, merge.Author, merge.AuthorEmail, merge.Subject));
        Assert.Equal([Line, Side], merge.Parents);
        Assert.Equal(new DateTimeOffset(2026, 10, 4, 6, 13, 35, TimeSpan.FromHours(11)), merge.AuthoredAt);
        Assert.Equal(new DateTimeOffset(2026, 10, 4, 6, 13, 36, TimeSpan.FromHours(11)), merge.CommittedAt);
        Assert.Null(merge.Side);

        Assert.Equal(("", "", "a subject with\ttabs and ünïcode"), (commits[1].Author, commits[1].AuthorEmail, commits[1].Subject));
        // A root commit has no parents, and a time git wrote none of is no time.
        Assert.Empty(commits[2].Parents);
        Assert.Null(commits[2].AuthoredAt);
    }

    [Fact]
    public void A_walk_of_two_sides_says_which_side_holds_each_commit()
    {
        var commits = GitFormats.ParseListed(Listed(Side, Root, mark: "<") + Listed(Line, Root, mark: ">"), marked: true);

        Assert.Equal([(Side, GitSide.Left), (Line, GitSide.Right)], commits.Select(each => (each.Commit, each.Side)));
    }

    /// <summary>A record cut short, or one naming no commit, is passed over rather than read askew.</summary>
    [Fact]
    public void A_record_naming_no_commit_or_cut_short_is_passed_over()
    {
        var commits = GitFormats.ParseListed(Listed("not-a-commit", Root) + Listed(Line, Root) + Root + "\0" + Side + "\0", marked: false);

        Assert.Equal([Line], commits.Select(each => each.Commit));
    }

    /// <summary>A commit's own fields: its committer apart from its author, and its whole message, every line of it.</summary>
    [Fact]
    public void A_commit_is_read_with_its_whole_message()
    {
        var answer = string.Join(
            '\0', Merge, $"{Line} {Side}", "Ann", "ann@example.test", "2026-10-04T06:13:35+11:00", "Bo", "bo@example.test",
            "2026-10-04T07:00:00Z", "Merge topic", "Merge topic\n\nThe body,\nover two lines.\n") + "\0";

        var detail = GitFormats.ParseCommit(answer);

        Assert.NotNull(detail);
        Assert.Equal((Merge, "Ann", "ann@example.test", "Bo", "bo@example.test"), (detail!.Commit, detail.Author, detail.AuthorEmail, detail.Committer, detail.CommitterEmail));
        Assert.Equal([Line, Side], detail.Parents);
        Assert.Equal(new DateTimeOffset(2026, 10, 4, 7, 0, 0, TimeSpan.Zero), detail.CommittedAt);
        Assert.Equal(("Merge topic", "Merge topic\n\nThe body,\nover two lines."), (detail.Subject, detail.Message));
        Assert.True(detail.Merge);
        Assert.Equal(Line, detail.Against);
    }

    [Fact]
    public void A_commit_git_did_not_answer_whole_is_none()
    {
        Assert.Null(GitFormats.ParseCommit(""));
        Assert.Null(GitFormats.ParseCommit(string.Join('\0', Merge, Line, "Ann")));
        Assert.Null(GitFormats.ParseCommit(string.Join('\0', "HEAD", "", "a", "b", "", "c", "d", "", "s", "m\n") + "\0"));
    }

    [Fact]
    public void A_file_s_history_follows_renames_and_asks_cumulatively_since_follow_ignores_skip()
    {
        Assert.Equal(
            ["--literal-pathspecs", "log", "--no-color", "--no-show-signature", "--encoding=UTF-8", "-z", "--follow", "--name-status",
             "--format=%x01%H%x00%P%x00%an%x00%ae%x00%aI%x00%cI%x00%s", "--max-count=201", Merge, "--", "src/c d.txt"],
            GitFormats.HistoryArguments(Merge, "src/c d.txt", page: 1));
    }

    /// <summary>
    /// <c>log --follow --name-status -z</c>: each commit's fields, then the path as it changed there, so a rename names both
    /// paths and each commit's row knows the path to blame at that commit. Shaped as Git 2.53 wrote it.
    /// </summary>
    [Fact]
    public void Each_change_to_a_file_names_the_path_it_had_at_that_commit()
    {
        var answer =
            "\u0001" + string.Join('\0', Merge, Line, "F", "f@example.test", "2026-10-04T06:13:37+11:00", "2026-10-04T06:13:37+11:00", "rename a to c")
            + "\0\nR100\0a.txt\0c.txt\0"
            + "\u0001" + string.Join('\0', Side, Root, "F", "f@example.test", "2026-10-04T06:13:34+11:00", "2026-10-04T06:13:34+11:00", "topic: three")
            + "\0\nM\0a.txt\0"
            + "\u0001" + string.Join('\0', Line, Root, "F", "f@example.test", "2026-10-04T06:13:35+11:00", "2026-10-04T06:13:35+11:00", "touch, no status")
            + "\0"
            + "\u0001" + string.Join('\0', Root, "", "F", "f@example.test", "2026-10-04T06:13:33+11:00", "2026-10-04T06:13:33+11:00", "first")
            + "\0\nA\0a.txt\0";

        var changes = GitFormats.ParseHistory(answer);

        Assert.Equal(
            [(Merge, "renamed", "c.txt", (string?)"a.txt"), (Side, "modified", "a.txt", null), (Line, "modified", "", null), (Root, "added", "a.txt", null)],
            changes.Select(each => (each.Commit.Commit, each.Status, each.Path, each.From)));
        Assert.Equal("rename a to c", changes[0].Commit.Subject);
        Assert.Equal([Line], changes[0].Commit.Parents);
    }

    [Fact]
    public void A_blame_asks_the_porcelain_form_in_utf8_with_paths_as_themselves()
    {
        Assert.Equal(
            ["-c", "core.quotePath=false", "blame", "--porcelain", "--encoding=UTF-8", Merge, "--", "c.txt"],
            GitFormats.BlameArguments(Merge, "c.txt"));
    }

    /// <summary><c>git blame --porcelain</c> as Git 2.53 wrote it over the probe: a commit's details once, then its lines.</summary>
    private static readonly string Porcelain = string.Concat(
        $"{Root} 1 1 2\n",
        "author F\nauthor-mail <f@example.test>\nauthor-time 1791054813\nauthor-tz +1100\n",
        "committer F\ncommitter-mail <f@example.test>\ncommitter-time 1791054813\ncommitter-tz +1100\n",
        "summary first\nboundary\nfilename a.txt\n",
        "\tone\n",
        $"{Root} 2 2\n",
        "\ttwo\n",
        $"{Side} 3 3 1\n",
        "author Ann\nauthor-mail <ann@example.test>\nauthor-time 1791054814\nauthor-tz -0230\n",
        "committer F\ncommitter-mail <f@example.test>\ncommitter-time 1791054814\ncommitter-tz +1100\n",
        $"summary topic: three\nprevious {Root} a.txt\nfilename a.txt\n",
        "\tthree\r\n",
        // The first commit again, a group later: its details are not written twice.
        $"{Root} 3 4 1\n",
        "\tfour 中文\n");

    [Fact]
    public void A_blame_is_each_run_of_lines_with_its_commit_and_each_commit_said_once()
    {
        var blame = GitFormats.ParseBlame(Porcelain, Merge, "a.txt");

        Assert.False(blame.Binary);
        Assert.Null(blame.StoppedAt);
        Assert.Equal(
            [(Root, 1, "a.txt", (string?)null, new[] { (1, "one"), (2, "two") }),
             (Side, 3, "a.txt", Root, new[] { (3, "three\r") }),
             (Root, 4, "a.txt", null, new[] { (3, "four 中文") })],
            blame.Runs.Select(run => (run.Commit, run.Line, run.Path, run.Previous, run.Lines.Select(line => (line.Original, line.Text)).ToArray())));
        Assert.Equal("a.txt", blame.Runs[1].PreviousPath);

        Assert.Equal(2, blame.Commits.Count);
        var first = blame.Commits[Root];
        Assert.Equal(("F", "f@example.test", "first", true), (first.Author, first.AuthorEmail, first.Summary, first.Boundary));
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1791054813).ToOffset(TimeSpan.FromHours(11)), first.AuthoredAt);
        var topic = blame.Commits[Side];
        Assert.Equal(("Ann", false), (topic.Author, topic.Boundary));
        Assert.Equal(TimeSpan.FromMinutes(-150), topic.AuthoredAt!.Value.Offset);
    }

    /// <summary>git hands its answer over in pieces of whatever size the pipe gives; a piece may end anywhere.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(4096)]
    public void A_blame_reads_the_same_however_git_hands_it_over(int piece)
    {
        var whole = GitFormats.ParseBlame(Porcelain, Merge, "a.txt");
        var split = new GitBlameSplit();
        for (var at = 0; at < Porcelain.Length; at += piece)
        {
            Assert.True(split.Take(Porcelain.AsSpan(at, Math.Min(piece, Porcelain.Length - at))));
        }

        var pieces = split.End(Merge, "a.txt");
        Assert.Equal(Flat(whole), Flat(pieces));
        Assert.Equal(whole.Commits.Keys.Order(), pieces.Commits.Keys.Order());
    }

    /// <summary>
    /// A path git quotes on its <c>filename</c> line, under <c>core.quotePath=false</c>: a double quote, a backslash or a
    /// control character, each escaped, the path inside quotes. Read back as the path itself.
    /// </summary>
    [Fact]
    public void A_path_git_quotes_is_read_back_as_itself()
    {
        var answer = $"{Root} 1 1 1\nauthor F\nauthor-mail <f@example.test>\nauthor-time 1\nauthor-tz +0000\nsummary s\n"
                     + "filename \"quo\\\"te\\ttab\\001\\\\中.txt\"\n\tx\n";

        Assert.Equal("quo\"te\ttab\u0001\\中.txt", Assert.Single(GitFormats.ParseBlame(answer, Merge, "x").Runs).Path);
        Assert.Equal("plain name.txt", GitFormats.Unquoted("plain name.txt"));
        Assert.Equal("é", GitFormats.Unquoted("\"\\303\\251\""));
    }

    /// <summary>
    /// D111's binary test holds (git's own: a NUL in the first 8,000 bytes): a binary file has a history and no blame, and
    /// once it is known to be binary nothing more of git's answer is wanted.
    /// </summary>
    [Fact]
    public void A_binary_file_has_no_blame_and_git_is_stopped_once_it_is_known()
    {
        var split = new GitBlameSplit();
        var answer = $"{Root} 1 1 2\nauthor F\nauthor-mail <f@example.test>\nauthor-time 1\nauthor-tz +0000\nsummary s\nfilename b.dat\n\tx\0y\n";

        Assert.False(split.Take(answer));

        var blame = split.End(Merge, "b.dat");
        Assert.True(blame.Binary);
        Assert.Empty(blame.Runs);
        Assert.True(split.Stopped);
    }

    /// <summary>A NUL past the first 8,000 bytes is not git's binary file, so it is not this one's either.</summary>
    [Fact]
    public void A_nul_past_the_probe_is_text()
    {
        var text = new string('a', FilePreview.BinaryProbe);
        var answer = $"{Root} 1 1 2\nauthor F\nauthor-mail <f@example.test>\nauthor-time 1\nauthor-tz +0000\nsummary s\nfilename b.dat\n\t{text}\n"
                     + $"{Root} 2 2\n\tx\0y\n";

        var blame = GitFormats.ParseBlame(answer, Merge, "b.dat");

        Assert.False(blame.Binary);
        Assert.Equal(2, blame.Runs.Single().Lines.Count);
    }

    /// <summary>
    /// D111's bound holds: the first <see cref="FilePreview.Budget"/> bytes, cut at a line's end, and the blame says the last
    /// line it shows. git is stopped there rather than read to the end.
    /// </summary>
    [Fact]
    public void A_long_file_s_blame_stops_at_the_bound_and_says_where()
    {
        var line = new string('x', 999); // with its newline, a thousand bytes
        var split = new GitBlameSplit();
        var answer = new System.Text.StringBuilder(
            $"{Root} 1 1 400\nauthor F\nauthor-mail <f@example.test>\nauthor-time 1\nauthor-tz +0000\nsummary s\nfilename long.txt\n\t{line}\n");
        for (var i = 2; i <= 400; i++) answer.Append($"{Root} {i} {i}\n\t{line}\n");

        var wanted = split.Take(answer.ToString());

        Assert.False(wanted);
        var blame = split.End(Merge, "long.txt");
        var shown = FilePreview.Budget / 1000;
        Assert.Equal(shown, blame.StoppedAt);
        Assert.Equal(shown, blame.Runs.Sum(run => run.Lines.Count));
    }

    [Fact]
    public void The_branches_holding_a_commit_are_read_without_origin_s_head()
    {
        Assert.Equal(
            ["for-each-ref", "--contains", Merge, "--format=%(refname)%00%(symref)%00", "refs/heads", "refs/remotes/origin"],
            GitFormats.HoldingArguments(Merge));
        Assert.Equal(
            ["refs/heads/main", "refs/heads/topic", "refs/remotes/origin/main"],
            GitFormats.ParseHolding(
                "refs/heads/main\0\0\nrefs/heads/topic\0\0\nrefs/remotes/origin/HEAD\0refs/remotes/origin/main\0\nrefs/remotes/origin/main\0\0\n"));
    }

    /// <summary>
    /// Each read says the command a person types in the checkout for the same answer in git's own form (D147 §3.3): the
    /// read asks a machine form, which a terminal would print as NULs.
    /// </summary>
    [Fact]
    public void Each_read_says_the_command_a_person_types_for_it()
    {
        Assert.Equal($"git log --graph --date-order {Line} {Side}", GitFormats.GraphCommand([Line, Side], sinceParted: false));
        Assert.Equal($"git log --graph --date-order --left-right {Side}...{Line}", GitFormats.GraphCommand([Side, Line], sinceParted: true));
        Assert.Equal([$"git show -s {Merge}", $"git diff {Line} {Merge}"], GitFormats.CommitCommands(Merge, Line));
        Assert.Equal([$"git show -s {Root}", $"git show --format= {Root}"], GitFormats.CommitCommands(Root, against: null));
        Assert.Equal($"git log --follow {Merge} -- \"src/c d.txt\"", GitFormats.HistoryCommand(Merge, "src/c d.txt"));
        Assert.Equal($"git --literal-pathspecs log --follow {Merge} -- \"a*.txt\"", GitFormats.HistoryCommand(Merge, "a*.txt"));
        Assert.Equal($"git blame {Merge} -- c.txt", GitFormats.BlameCommand(Merge, "c.txt"));
        Assert.Equal(
            [$"git log --left-right --date-order {Side}...{Line}", $"git diff {Side}...{Line}"],
            GitFormats.CompareCommands(Side, Line, everything: false));
        Assert.Equal($"git diff {Side}..{Line}", GitFormats.CompareCommands(Side, Line, everything: true)[1]);
        Assert.Equal("git rev-parse --verify \"main^{commit}\"", GitFormats.ResolveCommand("main"));
    }

    private static IReadOnlyList<(string, int, string, string?, string)> Flat(GitBlame blame) =>
        [.. blame.Runs.Select(run => (run.Commit, run.Line, run.Path, run.Previous, string.Join('\n', run.Lines.Select(line => $"{line.Original}:{line.Text}"))))];
}
