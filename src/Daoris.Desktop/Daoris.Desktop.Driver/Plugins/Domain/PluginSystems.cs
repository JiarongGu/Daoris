using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What a system's writes are guarded by, as its adapter declares it (design §5.4).</summary>
public static class SystemPreconditions
{
    /// <summary>Each resource's etag, sent as an <c>If-Match</c>.</summary>
    public const string Etag = "etag";

    /// <summary>A state serial the whole system carries.</summary>
    public const string Serial = "serial";

    /// <summary>A change set the system itself executes whole or not at all.</summary>
    public const string ChangeSet = "changeset";

    /// <summary>Nothing of the system's own: the lease and a read-back alone.</summary>
    public const string None = "none";

    public static readonly IReadOnlyList<string> All = [Etag, Serial, ChangeSet, None];
}

/// <summary>
/// A system an adapter plugin declares: a place reached only through its console, which a <c>system</c> department names by this
/// plugin's id and <paramref name="Id"/>.
/// </summary>
/// <param name="Id">Its id within the plugin: lowercase letters, digits and dashes.</param>
/// <param name="Name">Its name as the manifest writes it; its id where it writes none.</param>
/// <param name="Preconditions">What its writes are guarded by (<see cref="SystemPreconditions"/>); <c>etag</c> where unstated.</param>
public sealed record PluginSystem(string Id, string Name, string Preconditions);

/// <summary>A manifest's <c>systems</c> as read: the systems, or the first problem, which refuses the plugin.</summary>
public sealed record PluginSystemsRead(IReadOnlyList<PluginSystem> Systems, string? Problem);

/// <summary>
/// Reads a manifest's <c>systems</c>: the systems a plugin adapts, each planned at <see cref="HookPoints.PlanRead"/> and applied at
/// <see cref="HookPoints.ApplyPerform"/> on its process, so the manifest's hooks must name both.
/// </summary>
public static class PluginSystems
{
    /// <summary>The manifest's <c>systems</c> from its <paramref name="root"/> object: absent is none.</summary>
    public static PluginSystemsRead Read(JsonElement root)
    {
        if (!root.TryGetProperty("systems", out var declared)) return new([], null);
        if (declared.ValueKind != JsonValueKind.Array) return Refused("`systems` must be an array of the systems the plugin adapts.");
        var systems = new List<PluginSystem>();
        foreach (var row in declared.EnumerateArray())
        {
            var id = row.ValueKind == JsonValueKind.Object && row.TryGetProperty("id", out var named) && named.ValueKind == JsonValueKind.String
                ? named.GetString()!
                : null;
            if (id is null || !PluginSteps.NameShape.IsMatch(id)) return Refused("a declared system needs an `id` — lowercase letters, digits and dashes, at most 40.");
            if (systems.Any(system => system.Id == id)) return Refused($"system `{id}` is declared twice.");
            var preconditions = SystemPreconditions.Etag;
            if (row.TryGetProperty("preconditions", out var guarded))
            {
                if (guarded.ValueKind != JsonValueKind.String || !SystemPreconditions.All.Contains(guarded.GetString(), StringComparer.Ordinal))
                {
                    return Refused($"system `{id}`'s `preconditions` must be `etag`, `serial`, `changeset` or `none`: what each write is guarded by.");
                }

                preconditions = guarded.GetString()!;
            }

            if (!Names(root, HookPoints.PlanRead) || !Names(root, HookPoints.ApplyPerform))
            {
                return Refused($"system `{id}` is planned at `{HookPoints.PlanRead}` and applied at `{HookPoints.ApplyPerform}`, so the manifest's `hooks.points` must name both.");
            }

            var name = row.TryGetProperty("name", out var titled) && titled.ValueKind == JsonValueKind.String && titled.GetString() is { Length: > 0 } text ? text : id;
            systems.Add(new PluginSystem(id, name, preconditions));
        }

        return new(systems, null);
    }

    private static bool Names(JsonElement root, string point) =>
        root.TryGetProperty("hooks", out var hooks) && hooks.ValueKind == JsonValueKind.Object
        && hooks.TryGetProperty("points", out var points) && points.ValueKind == JsonValueKind.Array
        && points.EnumerateArray().Any(each => each.ValueKind == JsonValueKind.String && each.GetString() == point);

    private static PluginSystemsRead Refused(string problem) => new([], problem);
}
