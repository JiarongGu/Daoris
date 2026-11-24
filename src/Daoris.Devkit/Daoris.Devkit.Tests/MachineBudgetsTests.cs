using Daoris.Devkit;

namespace Daoris.Devkit.Tests;

/// <summary>
/// What the devkit reads of the Daoris home: the version this machine runs, the private patterns file's place and the local inputs, laid
/// over the declaration's own members, which an older repository still holds. With no home they are the declaration's, or nothing.
/// </summary>
public sealed class MachineBudgetsTests : IDisposable
{
    private readonly Fixture _repository = new("budgets-repository");
    private readonly Fixture _home = new("budgets-home");

    public void Dispose()
    {
        _repository.Dispose();
        _home.Dispose();
    }

    private string Name => System.IO.Path.GetFileName(_repository.Path);

    private void Declare(string json) => _repository.Write(GateDeclaration.FileName, json);

    private void Keep(string json) => _home.Write("data/check-budgets.json", $$"""{ "schema": 1, "repositories": { "{{Name}}": {{json}} } }""");

    [Fact]
    public void The_homes_members_win_and_the_declarations_stand_where_the_home_keeps_none()
    {
        Declare("""{ "devkit": "0.0.1", "localInputs": ["local/older.txt"], "sensitive": { "patternsFile": "local/older.txt" } }""");
        Keep("""{ "devkit": "0.2.0", "patternsFile": "local/machine.txt", "localInputs": ["local\\a.txt", "local/a.txt"] }""");

        var declaration = GateDeclaration.Read(_repository.Path, _home.Path);

        Assert.Equal("0.2.0", declaration.Devkit);
        Assert.Equal("local/machine.txt", declaration.Sensitive.PatternsFile);
        Assert.Equal(["local/a.txt"], declaration.LocalInputs);

        Keep("""{ "alone": true }""");
        var older = GateDeclaration.Read(_repository.Path, _home.Path);
        Assert.Equal(("0.0.1", "local/older.txt"), (older.Devkit, older.Sensitive.PatternsFile));
        Assert.Equal(["local/older.txt"], older.LocalInputs);
    }

    [Fact]
    public void An_empty_list_of_inputs_in_the_home_says_there_are_none()
    {
        Declare("""{ "localInputs": ["local/older.txt"] }""");
        Keep("""{ "localInputs": [] }""");

        Assert.Empty(GateDeclaration.Read(_repository.Path, _home.Path).LocalInputs);
    }

    [Fact]
    public void With_no_home_no_file_or_no_record_there_is_no_pin_and_the_patterns_file_is_the_default()
    {
        Declare("{}");

        foreach (var declaration in new[] { GateDeclaration.Read(_repository.Path, null), GateDeclaration.Read(_repository.Path, _home.Path) })
        {
            Assert.Null(declaration.Devkit);
            Assert.Equal("local/sensitive-patterns.txt", declaration.Sensitive.PatternsFile);
            Assert.Empty(declaration.LocalInputs);
        }

        _home.Write("data/check-budgets.json", """{ "schema": 1, "repositories": { "another": { "devkit": "9.9.9" } } }""");
        Assert.Null(GateDeclaration.Read(_repository.Path, _home.Path).Devkit);
    }

    [Fact]
    public void A_file_that_does_not_read_is_refused_by_name()
    {
        Declare("{}");
        _home.Write("data/check-budgets.json", "{ not json");

        var error = Assert.Throws<DevkitException>(() => GateDeclaration.Read(_repository.Path, _home.Path));

        Assert.Contains("check-budgets.json", error.Message);
    }

    [Fact]
    public void A_linked_tree_reads_the_record_of_its_main_checkouts_folder()
    {
        _repository.Write("main-checkout/.git/worktrees/a-tree/commondir", "../..\n");
        _repository.Write("a-tree/.git", $"gitdir: {_repository.Absolute("main-checkout/.git/worktrees/a-tree")}\n");

        Assert.Equal("main-checkout", MachineBudgets.RepositoryName(_repository.Absolute("a-tree")));
        Assert.Equal("main-checkout", MachineBudgets.RepositoryName(_repository.Absolute("main-checkout")));
        Assert.Equal("a-plain", MachineBudgets.RepositoryName(_repository.Absolute("a-plain")));
    }
}
