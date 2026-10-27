using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// PROCESS1d-i (D164's amendment point 4): a session in a tree of its own is told its resolved process at its start, in the
/// instruction's landing section: the commit rule (its mode, where it was set, the person's convention quoted) and the branch the
/// pull request comes from, so the agent and the repository's own rules no longer stop each other. Pure: <see cref="ProcessWords"/>
/// from the values the driver resolved, then the section as every form of the instruction carries it.
/// </summary>
public sealed class ProcessWordsTests
{
    private const string Branch = "feature/0fda18-fix";

    private static readonly LandingPlan Review = new(LandingForm.Branch, Branch, LandingSource.Workspace);

    private static readonly LandingPlan Auto = Review with { AutoAccept = true };

    private static readonly LandingPlan Merge = new(LandingForm.Merge, "main", LandingSource.Default);

    private static ResolvedCommits Rule(string mode, string source = CommitRules.DefaultSource, string? convention = null) =>
        new(new CommitRule(mode, convention), source);

    private const string Stash = "The stash is shared with the repository's checkout and every other tree: set work aside with a commit on this tree's branch, and never use git stash.";

    private static string Said(ResolvedCommits? commits, LandingPlan? plan) => ProcessWords.Said(commits, plan)!;

    /// <summary>The default, whole: the landing's lead, the branch the pull request comes from by name, then the process as set.</summary>
    [Fact]
    public void The_default_under_review_is_said_whole()
    {
        Assert.Equal(
            "\nThis work goes through review. When it is done, the person puts this tree's branch on `feature/0fda18-fix` and opens a "
            + "pull request from it. The branch the pull request comes from is named `feature/0fda18-fix` by this repository's rule; "
            + "you do not name it, and you do not merge it, push it, or open a pull request yourself.\n"
            + "\n"
            + "This repository's process on this machine:\n"
            + "\n"
            + "- Commit on this tree's branch as you go, each piece once it passes its checks.\n"
            + "- Do not create, switch or push branches: Daoris makes the pull request's branch when the person accepts the work.\n"
            + "- " + Stash + "\n",
            Said(Rule(CommitRules.AsYouGo), Review));
    }

    /// <summary>
    /// STASHSHARE1: git's stash is one stack per repository, so a tree of its own is told it is shared with the checkout and the other
    /// trees, and to set work aside with a commit. Said once, in every mode and every kind of tree, right after the branch line.
    /// </summary>
    [Theory]
    [InlineData(CommitRules.AsYouGo)]
    [InlineData(CommitRules.Ask)]
    [InlineData(CommitRules.AtEnd)]
    public void A_tree_of_its_own_is_told_the_stash_is_shared_in_every_mode_and_kind_of_tree(string mode)
    {
        foreach (var plan in new[] { Review, Auto, Merge, Review with { Own = true }, Review with { Plugin = "example.github-pull-request" } })
        {
            var said = Said(Rule(mode), plan);
            Assert.Equal(1, said.Split(Stash).Length - 1);
            Assert.Contains($"\n- Do not create, switch or push branches", said);
            Assert.Matches($"- Do not create, switch or push branches[^\n]*\n- {System.Text.RegularExpressions.Regex.Escape(Stash)}\n", said);
        }

        // After the existing lines, and before the auto-accept close, which stays last.
        var auto = Said(Rule(CommitRules.AsYouGo), Auto);
        Assert.True(auto.IndexOf(Stash, StringComparison.Ordinal) < auto.IndexOf("- Commit all of your work before", StringComparison.Ordinal));

        var conversation = ProcessWords.Conversation(Rule(mode), Review);
        Assert.Equal(1, conversation.Split(Stash).Length - 1);
        Assert.EndsWith($"\n- Do not create, switch or push branches.\n- {Stash}", conversation);
    }

    /// <summary>Each mode in its own words, and in every mode no branch is made, moved or pushed.</summary>
    [Theory]
    [InlineData(CommitRules.AsYouGo, "Commit on this tree's branch as you go, each piece once it passes its checks.")]
    [InlineData(CommitRules.Ask, "Ask the person before each commit, and commit only on their yes. Uncommitted work stays in this tree "
        + "and the person sees it as it stands, but Accept will not land it until it is committed.")]
    [InlineData(CommitRules.AtEnd, "Make one commit on this tree's branch when the work is done, and none before.")]
    public void Each_mode_is_said_in_its_own_words_and_no_branch_is_moved_in_any(string mode, string words)
    {
        foreach (var plan in new[] { Review, Merge, Review with { Own = true } })
        {
            var said = Said(Rule(mode), plan);
            Assert.Contains($"\n- {words}\n", said);
            Assert.Contains("- Do not create, switch or push branches", said);
            foreach (var other in CommitRules.Modes.Where(other => other != mode))
            {
                Assert.DoesNotContain(Said(Rule(other), plan).Split('\n').First(line => line.StartsWith("- ", StringComparison.Ordinal)), said);
            }
        }
    }

