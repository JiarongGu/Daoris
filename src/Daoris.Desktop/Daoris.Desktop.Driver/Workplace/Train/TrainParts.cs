namespace Daoris.Driver;

/// <summary>The ask and the part a train's request lands for.</summary>
public sealed record TrainTag(string Request, string Ask, string Part);

/// <summary>
/// Which ask and part each integration request of the train carries, kept beside the request (<c>integration_parts</c>) so the line's one
/// queue stays the only queue: a request tagged here is a part's landing, and one not tagged is any other landing.
/// </summary>
/// <remarks>
/// A part is <i>landed</i> when a member of its request is promoted onto the ask's branch (its batch judged green), never when its quest says
/// done: the worker's completion is not the team's delivery.
/// </remarks>
public sealed class TrainParts(IntegrationStore store)
{
    /// <summary>The tags of the asks' requests, kept under the integration family's write.</summary>
    public void Tag(string request, string ask, string part)
    {
        IntegrationStore.Required(request, "its request");
        IntegrationStore.Required(ask, "its ask");
        IntegrationStore.Required(part, "its part");
        using var writer = store.Writer();
        writer.Write(IntegrationStore.Family, write =>
            write.Execute(
                "INSERT INTO integration_parts (request, ask, part) VALUES ($request, $ask, $part) "
                + "ON CONFLICT (request) DO UPDATE SET ask = excluded.ask, part = excluded.part",
                ("$request", request), ("$ask", ask), ("$part", part)));
    }

    /// <summary>The tag of each request given, those that carry one.</summary>
    public IReadOnlyDictionary<string, TrainTag> Of(IEnumerable<string> requests)
    {
        var wanted = requests.ToHashSet(StringComparer.Ordinal);
        if (wanted.Count == 0) return new Dictionary<string, TrainTag>(StringComparer.Ordinal);
        return Read(
            "SELECT request, ask, part FROM integration_parts", [])
            .Where(tag => wanted.Contains(tag.Request))
            .ToDictionary(tag => tag.Request, StringComparer.Ordinal);
    }

    /// <summary>The parts of <paramref name="ask"/> whose request promoted onto the ask's branch.</summary>
    public IReadOnlySet<string> Landed(string ask) =>
        Read(
            """
            SELECT p.request, p.ask, p.part FROM integration_parts p
            WHERE p.ask = $ask AND EXISTS (
              SELECT 1 FROM integration_members m WHERE m.request = p.request AND m.state IN ($promoted, $settled))
            """,
            [("$ask", ask), ("$promoted", IntegrationMemberStates.Promoted), ("$settled", IntegrationMemberStates.Settled)])
        .Select(tag => tag.Part).ToHashSet(StringComparer.Ordinal);

    /// <summary>Every ask that has a part's request tagged.</summary>
    public IReadOnlyList<string> Asks() =>
        Read("SELECT request, ask, part FROM integration_parts", []).Select(tag => tag.Ask).Distinct(StringComparer.Ordinal).ToList();

    private IReadOnlyList<TrainTag> Read(string sql, (string Name, object? Value)[] values)
    {
        using var reader = DesktopStoreReader.Open(store.Location);
        if (reader is null || reader.Version < DesktopStore.IntegrationPartsVersion) return [];
        return reader.Query(
            sql, row => new TrainTag(row.GetString(0), row.GetString(1), row.GetString(2)), values);
    }
}
