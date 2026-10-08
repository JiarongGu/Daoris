using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// XAGENT1d (D155 points 5 and 6; the second-agent design §4–§5): what a pass hands its reviewer, read from facts with no
/// model — the candidate as git names it, the repository's own rules by path — and what holds it beside the instruction: the
/// rules handed, the posture recorded, its one turn's bound, and the words its door refuses.
/// </summary>
/// <remarks>
/// Runs no process, so it is in the fast half (MOD8): git's own reading of a candidate, and the clone it is read in, are
/// <see cref="ReviewTreeTests"/>'.
/// </remarks>
public sealed class OpinionPacketTests : IDisposable
{
    private const string Base = "1111111111111111111111111111111111111111";
    private const string Tip = "2222222222222222222222222222222222222222";

    private readonly string _tree = Path.Combine(Path.GetTempPath(), "daoris-opinion-packet-" + Guid.NewGuid().ToString("N")[..8]);

    public OpinionPacketTests() => Directory.CreateDirectory(_tree);

    public void Dispose()
    {
        try { Directory.Delete(_tree, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private void Write(string path, string text = "x")
    {
        var full = Path.Combine(_tree, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }

    // ——— The candidate, as git's answers are read.

    [Fact]
    public void The_commits_are_read_in_git_s_order_each_once_and_a_line_that_is_no_full_id_is_not_one()
    {
        var commits = OpinionPackets.Commits($"{Base}\n{Tip}\r\n\nnot-an-id\n{Tip}\n");

        Assert.Equal([Base, Tip], commits);
    }

    [Fact]
    public void Each_path_is_read_with_its_status_letter_and_a_rename_or_a_copy_by_where_it_now_is()
    {
        var paths = OpinionPackets.Paths("M\0src/report.ts\0R087\0docs/old.md\0docs/new.md\0A\0docs/a b.md\0C100\0x.md\0y.md\0D\0gone.md\0");

        Assert.Equal(
            [
                new OpinionPath("M", "src/report.ts"), new OpinionPath("R", "docs/new.md"), new OpinionPath("A", "docs/a b.md"),
                new OpinionPath("C", "y.md"), new OpinionPath("D", "gone.md"),
            ],
            paths);
        Assert.Empty(OpinionPackets.Paths(""));
    }

    // ——— The repository's own rules, named by path, as they stand in the candidate.

    [Fact]
    public void The_rules_are_its_root_instructions_its_indexes_and_the_records_its_manifest_declares()
    {
        Write("AGENTS.md");
        Write("CLAUDE.md", "@AGENTS.md\n");
        Write("docs/README.md");
        Write(".claude/INDEX.md");
        Write("docs/decisions/D1.md");
        Write("daoris.gates.json", "{}");
        Write("daoris.json", """{ "documents": { "router": "docs/README.md", "decisions": "docs/decisions", "gates": { "path": "daoris.gates.json" } } }""");

        var rules = OpinionPackets.RulesIn(_tree);

        // The router the manifest declares is one of the indexes, named once.
        Assert.Equal(["AGENTS.md", "CLAUDE.md", "docs/README.md", ".claude/INDEX.md"], rules.Doctrine);
        Assert.Equal("docs/decisions", rules.Decisions);
        Assert.Equal("daoris.gates.json", rules.Gates);
    }

    [Fact]
    public void A_record_declared_where_nothing_is_is_not_named_and_the_gates_file_is_found_by_its_own_name()
    {
        Write("AGENTS.md");
        Write("daoris.gates.json", "{}");
        Write("daoris.json", """{ "documents": { "decisions": "docs/decisions" } }""");

        var rules = OpinionPackets.RulesIn(_tree);

        Assert.Equal(["AGENTS.md"], rules.Doctrine);
        Assert.Null(rules.Decisions);
        Assert.Equal("daoris.gates.json", rules.Gates);
    }

    [Fact]
    public void A_manifest_that_is_no_json_names_no_record_and_a_path_reaching_outside_is_never_named()
    {
        Write("CLAUDE.md");
        Write("daoris.json", "{ not json");

        var unread = OpinionPackets.RulesIn(_tree);
        Assert.Equal(["CLAUDE.md"], unread.Doctrine);
        Assert.Null(unread.Decisions);
        Assert.Null(unread.Gates);

        Write("daoris.json", """{ "documents": { "decisions": "../elsewhere", "gates": "C:/gates.json" } }""");
        var outside = OpinionPackets.RulesIn(_tree);
        Assert.Null(outside.Decisions);
        Assert.Null(outside.Gates);
    }

    [Fact]
    public void A_tree_that_holds_nothing_names_nothing()
    {
        Assert.Equal(OpinionRulesRead.None, OpinionPackets.RulesIn(_tree));
    }

    // ——— What it is handed beside the instruction (design §5.2): the opinion's rules, over the person's.

    private static readonly string Connector = $"mcp__{KnowledgeConnector.ServerName}__";

    [Fact]
    public void The_opinion_s_rules_deny_every_edit_tool_and_withhold_the_commit_default()
    {
        var composed = PermissionRules.Compose(PermissionFile.Empty, "work", "reports");

        var handed = OpinionPermissions.Shape(composed);

        Assert.Contains("Edit", handed.Deny);
        Assert.Contains("Write", handed.Deny);
        Assert.Contains("NotebookEdit", handed.Deny);
        foreach (var commit in PermissionRules.Defaults.Single(shipped => shipped.Id == "commit").Rules.Where(rule => rule != "Bash(cd:*)"))
        {
            Assert.DoesNotContain(commit, handed.Allow);
        }

        Assert.Contains("Bash(git commit:*)", handed.Deny);
        Assert.Contains("Bash(git -* commit *)", handed.Deny);
        Assert.Contains("Bash(git merge:*)", handed.Deny);
        // `no-push` stands, as for every session.
        Assert.Contains("Bash(git push:*)", handed.Deny);
    }

    [Fact]
    public void Its_connector_offers_reading_and_opinion_give_and_denies_whatever_closes_publishes_permits_or_asks()
    {
        var handed = OpinionPermissions.Shape(PermissionRules.Compose(PermissionFile.Empty, null, "reports"));

        Assert.True(PermissionRules.AllowsConnector(handed, "opinion_give"));
        Assert.True(PermissionRules.AllowsConnector(handed, "knowledge_search"));
        Assert.True(PermissionRules.AllowsConnector(handed, "quest_list"));
        foreach (var tool in new[]
                 {
                     "quest_respond", "quest_publish", "go_ahead_ask", "permission_propose", "review_serve", "review_ready",
                     "opinion_answer",
                 })
        {
            Assert.False(PermissionRules.AllowsConnector(handed, tool), tool);
            Assert.Contains(Connector + tool, handed.Deny);
        }
    }

    [Fact]
    public void It_may_read_its_history_with_git_and_a_person_s_own_deny_and_read_still_stand()
    {
        var file = PermissionRules.Add(PermissionFile.Empty, RuleScope.Repository, "reports", RuleList.Deny, "Bash(git log:*)");
        file = PermissionRules.Add(file, RuleScope.Machine, null, RuleList.Allow, "Read(//c/notes/**)");

        var handed = OpinionPermissions.Shape(PermissionRules.Compose(file, null, "reports"));

        Assert.Contains("Bash(git diff:*)", handed.Allow);
        Assert.Contains("Bash(git show:*)", handed.Allow);
        Assert.Contains("Read(//c/notes/**)", handed.Allow);
        // The person's deny is handed as written, and deny wins at the harness (D72): Daoris owns the union, never the precedence.
        Assert.Contains("Bash(git log:*)", handed.Deny);
        Assert.Equal(handed.Allow.Distinct(StringComparer.Ordinal).Count(), handed.Allow.Count);
        Assert.Equal(handed.Deny.Distinct(StringComparer.Ordinal).Count(), handed.Deny.Count);
    }

    [Fact]
    public void Its_hard_denial_tells_the_classifier_it_changes_nothing()
    {
        Assert.Contains("second opinion", OpinionPermissions.HardDeny);
        Assert.Contains("commit", OpinionPermissions.HardDeny);
        Assert.Contains("process", OpinionPermissions.HardDeny);
    }

    // ——— The posture recorded (§5.2): which held, in the record's words.

    [Theory]
    [InlineData("claude-code", OpinionPosture.RulesAndCopy)]
    [InlineData("claude-code-acp", OpinionPosture.RulesAndCopy)]
    [InlineData("codex-acp", OpinionPosture.CopyAlone)]
    [InlineData("dsh", OpinionPosture.CopyAlone)]
    [InlineData("stub", OpinionPosture.CopyAlone)]
    public void Claude_Code_by_either_door_reads_by_its_rules_and_its_copy_and_every_other_agent_by_its_copy_alone(string adapter, string posture)
    {
        Assert.Equal(posture, OpinionPosture.Of(AdapterSet.Built().Resolve(adapter)));
    }

    [Fact]
    public void Each_posture_is_said_as_the_record_says_it()
    {
        Assert.Equal("read-only by its agent's rules and its copy", OpinionPosture.Said(OpinionPosture.RulesAndCopy));
        Assert.Equal("read-only by its copy alone", OpinionPosture.Said(OpinionPosture.CopyAlone));
    }

    // ——— One turn, bounded (§5.6).

    [Theory]
    [InlineData(null, 30, 20)]
    [InlineData(45, 30, 30)]
    [InlineData(10, 30, 10)]
    [InlineData(120, 200, 120)]
    public void A_pass_is_bounded_by_the_rule_s_minutes_or_the_machine_s_session_timeout_if_that_comes_first(int? minutes, int timeout, int bound)
    {
        var rule = new OpinionRule(["codex-acp"], [OpinionRules.Landing], Minutes: minutes);

        Assert.Equal(bound, OpinionPass.Bound(rule, DriverConfig.Empty with { TimeoutMinutes = timeout }));
    }

    [Fact]
    public void Words_to_a_reviewer_while_it_reads_are_refused_saying_why()
    {
        var said = OpinionPass.TakesNoWords("op1");

        Assert.Contains("second opinion `op1`", said);
        Assert.Contains("one turn", said);
        Assert.Equal("opinion", OpinionPass.WordsCode);
    }

    // ——— The tier (§10): every pass says it, and none ran is said too.

    [Fact]
    public void A_pass_no_reviewer_could_read_is_tier_none_with_the_choice_s_code_and_starts_nothing()
    {
        var choice = new ReviewerChoice(null, null, ReviewerUnavailable.NoReviewer, "No second opinion: no listed reviewer of another maker is installed.");

        var run = OpinionPassRun.Unavailable(choice);

        Assert.Equal(OpinionPass.TierNone, run.Tier);
        Assert.Equal(ReviewerUnavailable.NoReviewer, run.Code);
        Assert.False(run.Opened);
        Assert.Null(run.Session);
        Assert.Equal("unavailable  no agent could read this: No second opinion: no listed reviewer of another maker is installed.", run.Line);
    }

    [Fact]
    public void The_families_a_pass_names_are_the_owners_of_the_agents_that_wrote_the_work_each_once()
    {
        var built = AdapterSet.Built();
        var working = new[] { AgentFamily.Of(built, "claude-code"), AgentFamily.Of(built, "codex-acp") };

        Assert.Equal(["claude-code", "codex"], OpinionPass.Families(working));
    }

    // ——— The packet, as its folder keeps it.

    [Fact]
    public void An_opinion_s_folder_is_under_the_home_by_its_id_and_never_a_path_its_id_could_name()
    {
        Assert.Equal(Path.Combine(_tree, "opinions", "op1"), OpinionPackets.Folder(_tree, "op1"));
        Assert.Throws<DriverException>(() => OpinionPackets.Folder(_tree, "../elsewhere"));
        Assert.Throws<DriverException>(() => OpinionPackets.Folder(_tree, ""));
    }

    [Fact]
    public void A_clone_s_folder_is_beside_the_session_trees_of_its_repository_named_apart_from_them()
    {
        var path = OpinionTree.PathFor(_tree, "work", "reports");

        Assert.Equal(Path.Combine(_tree, "trees", "work", "reports"), Path.GetDirectoryName(path));
        Assert.StartsWith(OpinionTree.Prefix, Path.GetFileName(path), StringComparison.Ordinal);
        Assert.NotEqual(path, OpinionTree.PathFor(_tree, "work", "reports"));
        Assert.Equal(Path.Combine(_tree, "trees", "default", "reports"), Path.GetDirectoryName(OpinionTree.PathFor(_tree, null, "reports")));
    }

    [Fact]
    public void A_folder_outside_the_home_s_trees_is_never_removed_as_a_clone()
    {
        var outside = Path.Combine(_tree, "elsewhere");
        Directory.CreateDirectory(outside);

        var left = OpinionTree.RemoveAsync(Path.Combine(_tree, "home"), outside).GetAwaiter().GetResult();

        Assert.NotNull(left);
        Assert.Contains("not a second opinion's copy", left);
        Assert.True(Directory.Exists(outside));
    }

    [Fact]
    public void A_clone_s_read_only_objects_go_with_it()
    {
        var home = Path.Combine(_tree, "home");
        var clone = OpinionTree.PathFor(home, "work", "reports");
        var pack = Path.Combine(clone, ".git", "objects", "pack", "pack-1.pack");
        Directory.CreateDirectory(Path.GetDirectoryName(pack)!);
        File.WriteAllText(pack, "x");
        File.SetAttributes(pack, FileAttributes.ReadOnly);

        Assert.Null(OpinionTree.RemoveAsync(home, clone).GetAwaiter().GetResult());
        Assert.False(Directory.Exists(clone));
    }
}