    /// <summary>Where the rule was set is said for a repository's or a workspace's, and nothing for the default.</summary>
    [Theory]
    [InlineData(OpinionSource.Repository, "This repository's process on this machine, set for its repository:\n")]
    [InlineData(OpinionSource.Workspace, "This repository's process on this machine, set for its workspace:\n")]
    [InlineData(CommitRules.DefaultSource, "This repository's process on this machine:\n")]
    public void The_opener_names_where_the_rule_was_set(string source, string opener)
    {
        Assert.Contains($"\n{opener}", Said(Rule(CommitRules.AsYouGo, source), Review));
    }

    /// <summary>The person's convention is quoted to the session as a block of its own, after the process; none, and nothing is.</summary>
    [Fact]
    public void The_persons_convention_is_quoted_as_a_block()
    {
        const string convention = "Subject in the imperative, the ticket key first: `T-9 Add the note field`.";
        var said = Said(Rule(CommitRules.Ask, OpinionSource.Repository, convention), Review);

        Assert.EndsWith($"\n\nThe person's words on how a commit is written, to follow:\n\n> {convention}\n", said);
        Assert.DoesNotContain("how a commit is written", Said(Rule(CommitRules.Ask, OpinionSource.Repository), Review));
    }

    /// <summary>A tree its agent moved onto a branch of its own (TREEFOLLOW1) is told that branch lands as it stands; no other is named.</summary>
    [Fact]
    public void A_tree_on_its_own_branch_is_told_it_lands_as_it_stands()
    {
        var said = Said(Rule(CommitRules.AsYouGo), new LandingPlan(LandingForm.Branch, "feature/T-9-mine", LandingSource.Workspace) { Own = true });

        Assert.Contains("This tree is on `feature/T-9-mine`, which lands as it stands; you do not merge it, push it, or open a pull request yourself.", said);
        Assert.DoesNotContain("by this repository's rule", said);
        Assert.DoesNotContain("Daoris makes the pull request's branch", said);
        Assert.Contains("- Do not create, switch or push branches.\n", said);
    }

    /// <summary>A merge-form repository is told its commit rule too, and where the work goes when the person accepts it.</summary>
    [Fact]
    public void A_merge_is_told_its_commit_rule_and_where_it_is_merged()
    {
        var said = Said(Rule(CommitRules.AtEnd, OpinionSource.Repository), Merge);

        Assert.StartsWith("\nWhen the person accepts, this tree's branch is merged into `main`.\n\nThis repository's process on this machine, set for its repository:\n", said);
        Assert.Contains("- Make one commit on this tree's branch when the work is done, and none before.\n", said);
        Assert.Contains("- Do not create, switch or push branches.\n", said);
        Assert.DoesNotContain("pull request", said);
    }

    /// <summary>
    /// Under a rule that accepts automatically (LAND2b), the quest's done lands the work, so every mode commits before it: under
    /// <c>ask</c>, the yes and the commit come first, and no Accept is waited for.
    /// </summary>
    [Theory]
    [InlineData(CommitRules.AsYouGo, "- Commit all of your work before you close the quest done: uncommitted work does not land.\n")]
    [InlineData(CommitRules.Ask, "- Ask for the yes and commit before you close the quest done: uncommitted work does not land.\n")]
    [InlineData(CommitRules.AtEnd, "- Make that commit before you close the quest done: uncommitted work does not land.\n")]
    public void Under_auto_accept_every_mode_commits_before_the_quest_closes_done(string mode, string line)
    {
        var said = Said(Rule(mode), Auto);

        Assert.Contains(line, said);
        Assert.Contains("Daoris makes the pull request's branch when the work is accepted.", said);
        Assert.DoesNotContain("Accept will not land it", said);
        Assert.DoesNotContain("uncommitted work does not land", Said(Rule(mode), Review));
    }

    /// <summary>The sentences the landing's Process cases read (LandingTests, AutoLandingTests) stand, word for word.</summary>
    [Fact]
    public void The_sentences_the_landing_cases_read_stand()
    {
        var plugin = "example.github-pull-request";
        Assert.Contains("do not merge it, push it, or open a pull request yourself", Said(Rule(CommitRules.AsYouGo), Review));
        Assert.Contains("the person accepts it, this tree's branch is put on `feature/0fda18-fix`, pushed",
            Said(Rule(CommitRules.AsYouGo), Review with { Plugin = plugin }));
        Assert.Contains("This work goes through review. When it is done and the person accepts it", Said(Rule(CommitRules.AsYouGo), Review with { Plugin = plugin }));
        Assert.Contains("This work is accepted automatically. When you close the quest done, this tree's branch is put on "
            + "`feature/0fda18-fix`, pushed, and a pull request opened from it, where the work is judged.", Said(Rule(CommitRules.AsYouGo), Auto with { Plugin = plugin }));
        Assert.Contains("uncommitted work does not land", Said(Rule(CommitRules.AsYouGo), Auto));
    }

