namespace Daoris.Driver;

/// <summary>A slot's ignored tops sorted by each effective rule's <c>take</c>, a top holding a recorded copy by its <see cref="TopPartition"/>.</summary>
internal sealed record SlotIgnored(
    IReadOnlyList<string> Kept, IReadOnlyList<string> Outputs, IReadOnlyList<string> Intermediates, IReadOnlyList<string> Copies, IReadOnlyList<string> Own)
{
    internal static readonly SlotIgnored None = new([], [], [], [], []);

    internal IReadOnlyList<string> Based { get; init; } = [];

    internal IReadOnlyList<string> Pooled => [.. Kept, .. Outputs, .. Intermediates, .. Based, .. Copies];

    internal enum Sort
    {
        Kept,
        Output,
        Intermediate,
        Based,
        Copy,
        Own,
    }

    internal static async Task<SlotIgnored> ReadAsync(PoolSite site, string slot, IReadOnlyDictionary<string, string> status, string head, CancellationToken ct)
    {
        var ignored = status.Where(each => each.Value == "!!").Select(each => each.Key).ToList();
        if (ignored.Count == 0) return None;

        var rules = await StoreDependencyRules.ReadAsync(site.Home, slot, site.Repository, head, ct).ConfigureAwait(false);
        var record = TreeFilesRecord.Read(site.Home, slot);
        List<string> kept = [], outputs = [], intermediates = [], based = [], copies = [], own = [];
        foreach (var top in ignored)
        {
            if (TopPartition.Of(record, slot, top) is { } parts)
            {
                copies.AddRange(parts.Copies);
                foreach (var other in parts.Others) Add(other);
            }
            else
            {
                Add(top);
            }
        }

        return new SlotIgnored(kept, outputs, intermediates, copies, own) { Based = based };

        void Add(string path) => (SortOf(rules, record, slot, path) switch
        {
            Sort.Kept => kept,
            Sort.Output => outputs,
            Sort.Intermediate => intermediates,
            Sort.Based => based,
            Sort.Copy => copies,
            _ => own,
        }).Add(path);
    }

    internal static Sort SortOf(StoreDependencies rules, TreeFilesState? record, string slot, string path)
    {
        var file = rules.Rules.For(path, rules.Floor);
        var take = file.Rule is { } rule ? FileKindSets.TakeOf(rule) : "own";
        return (file.Kind, take) switch
        {
            (FileKind.Dependency, "keep") => record?.Of(path) is { Installing: false } || IsCompanion(record, path) ? Sort.Kept : Sort.Own,
            (_, "keep") => Sort.Kept,
            (_, "renew") => Sort.Intermediate,
            (_, "aside") => Sort.Output,
            (_, "base") => Sort.Based,
            (FileKind.Local, _) when TreeCopies.IsOwn(record, slot, path) => Sort.Copy,
            _ => Sort.Own,
        };
    }

    private static bool IsCompanion(TreeFilesState? record, string top) =>
        record is not null && record.Placements.Any(each => each.Companions.Any(companion => string.Equals(companion.Path, top, StringComparison.Ordinal)));
}
