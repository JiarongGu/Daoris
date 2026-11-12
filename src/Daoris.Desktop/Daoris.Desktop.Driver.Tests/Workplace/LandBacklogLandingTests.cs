using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A landing's replay onto the line's tip that conflicts only in the backlog the
/// line's <c>daoris.json</c> declares settles it by the merge base, as the merge tool settles it: a row one side added is
/// kept, one a side removed is dropped. Refusing a conflict in <c>TASKS.md</c> alone would cost the session, in a repository where
/// every session archives its own row, a rebase turn over a line the merge tool
/// settles by itself. A row both sides edited, a backlog conflict beside one in code, and a file only the session declares its backlog
/// stay the session's conflict, refused with the rebase words. Real git in a scratch repository, so this is the suite's Process half.
/// The rule itself is <c>BacklogMergeTests</c>' table.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class LandBacklogLandingTests : LandedFixture
{
    private static readonly LandingSubject Subject = new("s1a2b3c4", "0fda18", "Fix the gap");

    private const string Branch = "feature/0fda18-fix-the-gap";

    private const string Rows = "# Tasks\n\n- [ ] **A1** one.\n- [ ] **A2** two.\n- [ ] **A3** three.\n";

    /// <summary>
    /// The line declares <c>docs/backlog.md</c> its backlog. The work archives row A2 while the line adds B1 above it, which git reads as
    /// one conflict: the plan says none, the press lands the work replayed onto the line's tip with both changes, and says it settled it.
    /// </summary>
    [Fact]
    public async Task A_conflict_only_in_the_declared_backlog_is_settled_by_its_base_and_lands_replayed()
    {
        var root = await RepositoryAsync("engine");
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        await File.WriteAllTextAsync(Path.Combine(root, "docs", "backlog.md"), Rows);
        await CommitAsync(root, "daoris.json", "{ \"documents\": { \"backlog\": { \"path\": \"docs/backlog.md\" } } }\n", "the backlog declared");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "work.txt"), "the work\n");
        await CommitAsync(tree.Path, Path.Combine("docs", "backlog.md"), Rows.Replace("- [ ] **A2** two.\n", ""), "the work");
        await CommitAsync(root, Path.Combine("docs", "backlog.md"), Rows.Replace("**A1** one.\n", "**A1** one.\n- [ ] **B1** the line's.\n"), "the line's row");
        var line = await HeadAsync(root);

        var plan = await trees.PlanAsync(tree.Path, Subject);
        Assert.Null(plan.Conflict);
        Assert.Equal(["the work"], plan.Lands!.Select(commit => commit.Subject));

        var landed = await trees.LandAsync(tree.Path, Subject, gate: Lands);

        Assert.True(landed.Landed, landed.Message);
        Assert.StartsWith($"put the work on `{Branch}` — 1 commit(s) replayed onto `main`, which moved since the work began.", landed.Message);
        Assert.Contains(
            " The backlog `docs/backlog.md` conflicted with the line's and was settled by the merge base: 1 line(s) a side added kept, "
            + "1 line(s) a side removed dropped.",
            landed.Message);
        Assert.Equal(line, (await GitAsync(root, "rev-parse", $"{Branch}~1")).Trim());
        Assert.Equal("# Tasks\n\n- [ ] **A1** one.\n- [ ] **B1** the line's.\n- [ ] **A3** three.\n", await GitAsync(root, "show", $"{Branch}:docs/backlog.md"));
        Assert.Equal("the work\n", await GitAsync(root, "show", $"{Branch}:work.txt"));
        Assert.Equal((await GitAsync(root, "rev-parse", Branch)).Trim(), await HeadAsync(tree.Path));
        Assert.Equal(2, await TreesAsync(root));
    }

    /// <summary>
    /// One row both sides edited is the session's, as the merge tool leaves it for a hand: refused with git's line and the rebase words,
    /// nothing made and the tree unchanged. With no manifest the backlog is <c>TASKS.md</c>.
    /// </summary>
    [Fact]
    public async Task A_backlog_row_edited_on_both_sides_is_refused_with_the_rebase_words()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "TASKS.md", Rows, "the backlog");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "TASKS.md", Rows.Replace("**A2** two.", "**A2** two, the work's."), "the work");
        await CommitAsync(root, "TASKS.md", Rows.Replace("**A2** two.", "**A2** two, the line's."), "the line's edit");
        var before = await StateAsync(root, tree);

        var plan = await trees.PlanAsync(tree.Path, Subject);
        Assert.Contains("TASKS.md", plan.Conflict!.Says);

        var refused = await trees.LandAsync(tree.Path, Subject, gate: Lands);

        Assert.False(refused.Landed, refused.Message);
        Assert.Equal(LandingCode.Refused, refused.Refusal);
        Assert.StartsWith("Nothing was landed: replaying its own commits onto `main` conflicts with what the line changed since: CONFLICT", refused.Message);
        Assert.Contains("CONFLICT (content): Merge conflict in TASKS.md", refused.Conflict!.Says);
        Assert.StartsWith("Rebase your work onto `main`: the line moved since your work began, and landing it conflicts with what the line changed: CONFLICT",
            refused.Conflict.Words);
        Assert.Equal(before, await StateAsync(root, tree));
        Assert.Equal(2, await TreesAsync(root));
    }

    /// <summary>A backlog conflict its base would settle, beside one in code, stays the session's: refused, the code's file named.</summary>
    [Fact]
    public async Task A_backlog_conflict_beside_a_code_conflict_is_refused()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "TASKS.md", Rows, "the backlog");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "shared.txt"), "one\nfrom the tree\n");
        await CommitAsync(tree.Path, "TASKS.md", Rows.Replace("- [ ] **A2** two.\n", ""), "the work");
        await File.WriteAllTextAsync(Path.Combine(root, "shared.txt"), "one\nfrom the line\n");
        await CommitAsync(root, "TASKS.md", Rows.Replace("**A1** one.\n", "**A1** one.\n- [ ] **B1** the line's.\n"), "the line's own");
        var before = await StateAsync(root, tree);

        var plan = await trees.PlanAsync(tree.Path, Subject);
        Assert.Contains("shared.txt", plan.Conflict!.Says);

        var refused = await trees.LandAsync(tree.Path, Subject, gate: Lands);

        Assert.False(refused.Landed, refused.Message);
        Assert.Contains("Merge conflict in shared.txt", refused.Conflict!.Says);
        Assert.StartsWith("Rebase your work onto `main`", refused.Conflict.Words);
        Assert.Equal(before, await StateAsync(root, tree));
        Assert.Equal(2, await TreesAsync(root));
    }

    /// <summary>
    /// The declaration read is the line's, never the session's: a session that declares <c>TASKS.md</c> its backlog in its own commit,
    /// where the line declares another file, has its <c>TASKS.md</c> conflict refused as any file's is.
    /// </summary>
    [Fact]
    public async Task A_backlog_only_the_session_declared_is_not_settled()
    {
        var root = await RepositoryAsync("engine");
        await File.WriteAllTextAsync(Path.Combine(root, "TASKS.md"), Rows);
        await CommitAsync(root, "daoris.json", "{ \"documents\": { \"backlog\": \"docs/backlog.md\" } }\n", "another backlog declared");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "daoris.json"), "{ \"documents\": { \"backlog\": \"TASKS.md\" } }\n");
        await CommitAsync(tree.Path, "TASKS.md", Rows.Replace("- [ ] **A2** two.\n", ""), "the work");
        await CommitAsync(root, "TASKS.md", Rows.Replace("**A1** one.\n", "**A1** one.\n- [ ] **B1** the line's.\n"), "the line's row");
        var before = await StateAsync(root, tree);

        var plan = await trees.PlanAsync(tree.Path, Subject);
        Assert.Contains("TASKS.md", plan.Conflict!.Says);

        var refused = await trees.LandAsync(tree.Path, Subject, gate: Lands);

        Assert.False(refused.Landed, refused.Message);
        Assert.Contains("Merge conflict in TASKS.md", refused.Conflict!.Says);
        Assert.Equal(before, await StateAsync(root, tree));
    }

    private const string Census = "# Guide\n\n| When | Run |\n|---|---|\n| a test names a login | `node tools/live-logins.mjs` |\n{0}\nNext.\n";

    private static string CensusWith(params string[] rows) => string.Format(Census, string.Concat(rows.Select(row => row + "\n")));

    private const string RowsDeclared = "{ \"integration\": { \"merge\": [ { \"paths\": \"docs/development.md\", \"by\": \"rows\" } ] } }\n";

    /// <summary>
    /// The line's <c>daoris.gates.json</c> declares <c>docs/development.md</c> a row table. The work and the line each add a
    /// row at the census table's last line, which git reads as one conflict: the press lands
    /// the work replayed with both rows, the line's first, and says it settled the table.
    /// </summary>
    [Fact]
    public async Task A_conflict_only_in_a_declared_row_table_is_settled_by_its_base_and_lands_replayed()
    {
        var root = await RepositoryAsync("engine");
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        await File.WriteAllTextAsync(Path.Combine(root, "docs", "development.md"), CensusWith());
        await CommitAsync(root, "daoris.gates.json", RowsDeclared, "the row table declared");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, Path.Combine("docs", "development.md"), CensusWith("| a tool reads git | `node tools/optional-locks.mjs` |"), "the work");
        await CommitAsync(root, Path.Combine("docs", "development.md"), CensusWith("| a test waits | `node tools/short-waits.mjs` |"), "the line's row");

        var plan = await trees.PlanAsync(tree.Path, Subject);
        Assert.Null(plan.Conflict);

        var landed = await trees.LandAsync(tree.Path, Subject, gate: Lands);

        Assert.True(landed.Landed, landed.Message);
        Assert.Contains(
            " The declared row table `docs/development.md` conflicted with the line's and was settled by the merge base: 2 line(s) a side added kept, "
            + "0 line(s) a side removed dropped.",
            landed.Message);
        Assert.DoesNotContain("The backlog", landed.Message);
        Assert.Equal(
            CensusWith("| a test waits | `node tools/short-waits.mjs` |", "| a tool reads git | `node tools/optional-locks.mjs` |"),
            await GitAsync(root, "show", $"{Branch}:docs/development.md"));
    }

    /// <summary>The same table declared only by the session's own commit is not the line's declaration: refused as any file's conflict is.</summary>
    [Fact]
    public async Task A_row_table_only_the_session_declared_is_not_settled()
    {
        var root = await RepositoryAsync("engine");
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        await CommitAsync(root, Path.Combine("docs", "development.md"), CensusWith(), "the guide");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "daoris.gates.json"), RowsDeclared);
        await CommitAsync(tree.Path, Path.Combine("docs", "development.md"), CensusWith("| a tool reads git | `node tools/optional-locks.mjs` |"), "the work");
        await CommitAsync(root, Path.Combine("docs", "development.md"), CensusWith("| a test waits | `node tools/short-waits.mjs` |"), "the line's row");
        var before = await StateAsync(root, tree);

        var refused = await trees.LandAsync(tree.Path, Subject, gate: Lands);

        Assert.False(refused.Landed, refused.Message);
        Assert.Contains("Merge conflict in docs/development.md", refused.Conflict!.Says);
        Assert.Equal(before, await StateAsync(root, tree));
    }

    /// <summary>
    /// The work and the line each append an entry to a union-merged record (the task archive). Git's union merge keeps the
    /// blank line they both open with once, so the work's heading lands under the line's last line; the replay sets it apart in a commit on
    /// top, as the merge tool does, and says so. The entry the line already held that way is left alone.
    /// </summary>
    [Fact]
    public async Task A_heading_union_joined_to_the_line_above_in_the_replay_is_set_apart_in_a_commit_on_top()
    {
        var root = await RepositoryAsync("engine");
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        const string Archive = "# Archive\n\n## OLD - old (2026-10-01)\nOld.\n";
        await File.WriteAllTextAsync(Path.Combine(root, ".gitattributes"), "docs/task-archive.md merge=union\n");
        await CommitAsync(root, Path.Combine("docs", "task-archive.md"), Archive, "the archive");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, Path.Combine("docs", "task-archive.md"), Archive + "\n## B - b (2026-10-28)\nOutcome B.\n", "the work");
        await CommitAsync(root, Path.Combine("docs", "task-archive.md"), Archive + "\n## A - a (2026-10-27)\nOutcome A.\n", "the line's entry");
        var line = await HeadAsync(root);

        var landed = await trees.LandAsync(tree.Path, Subject, gate: Lands);

        Assert.True(landed.Landed, landed.Message);
        Assert.Contains(" Set apart in a commit on top, where union had joined it to the line above: `docs/task-archive.md:", landed.Message);
        Assert.Equal(
            Archive + "\n## A - a (2026-10-27)\nOutcome A.\n\n## B - b (2026-10-28)\nOutcome B.\n",
            await GitAsync(root, "show", $"{Branch}:docs/task-archive.md"));
        Assert.Equal(line, (await GitAsync(root, "rev-parse", $"{Branch}~2")).Trim());
        Assert.Equal("Set apart what union joined in the replay", (await GitAsync(root, "log", "-1", "--format=%s", Branch)).Trim());
        Assert.Equal(DaorisMaker.Trailer, (await GitAsync(root, "log", "-1", "--format=%(trailers:key=Co-Authored-By)", Branch)).Trim());
    }

    private const string Day = "# Tasks\n\n- [ ] **A1** one: its evidence here.\n- [ ] **A2** two.\n- [ ] **A3** three.\n- [ ] **A4** four.\n- [ ] **A5** five.\n";

    private const string Archive = "# Archive\n\n- **OLD1** (2026-11-01): old.\n";

    /// <summary>
    /// 2026-11-09's sequence: the work archives A2 and A3, side by side, while the line moved A1's evidence out of its row, filed B1
    /// between them and C1 at the end, and archived A5. Git stops in <c>TASKS.md</c>, which the merge base could not settle (B1 lands inside
    /// the run the work removed); by its rows' ids each row is one side's, so the landing goes on with no session asked, every open row
    /// once and every archived row once, the archive's lines from both sides in order.
    /// </summary>
    [Fact]
    public async Task The_day_whose_replay_met_the_backlog_lands_by_its_rows_with_no_session_asked()
    {
        var root = await RepositoryAsync("engine");
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        await File.WriteAllTextAsync(Path.Combine(root, ".gitattributes"), "docs/task-archive.md merge=union\n");
        await CommitAsync(root, Path.Combine("docs", "task-archive.md"), Archive, "the archive");
        await CommitAsync(root, "TASKS.md", Day, "the backlog");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "work.txt"), "the work\n");
        await CommitAsync(tree.Path, Path.Combine("docs", "task-archive.md"), Archive + "- **A2** (2026-11-09): two.\n- **A3** (2026-11-09): three.\n", "the work's archive");
        await CommitAsync(tree.Path, "TASKS.md", Day.Replace("- [ ] **A2** two.\n- [ ] **A3** three.\n", ""), "the work, A2 and A3 archived");
        await CommitAsync(root, Path.Combine("docs", "task-archive.md"), Archive + "- **A5** (2026-11-09): five.\n", "the line's archive");
        var lined = Day.Replace("one: its evidence here.", "one: its evidence in the area.").Replace("**A2** two.\n", "**A2** two.\n- [ ] **B1** filed.\n")
            .Replace("- [ ] **A5** five.\n", "- [ ] **C1** filed too.\n");
        await CommitAsync(root, "TASKS.md", lined, "the line's day");

        var plan = await trees.PlanAsync(tree.Path, Subject);
        Assert.Null(plan.Conflict);

        var landed = await trees.LandAsync(tree.Path, Subject, gate: Lands);

        Assert.True(landed.Landed, landed.Message);
        Assert.Contains(" The backlog `TASKS.md` conflicted with the line's and was settled by the merge base", landed.Message);
        Assert.Equal(
            "# Tasks\n\n- [ ] **A1** one: its evidence in the area.\n- [ ] **B1** filed.\n- [ ] **A4** four.\n- [ ] **C1** filed too.\n",
            await GitAsync(root, "show", $"{Branch}:TASKS.md"));
        Assert.Equal(
            Archive + "- **A5** (2026-11-09): five.\n- **A2** (2026-11-09): two.\n- **A3** (2026-11-09): three.\n",
            await GitAsync(root, "show", $"{Branch}:docs/task-archive.md"));
    }

    /// <summary>The work archived A2 while the line added to it: never deleted, but left for a person with both texts said, nothing made.</summary>
    [Fact]
    public async Task A_row_the_work_archived_while_the_line_changed_it_is_left_with_both_texts_said()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "TASKS.md", Rows, "the backlog");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "TASKS.md", Rows.Replace("- [ ] **A2** two.\n", ""), "the work, A2 archived");
        await CommitAsync(root, "TASKS.md", Rows.Replace("**A2** two.", "**A2** two, then B."), "the line's requirement");
        var before = await StateAsync(root, tree);

        var refused = await trees.LandAsync(tree.Path, Subject, gate: Lands);

        Assert.False(refused.Landed, refused.Message);
        Assert.Contains(
            " `TASKS.md`'s row `A2` changed on both sides and is left for a person: the line has “- [ ] **A2** two, then B.”, and the work removed it.",
            refused.Conflict!.Says);
        Assert.StartsWith("Rebase your work onto `main`", refused.Conflict.Words);
        Assert.Equal(before, await StateAsync(root, tree));
    }

    /// <summary>A row both sides rewrote in words apart is left, both texts said: no merge writes wording neither side wrote.</summary>
    [Fact]
    public async Task A_row_both_rewrote_in_words_apart_is_left_with_both_texts_said()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "TASKS.md", Rows.Replace("**A2** two.", "**A2** two: left a and b."), "the backlog");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "TASKS.md", Rows.Replace("**A2** two.", "**A2** two: left b."), "the work");
        await CommitAsync(root, "TASKS.md", Rows.Replace("**A2** two.", "**A2** two, renamed: left a and b."), "the line's name");

        var refused = await trees.LandAsync(tree.Path, Subject, gate: Lands);

        Assert.False(refused.Landed, refused.Message);
        Assert.Contains(
            "`TASKS.md`'s row `A2` changed on both sides and is left for a person: the line has “- [ ] **A2** two, renamed: left a and b.”, "
            + "and the work has “- [ ] **A2** two: left b.”.",
            refused.Conflict!.Says);
    }

    /// <summary>The work holds A1 twice where git stops: the merge base would land it twice, but the rows refuse it, the id said.</summary>
    [Fact]
    public async Task A_side_holding_a_row_id_twice_is_refused_with_the_id_said()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "TASKS.md", Rows, "the backlog");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "TASKS.md", Rows.Replace("- [ ] **A2** two.\n", "") + "- [ ] **A1** one, again.\n", "the work");
        await CommitAsync(root, "TASKS.md", Rows.Replace("**A1** one.\n", "**A1** one.\n- [ ] **B1** the line's.\n"), "the line's row");
        var before = await StateAsync(root, tree);

        var refused = await trees.LandAsync(tree.Path, Subject, gate: Lands);

        Assert.False(refused.Landed, refused.Message);
        Assert.Contains(" `TASKS.md` holds the row `A1` twice on a side, so none of it was settled by its rows.", refused.Conflict!.Says);
        Assert.Equal(before, await StateAsync(root, tree));
    }

    /// <summary>
    /// The line moved A2 under a later heading while the work archived it: git merges that clean and keeps A2, which the rows' rules
    /// remove since the line's copy is the base's, so the made replay is held to them: A2 leaves in a commit on top, said, and it lands.
    /// </summary>
    [Fact]
    public async Task A_clean_replay_keeping_a_row_the_line_moved_while_the_work_archived_it_lands_without_it()
    {
        var root = await RepositoryAsync("engine");
        const string Sections = "# Tasks\n\n- [ ] **A1** one.\n- [ ] **A2** two.\n\n## Later\n";
        await CommitAsync(root, "TASKS.md", Sections, "the backlog");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "work.txt"), "the work\n");
        await CommitAsync(tree.Path, "TASKS.md", Sections.Replace("- [ ] **A2** two.\n", ""), "the work, A2 archived");
        await CommitAsync(root, "TASKS.md", Sections.Replace("- [ ] **A2** two.\n", "") + "- [ ] **A2** two.\n", "the line moves A2");

        var landed = await trees.LandAsync(tree.Path, Subject, gate: Lands);

        Assert.True(landed.Landed, landed.Message);
        Assert.Contains(" `TASKS.md`'s row `A2` left by its rows, where git had kept it: one side removed it while the other only moved it.", landed.Message);
        Assert.Equal(Sections.Replace("- [ ] **A2** two.\n", ""), await GitAsync(root, "show", $"{Branch}:TASKS.md"));
        Assert.Equal("Settle the backlog by its rows in the replay", (await GitAsync(root, "log", "-1", "--format=%s", Branch)).Trim());
        Assert.Equal(2, await TreesAsync(root));
    }

    /// <summary>
    /// The line and the work each archive entry A at the top, the work with B before it: union keeps A's heading twice, which
    /// <c>doc-duplicates</c> refuses on the line, so the replay keeps it once in a commit on top, after setting apart the heading union glued,
    /// and says so; both sides' lines are kept, in order.
    /// </summary>
    [Fact]
    public async Task A_heading_union_kept_twice_in_the_replay_is_kept_once_in_a_commit_on_top()
    {
        var root = await RepositoryAsync("engine");
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        const string Entries = "# Archive\n\n## OLD - old (2026-10-01)\nOld.\n";
        await File.WriteAllTextAsync(Path.Combine(root, ".gitattributes"), "docs/task-archive.md merge=union\n");
        await CommitAsync(root, Path.Combine("docs", "task-archive.md"), Entries, "the archive");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        var work = Entries.Replace("\n## OLD", "\n## B - b (2026-11-09)\nB.\n\n## A - a (2026-11-09)\nBy the work.\n\n## OLD");
        await CommitAsync(tree.Path, Path.Combine("docs", "task-archive.md"), work, "the work");
        await CommitAsync(root, Path.Combine("docs", "task-archive.md"), Entries.Replace("\n## OLD", "\n## A - a (2026-11-09)\nBy the line.\n\n## OLD"), "the line's entry");

        var landed = await trees.LandAsync(tree.Path, Subject, gate: Lands);

        Assert.True(landed.Landed, landed.Message);
        Assert.Contains(" Kept once in a commit on top, where union had kept it twice: `docs/task-archive.md`: “## A - a (2026-11-09)”.", landed.Message);
        var archive = await GitAsync(root, "show", $"{Branch}:docs/task-archive.md");
        Assert.Single(archive.Split('\n'), line => line == "## A - a (2026-11-09)");
        Assert.True(archive.IndexOf("By the line.", StringComparison.Ordinal) < archive.IndexOf("By the work.", StringComparison.Ordinal), archive);
        Assert.Equal("Keep once what union kept twice in the replay", (await GitAsync(root, "log", "-1", "--format=%s", Branch)).Trim());
    }

    /// <summary>The line moved A2 and removed a word from it while the work archived it: git merges it clean, and the words are not lost.</summary>
    [Fact]
    public async Task A_clean_replay_losing_the_lines_words_in_a_row_the_work_archived_is_refused_with_both_texts_said()
    {
        var root = await RepositoryAsync("engine");
        const string Sections = "# Tasks\n\n- [ ] **A1** one.\n- [ ] **A2** Do not enable A.\n\n## Later\n";
        await CommitAsync(root, "TASKS.md", Sections, "the backlog");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "work.txt"), "the work\n");
        await CommitAsync(tree.Path, "TASKS.md", Sections.Replace("- [ ] **A2** Do not enable A.\n", ""), "the work, A2 archived");
        await CommitAsync(root, "TASKS.md", Sections.Replace("- [ ] **A2** Do not enable A.\n", "") + "- [ ] **A2** Do enable A.\n", "the line moves A2");
        var before = await StateAsync(root, tree);

        var refused = await trees.LandAsync(tree.Path, Subject, gate: Lands);

        Assert.False(refused.Landed, refused.Message);
        Assert.Contains("`TASKS.md`'s row `A2` changed on both sides and is left for a person: the line has “- [ ] **A2** Do enable A.”, and the work removed it.",
            refused.Conflict!.Says);
        Assert.Equal(before, await StateAsync(root, tree));
    }

    /// <summary>The work archives 3a of a compound row while the line archives 3l: it lands one row of the ids neither archived.</summary>
    [Fact]
    public async Task A_compound_row_each_side_archived_a_part_of_lands_as_one_row_of_the_ids_left()
    {
        var root = await RepositoryAsync("engine");
        const string Compound = "# Tasks\n\n- [ ] **WFREVIEW3a, 3d, 3l and 3m** the defects.\n";
        await CommitAsync(root, "TASKS.md", Compound, "the backlog");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "TASKS.md", Compound.Replace("3a, 3d, 3l", "3d, 3l"), "the work, 3a archived");
        await CommitAsync(root, "TASKS.md", Compound.Replace("3d, 3l and", "3d and"), "the line, 3l archived");

        var landed = await trees.LandAsync(tree.Path, Subject, gate: Lands);

        Assert.True(landed.Landed, landed.Message);
        Assert.Equal("# Tasks\n\n- [ ] **WFREVIEW3d and 3m** the defects.\n", await GitAsync(root, "show", $"{Branch}:TASKS.md"));
    }

    private static async Task<string> HeadAsync(string place) => (await GitAsync(place, "rev-parse", "HEAD")).Trim();

    /// <summary>How many working trees git lists for the checkout: a tree of Daoris's own made for a replay is gone after it.</summary>
    private static async Task<int> TreesAsync(string root) =>
        (await GitAsync(root, "worktree", "list", "--porcelain")).Split('\n').Count(line => line.StartsWith("worktree ", StringComparison.Ordinal));

    /// <summary>What a refused landing must leave unchanged, as <c>LandRebaseLandingTests</c> reads it.</summary>
    private static async Task<string> StateAsync(string root, TreeOpened tree) =>
        string.Join("\n",
            (await GitAsync(root, "for-each-ref", "--format=%(objectname) %(refname)", "refs/heads/", "refs/daoris/")).Trim(),
            (await GitAsync(root, "status", "--porcelain")).Trim(),
            (await GitAsync(tree.Path, "status", "--porcelain")).Trim(),
            (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim());
}
