using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The effective rule of a path git does not manage: the found layer, the repository's, the person's machine rules and then their
/// repository's, a match in a later layer winning and the last match deciding within one, and the floor read over all of them.
/// </summary>
public sealed class FileRulesTests
{
    private static readonly FileFloorFacts NoFloor = new([], []);

    private static FileRule Rule(FileKind kind, params string[] paths) => new(kind, paths);

    private static PersonFilesRead Person(string json) => PersonFiles.Parse(json);

    [Fact]
    public void A_later_layer_wins_and_within_one_layer_the_last_match_decides()
    {
        var rules = FileRules.Of(
            found: [Rule(FileKind.Intermediate, "out/"), Rule(FileKind.Scratch, "_fixtures/")],
            repository: [Rule(FileKind.Output, "out/"), Rule(FileKind.Cache, "out/"), Rule(FileKind.Dependency, "deps/") with { Install = "npm ci", Key = ["package-lock.json"] }],
            person: Person("""
                { "machine": { "rules": [ { "kind": "unknown", "paths": ["deps/"] } ] },
                  "repositories": { "Web": { "rules": [ { "kind": "local", "paths": ["deps/"] }, { "kind": "scratch", "paths": ["deps/"] } ] } } }
                """),
            name: "web");

        var output = rules.For("out/", NoFloor);
        Assert.Equal((FileKind.Cache, FileLayer.Repository), (output.Kind, output.Layer));
        Assert.Equal([FileLayer.Found, FileLayer.Repository], output.Below.Select(each => each.Layer));

        var deps = rules.For("deps/", NoFloor);
        Assert.Equal((FileKind.Scratch, FileLayer.Person), (deps.Kind, deps.Layer));
        Assert.Equal([FileLayer.Repository, FileLayer.Machine, FileLayer.Person], deps.Below.Select(each => each.Layer));

        Assert.Equal((FileKind.Scratch, FileLayer.Found), (rules.For("_fixtures/", NoFloor).Kind, rules.For("_fixtures/", NoFloor).Layer));
        var none = rules.For("stray/", NoFloor);
        Assert.Equal((FileKind.Unknown, (FileLayer?)null), (none.Kind, none.Layer));
    }

    [Fact]
    public void A_found_rule_never_overrides_the_floor()
    {
        var rules = FileRules.Of(
            found: [Rule(FileKind.Output, "src/cli/bin/"), Rule(FileKind.Scratch, ".claude/worktrees/"), Rule(FileKind.Local, ".claude/settings.local.json") with { Copy = true }],
            repository: [], person: PersonFilesRead.Empty, name: "web");
        var floor = new FileFloorFacts(["src/cli/bin/cli.mjs"], [".claude/worktrees/"]);

        var tracked = rules.For("src/cli/bin/", floor);
        Assert.Null(tracked.Kind);
        Assert.Equal(FileLayer.Floor, tracked.Layer);
        Assert.Equal("git tracks it", tracked.Floor);

        var worktrees = rules.For(".claude/worktrees/", floor);
        Assert.Equal((FileKind.Repository, FileLayer.Floor), (worktrees.Kind, worktrees.Layer));
        Assert.Equal(FileKind.Scratch, Assert.Single(worktrees.Below).Rule.Kind);

        var settings = rules.For(".claude/settings.local.json", floor);
        Assert.Equal((FileKind.Local, FileLayer.Found), (settings.Kind, settings.Layer));
        Assert.False(settings.Copy);
        Assert.Contains("never copied", settings.Floor, StringComparison.Ordinal);
    }

    [Fact]
    public void A_copy_acts_from_any_layer_but_never_onto_a_settings_file()
    {
        var rules = FileRules.Of(
            found: [],
            repository: [Rule(FileKind.Local, "local/sensitive-patterns.txt") with { Copy = true }, Rule(FileKind.Local, ".mcp.json") with { Copy = true }],
            person: PersonFilesRead.Empty, name: "web");

        Assert.True(rules.For("local/sensitive-patterns.txt", NoFloor).Copy);
        Assert.False(rules.For(".mcp.json", NoFloor).Copy);
    }

