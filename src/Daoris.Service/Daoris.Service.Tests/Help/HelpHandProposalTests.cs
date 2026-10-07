using System.Text.Json;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The <c>hand</c> kind's writer (WSR5b): a branch a landing made, handed to a landing plugin afterwards — named by
/// the session that landed it or the branch, with its repository where a name is in several, and a plugin only where
/// the rule names none. The driver judges it against its record; here only the shape is checked.
/// </summary>
public sealed class HelpHandProposalTests : HelpProposalBoxFixture
{
    [Fact]
    public void A_hand_off_is_written_with_its_session_or_branch_its_repository_and_its_plugin()
    {
        var (bySession, message) = Box().ProposeHand("s2a3b4c5", repository: null, plugin: null, "the person wants it pushed", "h1", Now);
        var (byBranch, _) = Box().ProposeHand(" feature/q2-second ", "engine", "example.lands", "the rule names no plugin yet", "h1", Now);

        Assert.Contains($"#{bySession}", message);
        var first = Written(bySession!);
        Assert.Equal(("hand", "hand", "s2a3b4c5"),
            (first.GetProperty("kind").GetString(), first.GetProperty("door").GetString(), first.GetProperty("target").GetString()));
        Assert.Equal(JsonValueKind.Null, first.GetProperty("value").ValueKind);
        Assert.Equal(JsonValueKind.Null, first.GetProperty("repository").ValueKind);
        var second = Written(byBranch!);
        Assert.Equal(("feature/q2-second", "engine", "example.lands"),
            (second.GetProperty("target").GetString(), second.GetProperty("repository").GetString(), second.GetProperty("value").GetString()));
    }

    /// <summary>WSR5b: a hand-off's shape, checked here and nothing more; the driver's twin table holds what the record says.</summary>
    [Theory]
    [InlineData("", "", "", "names the session that landed the branch, or the branch")]
    [InlineData("two words", "", "", "one word")]
    [InlineData("s2a3b4c5", "two words", "", "one word")]
    [InlineData("s2a3b4c5", "", "Not An Id", "is not a plugin id")]
    [InlineData("s2a3b4c5", "", "../elsewhere", "is not a plugin id")]
    public void A_malformed_hand_off_is_refused_with_nothing_written(string target, string repository, string plugin, string says)
    {
        var (written, message) = Box().ProposeHand(target, Named(repository), Named(plugin), "a reason", "h1", Now);

        Assert.Null(written);
        Assert.Contains(says, message);
        Assert.Contains("Nothing was proposed", message);
        Assert.False(Directory.Exists(Path.Combine(_home, "help", "proposals")));
        Assert.Null(Box().ProposeHand("s2a3b4c5", null, null, " ", "h1", Now).Id);
    }

    /// <summary>
    /// CASEFOLD1f: a plugin id's shape holds to the id's very end, as the driver's <c>PluginCatalog</c> and
    /// <c>LandingRules</c> shapes do since CASEFOLD1e and the CLI's always did. .NET's <c>$</c> also matches before a
    /// final line break, so <c>acme.gate</c> and a line break read as an id.
    /// </summary>
    [Theory]
    [InlineData("acme.gate", true)]
    [InlineData("acme.gate\n", false)]
    [InlineData("acme.gate\n\n", false)]
    [InlineData("\nacme.gate", false)]
    public void A_plugin_id_s_shape_ends_where_the_driver_s_does(string plugin, bool id) =>
        Assert.Equal(id, HelpProposalBox.IsPluginId(plugin));

    /// <summary>
    /// CASEFOLD1f: the door trims a field before its shape is checked, as it trims every field, so a line break after an
    /// id goes with the trim and the id is proposed. No proposal ever reached the shape's end.
    /// </summary>
    [Fact]
    public void A_line_break_after_a_plugin_id_goes_with_the_fields_trim()
    {
        var (id, _) = Box().ProposeHand("s2a3b4c5", null, "acme.gate\n", "a reason", "h1", Now);

        Assert.Equal("acme.gate", Written(id!).GetProperty("value").GetString());
    }
}
