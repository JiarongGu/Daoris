using System.ComponentModel;
using System.Reflection;
using System.Text.RegularExpressions;
using Daoris.Knowledge.Mcp;
using ModelContextProtocol.Server;

namespace Daoris.Service.Tests;

/// <summary>
/// What a session reads when it chooses a tool. The connector is handed to repositories that know
/// nothing of this one, so its words are the canon's, never this repository's decision numbers — the
/// rule the driven instruction already keeps.
/// </summary>
public sealed partial class McpToolDescriptionTests
{
    private static IEnumerable<(string Tool, string Text)> Descriptions() =>
        typeof(KnowledgeTools).GetMethods()
            .Select(method => (Tool: method.GetCustomAttribute<McpServerToolAttribute>()?.Name,
                Method: method))
            .Where(tool => tool.Tool is not null)
            .SelectMany(tool => new[] { tool.Method.GetCustomAttribute<DescriptionAttribute>()?.Description }
                .Concat(tool.Method.GetParameters().Select(p => p.GetCustomAttribute<DescriptionAttribute>()?.Description))
                .OfType<string>()
                .Select(text => (tool.Tool!, text)));

    [Fact]
    public void No_tool_description_names_a_decision_number()
    {
        var numbered = Descriptions().Where(d => DecisionNumber().IsMatch(d.Text)).ToList();

        Assert.True(numbered.Count == 0, string.Join("\n", numbered.Select(d => $"{d.Tool}: {d.Text}")));
    }

    /// <summary>
    /// Ask and wait: a session that needs another repository is steered to ask it and wait, from each
    /// tool it might otherwise reach for — publishing, answering its own quest, and widening its rules.
    /// </summary>
    [Theory]
    [InlineData("quest_publish")]
    [InlineData("quest_respond")]
    [InlineData("permission_propose")]
    public void The_tools_a_session_reaches_for_say_to_ask_and_wait(string tool)
    {
        var text = string.Join(" ", Descriptions().Where(d => d.Tool == tool).Select(d => d.Text));

        Assert.Contains("wait", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("quest_publish", tool == "quest_publish" ? "quest_publish " + text : text);
    }

    /// <summary>
    /// A server over one checkout says what it reads and that it matches words (ORIENT1c), in the words a
    /// session reads before it searches the files: the checkout's name, the folders it was given, the tier.
    /// </summary>
    [Fact]
    public void A_server_over_one_checkout_says_what_it_reads_and_which_tier_answers()
    {
        var checkout = Path.Combine(Path.GetTempPath(), "atlas");
        var options = new Daoris.Knowledge.ServiceOptions("root", "db", Repository: checkout, Documents: "docs", Index: "docs/index");

        var lexical = KnowledgeTools.Instructions(options, semantic: false);
        var semantic = KnowledgeTools.Instructions(options, semantic: true);

        Assert.NotNull(lexical);
        Assert.Contains("`atlas`", lexical);
        Assert.Contains("`docs/`", lexical);
        Assert.Contains("`docs/index/`", lexical);
        Assert.Contains("knowledge_search", lexical);
        Assert.Contains("words only", lexical);
        Assert.DoesNotContain("words only", semantic!);
        Assert.DoesNotMatch(DecisionNumber(), lexical);
    }

    /// <summary>
    /// HIST1b (D153, D37): removing a record is a person's act, so no connector tool clears history, and none is handed the
    /// desk that would. Ask Daoris may only propose one (HIST1f), as it proposes a delete.
    /// </summary>
    [Fact]
    public void No_tool_clears_history_or_is_handed_what_would()
    {
        var tools = typeof(KnowledgeTools).GetMethods()
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name)
            .OfType<string>()
            .ToList();

        Assert.Contains("quest_publish", tools);
        Assert.DoesNotContain(tools, name => name.Contains("clear") || name.Contains("history") || name.Contains("forget"));
        Assert.DoesNotContain(
            typeof(KnowledgeTools).GetConstructors().SelectMany(constructor => constructor.GetParameters()),
            parameter => parameter.ParameterType == typeof(Daoris.Knowledge.HistoryDesk)
                         || parameter.ParameterType == typeof(Daoris.Knowledge.ComposedService));
    }

    /// <summary>A server over a family of repositories says nothing more than its tools do, as before.</summary>
    [Fact]
    public void A_server_over_a_family_gives_no_instructions()
    {
        Assert.Null(KnowledgeTools.Instructions(new Daoris.Knowledge.ServiceOptions("root", "db"), semantic: false));
    }

    // Not after `#`: an entry id's example (`…/DECISIONS.md#D12`) is an anchor in the reader's own
    // repository, not a citation of this one's.
    [GeneratedRegex(@"(?<![#\w])D\d{1,3}\b")]
    private static partial Regex DecisionNumber();
}
