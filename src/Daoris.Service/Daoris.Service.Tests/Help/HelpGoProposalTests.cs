using System.Text.Json;

namespace Daoris.Service.Tests;

/// <summary>The <c>go</c> kind's writer (HELP6): a screen to open — a view, a Settings domain, a part of it or a setup step.</summary>
public sealed class HelpGoProposalTests : HelpProposalBoxFixture
{
    [Fact]
    public void A_go_is_written_with_the_place()
    {
        var (id, _) = Box().ProposeGo("Settings", "start", "helper", null, "the person asked where to name its agent", "h1", Now);
        var (view, _) = Box().ProposeGo("quests", null, null, null, "the person asked where asks are", "h1", Now);

        var file = Written(id!);
        Assert.Equal(("go", "go", "settings"), (file.GetProperty("kind").GetString(), file.GetProperty("door").GetString(), file.GetProperty("target").GetString()));
        Assert.Equal(("start", "helper"), (file.GetProperty("domain").GetString(), file.GetProperty("part").GetString()));
        Assert.Equal(JsonValueKind.Null, Written(view!).GetProperty("domain").ValueKind);
        Assert.Equal(JsonValueKind.Null, Written(view!).GetProperty("item").ValueKind);
    }

    /// <summary>
    /// ENTRY1f1 (D161's ENTRY1f note): a go may name one quest, by its id, or one ask, as <c>ask:&lt;id&gt;</c>. The box
    /// writes it as named, its spelling kept; whether the machine holds it, and where it may be named, is the driver's to judge.
    /// </summary>
    [Fact]
    public void A_go_naming_one_quest_or_ask_is_written_with_its_item()
    {
        var (quest, _) = Box().ProposeGo("quests", null, null, " #q1a2b3c4 ", "the person asked where their quest is", "h1", Now);
        var (ask, _) = Box().ProposeGo("Quests", null, null, "ask:A1b2c3d4", "the person asked what became of their ask", "h1", Now);

        Assert.Equal("#q1a2b3c4", Written(quest!).GetProperty("item").GetString());
        Assert.Equal(("quests", "ask:A1b2c3d4"), (Written(ask!).GetProperty("target").GetString(), Written(ask!).GetProperty("item").GetString()));
    }

    /// <summary>
    /// ENTRY1d2a (D161's ENTRY1d note): a go to Repositories' Add repository or Import a folder may carry the workspace the
    /// drawer opens with, written trimmed and not held to one word, since the drawer's free text takes any name. Where it may
    /// be named, and whether a repository is in it yet, is the driver's to judge; a go with none writes none.
    /// </summary>
    [Fact]
    public void A_go_to_add_or_import_is_written_with_its_workspace()
    {
        var (add, _) = Box().ProposeGo("projects", null, "add", null, "the person asked to add a repository to it", "h1", Now, workspace: " Team Alpha ");
        var (import, _) = Box().ProposeGo("projects", null, "import", null, "the person asked to import a folder into it", "h1", Now, workspace: "work");
        var (none, _) = Box().ProposeGo("projects", null, "add", null, "the person asked to add a repository", "h1", Now);

        Assert.Equal(("add", "Team Alpha"), (Written(add!).GetProperty("part").GetString(), Written(add!).GetProperty("workspace").GetString()));
        Assert.Equal("work", Written(import!).GetProperty("workspace").GetString());
        Assert.Equal(JsonValueKind.Null, Written(none!).GetProperty("workspace").ValueKind);
    }

    /// <summary>UX6i2b: the texts list the places the frame has (D150's UX6i2a note), never the ones it retired.</summary>
    [Fact]
    public void The_texts_name_the_places_the_window_has()
    {
        var method = typeof(Daoris.Knowledge.Mcp.KnowledgeTools).GetMethod(nameof(Daoris.Knowledge.Mcp.KnowledgeTools.ProposeGo))!;
        string Of(string name) => method.GetParameters().Single(parameter => parameter.Name == name)
            .GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>().Single().Description;
        var (_, refusal) = Box().ProposeGo("", null, null, null, "a reason", "h1", Now);

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
        // ENTRY1f1: the item names one quest or ask, under quests, and the part says where the one is named instead.
        var item = Of("item");
        Assert.Contains("quests", item);
        Assert.Contains("ask:<id>", item);
        Assert.Contains("no part", item);
        Assert.Contains("item", part);
        // ENTRY1f2: and one session under sessions, by its id, never Ask Daoris's own conversation.
        Assert.Contains("For sessions", item);
        Assert.Contains("a session by its id", item);
        Assert.Contains("Ask Daoris's own", item);
        var tool = method.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>().Single().Description;
        Assert.Contains("one session on Sessions", tool);
        // ENTRY1d2a: Add and Import open with a workspace filled, and the folder stays the person's pick.
        var workspace = Of("workspace");
        Assert.Contains("add or import", workspace);
        Assert.Contains("a new name", workspace);
        Assert.Contains("never a folder", workspace);
        Assert.Contains("may carry the workspace", part);
        Assert.Contains("Add repository or Import a folder", tool);
    }

    /// <summary>The shape, checked here and nothing more; which places exist is the driver's to judge.</summary>
    [Theory]
    [InlineData("||", "names the view")]
    [InlineData("quests|agents|", "domain is a part of Settings")]
    [InlineData("settings|work space||", "one word")]
    // ENTRY1f1: an item is one word, as an id is; which item is the driver's to judge.
    [InlineData("quests|||q1 a2", "item is one word")]
    // ENTRY1d2a: a workspace is named, never a folder, so no path the conversation was not given is written (D48 §3/§7).
    [InlineData("projects||add||C:\\work", "never by a folder")]
    [InlineData("projects||import||work/engine", "never by a folder")]
    [InlineData("projects||add||/checkouts/work", "never by a folder")]
    [InlineData("projects||add||D:", "never by a folder")]
    public void A_malformed_go_proposal_is_refused_with_nothing_written(string fields, string says)
    {
        var part = fields.Split('|').Select(field => field.Length == 0 ? null : field).ToArray();

        var (id, message) = Box().ProposeGo(
            part[0] ?? "", part[1], part[2], part.ElementAtOrDefault(3), "a reason", "h1", Now, workspace: part.ElementAtOrDefault(4));

        Assert.Null(id);
        Assert.Contains(says, message);
        Assert.Contains("Nothing was proposed", message);
        Assert.False(Directory.Exists(Path.Combine(_home, "help", "proposals")));
    }
}
