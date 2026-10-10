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

    /// <summary>UX6i2b: the texts list the places the frame has (D150's UX6i2a note), never the ones it retired.</summary>
    [Fact]
    public void The_texts_name_the_places_the_window_has()
    {
        var method = typeof(Daoris.Knowledge.Mcp.KnowledgeTools).GetMethod(nameof(Daoris.Knowledge.Mcp.KnowledgeTools.ProposeGo))!;
        string Of(string name) => method.GetParameters().Single(parameter => parameter.Name == name)
            .GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>().Single().Description;
        var (_, refusal) = Box().ProposeGo("", null, null, "a reason", "h1", Now);

        foreach (var text in new[] { Of("view"), refusal })
        {
            Assert.Contains("knowledge", text);
            Assert.Contains("agents", text);
            Assert.Contains("plugins", text);
            Assert.DoesNotContain("convergence, search", text);
        }

        var domain = Of("domain");
        Assert.Contains("logs", domain);
        Assert.DoesNotContain("plugins", domain);
        Assert.DoesNotContain("agents", domain);
        Assert.DoesNotContain("workspace", domain);
        Assert.DoesNotContain("permissions", domain);
        var part = Of("part");
        Assert.Contains("search", part);
        Assert.DoesNotContain("lines under workspace", part);
        // UX6g2c: the workspace page's six parts are named under projects.
        foreach (var name in new[] { "workspace-details", "workspace-branches", "workspace-workflow", "workspace-setup", "workspace-defaults", "workspace-remote" })
            Assert.Contains(name, part);
        // ENTRY1h: the four parts ENTRY1b added, and that a part names no session or quest.
        foreach (var name in new[] { "waiting", "review", "asks", "held" })
            Assert.Contains(name, part);
        Assert.Contains("names no session or quest", part);
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