    /// <summary>
    /// Nothing is said with no rule and no branch to go on: a session outside a tree of its own is handed neither. A branch plan
    /// handed without a rule is told the default, as a tree with none set would be.
    /// </summary>
    [Fact]
    public void Nothing_is_said_without_a_rule_or_a_branch()
    {
        Assert.Null(ProcessWords.Said(null, null));
        Assert.Null(ProcessWords.Said(null, Merge));
        Assert.Equal(Said(Rule(CommitRules.AsYouGo), Review), Said(null, Review));
        Assert.StartsWith("\nThis repository's process on this machine, set for its workspace:\n", Said(Rule(CommitRules.Ask, OpinionSource.Workspace), null));
    }

    /// <summary>A named workflow's landing that cannot start has no branch to name (WORKFLOW1f): the process is said, no name is.</summary>
    [Fact]
    public void A_landing_with_no_branch_named_says_no_name()
    {
        var said = Said(Rule(CommitRules.AsYouGo), Review with { Target = "", Problem = "its landing cannot start." });

        Assert.DoesNotContain("``", said);
        Assert.Contains("- Commit on this tree's branch as you go", said);
    }

    // ——— the instruction: the landing section, in every form the tail carries

    /// <summary>A claim, a carry-on and a resume each carry the section once, after the close, its words the driver's from the landing rule.</summary>
    [Theory]
    [InlineData("bare")]
    [InlineData("carry-on")]
    [InlineData("resume")]
    public void Every_form_of_the_instruction_says_it_once(string which)
    {
        var commits = Rule(CommitRules.Ask, OpinionSource.Repository, "Subject in the imperative.");
        var target = Of(which) with { LandsOn = Review, Commits = commits };
        var section = ProcessWords.Said(commits, Review)!;

        var composed = TargetPrompt.Composed(target);
        Assert.Equal(1, composed.Text.Split(section).Length - 1);
        Assert.Equal(composed.Text.Length, composed.Account.Chars);
        var landing = Assert.Single(composed.Account.Sections, handed => handed.Name == HandedSections.Landing);
        Assert.Equal(section.Length, landing.Chars);
        Assert.Equal(HandedSources.Landing, landing.Source);
        Assert.Equal(Branch, landing.From);
    }

    /// <summary>A merge's section and a rule's alone are accounted too, with no branch to dereference (the account's sentence).</summary>
    [Fact]
    public void A_merges_section_and_a_rules_alone_are_accounted()
    {
        var merge = TargetPrompt.Composed(TargetPromptGoldenTests.Bare with { LandsOn = Merge, Commits = Rule(CommitRules.AtEnd, OpinionSource.Workspace) });
        var merged = Assert.Single(merge.Account.Sections, handed => handed.Name == HandedSections.Landing);
        Assert.Contains("merged into `main`", merged.Said);
        Assert.Contains("`at-end`", merged.Said);
        Assert.Equal("main", merged.From);

        var alone = TargetPrompt.Composed(TargetPromptGoldenTests.Bare with { Commits = Rule(CommitRules.AsYouGo) });
        var told = Assert.Single(alone.Account.Sections, handed => handed.Name == HandedSections.Landing);
        Assert.Contains("`as-you-go`", told.Said);
        Assert.Null(told.From);
    }

