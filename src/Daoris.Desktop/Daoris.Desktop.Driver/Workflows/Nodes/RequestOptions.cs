namespace Daoris.Driver;

/// <summary>
/// A <c>request</c> node's options (INTEGRATE1p; the integration design §17.5): <c>to</c>, a repository with lanes as a quest's address takes
/// them; <c>title</c> and <c>body</c>, the person's words kept as written; <c>start</c>, the person's <c>you</c> or the standing
/// <c>automatic</c>; <c>until</c>, the quest's <c>done</c> or its <c>taken</c>; <c>declined</c>, <c>wait</c> for the person or <c>stop</c>.
/// </summary>
public static class RequestOptions
{
    public const string You = "you";
    public const string Automatic = "automatic";
    public const string Done = "done";
    public const string Taken = "taken";
    public const string Wait = "wait";
    public const string Stop = "stop";

    /// <summary>The code a request fails with when its quest was declined.</summary>
    public const string DeclinedCode = "declined";

    public static readonly IReadOnlyList<string> Starts = [You, Automatic];
    public static readonly IReadOnlyList<string> Untils = [Done, Taken];
    public static readonly IReadOnlyList<string> Declines = [Wait, Stop];

    /// <summary>
    /// The first option that holds no request, by its name: <c>to</c>, <c>title</c> and <c>body</c> each a text with words; <c>start</c>,
    /// <c>until</c> and <c>declined</c>, where written, one of their choices. Whether <c>to</c> is a repository this machine holds, and never the
    /// run's own, is judged at the start, where the registry and the run are known.
    /// </summary>
    public static string? Problem(NodeDeclaration node)
    {
        foreach (var name in new[] { "to", "title", "body" })
        {
            if (NodeOptions.Text(node, name) is not { } text || string.IsNullOrWhiteSpace(text)) return name;
        }

        foreach (var (name, choices) in new (string, IReadOnlyList<string>)[] { ("start", Starts), ("until", Untils), ("declined", Declines) })
        {
            if (node.Options.ContainsKey(name) && !(NodeOptions.Text(node, name) is { } text && choices.Contains(text, StringComparer.Ordinal))) return name;
        }

        return null;
    }

    /// <summary>The repository part of a <c>to</c>, before any <c>:lane</c>.</summary>
    public static string Repository(string to) => to.Split(':', 2)[0].Trim();
}
