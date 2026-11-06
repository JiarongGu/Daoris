namespace Daoris.Driver;

/// <summary>
/// A plugin's <c>&lt;plugin&gt;/pull-request</c> step (G8; D241 point 4): the performed step that opens the pull request of the branch the run's
/// <c>push</c> pushed. The step's evidence address is the pull request's, which the runtime writes on the landing record, so the
/// <c>work/state</c> asks of a pull request have the address to ask about and the page has one to show.
/// </summary>
public static class PullRequestSteps
{
    /// <summary>The id a plugin's pull-request step ends in.</summary>
    public const string Id = "pull-request";

    /// <summary>The plugin whose pull-request step <paramref name="kind"/> is, or null where it is any other node.</summary>
    public static string? PluginOf(string kind) =>
        kind.EndsWith("/" + Id, StringComparison.Ordinal) && kind.Length > Id.Length + 1 ? kind[..^(Id.Length + 1)] : null;

    /// <summary>Whether <paramref name="address"/> is an address a pull request is kept by: an absolute <c>http</c> or <c>https</c> one.</summary>
    public static bool IsAddress(string? address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}
