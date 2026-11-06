using Daoris.Driver;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CONTEXTONE1c (D230 point 3; the context-one design §3 C): the standing block, one section composed by the driver for every kind of
/// session. Where things are, the routing, the person's choices on this machine, the repository's and workspace's facts and the memory
/// the line read; the workspace's part alone where there is no repository; its own section of the composer's account; and a whole first
/// prompt kept under the native door's single argument, the block going through its file past it.
/// </summary>
public sealed class StandingBlockTests(ITestOutputHelper output) : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    private readonly string _tree = Path.Combine(Path.GetTempPath(), "daoris-standing-block-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_tree, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static RepoView Engine(params string[] lanes) => new("engine", true, "/work/engine") { Lanes = lanes };

    private static DriverConfig Config(bool driven, bool trees) => DriverConfig.Empty.WithDrivable("engine", driven).WithTrees("engine", trees);

    private static string Routed(DriverConfig config, RepoView repository, string work = ParticipantWork.Build) =>
        StandingBlock.Compose(StandingBlocks.OnMachine(new StandingFacts { Repository = "engine" }, work, config, [repository], "work")).Text;

    // ——— The routing, moved here from the routing line (ROUTEQUEST1's words).

    [Fact]
    public void The_routing_names_the_lanes_by_their_addresses_where_there_are_some()
    {
        var open = Routed(Config(true, true), Engine("core", "web"));
        var oneAtATime = Routed(Config(true, false), Engine("core", "web"));
        var undriven = Routed(Config(false, true), Engine("core", "web"));

        foreach (var told in new[] { open, oneAtATime, undriven })
        {
            Assert.Contains("`engine:core`", told, StringComparison.Ordinal);
            Assert.Contains("`engine:web`", told, StringComparison.Ordinal);
        }

        Assert.Contains("Routing standing here: open. ", open, StringComparison.Ordinal);
        Assert.Contains("can start beside your work", open, StringComparison.Ordinal);
        Assert.Contains("only after your work ends", oneAtATime, StringComparison.Ordinal);
        Assert.Contains("not driven on this machine", undriven, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void Every_routing_names_the_quest_route_to_another_repository_and_the_helper_for_a_question_about_this_one(bool driven, bool lanes, bool trees)
    {
        var words = Routed(Config(driven, trees), lanes ? Engine("core", "web") : Engine());

        Assert.Contains("goes to that repository as a quest (`quest_publish`", words, StringComparison.Ordinal);
        Assert.Contains("registry or this repository's `uses`", words, StringComparison.Ordinal);
        Assert.Contains("`helper_ask`", words, StringComparison.Ordinal);
    }

    [Fact]
    public void Yourself_is_kept_to_this_repository_s_own_rows_where_no_lane_can_start_a_quest()
    {
        foreach (var words in new[] { Routed(Config(true, true), Engine()), Routed(Config(false, true), Engine("core")) })
        {
            Assert.Contains("Work this repository's own rows yourself.", words, StringComparison.Ordinal);
            Assert.DoesNotContain("`engine:", Routed(Config(true, true), Engine()), StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(ParticipantWork.Help)]
    [InlineData(ParticipantWork.Partner)]
    [InlineData(ParticipantWork.Review)]
    [InlineData(ParticipantWork.Intake)]
    public void A_work_that_publishes_to_no_lane_of_its_own_is_told_no_routing(string work)
    {
        Assert.DoesNotContain("Routing standing here", Routed(Config(true, true), Engine("core"), work), StringComparison.Ordinal);
    }

    // ——— What it carries, and the account.

    [Fact]
    public void A_repository_s_block_says_where_things_are_its_choices_its_facts_and_its_memory_in_one_section()
    {
        var facts = new StandingFacts
        {
            Repository = "engine", Workspace = "work", CodeMap = "docs/code-map.json", Indexes = ["docs/README.md"],
            Choices = new StandingAnswer("test locally against dev.", At), Memory = ["engine: the loader reads the old key too"],
        };

        var block = StandingBlock.Compose(facts);

        Assert.StartsWith(StandingBlock.Opening, block.Text, StringComparison.Ordinal);
        Assert.Contains("Where things are. This repository's own documents, code and history", block.Text, StringComparison.Ordinal);
        Assert.Contains("`knowledge_search` and `memory_search`", block.Text, StringComparison.Ordinal);
        Assert.Contains("indexes its own documents in `docs/README.md`", block.Text, StringComparison.Ordinal);
        Assert.Contains("It keeps a code map in `docs/code-map.json`", block.Text, StringComparison.Ordinal);
        Assert.Contains("The person's choices on this machine for `engine`, in their own words, set 2026-11-03 09:00 UTC.", block.Text, StringComparison.Ordinal);
        Assert.Contains("  > test locally against dev.", block.Text, StringComparison.Ordinal);
        Assert.Contains("The repository and its workspace: you work in `engine`, of the workspace `work`.", block.Text, StringComparison.Ordinal);
        Assert.Contains("Memory this line read, as it stands now:\n- engine: the loader reads the old key too", block.Text, StringComparison.Ordinal);

        var account = block.Account;
        Assert.Equal((HandedSections.Block, HandedSources.Machine, "engine", At), (account.Name, account.Source, account.From, account.At));
        Assert.Equal(block.Text.Length, account.Chars);
        Assert.Contains("1 index", account.Said, StringComparison.Ordinal);
        Assert.Contains("1 memory entry the line read", account.Said, StringComparison.Ordinal);
        Assert.Null(account.Cuts);
    }

    [Fact]
    public void Memory_names_the_newest_five_each_clipped_and_counts_the_rest()
    {
        var memory = Enumerable.Range(1, 7).Select(n => $"entry {n} " + new string('m', StandingBlock.MemoryItemLimit)).ToList();

        var block = StandingBlock.Compose(new StandingFacts { Repository = "engine", Memory = memory });

        Assert.Contains("entry 5 ", block.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("entry 6 ", block.Text, StringComparison.Ordinal);
        Assert.Contains("… and 2 more memory entries this line read, which `memory_get` reads as they stand.", block.Text, StringComparison.Ordinal);
        Assert.Equal(2, Assert.Single(block.Account.Cuts!, cut => cut.Code == HandedCuts.BriefList).Count);
        Assert.DoesNotContain("Memory this line read", StandingBlock.Compose(new StandingFacts { Repository = "engine" }).Text, StringComparison.Ordinal);
    }

    /// <summary>A kind with no repository, a brainstorm's pass, an intake, Ask Daoris: the workspace's part alone (§3 C).</summary>
    [Fact]
    public void A_kind_with_no_repository_is_handed_the_workspace_s_part_alone()
    {
        var intake = StandingBlock.Compose(new StandingFacts { Workspace = "work", Choices = new StandingAnswer("x", At), Routing = RoutingStanding.Open });
        var brainstorm = StandingBlock.Compose(new StandingFacts { Knowledge = false });

        Assert.Contains("Where things are. The workspace's knowledge and its memory", intake.Text, StringComparison.Ordinal);
        Assert.Contains("The workspace: this session works for the workspace `work`, in no repository.", intake.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Routing standing", intake.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("choices", intake.Text, StringComparison.Ordinal);
        Assert.Equal(
            $"{StandingBlock.Opening}\n\nThe workspace: this session works for this machine's workspaces, in no repository.", brainstorm.Text);
    }

    // ——— Bound to the harness: the tree's skills where it loads none, the rules where it cuts the instruction file.

    [Fact]
    public void Codex_is_told_the_tree_s_skill_folders_and_its_rules_past_a_cut_instruction_file_and_Claude_neither()
    {
        Directory.CreateDirectory(Path.Combine(_tree, ".claude", "skills", "x"));
        Directory.CreateDirectory(Path.Combine(_tree, ".claude", "rules"));
        File.WriteAllText(Path.Combine(_tree, ".claude", "rules", "b-rule.md"), "b");
        File.WriteAllText(Path.Combine(_tree, ".claude", "rules", "a-rule.md"), "a");
        File.WriteAllText(Path.Combine(_tree, "AGENTS.md"), new string('a', 32_769));
        var facts = new StandingFacts { Repository = "engine" };

        var codex = StandingBlocks.OnHarness(facts, ProviderRegistry.Built().Resolve("codex-acp"), _tree);
        var claude = StandingBlocks.OnHarness(facts, BuiltDoors.Claude, _tree);
        var acp = StandingBlocks.OnHarness(facts, ProviderRegistry.Built().Resolve("claude-code-acp"), _tree);

        Assert.Equal([".claude/skills"], codex.Skills);
        Assert.Equal(["engine:.claude/rules/a-rule.md", "engine:.claude/rules/b-rule.md"], codex.Rules!.Ids);
        var told = StandingBlock.Compose(codex).Text;
        Assert.Contains("Its skills are in `.claude/skills`, and your harness does not load them itself", told, StringComparison.Ordinal);
        Assert.Contains("reads the first 32,768 bytes of this repository's `AGENTS.md`", told, StringComparison.Ordinal);
        Assert.Contains("`engine:.claude/rules/a-rule.md`, `engine:.claude/rules/b-rule.md`.", told, StringComparison.Ordinal);
        foreach (var native in new[] { claude, acp })
        {
            Assert.Empty(native.Skills);
            Assert.Null(native.Rules);
        }

        // An instruction file the harness reads whole carries no rule index.
        File.WriteAllText(Path.Combine(_tree, "AGENTS.md"), new string('a', 32_768));
        Assert.Null(StandingBlocks.OnHarness(facts, ProviderRegistry.Built().Resolve("codex-acp"), _tree).Rules);
        // A kind with no repository is told neither, only that its harness holds none of the person's denies.
        Assert.Equal(new StandingFacts { NoDenies = true }, StandingBlocks.OnHarness(new StandingFacts(), ProviderRegistry.Built().Resolve("codex-acp"), _tree));
    }

    /// <summary>
    /// D233 choice 3: a harness that takes no rules file (Codex) holds none of the person's denies, and its block says so with what the git
    /// doors still refuse; one that takes the rules is told nothing of it.
    /// </summary>
    [Fact]
    public void A_harness_that_takes_no_rules_is_told_it_holds_none_of_the_persons_denies()
    {
        var facts = new StandingFacts { Repository = "engine" };

        var codex = StandingBlocks.OnHarness(facts, ProviderRegistry.Built().Resolve("codex-acp"), _tree);
        var claude = StandingBlocks.OnHarness(facts, BuiltDoors.Claude, _tree);

        Assert.True(codex.NoDenies);
        Assert.False(claude.NoDenies);
        var told = StandingBlock.Compose(codex);
        Assert.Contains(StandingBlock.NoDenies, told.Text, StringComparison.Ordinal);
        Assert.Contains("refuse a push, a force-push and a branch delete", told.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(StandingBlock.NoDenies, StandingBlock.Compose(claude).Text, StringComparison.Ordinal);
    }

    // ——— The bound: the whole first prompt under the native door's single argument, the block through its file past it.

    [Fact]
    public void A_block_past_the_budget_goes_through_its_file_and_the_prompt_names_it()
    {
        var facts = new StandingFacts { Repository = "engine", File = "C:/daoris/run/spawn/s1.standing/block.md" };
        var block = StandingBlock.Compose(facts);

        var fits = StandingBlock.Fitted(facts, StandingBlock.Budget - block.Text.Length - StandingBlock.Joint.Length);
        var over = StandingBlock.Fitted(facts, StandingBlock.Budget - block.Text.Length - StandingBlock.Joint.Length + 1);
        var nowhere = StandingBlock.Fitted(facts with { File = null }, StandingBlock.Budget);

        Assert.False(fits.Filed);
        Assert.Equal(block.Text, fits.Text);
        Assert.True(over.Filed);
        Assert.Contains("is in `C:/daoris/run/spawn/s1.standing/block.md`", over.Text, StringComparison.Ordinal);
        Assert.Contains("read that file before anything else", over.Text, StringComparison.Ordinal);
        Assert.Equal(over.Text.Length, over.Account.Chars);
        Assert.Equal(StandingBlock.Budget, Assert.Single(over.Account.Cuts!, cut => cut.Code == HandedCuts.BlockFiled).Limit);
        Assert.False(nowhere.Filed);
    }

    [Fact]
    public void A_start_writes_a_filed_block_where_the_line_names_it_and_nothing_where_it_fits()
    {
        var home = Path.Combine(_tree, "home");
        var file = StandingBlocks.FileOf(home, "s1");
        var small = TargetPromptGoldenTests.Bare with { Block = new StandingFacts { Repository = "console-ui", File = file } };
        var large = small with { Body = new string('b', StandingBlock.Budget) };

        Assert.False(StandingBlocks.Hand(small));
        Assert.False(File.Exists(file));
        Assert.True(StandingBlocks.Hand(large));
        Assert.Equal(StandingBlock.Compose(large.Block!).Text + "\n", File.ReadAllText(file));
        Assert.Contains(file.Replace('\\', '/'), TargetPrompt.Compose(large), StringComparison.Ordinal);
        Assert.Equal(StandingBlocks.FolderOf(home, "s1"), Path.GetDirectoryName(file));
    }

    /// <summary>
    /// The proof (§4 row c): the whole first prompt at its bounds, per door and harness: a quest's instruction with the person's words and
    /// the channel at their caps and a block with every part at its own, measured as each harness's spawn binds it. Inline, each is under
    /// the native door's single argument with its margin; past it, the block goes through its file and the prompt names it.
    /// </summary>
    [Fact]
    public void The_whole_first_prompt_at_its_bounds_is_under_the_argument_on_every_door_and_harness()
    {
        Directory.CreateDirectory(Path.Combine(_tree, ".claude", "skills"));
        Directory.CreateDirectory(Path.Combine(_tree, ".agents", "skills"));
        Directory.CreateDirectory(Path.Combine(_tree, ".claude", "rules"));
        foreach (var n in Enumerable.Range(1, 12)) File.WriteAllText(Path.Combine(_tree, ".claude", "rules", $"rule-{n:00}.md"), "r");
        File.WriteAllText(Path.Combine(_tree, "AGENTS.md"), new string('a', 40_000));
        var bounded = Bounds(_tree);

        foreach (var name in new[] { "claude-code", "claude-code-acp", "codex-acp" })
        {
            var adapter = ProviderRegistry.Built().Resolve(name);
            var target = TargetPrompt.ForStart(bounded, adapter);
            var composed = TargetPrompt.Composed(target);
            var filed = TargetPrompt.Filed(target);
            output.WriteLine($"{name}: the whole first prompt {composed.Text.Length:N0} characters, its block {(filed ? "filed" : "inline")} "
                             + $"({Section(composed, HandedSections.Block).Chars:N0}), the argument {StandingBlock.ArgumentLimit:N0}");
            // Inline or through its file, the prompt the door carries keeps the margin under the argument.
            Assert.True(composed.Text.Length <= StandingBlock.Budget, $"{name}: {composed.Text.Length}");
            Assert.Equal(filed, Section(composed, HandedSections.Block).Cuts?.Any(cut => cut.Code == HandedCuts.BlockFiled) == true);
        }

        // Every part at its own cap besides, requirements and go-aheads too: the block goes through its file, and the prompt names it.
        var everything = TargetPrompt.ForStart(Everything(bounded), ProviderRegistry.Built().Resolve("codex-acp"));
        var all = TargetPrompt.Composed(everything);
        output.WriteLine($"every part at its cap: the instruction {all.Text.Length:N0} characters with the line naming its block's file, "
                         + $"the block {StandingBlock.Compose(everything.Block!).Text.Length:N0} in the file");
        Assert.True(TargetPrompt.Filed(everything));
        Assert.Contains("block.md`, since this instruction with it would pass", all.Text, StringComparison.Ordinal);
    }

    /// <summary>The kinds that compose their own prompt, and a conversation's first message, at their bounds: each under the budget.</summary>
    [Fact]
    public void Every_other_kind_s_first_prompt_with_its_block_is_under_the_budget()
    {
        var choices = new StandingAnswer(new string('c', DriverConfig.StandingLimit), At);
        var repository = new StandingFacts
        {
            Repository = "reports", Workspace = "work", Indexes = [.. Enumerable.Range(1, 11).Select(n => $"docs/INDEX{n}.md")],
            CodeMap = "docs/code-map.json", Choices = choices,
        };
        var kinds = new (string Kind, string Prompt, StandingFacts Block)[]
        {
            ("intake", IntakePrompt.Compose(new AskView("a1", "work", new string('s', 4_000), "Published", "full")), new StandingFacts { Workspace = "work" }),
            ("helper", new string('h', 6_000), repository),
            ("brainstorm", new string('p', 6_000), new StandingFacts { Knowledge = false }),
        };
        foreach (var (kind, prompt, block) in kinds)
        {
            var composed = TargetPrompt.Composed(TargetPromptGoldenTests.Bare with { Prompt = prompt, Block = block });
            output.WriteLine($"{kind}: {composed.Text.Length:N0} characters, its block {Section(composed, HandedSections.Block).Chars:N0}");
            Assert.True(composed.Text.Length <= StandingBlock.Budget, kind);
        }

        var chat = new ChatMessage(new string('w', 8_000), [])
        {
            Preface = new string('p', ChatMessage.PrefaceLimit),
            Block = StandingBlock.Compose(StandingBlocks.OnMachine(repository with { Repository = "engine" }, ParticipantWork.Chat, Config(true, true), [Engine("core", "web")], "work")),
        };
        output.WriteLine($"chat: its first message {chat.Prompt.Length:N0} characters, its block {chat.Block.Text.Length:N0}");
        Assert.True(chat.Prompt.Length <= StandingBlock.Budget);
    }

    /// <summary>A quest's instruction with the person's words and the channel at their caps, and its block with every part at its own.</summary>
    private static SessionTarget Bounds(string tree)
    {
        var at = At;
        var words = new AskWords("a1b2c3",
        [
            new AskWordView(AskWordView.Asked, new string('a', 600), at),
            .. Enumerable.Range(1, 8).Select(n => new AskWordView(AskWordView.Added, new string('w', AskWordsText.WordLimit), at.AddMinutes(n), "s1", "abc123")),
        ]);
        var channel = new ChannelHeld("a1b2c3", "ask:a1b2c3/reports",
        [
            .. Enumerable.Range(1, ChannelText.HandedNewest).Select(n => new ChannelItemView(ChannelItemView.Message, at.AddMinutes(n))
            {
                Id = $"m{n}", Person = true, Text = new string('m', ChannelText.MessageLimit),
            }),
        ], "s1");
        return TargetPromptGoldenTests.Full with
        {
            Root = tree,
            Words = words,
            Channel = channel,
            Block = new StandingFacts
            {
                Repository = "reports", Workspace = "work", Checkouts = true, CodeMap = "docs/code-map.json",
                Indexes = [.. Enumerable.Range(1, 11).Select(n => $"docs/INDEX{n}.md")],
                Routing = RoutingStanding.Open, Lanes = ["core", "web", "docs"],
                Choices = new StandingAnswer(new string('c', DriverConfig.StandingLimit + 100), At),
                Memory = [.. Enumerable.Range(1, 7).Select(n => new string('e', StandingBlock.MemoryItemLimit + 50))],
                File = Path.Combine(tree, "home", "run", "spawn", "s1.standing", "block.md"),
            },
        };
    }

    /// <summary>The bounds and the requirements and the go-aheads at their own caps.</summary>
    private static SessionTarget Everything(SessionTarget bounds) => bounds with
    {
        Requirements = [.. Enumerable.Range(1, 12).Select(n => new QuestRequirementView(new string('r', 1_000), new string('k', 400)))],
        Words = ((AskWords)bounds.Words!) with
        {
            GoAheads = [.. Enumerable.Range(1, 30).Select(n => new GoAheadView(n, "write", "production", new string('t', 300), [new GoAheadRequestView("s1", "abc123", At, "why")]))],
        },
    };

    private static HandedSection Section(ComposedInstruction composed, string name) => composed.Account.Sections.Single(section => section.Name == name);
}
