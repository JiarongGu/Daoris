using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>What a press that sets a rule for a repository or a whole workspace names, as the registry spells it, or why it cannot be shown.</summary>
internal sealed record PlaceScoped(string? Repository, string? Workspace, string? Refusal)
{
    public bool Circle => Workspace is not null;

    /// <summary>The scope as a <c>daoris place</c> command spells it, for any shell.</summary>
    public string Words => Circle ? $"--workspace {ShellWord.Of(Workspace!, ShellWord.Workspace)}" : ShellWord.Of(Repository!, ShellWord.Name);

    /// <summary>The facts a sentence names: the repository, or the workspace.</summary>
    public Dictionary<string, string> Facts() =>
        new(StringComparer.Ordinal) { [Circle ? "workspace" : "repository"] = Circle ? Workspace! : Repository! };
}

/// <summary>The repository or workspace a press names, judged against the registry as <c>daoris place</c> judges what it is handed.</summary>
internal static class PlaceScope
{
    /// <param name="noun">What is set, in the refusal's words: <c>a line</c>, <c>a session language</c>.</param>
    /// <param name="workspaceToo">Whether a whole workspace may be named, or only a repository.</param>
    public static async Task<PlaceScoped> ReadAsync(
        IPipeGroundWorld world, JsonElement? payload, string noun, bool workspaceToo, CancellationToken cancellationToken)
    {
        var (repository, workspace) = (PressPayload.Text(payload, "repository"), PressPayload.Text(payload, "workspace"));
        if (!workspaceToo && workspace.Length > 0) return Refused($"{noun} is set for a repository — name it, as Repositories lists it.");
        if ((repository.Length > 0) == (workspace.Length > 0))
        {
            return Refused($"{noun} is set for a repository{(workspaceToo ? " or a workspace" : "")} — name {(workspaceToo ? "exactly one" : "it")}.");
        }

        var registry = await world.RegistryAsync(cancellationToken).ConfigureAwait(false);
        if (repository.Length > 0)
        {
            return registry.FirstOrDefault(row => string.Equals(row.Repository, repository, StringComparison.OrdinalIgnoreCase)) is { } found
                ? new PlaceScoped(found.Repository, null, null)
                : Refused($"`{repository}` is not registered on this machine — use a repository's name as Repositories lists it.");
        }

        var names = registry.Select(row => row.Workspace).Where(name => !string.IsNullOrEmpty(name)).Select(name => name!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return names.FirstOrDefault(name => string.Equals(name, workspace, StringComparison.OrdinalIgnoreCase)) is { } named
            ? new PlaceScoped(null, named, null)
            : Refused($"there is no workspace `{workspace}` on this machine — one of {string.Join(", ", names.Select(name => $"`{name}`"))}.");
    }

    /// <summary>The refusal a place's rule makes when a name or two does not fit, said as a view's.</summary>
    public static PressViewed Refusal(PlaceScoped scoped) => PressViewed.Refused(scoped.Refusal!);

    private static PlaceScoped Refused(string why) => new(null, null, why);
}