    /// <summary>The person's checkout, handed neither, reads as it did: the goldens hold no landing section.</summary>
    [Fact]
    public void A_target_handed_neither_says_nothing_of_it()
    {
        var bare = TargetPrompt.Compose(TargetPromptGoldenTests.Bare);
        Assert.DoesNotContain("process on this machine", bare);
        Assert.DoesNotContain("stash", bare, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(TargetPrompt.Composed(TargetPromptGoldenTests.Bare).Account.Sections, handed => handed.Name == HandedSections.Landing);
    }

    // ——— a conversation in a tree of its own (PROCESS1d-ii): told once, with its first message

    /// <summary>
    /// The default, whole: the landing's form and who takes the branch on, the branch never said by name (a conversation's is made
    /// at Accept from its opening), then the process as a driven session is told it, with no quest to close.
    /// </summary>
    [Fact]
    public void A_conversation_under_review_is_told_its_process_whole()
    {
        Assert.Equal(
            "This work goes through review. Daoris names the branch from this repository's rule when the person accepts it, and "
            + "this tree's branch is put on it for the person to push and open a pull request from; you do not name it, and you do "
            + "not merge it, push it, or open a pull request yourself.\n"
            + "\n"
            + "This repository's process on this machine:\n"
            + "\n"
            + "- Commit on this tree's branch as you go, each piece once it passes its checks.\n"
            + "- Do not create, switch or push branches.\n"
            + "- " + Stash,
            ProcessWords.Conversation(Rule(CommitRules.AsYouGo), Review));
    }

    /// <summary>The plan's branch is the session's placeholder until Accept names it from the opening, so no name is said.</summary>
    [Fact]
    public void A_conversation_is_never_told_a_branch_by_name()
    {
        foreach (var plan in new[] { Review, Review with { Plugin = "example.github-pull-request" }, Auto })
        {
            var said = ProcessWords.Conversation(Rule(CommitRules.AsYouGo), plan);
            Assert.DoesNotContain(Branch, said);
            Assert.Contains("Daoris names the branch from this repository's rule when the person accepts it", said);
        }

        Assert.Contains("this tree's branch is put on it, pushed, and a pull request opened from it; you do not name it",
            ProcessWords.Conversation(Rule(CommitRules.AsYouGo), Review with { Plugin = "example.github-pull-request" }));
    }

    /// <summary>Each mode in its own words, Accept waited for in every one: a conversation has no quest whose done lands it.</summary>
    [Theory]
    [InlineData(CommitRules.AsYouGo, "Commit on this tree's branch as you go, each piece once it passes its checks.")]
    [InlineData(CommitRules.Ask, "Ask the person before each commit, and commit only on their yes. Uncommitted work stays in this tree "
        + "and the person sees it as it stands, but Accept will not land it until it is committed.")]
    [InlineData(CommitRules.AtEnd, "Make one commit on this tree's branch when the work is done, and none before.")]
    public void A_conversation_is_told_each_mode_in_its_own_words_and_no_quest(string mode, string words)
    {
        foreach (var plan in new[] { Review, Merge, Auto, null })
        {
            var said = ProcessWords.Conversation(Rule(mode), plan);
            Assert.Contains($"\n- {words}\n", said);
            Assert.EndsWith($"\n- Do not create, switch or push branches.\n- {Stash}", said);
            Assert.DoesNotContain("the quest", said);
            Assert.DoesNotContain("accepted automatically", said);
        }
    }

    /// <summary>The opener names where the rule was set as a driven session's does, and the convention closes it as a block.</summary>
    [Fact]
    public void A_conversation_is_told_where_the_rule_was_set_and_the_persons_convention()
    {
        const string convention = "Subject in the imperative, the ticket key first: `T-9 Add the note field`.";
        var said = ProcessWords.Conversation(Rule(CommitRules.Ask, OpinionSource.Repository, convention), Review);

        Assert.Contains("\n\nThis repository's process on this machine, set for its repository:\n\n", said);
        Assert.EndsWith($"\n\nThe person's words on how a commit is written, to follow:\n\n> {convention}", said);
        Assert.Contains("This repository's process on this machine, set for its workspace:\n",
            ProcessWords.Conversation(Rule(CommitRules.AsYouGo, OpinionSource.Workspace), Merge));
    }

    /// <summary>A merge says the line it goes into, or the repository's line where none was read; no plan, the process alone.</summary>
    [Fact]
    public void A_conversation_under_a_merge_is_told_its_line_and_one_with_no_plan_its_process_alone()
    {
        var merge = ProcessWords.Conversation(Rule(CommitRules.AtEnd), Merge);
        Assert.StartsWith("When the person accepts, this tree's branch is merged into `main`.\n\nThis repository's process on this machine:\n", merge);
        Assert.DoesNotContain("pull request", merge);

        var unread = ProcessWords.Conversation(Rule(CommitRules.AtEnd), Merge with { Target = "" });
        Assert.StartsWith("When the person accepts, this tree's branch is merged into its repository's line.\n\n", unread);
        Assert.DoesNotContain("``", unread);

        Assert.StartsWith("This repository's process on this machine:\n", ProcessWords.Conversation(Rule(CommitRules.AsYouGo), null));
    }

    private static SessionTarget Of(string which) => which == "resume"
        ? TargetPromptGoldenTests.Bare with
        {
            Answered = new QuestView("q2", "console-ui", "console-api", "Which column?", "Name it.", "Done") { Note = "`note`." },
        }
        : TargetPromptGoldenTests.Of(which);
}
