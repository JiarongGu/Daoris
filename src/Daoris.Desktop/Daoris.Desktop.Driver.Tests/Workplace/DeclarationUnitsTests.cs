using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A declaration's units: a digest per gate, per <c>generated</c>, <c>reached</c> and <c>doctrine</c> and per
/// any other part, with only the inert members set aside, so a change holds only the unit it changed. A command change touches its gate
/// alone; a path, a time, a memory, words, a test set or a tier touches none; a gate added, removed or any unknown part is a unit of its own.
/// </summary>
public sealed class DeclarationUnitsTests
{
    private const string Accepted = """
        {
          "_why": "the checks",
          "tiers": { "needs": { "git": ["src/"] } },
          "generated": [ { "paths": "tools/size.json", "run": "node size.mjs", "_why": "the ratchet" } ],
          "reached": { "run": "node reached.mjs", "clock": true },
          "doctrine": { "command": "node", "args": ["daoris.mjs"] },
          "gates": [
            { "name": "unit", "run": "dotnet test", "paths": ["src/"], "timeoutMinutes": 10, "timeoutWhy": "p90", "memoryGb": 4 },
            { "name": "tools", "run": "npm test", "cwd": "tools", "_why": "the tools" }
          ],
          "tests": { "_why": "sets", "sets": [
            { "name": "a", "gate": "tools", "runner": "node", "files": ["tools/a/*.test.mjs", "tools/b/*.test.mjs"], "whole": ["tools/vitest.config.ts"] },
            { "name": "b", "gate": "tools", "runner": "node", "files": ["tools/c/*.test.mjs"] }
          ] },
          "integration": { "merge": [ { "paths": "docs/README.md", "by": "rows" } ] }
        }
        """;

    private static string Edited(string from, string to) => Accepted.Replace(from, to, StringComparison.Ordinal);

