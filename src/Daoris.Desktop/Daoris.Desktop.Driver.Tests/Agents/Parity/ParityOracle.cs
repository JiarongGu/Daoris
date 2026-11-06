using Xunit.Sdk;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The oracle (the agent-lyntai design §6.3): what the Daoris path and the layer path each observed of one scenario, compared channel by
/// channel and entry by entry in order, every field exactly but the projection's, which go by their correspondence; the writes' order across
/// the transcript, the events, the conversation and the records compared too. Each provenance pair the paths produced must be one the
/// door's relation permits and the one the scenario expects, each path's value its own expected source.
/// </summary>
internal static class ParityOracle
{
    private static readonly ParityChannel[] Writes =
        [ParityChannel.Transcript, ParityChannel.Events, ParityChannel.Conversation, ParityChannel.Records];

    /// <summary>Fails naming every difference the oracle finds.</summary>
    public static void Equal(ParityObservation daoris, ParityObservation layer, ParityRelation relation, IReadOnlyList<ParityPair> expected)
    {
        if (Differences(daoris, layer, relation, expected) is { Count: > 0 } found)
        {
            throw new XunitException($"the two paths differ:{Environment.NewLine}  {string.Join(Environment.NewLine + "  ", found)}");
        }
    }

    public static IReadOnlyList<string> Differences(
        ParityObservation daoris, ParityObservation layer, ParityRelation relation, IReadOnlyList<ParityPair> expected)
    {
        var found = new List<string>();
        var pairs = new List<ParityPair>();
        var ids = (Daoris: new Dictionary<string, int>(StringComparer.Ordinal), Layer: new Dictionary<string, int>(StringComparer.Ordinal));
        var (mine, theirs) = (daoris.Seen, layer.Seen);

        foreach (var channel in Enum.GetValues<ParityChannel>())
        {
            var d = mine.Where(each => each.Channel == channel).ToList();
            var l = theirs.Where(each => each.Channel == channel).ToList();
            for (var at = 0; at < Math.Max(d.Count, l.Count); at++)
            {
                if (at >= d.Count || at >= l.Count)
                {
                    found.Add($"{channel} #{at + 1}: {daoris.Path} saw {(at < d.Count ? d[at] : "nothing")}, {layer.Path} saw {(at < l.Count ? l[at] : "nothing")}");
                    break;
                }

                if (Entry(d[at], l[at], daoris, layer, pairs, ids) is { } difference)
                {
                    found.Add($"{channel} #{at + 1}: {difference}");
                    break;
                }
            }
        }

        var order = (Daoris: Interleaving(mine), Layer: Interleaving(theirs));
        if (!order.Daoris.SequenceEqual(order.Layer))
        {
            found.Add($"the writes' order: {daoris.Path} wrote {string.Join(" ", order.Daoris)}, {layer.Path} wrote {string.Join(" ", order.Layer)}");
        }

        found.AddRange(Provenance(pairs, relation, expected));
        return found;
    }

    /// <summary>
    /// A held case holds alike (§6.3 point 4): both paths fail at the same step with the same missing reader, so a held line hides no
    /// difference.
    /// </summary>
    public static void HeldAlike(Exception? daoris, Exception? layer)
    {
        if (daoris is NotAnsweredYet mine && layer is NotAnsweredYet theirs && mine.Message == theirs.Message) return;
        throw new XunitException(
            $"a held case holds alike on both paths, the same missing reader: the Daoris path {Said(daoris)}, the layer path {Said(layer)}");
    }

    private static string Said(Exception? error) => error is null ? "passed" : $"failed with {error.GetType().Name}: {error.Message}";

