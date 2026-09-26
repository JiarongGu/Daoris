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

    // Not after `#`: an entry id's example (`…/DECISIONS.md#D12`) is an anchor in the reader's own
    // repository, not a citation of this one's.
    [GeneratedRegex(@"(?<![#\w])D\d{1,3}\b")]
    private static partial Regex DecisionNumber();
}
