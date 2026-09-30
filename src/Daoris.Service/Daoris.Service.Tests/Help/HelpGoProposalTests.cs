using System.Text.Json;

namespace Daoris.Service.Tests;

/// <summary>The <c>go</c> kind's writer (HELP6): a screen to open — a view, a Settings domain, a part of it or a setup step.</summary>
public sealed class HelpGoProposalTests : HelpProposalBoxFixture
{
    [Fact]
    public void A_go_is_written_with_the_place()
    {
        var (id, _) = Box().ProposeGo("Settings", "start", "helper", "the person asked where to name its agent", "h1", Now);
        var (view, _) = Box().ProposeGo("quests", null, null, "the person asked where asks are", "h1", Now);

        var file = Written(id!);
        Assert.Equal(("go", "go", "settings"), (file.GetProperty("kind").GetString(), file.GetProperty("door").GetString(), file.GetProperty("target").GetString()));
        Assert.Equal(("start", "helper"), (file.GetProperty("domain").GetString(), file.GetProperty("part").GetString()));
        Assert.Equal(JsonValueKind.Null, Written(view!).GetProperty("domain").ValueKind);
    }

    /// <summary>The shape, checked here and nothing more; which places exist is the driver's to judge.</summary>
    [Theory]
    [InlineData("||", "names the view")]
    [InlineData("quests|agents|", "domain is a part of Settings")]
    [InlineData("settings|work space|", "one word")]
    public void A_malformed_go_proposal_is_refused_with_nothing_written(string fields, string says)
    {
        var part = fields.Split('|').Select(field => field.Length == 0 ? null : field).ToArray();

        var (id, message) = Box().ProposeGo(part[0] ?? "", part[1], part[2], "a reason", "h1", Now);

        Assert.Null(id);
        Assert.Contains(says, message);
        Assert.Contains("Nothing was proposed", message);
        Assert.False(Directory.Exists(Path.Combine(_home, "help", "proposals")));
    }
}
