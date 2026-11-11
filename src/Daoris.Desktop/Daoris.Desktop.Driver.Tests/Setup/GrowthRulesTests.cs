using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The growth switch (<c>docs/2026-11-11-grow-department-design.md</c> §4a): <c>growth</c> by repository and <c>workspaceGrowth</c> by
/// workspace in <c>driver.json</c>, read, resolved and edited by the one rule every door asks. Absent is <c>mode</c>; a repository's own value
/// overrides its workspace's; <c>mode</c> writes nothing where it changes nothing.
/// </summary>
public sealed class GrowthRulesTests
{
    private static DriverConfig Of(string json) => DriverConfig.Parse(json);

    private static GrowthEdit Edit(string? repository, string? workspace, string? value) => new() { Repository = repository, Workspace = workspace, Value = value };

    [Fact]
    public void A_new_workspace_and_a_new_repository_stand_at_mode_from_the_default()
    {
        var resolved = GrowthRules.Resolve(DriverConfig.Empty, "ledger", "aurora");

        Assert.Equal(("mode", "default"), (resolved.Value, resolved.Source));
        Assert.Equal(("mode", "default"), (GrowthRules.Resolve(DriverConfig.Empty, null, "aurora").Value, GrowthRules.Resolve(DriverConfig.Empty, null, "aurora").Source));
    }

    [Theory]
    [InlineData("""{"workspaceGrowth":{"aurora":"off"}}""", "ledger", "aurora", "off", "workspace")]
    [InlineData("""{"workspaceGrowth":{"Aurora":"asked"}}""", null, "aurora", "asked", "workspace")]
    [InlineData("""{"workspaceGrowth":{"aurora":"off"},"growth":{"ledger":"asked"}}""", "ledger", "aurora", "asked", "repository")]
    [InlineData("""{"workspaceGrowth":{"aurora":"asked"},"growth":{"LEDGER":"off"}}""", "ledger", "aurora", "off", "repository")]
    [InlineData("""{"workspaceGrowth":{"aurora":"off"},"growth":{"ledger":"mode"}}""", "ledger", "aurora", "mode", "repository")]
    [InlineData("""{"growth":{"ledger":"off"}}""", "atlas", "aurora", "mode", "default")]
    [InlineData("""{"workspaceGrowth":{"aurora":"off"}}""", "atlas", "other", "mode", "default")]
    public void A_repositorys_own_value_overrides_its_workspaces_and_names_are_matched_in_any_case(
        string file, string? repository, string workspace, string value, string source)
    {
        var resolved = GrowthRules.Resolve(Of(file), repository, workspace);

        Assert.Equal((value, source), (resolved.Value, resolved.Source));
    }

    [Fact]
    public void A_value_that_is_none_of_the_three_is_not_read()
    {
        var config = Of("""{"growth":{"ledger":"sometimes","atlas":3},"workspaceGrowth":{"aurora":"OFF"}}""");

        Assert.Empty(config.Growth);
        Assert.Empty(config.WorkspaceGrowth);
    }

