using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A changed declaration said as checks added, removed and changed, by name with each command, read from the two texts' JSON.</summary>
public sealed class DeclarationChangeTests
{
    private const string Was = """{ "gates": [ { "name": "cli", "run": "npm run verify" }, { "name": "old", "run": "echo old" }, { "name": "tools", "run": "npm test", "paths": ["a/"] } ], "integration": { "by": "daoris" } }""";

    private const string Now = """{ "gates": [ { "name": "cli", "run": "npm run verify" }, { "name": "tools", "run": "npm test", "paths": ["a/", "b/"] }, { "name": "comments", "run": "node tools/comment-gate.mjs" } ], "integration": { "by": "daoris", "scratch": ["bin/**"] } }""";

    [Fact]
    public void Checks_added_removed_and_changed_are_named_with_their_commands()
    {
        var change = DeclarationChange.Between(Was, Now);

        Assert.Equal([new DeclaredCheck("comments", "node tools/comment-gate.mjs")], change.Added);
        Assert.Equal([new DeclaredCheck("old", "echo old")], change.Removed);
        Assert.Empty(change.Changed);
        Assert.Equal(["integration"], change.Other);
        Assert.Equal("added `comments`; removed `old`; changed its `integration` part", change.Said());
    }

    [Fact]
    public void A_change_of_only_what_is_set_aside_is_no_change()
    {
        const string Was = """{ "_why": "a", "gates": [ { "name": "b", "run": "npm test", "paths": ["a/"], "timeoutMinutes": 5, "_why": "x" } ], "tests": { "sets": [] }, "tiers": {} }""";
        const string Now = """{ "_why": "b", "gates": [ { "name": "b", "run": "npm test", "paths": ["b/"], "timeoutMinutes": 9, "_why": "y" } ], "tests": { "sets": [ { "name": "s" } ] }, "tiers": { "fast": [] } }""";

        var change = DeclarationChange.Between(Was, Now);

        Assert.True(change.IsEmpty, change.Said());
    }

    [Fact]
    public void A_run_that_is_not_a_string_or_a_text_that_does_not_read_never_throws()
    {
        const string Was = """{ "gates": [ { "name": "a", "run": "one" } ] }""";

        var numeric = DeclarationChange.Between(Was, """{ "gates": [ { "name": "a", "run": 2 } ] }""");
        Assert.Equal([new DeclaredCheck("a", "", "one")], numeric.Changed);

        var twice = DeclarationChange.Between(Was, """{ "gates": [ { "name": "a", "run": "one", "run": "two" } ] }""");
        Assert.True(twice.Unread);
        Assert.False(twice.IsEmpty);
        Assert.Empty(twice.Removed);
        Assert.Equal("its text does not read as strict JSON (a member written twice, a comment or a trailing comma)", twice.Said());
    }

    [Fact]
    public void A_changed_command_says_the_one_it_ran_before_and_nothing_accepted_makes_every_check_an_addition()
    {
        var edited = DeclarationChange.Between("""{ "gates": [ { "name": "a", "run": "one" } ] }""", """{ "gates": [ { "name": "a", "run": "two" } ] }""");
        Assert.Equal([new DeclaredCheck("a", "two", "one")], edited.Changed);

        var first = DeclarationChange.Between(null, Was);
        Assert.Equal(["cli", "old", "tools"], first.Added.Select(each => each.Name));
        Assert.True(DeclarationChange.Between(Was, Was).IsEmpty);
    }
}