    private static string? Entry(
        Observed d, Observed l, ParityObservation daoris, ParityObservation layer, List<ParityPair> pairs,
        (Dictionary<string, int> Daoris, Dictionary<string, int> Layer) ids)
    {
        if (d.What != l.What) return $"{daoris.Path} saw {d}, {layer.Path} saw {l}";
        var names = d.Fields.Select(field => field.Name).Where(name => name != ParityProjection.Clock).ToList();
        if (!names.SequenceEqual(l.Fields.Select(field => field.Name).Where(name => name != ParityProjection.Clock)))
        {
            return $"{daoris.Path} saw {d}, {layer.Path} saw {l}: the fields differ";
        }

        if (d[ParityProjection.Clock] is { } named) return $"{d}: a library clock is named on the layer path alone, never {daoris.Path}'s ({named})";
        if (l[ParityProjection.Clock] is { } clock && !ParityProjection.LibraryClocks.ContainsKey(clock))
        {
            return $"{l}: clock '{clock}' is no library clock the projection names";
        }

        foreach (var name in names)
        {
            var (mine, theirs) = (d[name], l[name]);
            if (!ParityProjection.Fields.TryGetValue(name, out var correspondence))
            {
                if (mine != theirs) return $"{d.What}.{name}: {daoris.Path} '{mine}', {layer.Path} '{theirs}'";
                continue;
            }

            switch (correspondence)
            {
                case ParityCorrespondence.Provenance:
                    pairs.Add(new ParityPair(mine ?? "", theirs ?? ""));
                    break;
                case ParityCorrespondence.GeneratedId when !SameOrdinal(mine, theirs, ids):
                    return $"{d.What}.{name}: {daoris.Path} '{mine}' and {layer.Path} '{theirs}' are not the same id by first appearance";
                case ParityCorrespondence.Time when !SameTime(mine, theirs, daoris.Start, layer.Start, l[ParityProjection.Clock]):
                    return $"{d.What}.{name}: {daoris.Path} {mine ?? "null"}, {layer.Path} {theirs ?? "null"}: neither the same instant nor the same interval from the run's start";
            }
        }

        return null;
    }

    private static bool SameOrdinal(string? mine, string? theirs, (Dictionary<string, int> Daoris, Dictionary<string, int> Layer) ids)
    {
        if (mine is null || theirs is null) return mine == theirs;
        if (!ids.Daoris.TryGetValue(mine, out var d)) ids.Daoris[mine] = d = ids.Daoris.Count;
        if (!ids.Layer.TryGetValue(theirs, out var l)) ids.Layer[theirs] = l = ids.Layer.Count;
        return d == l;
    }

    private static bool SameTime(string? mine, string? theirs, DateTimeOffset daorisStart, DateTimeOffset layerStart, string? clock)
    {
        if (mine is null || theirs is null) return mine == theirs;
        if (!DateTimeOffset.TryParse(mine, out var d) || !DateTimeOffset.TryParse(theirs, out var l)) return false;
        if (d == l) return true;
        var apart = (d - daorisStart) - (l - layerStart);
        return apart == TimeSpan.Zero
               || (clock is not null && ParityProjection.LibraryClocks.TryGetValue(clock, out var held) && apart.Duration() <= held);
    }

    private static List<string> Interleaving(IReadOnlyList<Observed> seen) =>
        [.. seen.Where(each => Writes.Contains(each.Channel)).Select(each => each.Channel.ToString())];

    private static IEnumerable<string> Provenance(List<ParityPair> pairs, ParityRelation relation, IReadOnlyList<ParityPair> expected)
    {
        for (var at = 0; at < Math.Max(pairs.Count, expected.Count); at++)
        {
            var seen = at < pairs.Count ? pairs[at] : null;
            var wanted = at < expected.Count ? expected[at] : null;
            if (seen is null)
            {
                yield return $"provenance #{at + 1}: the scenario expects {wanted}, and the paths produced none";
                yield break;
            }

            if (!relation.Names(seen.Layer)) yield return $"provenance #{at + 1}: the layer's '{seen.Layer}' is in no pair the relation declares";
            else if (!relation.Permits(seen)) yield return $"provenance #{at + 1}: {seen} is no pair the relation declares";
            else if (wanted is null) yield return $"provenance #{at + 1}: {seen} is declared, and the scenario expects none";
            else if (seen.Daoris != wanted.Daoris) yield return $"provenance #{at + 1}: the Daoris path's '{seen.Daoris}' is not its expected '{wanted.Daoris}'";
            else if (seen.Layer != wanted.Layer) yield return $"provenance #{at + 1}: {seen} is declared, and the scenario expects {wanted}";
        }
    }
}
