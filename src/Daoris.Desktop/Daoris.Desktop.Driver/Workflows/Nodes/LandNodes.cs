using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// A <c>land</c> node's options (LANDNODE1a; the land node design §2): <c>method</c>, <c>accept</c>, <c>target</c>, <c>pattern</c>,
/// <c>plugin</c>, <c>squash</c> and <c>message</c>. Each is optional here, since a version leaves some open and the bind resolves them; a
/// written one is held to its choices, and the method's own refusals (a pattern on a merge, a plugin beside an integrate, a squash of an
/// integrate) are the declaration's. Where the graph's other nodes must stand is <see cref="NodeGraphs.Problems"/>'s.
/// </summary>
public static class LandNodes
{
    public const string Merge = "merge";
    public const string Branch = "branch";
    public const string Push = "push";
    public const string PullRequest = "pull-request";
    public const string Integrate = "integrate";
    public const string You = "you";
    public const string Automatic = "automatic";

    /// <summary>The word a <c>plugin</c> option takes for no plugin.</summary>
    public const string NoPlugin = "none";

    public static readonly IReadOnlyList<string> Methods = [Merge, Branch, Push, PullRequest, Integrate];
    public static readonly IReadOnlyList<string> Accepts = [You, Automatic];

    /// <summary>What a commit message template may say: the landing's own names, frozen before the landing.</summary>
    public static readonly IReadOnlyList<string> MessagePlaceholders = ["title", "quest", "ticket", "session"];

    private static readonly Regex Placeholder = new(@"\{([^{}]*)\}", RegexOptions.CultureInvariant);

    /// <summary>The first option that does not hold, by its name, or null.</summary>
    public static string? Problem(NodeDeclaration node)
    {
        var method = Written(node, "method");
        if (node.Options.ContainsKey("method") && (method is null || !Methods.Contains(method, StringComparer.Ordinal))) return "method";
        if (node.Options.ContainsKey("accept") && !(Written(node, "accept") is { } accept && Accepts.Contains(accept, StringComparer.Ordinal))) return "accept";
        if (node.Options.ContainsKey("target") && !(Written(node, "target") is { } target && BranchName.IsValid(target))) return "target";

        if (node.Options.ContainsKey("pattern"))
        {
            // A merge makes no branch to name.
            if (method == Merge || Written(node, "pattern") is not { } pattern || LandingKey.Problem(pattern) is not null) return "pattern";
        }

        // A pull request and a push are a plugin's to do; an integrate is the line's, and no plugin lands it (D87, D215).
        var plugin = Written(node, "plugin");
        if (node.Options.ContainsKey("plugin") && (plugin is null || (plugin != NoPlugin && !PluginIds.IsId(plugin)))) return "plugin";
        var named = plugin is not null && plugin != NoPlugin;
        if (method is Push or PullRequest && node.Options.ContainsKey("method") && !named) return "plugin";
        if (method is Merge or Integrate && named) return "plugin";

        if (node.Options.TryGetValue("squash", out var squash))
        {
            if (NodeOptions.Value(squash) is not bool on) return "squash";
            if (on && method == Integrate) return "squash";
        }

        if (node.Options.ContainsKey("message") && !(Written(node, "message") is { } message && MessageProblem(message) is null)) return "message";
        return null;
    }

    /// <summary>An option written as a JSON string, or null: <see cref="NodeOptions.Text"/> reads a number by its text, which no option here takes.</summary>
    private static string? Written(NodeDeclaration node, string name) =>
        node.Options.TryGetValue(name, out var raw) && raw.TrimStart().StartsWith('"') ? NodeOptions.Value(raw) as string : null;

    /// <summary>What is wrong with a commit message template, or null: some words, and only the names a landing has.</summary>
    public static string? MessageProblem(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return "a message needs some words.";
        foreach (Match used in Placeholder.Matches(message))
        {
            if (!MessagePlaceholders.Contains(used.Groups[1].Value, StringComparer.Ordinal))
            {
                return $"`{used.Value}` is not something a message can say — " + string.Join(", ", MessagePlaceholders.Select(name => $"`{{{name}}}`")) + ".";
            }
        }

        return null;
    }
}
