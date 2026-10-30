using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDRETIRE1: the two doors of the withdrawal of a landing's branch (D50), without git: the names the terminal's verb takes and the
/// repositories they belong to, the lease and the checkout the door asks before the act, the words both doors say, and the verb's usage.
/// The act over real git is <see cref="LandedWithdrawTests"/>.
/// </summary>
public sealed class LandedBranchWithdrawalTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-withdrawal-" + Guid.NewGuid().ToString("N")[..8]);

    public LandedBranchWithdrawalTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_home, recursive: true);
        }
        catch (DirectoryNotFoundException) { /* nothing was written */ }
    }

    private static LandedBranch Landed(string repository, string branch) =>
        new(repository, "default", branch, "main", "t1", "s1", null, null, new DateTimeOffset(2026, 10, 29, 9, 0, 0, TimeSpan.Zero));

    private LandedBranchWithdrawal Door(params LandedBranch[] landed)
    {
        var trees = new SessionTrees(_home);
        foreach (var entry in landed) trees.Recorded.Record(entry);
        return new(trees, _home);
    }

    /// <summary>The terminal's naming: a name a standing landing made, in the repositories that made it; none for a session branch or a person's own.</summary>
    [Fact]
    public void A_name_is_a_landing_branchs_only_where_a_standing_landing_made_it()
    {
        var door = Door(Landed("engine", "quest/row-1"), Landed("deck", "quest/row-1"), Landed("engine", "quest/row-2"));

        Assert.Equal(["engine", "deck"], door.Owners("quest/row-1", null));
        Assert.Equal(["deck"], door.Owners("quest/row-1", "DECK"));
        Assert.Equal(["engine"], door.Owners("quest/row-2", null));
        Assert.Empty(door.Owners("quest/row-3", null));
        Assert.Empty(door.Owners("daoris/s-1a2b3c4d", null));
        Assert.Empty(door.Owners("quest/row-2", "deck"));
    }

    /// <summary>A landing's branch given to the tree removal as a path is still refused as no session tree, and the refusal names the door it has.</summary>
    [Fact]
    public async Task A_landings_branch_named_as_a_path_is_refused_as_no_tree_and_names_its_door()
    {
        var trees = new SessionTrees(_home);
        trees.Recorded.Record(Landed("engine", "quest/row-1"));

        var landing = await trees.RemoveAsync("quest/row-1");
        var other = await trees.RemoveAsync("quest/row-2");

        Assert.False(landing.Removed);
        Assert.Contains("is not a session tree", landing.Message);
        Assert.Contains("trees remove quest/row-1 --why", landing.Message);
        Assert.False(other.Removed);
        Assert.Contains("is not a session tree", other.Message);
        Assert.DoesNotContain("--why", other.Message);
    }

    [Fact]
    public async Task A_name_two_repositories_made_is_refused_until_the_person_says_which()
    {
        var door = Door(Landed("engine", "quest/row-1"), Landed("deck", "quest/row-1"));
        var ledger = new DiscardLedger { Registry = [("engine", _home), ("deck", _home)] };

        var refused = await door.WithdrawNamedAsync(ledger.Client(), "quest/row-1", null, "by hand", plan: false);

        Assert.False(refused.Done);
        Assert.Equal(TreesWords.NamesSeveral("quest/row-1", ["engine", "deck"]), refused.Message);
        Assert.Contains("--repository", refused.Message);
        Assert.DoesNotContain("/api/registry", ledger.Asked);
    }

    [Fact]
    public async Task A_name_no_landing_made_is_refused_in_the_doors_own_words()
    {
        var door = Door(Landed("engine", "quest/row-1"));
        var ledger = new DiscardLedger { Registry = [("engine", _home)] };

        var refused = await door.WithdrawNamedAsync(ledger.Client(), "feature/mine", null, "by hand", plan: false);

        Assert.False(refused.Done);
        Assert.Contains("no landing of this machine's made `feature/mine`", refused.Message);
    }

    /// <summary>
    /// Only the process holding the home acts: a test process holds none, so a press is refused in the sentence the recovery refs' press says, before
    /// the checkout is asked; a plan, which changes nothing, goes on to ask it.
    /// </summary>
    [Fact]
    public async Task A_press_without_the_home_is_refused_and_a_plan_is_not()
    {
        var door = Door(Landed("engine", "quest/row-1"));
        var ledger = new DiscardLedger { Registry = [] };

        var press = await door.WithdrawAsync(ledger.Client(), "engine", "quest/row-1", "by hand", plan: false);
        Assert.False(press.Done);
        Assert.Equal(RecoveryDropping.WithoutHome(_home), press.Message);
        Assert.Empty(ledger.Asked);

        var plan = await door.WithdrawAsync(ledger.Client(), "engine", "quest/row-1", "by hand", plan: true);
        Assert.False(plan.Done);
        Assert.Contains("has no checkout here", plan.Message);
        Assert.Contains("/api/registry", ledger.Asked);
    }

    [Fact]
    public void The_words_are_one_sentence_and_the_exit_says_whether_it_was_kept()
    {
        var done = TreesWords.Withdraw(new(true, "withdrew `quest/row-1`"));
        Assert.Equal(0, done.Code);
        Assert.Equal("trees: withdrew `quest/row-1`", Assert.Single(done.Out));

        var planned = TreesWords.Withdraw(new(false, "would withdraw `quest/row-1`") { Planned = true });
        Assert.Equal(0, planned.Code);
        Assert.Equal("trees: would withdraw `quest/row-1`", Assert.Single(planned.Out));

        var kept = TreesWords.Withdraw(new(false, "`quest/row-1` in `engine` is not withdrawn: it is checked out in a working tree."));
        Assert.Equal(1, kept.Code);
        Assert.Contains("not withdrawn", Assert.Single(kept.Out));
    }

    /// <summary>
    /// The terminal's verb: a recorded landing's name is asked before the path (`quest/…` holds a separator), takes --why and refuses --force, prints
    /// through <see cref="TreesWords.Withdraw"/>, tells the windows what it changed, and its usage names the form.
    /// </summary>
    [Fact]
    public void The_terminals_verb_routes_a_landings_name_to_the_withdrawal_and_its_usage_names_it()
    {
        var console = File.ReadAllText(Path.Combine(SourceFiles.Project("Daoris.Desktop.Driver.Host"), "TreesConsole.cs"));

        var owners = console.IndexOf("new LandedBranchWithdrawal(trees, home).Owners(named", StringComparison.Ordinal);
        Assert.True(owners > 0);
        Assert.True(owners < console.IndexOf("var removal = LooksLikePath(named)", StringComparison.Ordinal));
        Assert.Contains("WithdrawNamedAsync(service, named, repository, why, plan)", console);
        Assert.Contains("TreesWords.Withdraw(withdrawal)", console);
        Assert.Contains("Told(home, PressesTold.WithdrawLanding)", console);
        Assert.Contains("remove <landing branch> [--repository <name>] --why \\\"…\\\" [--plan]", console);
        Assert.Contains("remove <landing branch> [--repository <name>] --why \"…\" [--plan]", DriverCommand.Usage);
    }
}
