using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A limit's go-on carries its conversation to the next account where its door's provider declares a carry: whole at or under the
/// provider's bound, compacted past it where the harness can be asked to, else briefed, and briefed where the copy fails, each by code.
/// </summary>
public sealed class LimitCarryTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-limit-carry-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>🔴 A door whose provider declares no carry is briefed, said by its code on the new part and in the machine log.</summary>
    [Fact]
    public void A_door_that_declares_no_carry_is_briefed_and_says_so()
    {
        var registry = ProviderRegistry.Built();
        Assert.Null(LimitCarry.Declared(registry, new DoorId("dsh")));
        Assert.Null(LimitCarry.Declared(registry, new DoorId("alt-acp-stub")));

        var plan = LimitCarry.Plan(LimitCarry.Declared(registry, new DoorId("dsh")), "conv", "from", "to", 1_000, 1_000_000);
        Assert.Equal(new CarryPlan.Briefed(LimitCarry.Unable), plan);
        var said = CarriedNotes.NotCarried(LimitCarry.Unable);
        Assert.Contains(said.Parts, part => part.Code == "moved.not-carried" && Equals(part.Value("reason"), LimitCarry.Unable));
        var line = Continuations.Moved("s2", "s1", "dsh", "dsh", "account-1", "account-2", HandOverWhy.Limit, briefed: true, carry: LimitCarry.Unable);
        Assert.Contains(line.Data, field => field is ("carry", "unable"));
        Assert.DoesNotContain(
            Continuations.Moved("s2", "s1", "dsh", "dsh", "account-1", "account-2", HandOverWhy.Agent, briefed: true).Data,
            field => field.Key == "carry");
    }

    /// <summary>Every door that can carry a conversation across accounts declares it through its provider, with its own bound.</summary>
    [Fact]
    public void The_doors_that_carry_declare_it_through_their_provider()
    {
        var registry = ProviderRegistry.Built();
        foreach (var door in new[] { "claude-code", "claude-code-acp" })
        {
            Assert.Equal(ClaudeConversationCarry.Bound, LimitCarry.Declared(registry, new DoorId(door))!.WholeBound);
        }

        Assert.Equal(CodexConversationHome.Bound, LimitCarry.Declared(registry, new DoorId("codex-acp"))!.WholeBound);
        Assert.Equal(StubConversationHome.Bound, LimitCarry.Declared(registry, new DoorId("acp-stub"))!.WholeBound);
    }

    /// <summary>🔴 Whole at or under the bound and where no context was said; compacted past it, Claude Code asked at the bound's share of its window.</summary>
    [Fact]
    public void The_bound_chooses_whole_compacted_or_briefed()
    {
        var claude = ClaudeConversationCarry.Carry;
        Assert.Equal(new CarryPlan.Whole("c", 155_233), LimitCarry.Plan(claude, "c", "a", "b", 155_233, 1_000_000));
        Assert.Equal(new CarryPlan.Whole("c", 200_000), LimitCarry.Plan(claude, "c", "a", "b", 200_000, 1_000_000));
        Assert.Equal(new CarryPlan.Whole("c", null), LimitCarry.Plan(claude, "c", "a", "b", null, null));

        var past = Assert.IsType<CarryPlan.Compacted>(LimitCarry.Plan(claude, "c", "a", "b", 400_000, 1_000_000));
        Assert.Equal(("c", 400_000L, ClaudeConversationCarry.Bound), (past.Conversation, past.Tokens!.Value, past.Bound));
        Assert.Equal("20", past.Variables[ClaudeConversationCarry.CompactVariable]);

        Assert.Equal(new CarryPlan.Briefed(LimitCarry.Large), LimitCarry.Plan(claude, "c", "a", "b", 400_000, null));
        Assert.Equal(new CarryPlan.Briefed(LimitCarry.Large), LimitCarry.Plan(CodexConversationHome.Home, "c", "a", "b", 250_000, 272_000));
        Assert.Equal(new CarryPlan.Briefed(LimitCarry.Unkept), LimitCarry.Plan(claude, null, "a", "b", 1_000, null));
        Assert.Equal(new CarryPlan.Briefed(LimitCarry.Unhomed), LimitCarry.Plan(claude, "c", null, "b", 1_000, null));
    }

    /// <summary>🔴 Claude Code's copy: the conversation's file and its folder under the tree's project folder, over an older copy there.</summary>
    [Fact]
    public void Claude_Code_s_conversation_is_copied_under_the_tree_s_project_folder()
    {
        var (from, to, tree) = (Path.Combine(_home, "account-3"), Path.Combine(_home, "account-1"), Path.Combine(_home, "engine"));
        var folder = ClaudeConversationHome.Home.Projects!.Flatten(Path.GetFullPath(tree));
        var source = Path.Combine(from, "projects", folder);
        Directory.CreateDirectory(Path.Combine(source, "2eacc9b5", "tool-results"));
        File.WriteAllText(Path.Combine(source, "2eacc9b5.jsonl"), "every turn\n");
        File.WriteAllText(Path.Combine(source, "2eacc9b5", "tool-results", "one.txt"), "a result");
        Directory.CreateDirectory(Path.Combine(to, "projects", folder));
        File.WriteAllText(Path.Combine(to, "projects", folder, "2eacc9b5.jsonl"), "an older copy\n");

        var plan = LimitCarry.Copied(new CarryPlan.Whole("2eacc9b5", 155_233), ClaudeConversationCarry.Carry, from, to, tree);

        Assert.IsType<CarryPlan.Whole>(plan);
        Assert.Equal("every turn\n", File.ReadAllText(Path.Combine(to, "projects", folder, "2eacc9b5.jsonl")));
        Assert.Equal("a result", File.ReadAllText(Path.Combine(to, "projects", folder, "2eacc9b5", "tool-results", "one.txt")));
        Assert.Empty(Directory.EnumerateFiles(to, "*.carrying", SearchOption.AllDirectories));
    }

    /// <summary>🔴 A copy that finds no conversation is briefed by its code.</summary>
    [Fact]
    public void A_carry_whose_conversation_is_missing_is_briefed_missing()
    {
        var (from, to) = (Path.Combine(_home, "account-3"), Path.Combine(_home, "account-1"));
        Directory.CreateDirectory(from);
        Assert.Equal(
            new CarryPlan.Briefed(LimitCarry.Missing),
            LimitCarry.Copied(new CarryPlan.Whole("gone", 10), ClaudeConversationCarry.Carry, from, to, Path.Combine(_home, "engine")));
        Assert.Equal(
            new CarryPlan.Briefed(LimitCarry.Missing),
            LimitCarry.Copied(new CarryPlan.Whole("gone", 10), CodexConversationHome.Home, from, to, Path.Combine(_home, "engine")));
    }

    /// <summary>Codex's thread is one rollout file under its date, copied to the same place in the other home.</summary>
    [Fact]
    public void A_Codex_thread_is_copied_to_its_date_s_place()
    {
        var (from, to) = (Path.Combine(_home, "codex-1"), Path.Combine(_home, "codex-2"));
        var dated = Path.Combine("sessions", "2026", "11", "08");
        Directory.CreateDirectory(Path.Combine(from, dated));
        var name = "rollout-2026-11-08T10-00-00-0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b.jsonl";
        File.WriteAllText(Path.Combine(from, dated, name), "a thread\n");

        Assert.Null(CodexConversationHome.Home.Copy(from, to, _home, "0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b"));
        Assert.Equal("a thread\n", File.ReadAllText(Path.Combine(to, dated, name)));
    }
}
