using System.Text.Json;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The <c>role</c> kind's writer (FOUNDATION1y-i, the foundation design §2.7): a change of the role catalogue, its door the change, its
/// target the role, and its own <c>press</c> the record's fields and choices as the driver's role door reads them. Shape alone is checked.
/// </summary>
public sealed class HelpRoleProposalTests : HelpProposalBoxFixture
{
    [Fact]
    public void A_set_is_written_with_its_names_work_scope_choices_and_the_revision_read()
    {
        var (id, message) = Box().ProposeRole(
            "set", " designer ", "Designer", new Dictionary<string, string> { ["zh"] = "设计师" }, ["design"], "workspace:studio", ["claude-code opus high", "codex-acp - -"], 0,
            "the person wants design work on Opus", "h1", Now);

        var written = Written(id!);
        Assert.Equal(("role", "set", "designer"), (written.GetProperty("kind").GetString(), written.GetProperty("door").GetString(), written.GetProperty("target").GetString()));
        var press = written.GetProperty("press");
        Assert.Equal(("Designer", "设计师"), (press.GetProperty("name").GetProperty("en").GetString(), press.GetProperty("name").GetProperty("zh").GetString()));
        Assert.Equal(["en", "zh"], press.GetProperty("name").EnumerateObject().Select(each => each.Name));
        Assert.Equal(["design"], press.GetProperty("takes").EnumerateArray().Select(work => work.GetString()));
        Assert.Equal(("workspace", "studio"), (press.GetProperty("scope").GetProperty("kind").GetString(), press.GetProperty("scope").GetProperty("id").GetString()));
        Assert.Equal("opus", press.GetProperty("choices")[0].GetProperty("model").GetString());
        Assert.Equal(JsonValueKind.Null, press.GetProperty("choices")[1].GetProperty("model").ValueKind);
        Assert.Equal(0, press.GetProperty("revision").GetInt64());
        Assert.Contains($"Proposed `#{id}`", message);
    }

    /// <summary>I18NOPEN1 (D222): a name is one text as written, or any languages by tag; a name needs no English and no Chinese.</summary>
    [Fact]
    public void A_name_is_one_text_or_any_languages_by_tag_and_a_bad_tag_is_refused()
    {
        var (one, _) = Box().ProposeRole("set", "designer", "Designer", null, ["design"], null, null, null, "why", "h1", Now);
        Assert.Equal("Designer", Written(one!).GetProperty("press").GetProperty("name").GetString());

        var (french, _) = Box().ProposeRole("set", "relecteur", null, new Dictionary<string, string> { ["fr"] = "Relecteur" }, ["review"], null, null, null, "why", "h1", Now);
        var name = Written(french!).GetProperty("press").GetProperty("name");
        Assert.Equal(("Relecteur", false), (name.GetProperty("fr").GetString(), name.TryGetProperty("en", out _)));

        var (none, message) = Box().ProposeRole("set", "relecteur", null, new Dictionary<string, string> { ["not a tag"] = "x" }, ["review"], null, null, null, "why", "h1", Now);
        Assert.Null(none);
        Assert.Contains("is not a language tag", message);
    }

    [Fact]
    public void A_removal_names_the_role_alone()
    {
        var (id, _) = Box().ProposeRole("remove", "night", null, null, null, null, null, 3, "the person asked", "h1", Now);

        var written = Written(id!);
        Assert.Equal(("remove", "night", 3L), (written.GetProperty("door").GetString(), written.GetProperty("target").GetString(), written.GetProperty("press").GetProperty("revision").GetInt64()));
    }

    [Fact]
    public void A_restore_of_every_default_names_no_role_and_is_written_with_the_id_the_door_stands_for()
    {
        var (id, message) = Box().ProposeRole("default-all", "*", null, null, null, null, null, null, "the person asked", "h1", Now);

        var written = Written(id!);
        Assert.Equal(("role", "default-all", "*"), (written.GetProperty("kind").GetString(), written.GetProperty("door").GetString(), written.GetProperty("target").GetString()));
        Assert.False(written.GetProperty("press").TryGetProperty("revision", out _));
        Assert.Contains($"Proposed `#{id}`", message);
    }

    [Theory]
    [InlineData("rename", "night", null, null, "a role's change is `set`, `remove`, `default` or `default-all`.")]
    [InlineData("default-all", "night", null, null, "is the id of `default-all` alone")]
    [InlineData("default", "*", null, null, "is the id of `default-all` alone")]
    [InlineData("default-all", "*", "Night", null, "names the role alone")]
    [InlineData("remove", "night", "Night", null, "`remove` names the role alone")]
    [InlineData("set", "night", null, "planet:x", "is not a scope")]
    [InlineData("set", " ", null, null, "names the role's id")]
    public void A_change_of_the_wrong_shape_is_refused_and_nothing_proposed(string change, string role, string? name, string? scope, string said)
    {
        var (id, message) = Box().ProposeRole(change, role, name, null, null, scope, null, null, "why", "h1", Now);

        Assert.Null(id);
        Assert.Contains(said, message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Nothing was proposed.", message);
    }
}
