using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// A <c>land</c> node's options (LANDNODE1a; the land node design §2): <c>method</c>, <c>target</c>, <c>pattern</c>, <c>plugin</c>,
/// <c>squash</c> and <c>message</c>. Each is optional here, since a version leaves some open and the bind resolves them; a written one is held
/// to its choices, and the method's own refusals (a pattern on a merge, a plugin beside an integrate, a squash of an integrate) are the
/// declaration's. Where the graph's other nodes must stand is <see cref="NodeGraphs.Problems"/>'s. The person's Accept is a
/// <see cref="NodeKinds.Review"/> before it (LANDNODE1c2), so an <c>accept</c> option is refused: a version 1a to 1h saved with one is held,
/// never landed without the press it named.
/// </summary>
public static class LandNodes
{
    public const string Merge = "merge";
    public const string Branch = "branch";
    public const string Push = "push";
    public const string PullRequest = "pull-request";
    public const string Integrate = "integrate";

    /// <summary>The option LANDNODE1a gave the Accept, refused by name since LANDNODE1c2.</summary>
    public const string AcceptOption = "accept";

    /// <summary>The word a <c>plugin</c> option takes for no plugin.</summary>
    public const string NoPlugin = "none";

    public static readonly IReadOnlyList<string> Methods = [Merge, Branch, Push, PullRequest, Integrate];

    /// <summary>What a commit message template may say: the landing's own names, frozen before the landing.</summary>
    public static readonly IReadOnlyList<string> MessagePlaceholders = ["title", "quest", "ticket", "session"];

    private static readonly Regex Placeholder = new(@"\{([^{}]*)\}", RegexOptions.CultureInvariant);

    /// <summary>The <c>land</c> option that is a ticket's key, and the name it takes among a run's resolved options.</summary>
    public const string KeyOption = "key";

    /// <summary>The branch a landing names where its <c>pattern</c> is left open (the cleanup design §1.4): not a rule's, the default.</summary>
    public const string DefaultPattern = "feature/{quest}-{slug}";

    /// <summary>No option resolved: one instance, so a binding with none reads equal to one that never had any.</summary>
    public static readonly IReadOnlyDictionary<string, string> NoResolved = new Dictionary<string, string>();

    /// <summary>
    /// The options a version's <paramref name="land"/> leaves open, resolved from derived defaults and never from a rule (WORKFLOWCLEAN1 1a; the
    /// cleanup design §1.4): the <see cref="DefaultPattern"/>, the plugin that serves the remote's platform (<paramref name="platformPlugin"/>,
    /// null where none does), and the default key where the pattern says <c>{ticket}</c>. A merge has none to resolve, and an integrate no
    /// plugin. Called once, at the run's bind, and kept; a written option stands.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Resolve(NodeDeclaration land, string? platformPlugin)
    {
        var method = Written(land, "method");
        if (method is null or Merge) return NoResolved;
        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
        var pattern = Written(land, "pattern");
        if (pattern is null) resolved["pattern"] = pattern = DefaultPattern;
        if (method != Integrate && Written(land, "plugin") is null && platformPlugin is not null && method is PullRequest or Push)
        {
            resolved["plugin"] = platformPlugin;
        }

        // The key rides with the binding whether written or defaulted, since the landing reads it from there.
        if (pattern.Contains("{ticket}", StringComparison.Ordinal)) resolved[KeyOption] = Written(land, KeyOption) ?? BranchPatterns.DefaultKey;
        return resolved.Count == 0 ? NoResolved : resolved;
    }

    /// <summary>
    /// A <c>land</c> node's options as the landing step they stand for reads them (<c>form</c>, <c>accept</c>, <c>pattern</c>, <c>plugin</c>), the
    /// open ones taken from <paramref name="resolved"/>; an option left out stays left out. JSON texts, as a node's options are. Its
    /// <c>accept</c> is the graph's: <c>you</c> where a <see cref="NodeKinds.Review"/> comes before it (<paramref name="reviewed"/>).
    /// </summary>
    public static IReadOnlyDictionary<string, string> AsLanding(NodeDeclaration land, IReadOnlyDictionary<string, string>? resolved, bool reviewed)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        if (Written(land, "method") is { } method) options["form"] = JsonSerializer.Serialize(method == Merge ? LandingForm.Merge : LandingForm.Branch);
        options[AcceptOption] = JsonSerializer.Serialize(reviewed ? "you" : "automatic");
        foreach (var name in new[] { "pattern", "plugin" })
        {
            if (land.Options.TryGetValue(name, out var written)) options[name] = written;
            else if (resolved is not null && resolved.TryGetValue(name, out var bound)) options[name] = JsonSerializer.Serialize(bound);
        }

