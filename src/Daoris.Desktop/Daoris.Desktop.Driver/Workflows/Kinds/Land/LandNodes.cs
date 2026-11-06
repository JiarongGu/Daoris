using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// A <c>land</c> node's options (LANDNODE1a; the land node design §2): <c>method</c>, <c>target</c>, <c>pattern</c>,
/// <c>squash</c> and <c>message</c>, their resolution at the bind and the landing intent a run carries. Each is optional, since a version leaves
/// some open and the bind resolves them; a written one is held to its choices by the declaration (<see cref="LandNode.Problem"/>). The person's
/// Accept is a <see cref="NodeKinds.Review"/> before it (LANDNODE1c2), so an <c>accept</c> option is refused: a version 1a to 1h saved with one
/// is held, never landed without the press it named.
/// </summary>
public static class LandNodes
{
    public const string Merge = "merge";
    public const string Branch = "branch";
    public const string Integrate = "integrate";

    /// <summary>The option LANDNODE1a gave the Accept, refused by name since LANDNODE1c2.</summary>
    public const string AcceptOption = "accept";

    /// <summary>The methods a <c>land</c> takes. A push and a pull request are steps after a <c>branch</c> (G8, D241 point 4).</summary>
    public static readonly IReadOnlyList<string> Methods = [Merge, Branch, Integrate];

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
    /// cleanup design §1.4): the <see cref="DefaultPattern"/> and the default key where the pattern says <c>{ticket}</c>. A merge has none to
    /// resolve. Called once, at the run's bind, and kept; a written option stands.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Resolve(NodeDeclaration land)
    {
        var method = Written(land, "method");
        if (method is null or Merge) return NoResolved;
        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
        var pattern = Written(land, "pattern");
        if (pattern is null) resolved["pattern"] = pattern = DefaultPattern;

        // The key rides with the binding whether written or defaulted, since the landing reads it from there.
        if (pattern.Contains("{ticket}", StringComparison.Ordinal)) resolved[KeyOption] = Written(land, KeyOption) ?? BranchPatterns.DefaultKey;
        return resolved.Count == 0 ? NoResolved : resolved;
    }

    /// <summary>
    /// A <c>land</c> node's options as the landing step they stand for reads them (<c>form</c>, <c>accept</c>, <c>pattern</c>), the
    /// open ones taken from <paramref name="resolved"/>; an option left out stays left out. JSON texts, as a node's options are. Its
    /// <c>accept</c> is the graph's: <c>you</c> where a <see cref="NodeKinds.Review"/> comes before it (<paramref name="reviewed"/>).
    /// </summary>
    public static IReadOnlyDictionary<string, string> AsLanding(NodeDeclaration land, IReadOnlyDictionary<string, string>? resolved, bool reviewed)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        if (Written(land, "method") is { } method) options["form"] = JsonSerializer.Serialize(method == Merge ? LandingForm.Merge : LandingForm.Branch);
        options[AcceptOption] = JsonSerializer.Serialize(reviewed ? "you" : "automatic");
        foreach (var name in new[] { "pattern" })
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
        var key = resolved?.GetValueOrDefault(KeyOption);
        var squash = land.Options.TryGetValue("squash", out var written) && NodeOptions.Value(written) is true && method is Branch;
        return new Landing(LandingForm.Branch, pattern, AutoAccept: !reviewed, key, squash);
    }

    /// <summary>Whether the person's Accept comes before the graph's landing: a <see cref="NodeKinds.Review"/>, which the graph rules hold before every landing.</summary>
    public static bool Reviewed(NodeGraph? graph) => graph?.Nodes.Any(node => node.Kind == NodeKinds.Review) == true;

    /// <summary>The <c>method</c> and <c>pattern</c> a graph's <c>land</c> node writes, null for one it leaves open or a graph with none.</summary>
    public static (string? Method, string? Pattern) WrittenLanding(NodeGraph? graph) =>
        graph?.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.Land) is { } land ? (Written(land, "method"), Written(land, "pattern")) : (null, null);

    /// <summary>An option written as a JSON string, or null: <see cref="NodeOptions.Text"/> reads a number by its text, which no option here takes.</summary>
    internal static string? Written(NodeDeclaration node, string name) =>
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
