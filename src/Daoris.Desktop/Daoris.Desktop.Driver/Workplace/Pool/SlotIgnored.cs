namespace Daoris.Driver;

/// <summary>A slot's ignored tops sorted by each effective rule's <c>take</c>, a top holding a copy or a share by its <see cref="TopPartition"/>, a share only while it holds.</summary>
internal sealed record SlotIgnored(
    IReadOnlyList<string> Kept, IReadOnlyList<string> Outputs, IReadOnlyList<string> Intermediates, IReadOnlyList<string> Copies, IReadOnlyList<string> Own)
{
    internal static readonly SlotIgnored None = new([], [], [], [], []);

    internal IReadOnlyList<string> Shared { get; init; } = [];

    internal IReadOnlyList<string> Pooled => [.. Kept, .. Outputs, .. Intermediates, .. Copies, .. Shared];

    internal static async Task<SlotIgnored> ReadAsync(PoolSite site, string slot, IReadOnlyDictionary<string, string> status, string head, CancellationToken ct)
    {
        var ignored = status.Where(each => each.Value == "!!").Select(each => each.Key).ToList();
        if (ignored.Count == 0) return None;

        var rules = await StoreDependencyRules.ReadAsync(site.Home, slot, site.Repository, head, ct).ConfigureAwait(false);
        var holding = TreeShares.Holding(site.Home, slot).Select(share => share.Path).ToHashSet(StringComparer.Ordinal);
        var record = TreeFilesRecord.Read(site.Home, slot);
        var declared = rules.Rules.Rules.Select(each => each.Rule).Where(rule => !rule.FirstForm && FileKindSets.Shares(rule))
            .SelectMany(rule => rule.Paths.Select(pattern => GitPattern.LiteralOf(rule, pattern))).OfType<string>().ToList();
        List<string> kept = [], outputs = [], intermediates = [], copies = [], shared = [], own = [];
        foreach (var top in ignored)
        {
            if (holding.Contains(top) || holding.Contains(top + "/"))
            {
                shared.Add(holding.Contains(top) ? top : top + "/");
            }
            else if (TopPartition.Of(record, slot, top, declared) is { } parts)
            {
                copies.AddRange(parts.Copies);
                shared.AddRange(parts.Shares);
                foreach (var other in parts.Others) Sort(other);
            }
            else
            {
                Sort(top);
            }
        }

        return new SlotIgnored(kept, outputs, intermediates, copies, own) { Shared = shared };

        void Sort(string path)
        {
            var file = rules.Rules.For(path, rules.Floor);
            var take = file.Rule is { } rule ? FileKindSets.TakeOf(rule) : "own";
            var list = (file.Kind, take) switch
            {
                (FileKind.Dependency, "keep") => record?.Of(path) is { Installing: false } || IsCompanion(record, path) ? kept : own,
                (_, "keep") => kept,
                (_, "renew") => intermediates,
                (_, "aside" or "base") => outputs,
                (FileKind.Local, _) when TreeCopies.IsOwn(record, slot, path) => copies,
                _ => own,
            };
            list.Add(path);
        }
    }

    private static bool IsCompanion(TreeFilesState? record, string top) =>
        record is not null && record.Placements.Any(each => each.Companions.Any(companion => string.Equals(companion.Path, top, StringComparison.Ordinal)));
}