    [Fact]
    public void A_setting_is_kept_as_written_and_a_file_with_none_writes_none()
    {
        var set = GrowthRules.Apply(GrowthRules.Apply(DriverConfig.Empty, Edit(null, "aurora", "off")), Edit("ledger", "aurora", "asked"));

        var written = JsonNode.Parse(set.ToJson())!.AsObject();
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("""{"aurora":"off"}"""), written["workspaceGrowth"]), written.ToJsonString());
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("""{"ledger":"asked"}"""), written["growth"]), written.ToJsonString());
        Assert.Equal("asked", DriverConfig.Parse(set.ToJson()).Growth["ledger"]);
        var none = JsonNode.Parse(DriverConfig.Empty.ToJson())!.AsObject();
        Assert.False(none.ContainsKey("growth"));
        Assert.False(none.ContainsKey("workspaceGrowth"));
    }

    [Fact]
    public void Setting_a_workspace_to_mode_takes_its_key_away_and_writes_nothing()
    {
        var off = GrowthRules.Apply(DriverConfig.Empty, Edit(null, "aurora", "off"));

        var back = GrowthRules.Apply(off, Edit(null, "Aurora", "mode"));

        Assert.Empty(back.WorkspaceGrowth);
        Assert.False(JsonNode.Parse(back.ToJson())!.AsObject().ContainsKey("workspaceGrowth"));
    }

    [Fact]
    public void A_repository_set_to_mode_takes_its_key_away_unless_its_workspace_asks_or_is_off_where_mode_is_what_it_overrides()
    {
        var plain = GrowthRules.Apply(Of("""{"growth":{"ledger":"off"}}"""), Edit("ledger", "aurora", "mode"));
        var underOff = GrowthRules.Apply(Of("""{"workspaceGrowth":{"aurora":"off"},"growth":{"ledger":"asked"}}"""), Edit("ledger", "aurora", "mode"));

        Assert.Empty(plain.Growth);
        Assert.Equal("mode", underOff.Growth["ledger"]);
        Assert.Equal(("mode", "repository"), (GrowthRules.Resolve(underOff, "ledger", "aurora").Value, GrowthRules.Resolve(underOff, "ledger", "aurora").Source));
    }

    [Fact]
    public void An_edit_keeps_the_spelling_a_key_was_first_written_in()
    {
        var config = Of("""{"workspaceGrowth":{"Aurora":"off"}}""");

        var changed = GrowthRules.Apply(config, Edit(null, "aurora", "asked"));

        Assert.Equal(["Aurora"], changed.WorkspaceGrowth.Keys);
        Assert.Equal("asked", changed.WorkspaceGrowth["Aurora"]);
    }

    [Theory]
    [InlineData(null, null, "off", "name the workspace, or a repository and its workspace: the growth switch is set on one of them.")]
    [InlineData(null, "aurora", "never", "`growth` is `mode` (as the access mode says), `asked` (a Confirm card even in full access) or `off` (refused even in full access).")]
    [InlineData("ledger", "aurora", "", "`growth` is `mode` (as the access mode says), `asked` (a Confirm card even in full access) or `off` (refused even in full access).")]
    public void An_edit_that_names_no_place_or_no_value_of_the_three_is_refused_in_its_words(string? repository, string? workspace, string? value, string words)
    {
        Assert.Equal(words, Assert.Throws<DriverException>(() => GrowthRules.Apply(DriverConfig.Empty, Edit(repository, workspace, value))).Message);
    }

    [Fact]
    public void The_pages_edit_names_the_place_and_the_value()
    {
        using var document = JsonDocument.Parse("""{"repository":"ledger","workspace":"aurora","value":"asked"}""");

        var edit = GrowthRules.EditOf(document.RootElement);

        Assert.Equal(("ledger", "aurora", "asked"), (edit.Repository, edit.Workspace, edit.Value));
    }

    [Fact]
    public void A_value_that_is_not_a_string_is_kept_to_be_refused_in_the_words()
    {
        using var document = JsonDocument.Parse("""{"workspace":"aurora","value":3}""");

        var edit = GrowthRules.EditOf(document.RootElement);

        Assert.Throws<DriverException>(() => GrowthRules.Apply(DriverConfig.Empty, edit));
    }

    /// <summary>What an agent reads where the switch is off: the route's words naming the place and where the person turns it back on.</summary>
    [Fact]
    public void What_off_says_names_the_place_and_the_row_that_turns_it_on()
    {
        Assert.Equal("Opening repositories in `aurora` is off (the Settings of the workspace `aurora`, row New repositories and rooms).", GrowthRules.OffOpening("aurora"));
        Assert.Equal("Opening rooms in `ledger` is off (the Settings of `ledger`, row New repositories and rooms).", GrowthRules.OffRoom("ledger"));
    }
}
