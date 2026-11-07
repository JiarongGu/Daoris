using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// A repository's routing standing for the at-work view's hint: <see cref="RoutingStanding.Of"/>
/// over the drive and trees the driver slice holds and the lanes the tick last read from the registry. The registry's readiness is told
/// apart from an answered empty lane list: before the first answer a repository's word is <see cref="AtWorkRouting.Unheard"/>, never
/// <c>no-lanes</c>, and the first answer moves it.
/// </summary>
public sealed class AtWorkStanding(Func<StateSlice?> driver)
{
    private readonly Lock _gate = new();
    private Dictionary<string, IReadOnlyList<string>>? _lanes;

    /// <summary>Whether the registry has answered.</summary>
    public bool Heard
    {
        get
        {
            lock (_gate) return _lanes is not null;
        }
    }

    /// <summary>
    /// The registry answered: its repositories' lanes. Returns whether this changed what the standing reads (the first answer, or any
    /// repository's lanes).
    /// </summary>
    public bool Hear(IEnumerable<RepoView> registry)
    {
        var lanes = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var each in registry) lanes.TryAdd(each.Repository, each.Lanes);
        lock (_gate)
        {
            var same = _lanes is not null && _lanes.Count == lanes.Count
                && lanes.All(each => _lanes.TryGetValue(each.Key, out var was) && was.SequenceEqual(each.Value, StringComparer.Ordinal));
            _lanes = lanes;
            return !same;
        }
    }

    /// <summary>The word for <paramref name="repository"/>: a <see cref="RoutingStanding"/> standing, <c>unheard</c> or <c>unnamed</c>.</summary>
    public string Word(string repository)
    {
        IReadOnlyList<string>? lanes;
        lock (_gate)
        {
            if (_lanes is null) return AtWorkRouting.Unheard;
            lanes = _lanes.GetValueOrDefault(repository);
        }

        if (Config() is not { } config) return AtWorkRouting.Unheard;
        if (lanes is null) return AtWorkRouting.Unnamed;
        return RoutingStanding.Of(config, new RepoView(repository, true, null) { Lanes = lanes }).Standing;
    }

    /// <summary>The drive and trees as the driver slice's choices hold them, or null before the slice has judged.</summary>
    private DriverConfig? Config()
    {
        if (driver() is not { } slice) return null;
        var choices = slice.Snapshot().Partitions.FirstOrDefault(each => each.Partition == "choices")?.Data;
        var node = choices as JsonNode ?? (choices is null ? null : JsonSerializer.SerializeToNode(choices, IpcJson.Options));
        if (node is not JsonObject fields || fields["drivable"] is not JsonArray drivable) return null;
        return DriverConfig.Empty with { Drivable = Names(drivable), Trees = Names(fields["trees"] as JsonArray) };
    }

    private static string[] Names(JsonArray? names) =>
        [.. (names ?? []).Select(each => each is JsonValue value && value.TryGetValue<string>(out var text) ? text : null).OfType<string>()];
}