    [Fact]
    public void A_declaration_is_a_unit_per_gate_and_per_named_part_and_each_other_part_and_the_inert_ones_are_none()
    {
        var units = DeclarationUnits.Of(Accepted)!;

        Assert.Equal(
            ["doctrine", "gate:tools", "gate:unit", "generated", "order", "part:integration", "reached"],
            units.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal("gate:unit\ngate:tools", units["order"]);
    }

    /// <summary>The same declaration read twice, or written with other whitespace and member order, is the same digests.</summary>
    [Fact]
    public void A_digest_is_the_canonical_form_so_whitespace_and_member_order_are_none()
    {
        var reflowed = """{"integration":{"merge":[{"by":"rows","paths":"docs/README.md"}]},"gates":[{"run":"dotnet test","name":"unit"},{"cwd":"tools","name":"tools","run":"npm test"}],"reached":{"clock":true,"run":"node reached.mjs"},"generated":[{"run":"node size.mjs","paths":"tools/size.json"}],"doctrine":{"args":["daoris.mjs"],"command":"node"}}""";

        Assert.Equal(DeclarationUnits.Of(Accepted), DeclarationUnits.Of(reflowed));
        Assert.Empty(DeclarationUnits.Changed(Accepted, reflowed)!);
    }

    [Fact]
    public void A_command_change_changes_that_gates_unit_alone()
    {
        Assert.Equal(["gate:unit"], DeclarationUnits.Changed(Accepted, Edited("\"dotnet test\"", "\"dotnet test --no-build\"")));
        Assert.Equal(["gate:tools"], DeclarationUnits.Changed(Accepted, Edited("\"cwd\": \"tools\"", "\"cwd\": \"src\"")));
    }

    /// <summary>
    /// Reach, a timeout or memory not yet moved to the home, words, the test sets, the tiers and the docs change no unit:
    /// the whole file's acceptance that these once kept is the units' now.
    /// </summary>
    [Theory]
    [InlineData("\"src/\"", "\"src/\", \"tests/\"")]
    [InlineData("\"timeoutMinutes\": 10", "\"timeoutMinutes\": 20")]
    [InlineData("\"p90\"", "\"p90 x 2\"")]
    [InlineData("\"memoryGb\": 4", "\"memoryGb\": 6")]
    [InlineData("\"the checks\"", "\"the checks, said again\"")]
    [InlineData("\"the tools\"", "\"words about the tools\"")]
    [InlineData("\"the ratchet\"", "\"the ratchet, said again\"")]
    [InlineData("\"tools/a/*.test.mjs\"", "\"tools/a/**/*.test.mjs\"")]
    [InlineData("\"name\": \"a\", \"gate\": \"tools\"", "\"name\": \"z\", \"gate\": \"unit\"")]
    [InlineData("\"git\": [\"src/\"]", "\"git\": [\"src/\", \"lib/\"]")]
    [InlineData("\"memoryGb\": 4", "\"memoryGb\": 4, \"_why\": \"the build's peak\"")]
    public void A_path_a_time_a_memory_words_a_test_set_or_a_tier_changes_no_unit(string from, string to)
    {
        var now = Edited(from, to);
        Assert.NotEqual(Accepted, now);
        Assert.Empty(DeclarationUnits.Changed(Accepted, now)!);
    }

    [Theory]
    [InlineData("\"node size.mjs\"", "\"node size.mjs --shrink\"", "generated")]
    [InlineData("\"node reached.mjs\"", "\"node reached.mjs --all\"", "reached")]
    [InlineData("\"args\": [\"daoris.mjs\"]", "\"args\": [\"other.mjs\"]", "doctrine")]
    [InlineData("\"by\": \"rows\"", "\"by\": \"daoris\"", "part:integration")]
    [InlineData("\"by\": \"rows\"", "\"by\": \"rows\", \"_by\": \"daoris\"", "part:integration")]
    [InlineData("\"tests\":", "\"unasked\": { \"may\": [\"push\"] }, \"tests\":", "part:unasked")]
    [InlineData("\"tests\":", "\"anything\": 1, \"tests\":", "part:anything")]
    [InlineData("\"integration\"", "\"verification\": { \"run\": \"node v.mjs\" }, \"integration\"", "part:verification")]
    public void A_change_of_generated_reached_doctrine_or_any_other_part_changes_that_unit_alone(string from, string to, string unit)
    {
        Assert.Equal([unit], DeclarationUnits.Changed(Accepted, Edited(from, to)));
    }

    /// <summary>A part nobody named fails safe: it is in the digest, so a new member of any name inside a unit changes it.</summary>
    [Fact]
    public void A_member_nobody_named_inside_a_gate_is_in_its_digest()
    {
        Assert.Equal(["gate:tools"], DeclarationUnits.Changed(Accepted, Edited("\"cwd\": \"tools\"", "\"cwd\": \"tools\", \"alone\": true")));
        Assert.Equal(["gate:tools"], DeclarationUnits.Changed(Accepted, Edited("\"cwd\": \"tools\"", "\"cwd\": \"tools\", \"clock\": true")));
    }

    [Fact]
    public void A_gate_added_or_removed_is_a_unit_of_its_own_and_a_reorder_of_the_gates_both_hold_is_the_order()
    {
        var added = Edited("{ \"name\": \"tools\"", "{ \"name\": \"e2e\", \"run\": \"npm run e2e\" }, { \"name\": \"tools\"");
        Assert.Equal(["gate:e2e"], DeclarationUnits.Changed(Accepted, added));
        Assert.Equal(["gate:e2e"], DeclarationUnits.Changed(added, Accepted));

        const string Two = """{ "gates": [ { "name": "a", "run": "one" }, { "name": "b", "run": "two" } ] }""";
        Assert.Equal(["order"], DeclarationUnits.Changed(Two, """{ "gates": [ { "name": "b", "run": "two" }, { "name": "a", "run": "one" } ] }"""));
        Assert.Equal(["gate:b"], DeclarationUnits.Changed(Two, """{ "gates": [ { "name": "a", "run": "one" } ] }"""));
    }

    [Fact]
    public void A_text_that_does_not_read_has_no_units_and_changes_none_of_the_others()
    {
        Assert.Null(DeclarationUnits.Of("{ not json"));
        Assert.Null(DeclarationUnits.Of("""{ "gates": [ { "name": "a", "run": "x", "run": "y" } ] }"""));
        Assert.Null(DeclarationUnits.Of(null));
        Assert.Null(DeclarationUnits.Changed(Accepted, "{ not json"));
    }

    /// <summary>A gate with no name, or a name written for two, is held apart by its place, so a change to it still changes a unit.</summary>
    [Fact]
    public void A_gate_with_no_name_or_a_name_written_twice_still_changes_a_unit()
    {
        const string Odd = """{ "gates": [ { "run": "one" }, { "name": "a", "run": "two" }, { "name": "a", "run": "three" } ] }""";
        var now = Odd.Replace("\"one\"", "\"uno\"", StringComparison.Ordinal);
        Assert.NotEmpty(DeclarationUnits.Changed(Odd, now)!);
        Assert.NotEmpty(DeclarationUnits.Changed(Odd, Odd.Replace("\"three\"", "\"tres\"", StringComparison.Ordinal))!);
    }

    /// <summary>
    /// A run depends on the gates it runs and on what every run shares: a named gate's run is held by its own unit, a run of all gates by
    /// each, and the parts nobody named hold every run.
    /// </summary>
    [Fact]
    public void A_run_depends_on_its_own_gates_and_on_what_every_run_shares()
    {
        var named = DeclarationUnits.Run(["unit"], reached: false);
        Assert.True(named("gate:unit"));
        Assert.False(named("gate:tools"));
        Assert.False(named("order"));
        Assert.False(named("reached"));
        Assert.False(named("doctrine"));
        Assert.True(named("generated"));
        Assert.True(named("part:integration"));

        var all = DeclarationUnits.Run(null, reached: false);
        Assert.True(all("gate:unit") && all("gate:tools") && all("order"));
        Assert.False(all("reached"));
        Assert.True(DeclarationUnits.Run(["unit"], reached: true)("reached"));
    }

    // Narrowing: what a change drops from a reach, said in one line.

    [Fact]
    public void A_dropped_gate_path_or_test_file_is_a_narrowing_and_an_added_or_kept_one_is_not()
    {
        Assert.Empty(DeclarationUnits.Narrowed(Accepted, Accepted));
        Assert.Empty(DeclarationUnits.Narrowed(Accepted, Edited("\"src/\"", "\"src/\", \"tests/\"")));
        Assert.Empty(DeclarationUnits.Narrowed(Accepted, Edited("\"tools/a/*.test.mjs\"", "\"tools/a/*.test.mjs\", \"tools/d/*.test.mjs\"")));

        var narrowed = DeclarationUnits.Narrowed(
            Accepted, Edited("\"paths\": [\"src/\"]", "\"paths\": []").Replace("\"tools/b/*.test.mjs\"", "\"tools/x/*.test.mjs\"", StringComparison.Ordinal));

        Assert.Equal(
            [new DeclaredNarrowing("the paths of `unit`", ["src/"]), new DeclaredNarrowing("the test files of the set `a`", ["tools/b/*.test.mjs"])],
            narrowed);
        Assert.Equal(
            "narrowed: the paths of `unit` lost `src/`; the test files of the set `a` lost `tools/b/*.test.mjs`",
            DeclaredNarrowing.Said(narrowed));
    }

    [Fact]
    public void A_dropped_whole_pattern_a_dropped_set_and_a_dropped_gate_with_paths_are_narrowings()
    {
        var narrowed = DeclarationUnits.Narrowed(
            Accepted, Edited("\"whole\": [\"tools/vitest.config.ts\"]", "\"whole\": []"));
        Assert.Equal([new DeclaredNarrowing("the whole-run files of the set `a`", ["tools/vitest.config.ts"])], narrowed);

        var noSet = Edited("\"name\": \"b\", \"gate\": \"tools\", \"runner\": \"node\", \"files\": [\"tools/c/*.test.mjs\"]", "\"name\": \"z\", \"gate\": \"tools\", \"runner\": \"node\", \"files\": []");
        Assert.Equal([new DeclaredNarrowing("the test files of the set `b`", ["tools/c/*.test.mjs"])], DeclarationUnits.Narrowed(Accepted, noSet));

        var noGate = Accepted.Replace("{ \"name\": \"unit\", \"run\": \"dotnet test\", \"paths\": [\"src/\"], \"timeoutMinutes\": 10, \"timeoutWhy\": \"p90\", \"memoryGb\": 4 },", "", StringComparison.Ordinal);
        Assert.Equal([new DeclaredNarrowing("the paths of `unit`", ["src/"])], DeclarationUnits.Narrowed(Accepted, noGate));
    }

    [Fact]
    public void A_path_written_as_one_string_and_a_text_that_does_not_read_are_read_without_a_throw()
    {
        const string One = """{ "gates": [ { "name": "a", "run": "x", "paths": "src/" } ] }""";
        Assert.Equal([new DeclaredNarrowing("the paths of `a`", ["src/"])], DeclarationUnits.Narrowed(One, One.Replace("\"paths\": \"src/\"", "\"paths\": \"lib/\"", StringComparison.Ordinal)));
        Assert.Empty(DeclarationUnits.Narrowed("{ not json", One));
        Assert.Empty(DeclarationUnits.Narrowed(One, null));
    }
}
