namespace Daoris.Driver;

/// <summary>
/// The shape of a plugin's <c>pull-request</c> step's kind, <c>&lt;plugin&gt;/pull-request</c>: read by the plugins that take the host's token
/// and by the workflows that name the step, so it lies beneath both (<see cref="PullRequestStep"/> is Daoris's half of the step).
/// </summary>
public static class PullRequestKind
{
    /// <summary>The id a plugin's pull-request step ends in.</summary>
    public const string Id = "pull-request";

    /// <summary>The plugin whose pull-request step <paramref name="kind"/> is, or null where it is any other node.</summary>
    public static string? PluginOf(string kind) =>
        kind.EndsWith("/" + Id, StringComparison.Ordinal) && kind.Length > Id.Length + 1 ? kind[..^(Id.Length + 1)] : null;
}