    [Fact]
    public void A_field_a_rule_leaves_out_is_taken_from_its_signature_and_a_dependency_with_neither_acts_on_nothing()
    {
        FileRule? Signature(string path, FileKind kind) => path == "node_modules/" && kind == FileKind.Dependency
            ? Rule(FileKind.Dependency, "node_modules/") with { Install = "npm ci", Key = ["package-lock.json", "package.json"] }
            : null;
        var rules = FileRules.Of(
            found: [], repository: [Rule(FileKind.Dependency, "node_modules/") with { Key = ["package-lock.json"] }, Rule(FileKind.Dependency, "vendor/")],
            person: PersonFilesRead.Empty, name: "web", signature: Signature);

        var placed = rules.For("node_modules/", NoFloor);
        Assert.Equal("npm ci", placed.Rule!.Install);
        Assert.Equal(["package-lock.json"], placed.Rule.Key);
        Assert.Null(placed.Idle);
        Assert.Contains("acts on nothing", rules.For("vendor/", NoFloor).Idle, StringComparison.Ordinal);
    }

    [Fact]
    public void The_permissions_restrict_nothing_by_default_and_are_read_deny_then_allow_then_ask()
    {
        var open = FileRules.Of([], [], PersonFilesRead.Empty, "web");
        Assert.Equal(FileVerdict.Made, open.Judge(FileActs.Run("npm ci")));
        Assert.Equal(FileVerdict.Made, open.Judge(FileActs.Share("node_modules/")));

        var person = Person("""
            { "machine": { "permissions": { "ask": ["run(*)"], "deny": ["copy(local/*.pem)"] } },
              "repositories": { "web": { "permissions": { "allow": ["run(npm ci)", { "rule": "copy(local/*)", "by": "person" }] } } } }
            """);
        var rules = FileRules.Of([], [], person, "web");
        Assert.Equal(FileVerdict.Made, rules.Judge(FileActs.Run("npm ci")));
        Assert.Equal(FileVerdict.Held, rules.Judge(FileActs.Run("node tools/x.mjs")));
        Assert.Equal(FileVerdict.Denied, rules.Judge(FileActs.Copy("local/key.pem")));
        Assert.Equal(FileVerdict.Made, rules.Judge(FileActs.Copy("local/notes.txt")));
        Assert.Equal(FileVerdict.Held, FileRules.Of([], [], person, "other").Judge(FileActs.Run("npm ci")));
    }

    [Fact]
    public void A_person_s_file_that_does_not_read_holds_every_act_as_an_ask_no_allow_answers()
    {
        var person = Person("""{ "machine": { "permissions": { "allow": ["run(*)"] } }, }""");

        Assert.NotNull(person.Unread);
        var rules = FileRules.Of([], [], person, "web");
        Assert.Equal(FileVerdict.Held, rules.Judge(FileActs.Run("npm ci")));
        Assert.Equal(FileVerdict.Held, rules.Judge(FileActs.Copy("local/a.txt")));
        Assert.Equal(FileVerdict.Held, rules.Judge(FileActs.Share("node_modules/")));
        Assert.NotNull(rules.Unread);
    }

    [Fact]
    public void A_rule_or_a_permission_that_does_not_read_is_refused_alone_and_said()
    {
        var person = Person("""
            { "machine": { "rules": [ { "kind": "output", "paths": ["out/"], "copy": true }, { "kind": "bogus", "paths": ["x/"] },
                                      { "kind": "scratch", "paths": ["C:\\tmp/"] }, { "kind": "scratch", "paths": ["tmp/"] } ],
                           "permissions": { "ask": ["delete(*)", "run(npm *)"] } } }
            """);

        Assert.Null(person.Unread);
        Assert.Equal(["tmp/"], Assert.Single(person.Machine.Rules).Paths);
        Assert.Equal(["run(npm *)"], person.Machine.Permissions.Ask);
        Assert.Equal(4, person.Problems.Count);
        Assert.Contains(person.Problems, problem => problem.Contains("`copy` is not a field of an `output` rule", StringComparison.Ordinal));
    }
}