        return options;
    }

    /// <summary>
    /// The landing intent a run bound to a version holding this <c>land</c> carries (WORKFLOWCLEAN1 1c; the cleanup design §13 point 3): the node's
    /// written options, the open ones the bind resolved (<paramref name="resolved"/>), and its press from the graph. A squash is the node's own,
    /// and the ticket key is the bind's.
    /// </summary>
    public static Landing LandingOf(NodeDeclaration land, bool reviewed, IReadOnlyDictionary<string, string>? resolved)
    {
        var method = Written(land, "method") ?? Merge;
        if (method == Merge) return new Landing(LandingForm.Merge, AutoAccept: !reviewed);

        var pattern = Written(land, "pattern") ?? resolved?.GetValueOrDefault("pattern");
        var plugin = Written(land, "plugin") switch
        {
            NoPlugin => null,
            null => resolved?.GetValueOrDefault("plugin"),
            var id => id,
        };
        var key = resolved?.GetValueOrDefault(KeyOption);
        var squash = land.Options.TryGetValue("squash", out var written) && NodeOptions.Value(written) is true && method is Branch or Push or PullRequest;
        return new Landing(LandingForm.Branch, pattern, plugin, AutoAccept: !reviewed, key, squash);
    }

    /// <summary>Whether the person's Accept comes before the graph's landing: a <see cref="NodeKinds.Review"/>, which the graph rules hold before every landing.</summary>
    public static bool Reviewed(NodeGraph? graph) => graph?.Nodes.Any(node => node.Kind == NodeKinds.Review) == true;

    /// <summary>The first option that does not hold, by its name, or null.</summary>
    public static string? Problem(NodeDeclaration node)
    {
        var method = Written(node, "method");
        if (node.Options.ContainsKey("method") && (method is null || !Methods.Contains(method, StringComparer.Ordinal))) return "method";
        if (node.Options.ContainsKey(AcceptOption)) return AcceptOption;
        if (node.Options.ContainsKey("target") && !(Written(node, "target") is { } target && BranchName.IsValid(target))) return "target";
        // Every method but an integrate lands on the tree's line, so a target there would only misname it; an integrate's must also be the
        // line the repository declares, which the landing's door judges (CurrentDeliveries.StartAsync).
        if (node.Options.ContainsKey("target") && method is not null && method != Integrate) return "target";

        if (node.Options.ContainsKey("pattern"))
        {
            // A merge makes no branch to name.
            if (method == Merge || Written(node, "pattern") is not { } pattern || BranchPatterns.Problem(pattern) is not null) return "pattern";
        }

        // A pull request and a push are a plugin's to do; an integrate is the line's, and no plugin lands it (D87, D215).
        var plugin = Written(node, "plugin");
        if (node.Options.ContainsKey("plugin") && (plugin is null || (plugin != NoPlugin && !PluginIds.IsId(plugin)))) return "plugin";
        var named = plugin is not null && plugin != NoPlugin;
        // A plugin left open is the repository's, resolved at the bind (LANDNODE1h), which refuses a run whose rule names none; `none` is refused here.
        if (method is Push or PullRequest && plugin == NoPlugin) return "plugin";
        if (method is Merge or Integrate && named) return "plugin";

        // The ticket's key (WORKFLOWCLEAN1 1a): read only for a pattern that says {ticket}, so a merge, a pattern without it and an
        // integrate (which names no branch by a pattern) refuse one; its shape is the landing key's to judge.
        if (node.Options.ContainsKey(KeyOption))
        {
            if (Written(node, KeyOption) is not { } key || BranchPatterns.KeyProblem(key) is not null) return KeyOption;
            if (method == Merge || (Written(node, "pattern") is { } written && !written.Contains("{ticket}", StringComparison.Ordinal))) return KeyOption;
        }

        if (node.Options.TryGetValue("squash", out var squash))
        {
            if (NodeOptions.Value(squash) is not bool on) return "squash";
            if (on && method == Integrate) return "squash";
        }

        if (node.Options.ContainsKey("message") && !(Written(node, "message") is { } message && MessageProblem(message) is null)) return "message";
        return null;
    }

    /// <summary>The <c>method</c> and <c>pattern</c> a graph's <c>land</c> node writes, null for one it leaves open or a graph with none.</summary>
    public static (string? Method, string? Pattern) WrittenLanding(NodeGraph? graph) =>
        graph?.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.Land) is { } land ? (Written(land, "method"), Written(land, "pattern")) : (null, null);

    /// <summary>An option written as a JSON string, or null: <see cref="NodeOptions.Text"/> reads a number by its text, which no option here takes.</summary>
    private static string? Written(NodeDeclaration node, string name) =>
        node.Options.TryGetValue(name, out var raw) && raw.TrimStart().StartsWith('"') ? NodeOptions.Value(raw) as string : null;

    /// <summary>
    /// A <c>message</c> template rendered once (LANDNODE1f; the land node design §2), for the delivery's intent to freeze before the landing: each
    /// placeholder replaced in one pass, so a title that says <c>{quest}</c> stays as written. The title is taken onto one line; a ticket the
    /// subject has none of is empty. Null where the template is not one <see cref="MessageProblem"/> accepts, which the node's judgement refused.
    /// </summary>
    public static string? RenderMessage(string template, string? title, string? quest, string? ticket, string session)
    {
        if (MessageProblem(template) is not null) return null;
        return Placeholder.Replace(template, used => used.Groups[1].Value switch
        {
            "title" => TextBounds.Fold(title),
            "quest" => quest ?? "",
            "ticket" => ticket ?? "",
            _ => session,
        });
    }

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
