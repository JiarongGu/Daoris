using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A <c>land</c> node's <c>message</c> template rendered once, over <c>{title}</c>, <c>{quest}</c>,
/// <c>{ticket}</c> and <c>{session}</c>, for the delivery's intent to freeze. The commits it lands on are <see cref="LandDeliveryGitTests"/>'s;
/// the refusal of another placeholder, where the node is judged, is <see cref="LandNodeKindTests"/>'s.
/// </summary>
public sealed class LandMessageTests
{
    [Fact]
    public void A_template_renders_each_of_the_landing_s_names()
    {
        Assert.Equal(
            "feat: Tidy the readme (ENG-12) — quest 0fda18, session s1",
            LandNodes.RenderMessage("feat: {title} ({ticket}) — quest {quest}, session {session}", "Tidy the readme", "0fda18", "ENG-12", "s1"));
    }

    /// <summary>One pass: a title that says a placeholder is text, not a second template; a title's line breaks fold; no ticket reads empty.</summary>
    [Fact]
    public void A_rendered_message_is_one_pass_over_the_template()
    {
        Assert.Equal("Fix {quest} the\nrest|", LandNodes.RenderMessage("{title}\nrest|{ticket}", "Fix {quest}\n the", "0fda18", null, "s1"));
    }

    [Theory]
    [InlineData("Land {slug}")]
    [InlineData("   ")]
    public void A_template_the_node_refuses_renders_to_nothing(string template)
    {
        Assert.Null(LandNodes.RenderMessage(template, "Tidy", "q", "T-1", "s1"));
    }
}
