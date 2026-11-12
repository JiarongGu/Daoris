using System.Text.Json;

namespace Daoris.Driver;

/// <summary>Where a growth switch stands: its value and where it is set, <see cref="GrowthRules.DefaultSource"/> where nothing is.</summary>
public sealed record ResolvedGrowth(string Value, string Source);

/// <summary>One change to the growth switch, as every door makes it: set a value for a repository, or for a workspace where none is named.</summary>
public sealed record GrowthEdit
{
    public string? Repository { get; init; }

    /// <summary>The workspace a value is set for, or the one a repository's value is set under.</summary>
    public string? Workspace { get; init; }

    /// <summary>One of <see cref="GrowthKey.Values"/>; anything else is refused in the rule's words.</summary>
    public string? Value { get; init; }
}

/// <summary>
/// The growth switch (<c>growth</c> and <c>workspaceGrowth</c> in <c>driver.json</c>, <see cref="GrowthKey"/>): whether an agent's press that opens a
/// department or a room runs as the access mode says, is asked even in full access, or is refused. Resolved, edited and worded here, the one place
/// every door asks: the plans that refuse and carry it, the pipe that asks where it says so, the verb and the page that set it. It is the person's
/// to set and no agent's, in any mode.
/// </summary>
public static class GrowthRules
{
    public const string DefaultSource = "default";
    public const string RepositorySource = "repository";
    public const string WorkspaceSource = "workspace";

    private const string Values =
        "`growth` is `mode` (as the access mode says), `asked` (a Confirm card even in full access) or `off` (refused even in full access).";

    private const string Scope = "name the workspace, or a repository and its workspace: the growth switch is set on one of them.";

    /// <summary>
    /// The value standing for a department opened in <paramref name="workspace"/> (<paramref name="repository"/> null), or for a room in
    /// <paramref name="repository"/>: the repository's own, else its workspace's, else <see cref="GrowthKey.Mode"/> from <see cref="DefaultSource"/>;
    /// names matched in any case.
    /// </summary>
    public static ResolvedGrowth Resolve(DriverConfig config, string? repository, string? workspace)
    {
        if (!string.IsNullOrWhiteSpace(repository) && config.Growth.TryGetValue(repository.Trim(), out var own)) return new(own, RepositorySource);
        var named = RemoteTarget.Workspace(workspace);
        return config.WorkspaceGrowth.TryGetValue(named, out var shared) ? new(shared, WorkspaceSource) : new(GrowthKey.Mode, DefaultSource);
    }

    /// <summary>
    /// One change made to the config, or its refusal thrown in the rule's words, nothing changed. A workspace set to <c>mode</c> loses its key, since
    /// absent is <c>mode</c>; a repository set to <c>mode</c> loses its key too unless its workspace asks or is off, where <c>mode</c> is what it
    /// overrides and is kept. The key keeps the spelling first written.
    /// </summary>
    /// <exception cref="DriverException">No place, or a value that is none of the three.</exception>
    public static DriverConfig Apply(DriverConfig config, GrowthEdit edit)
    {
        var repository = edit.Repository?.Trim() is { Length: > 0 } r ? r : null;
        var workspace = edit.Workspace?.Trim() is { Length: > 0 } w ? w : null;
        if (repository is null && workspace is null) throw new DriverException(Scope);
        if (!GrowthKey.Is(edit.Value)) throw new DriverException(Values);

        var value = edit.Value!;
        var map = repository is not null ? config.Growth : config.WorkspaceGrowth;
        var scope = (repository ?? workspace)!;
        var key = map.Keys.FirstOrDefault(each => string.Equals(each, scope, StringComparison.OrdinalIgnoreCase)) ?? scope;
        var overrides = repository is not null && workspace is not null
            && config.WorkspaceGrowth.TryGetValue(RemoteTarget.Workspace(workspace), out var shared) && shared != GrowthKey.Mode;
        var changed = new Dictionary<string, string>(map, StringComparer.OrdinalIgnoreCase);
        if (value == GrowthKey.Mode && !overrides) changed.Remove(key);
        else changed[key] = value;
        return repository is not null ? config with { Growth = changed } : config with { WorkspaceGrowth = changed };
    }

    /// <summary>An edit as the page's route and the verb spell it: <c>repository</c>, <c>workspace</c> and <c>value</c>; a value that is not a string is kept to be refused.</summary>
    public static GrowthEdit EditOf(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return new GrowthEdit();
        return new GrowthEdit
        {
            Repository = Json.Text(root, "repository"),
            Workspace = Json.Text(root, "workspace"),
            Value = root.TryGetProperty("value", out var value) ? value.ValueKind == JsonValueKind.String ? value.GetString() : "" : null,
        };
    }

    /// <summary>What the terminal says of a value as it stands, and what the page's row explains: what an agent's press that opens a place meets.</summary>
    public static string Says(string value) => value switch
    {
        GrowthKey.Asked => "An agent's press that opens a repository or a room waits for your Confirm in Daoris's window, even in full access.",
        GrowthKey.Off => "An agent's press that opens a repository or a room is refused, even in full access; it works inside what exists.",
        _ => "An agent's press that opens a repository or a room runs as the access mode says: at once in full access, on your Confirm in restricted mode.",
    };

    /// <summary>The refusal of a department opened where the switch is off, naming the workspace and the row that turns it back on.</summary>
    public static string OffOpening(string workspace) =>
        $"Opening repositories in `{workspace}` is off (the Settings of the workspace `{workspace}`, row New repositories and rooms).";

    /// <summary>The refusal of a room asked where the switch is off, naming the repository and the row that turns it back on.</summary>
    public static string OffRoom(string repository) =>
        $"Opening rooms in `{repository}` is off (the Settings of `{repository}`, row New repositories and rooms).";
}
