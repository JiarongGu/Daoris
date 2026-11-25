using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a tree's preparation and its machine-local inputs read: this machine's value in the home wins, an older declaration's member
/// stands where the home keeps none, and with no home the defaults apply. The repository is the registry's name, as the door writes it.
/// </summary>
public sealed class HomeBudgetReadersTests : IDisposable
{
    private readonly string _scratch = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "home-budget-readers", Guid.NewGuid().ToString("N")[..8]);
    private string Home => Path.Combine(_scratch, "home");
    private string Checkout => Path.Combine(_scratch, "engine");

    public HomeBudgetReadersTests()
    {
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Checkout);
    }

    public void Dispose() => GitSeed.Discard(_scratch);

    private void Gates(string text) => File.WriteAllText(Path.Combine(Checkout, "daoris.gates.json"), text);

    private const string Older = """
        { "gates": [ { "name": "a", "run": "x", "regenerate": "node regen.cjs" } ], "treeBaseline": "node older.cjs",
          "localInputs": ["local/older.txt"], "sensitive": { "patternsFile": "local/older-patterns.txt" } }
        """;

    [Fact]
    public void The_tree_baseline_is_the_homes_where_it_keeps_one_and_the_declarations_where_it_keeps_none()
    {
        Gates(Older);
        Assert.Equal("node older.cjs", TreePrepDeclared.Read(Checkout, CheckBudgets.Of(Home, "engine")).Baseline);

        CheckBudgets.SetTreeBaseline(Home, "engine", "node machine.cjs");

        var read = TreePrepDeclared.Read(Checkout, CheckBudgets.Of(Home, "engine"));
        Assert.Equal("node machine.cjs", read.Baseline);
        Assert.Equal(["node regen.cjs"], read.Regenerate);
        Assert.Equal("node older.cjs", TreePrepDeclared.Read(Checkout, CheckBudgets.Of(Home, "another")).Baseline);
        Assert.Equal("node older.cjs", TreePrepDeclared.Read(Checkout).Baseline);
        Assert.Contains("`node machine.cjs`", TreePreparation.Told(Checkout, CheckBudgets.Of(Home, "engine")), StringComparison.Ordinal);
    }

    [Fact]
    public void A_baseline_the_home_keeps_is_declared_with_no_gates_file_at_all()
    {
        CheckBudgets.SetTreeBaseline(Home, "engine", "node machine.cjs");

        Assert.Equal("node machine.cjs", TreePrepDeclared.Read(Checkout, CheckBudgets.Of(Home, "engine")).Baseline);
        Assert.True(TreePrepDeclared.Read(Checkout, CheckBudgets.Of(Home, "another")).Empty);
    }

    [Fact]
    public void The_local_inputs_and_the_patterns_file_are_the_homes_where_it_keeps_them()
    {
        Gates(Older);
        CheckBudgets.SetLocalInputs(Home, "engine", ["local/machine.txt"]);
        CheckBudgets.SetPatternsFile(Home, "engine", "local/machine-patterns.txt");
        var kept = CheckBudgets.Of(Home, "engine");

        Assert.Equal([new LocalInput("local/machine.txt", true), new LocalInput("local/machine-patterns.txt", true)], TreeLocalInputs.Declared(Checkout, kept));
        Assert.Equal("local/machine-patterns.txt", TreeLocalInputs.PatternsFile(Checkout, kept));
    }

    [Fact]
    public void A_member_the_home_keeps_none_of_falls_back_to_the_declarations()
    {
        Gates(Older);
        CheckBudgets.SetLocalInputs(Home, "engine", ["local/machine.txt"]);

        Assert.Equal(
            [new LocalInput("local/machine.txt", true), new LocalInput("local/older-patterns.txt", true)],
            TreeLocalInputs.Declared(Checkout, CheckBudgets.Of(Home, "engine")));
        Assert.Equal("local/older-patterns.txt", TreeLocalInputs.PatternsFile(Checkout, CheckBudgets.Of(Home, "engine")));
        Assert.Equal(
            [new LocalInput("local/older.txt", true), new LocalInput("local/older-patterns.txt", true)], TreeLocalInputs.Declared(Checkout, CheckBudgets.Of(Home, "another")));
    }

    [Fact]
    public void An_empty_list_in_the_home_says_there_are_no_inputs_over_the_declarations()
    {
        Gates(Older);
        CheckBudgets.SetLocalInputs(Home, "engine", []);

        Assert.Equal([new LocalInput("local/older-patterns.txt", true)], TreeLocalInputs.Declared(Checkout, CheckBudgets.Of(Home, "engine")));
    }

    [Fact]
    public void With_no_home_the_declaration_is_read_as_it_was_and_with_neither_the_defaults_apply()
    {
        Assert.Empty(TreeLocalInputs.Declared(Checkout, CheckBudgets.Of(null, "engine")));
        Assert.Null(TreeLocalInputs.PatternsFile(Checkout, CheckBudgets.Of(null, "engine")));

        Gates("{ }");
        Assert.Equal([new LocalInput(TreeLocalInputs.DefaultPatternsFile, false)], TreeLocalInputs.Declared(Checkout, CheckBudgets.Of(null, "engine")));
        Assert.Equal(TreeLocalInputs.DefaultPatternsFile, TreeLocalInputs.PatternsFile(Checkout, CheckBudgets.Of(null, "engine")));
    }

    [Fact]
    public void A_patterns_file_the_home_keeps_is_read_with_no_gates_file_and_one_outside_local_is_not_an_input()
    {
        CheckBudgets.SetPatternsFile(Home, "engine", "local/p.txt");

        Assert.Equal("local/p.txt", TreeLocalInputs.PatternsFile(Checkout, CheckBudgets.Of(Home, "engine")));
        Assert.Null(TreeLocalInputs.PatternsFile(Checkout, new RepositoryBudgets(RepositoryBudgets.None.Gates, RepositoryBudgets.None.SetTimeoutMs, null, null, null, "secrets/p.txt")));
    }
}
