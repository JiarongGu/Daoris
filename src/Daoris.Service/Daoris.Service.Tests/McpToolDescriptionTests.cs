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

    /// <summary>
    /// XAGENT1c (D155 points 5, 8 and 9): an agent is never the person. A second opinion has two tools, the reviewer's and the
    /// working session's, and no tool asks for one, hands one on, or answers a dispute (*Go on anyway…*, *I looked myself…*);
    /// each says the findings are claims, not facts, and that the person sees them.
    /// </summary>
    [Fact]
    public void No_tool_settles_a_second_opinion_and_both_say_its_findings_are_claims()
    {
        var tools = typeof(KnowledgeTools).GetMethods()
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name)
            .OfType<string>()
            .ToList();

        Assert.Equal(["opinion_answer", "opinion_give"], tools.Where(name => name.Contains("opinion")).Order());
        Assert.DoesNotContain(tools, name => name.Contains("anyway") || name.Contains("myself") || name.Contains("settle")
                                             || name.Contains("dispute") || name.Contains("reviewer"));
        foreach (var tool in new[] { "opinion_give", "opinion_answer" })
        {
            var text = string.Join(" ", Descriptions().Where(d => d.Tool == tool).Select(d => d.Text));
            Assert.Contains("claims", text);
            Assert.Contains("person sees", text);
        }
    }

    /// <summary>
    /// EVID1a (D144 §1, §3): the session a requirement will judge never writes its verdict — the driver reads the commit and
    /// posts it to a local door no connector tool reaches. What the session is told is to commit what its evidence names,
    /// since Daoris reads its last commit when it ends; and the publish says a requirement may name its evidence.
    /// </summary>
    [Fact]
    public void No_tool_records_evidence_and_the_tools_say_what_is_read()
    {
        var tools = typeof(KnowledgeTools).GetMethods()
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name)
            .OfType<string>()
            .ToList();
        var respond = string.Join(" ", Descriptions().Where(d => d.Tool == "quest_respond").Select(d => d.Text));
        var publish = string.Join(" ", Descriptions().Where(d => d.Tool == "quest_publish").Select(d => d.Text));

        Assert.DoesNotContain(tools, name => name.Contains("evidence") || name.Contains("verdict"));
        Assert.Contains("evidence", respond);
        Assert.Contains("last commit", respond);
        Assert.Contains("evidence", publish);
    }

    /// <summary>
    /// ORIENT2e (the orientation design §3.1–§3.2): the search says it finds where things are and its kinds name
    /// the index, and the reader takes the lines a hit names, so a session asks for the range and not the file.
    /// </summary>
    [Fact]
    public void The_search_names_the_index_and_the_reader_takes_the_lines_a_hit_names()
    {
        var search = Descriptions().Where(d => d.Tool == "knowledge_search").Select(d => d.Text).ToList();
        var get = Descriptions().Where(d => d.Tool == "knowledge_get").Select(d => d.Text).ToList();

        Assert.Contains(search, text => text.Contains("where things are"));
        Assert.Contains(search, text => text.StartsWith("Restrict to kinds:") && text.Contains("index"));
        Assert.Contains(search, text => text.Contains("lines"));
        Assert.Contains(get, text => text.Contains("lines") && text.Contains("12-30"));
        Assert.Contains(
            typeof(KnowledgeTools).GetMethod(nameof(KnowledgeTools.GetAsync))!.GetParameters(),
            parameter => parameter.Name == "lines" && parameter.HasDefaultValue);
    }

    /// <summary>A server over one checkout says a reading may be a hit's lines, not only an entry whole.</summary>
    [Fact]
    public void A_server_over_one_checkout_says_the_reader_takes_a_hit_s_lines()
    {
        var options = new Daoris.Knowledge.ServiceOptions("root", "db", Repository: Path.Combine(Path.GetTempPath(), "atlas"));

        Assert.Contains("the lines a hit names", KnowledgeTools.Instructions(options, semantic: false));
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
