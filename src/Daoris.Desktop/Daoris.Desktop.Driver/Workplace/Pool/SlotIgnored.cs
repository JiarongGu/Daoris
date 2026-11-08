namespace Daoris.Driver;

/// <summary>
/// A slot's ignored tops sorted by the effective rules: what it keeps, what a take moves aside, Daoris's own copies, and the line's own.
/// </summary>
internal sealed record SlotIgnored(
    IReadOnlyList<string> Kept, IReadOnlyList<string> Outputs, IReadOnlyList<string> Intermediates, IReadOnlyList<string> Copies, IReadOnlyList<string> Own)
{
    internal static readonly SlotIgnored None = new([], [], [], [], []);

    internal IReadOnlyList<string> Pooled => [.. Kept, .. Outputs, .. Intermediates, .. Copies];

    internal static async Task<SlotIgnored> ReadAsync(PoolSite site, string slot, IReadOnlyDictionary<string, string> status, string head, CancellationToken ct)
    {
        var ignored = status.Where(each => each.Value == "!!").Select(each => each.Key).ToList();
        if (ignored.Count == 0) return None;

        var rules = await StoreDependencyRules.ReadAsync(site.Home, slot, site.Repository, head, ct).ConfigureAwait(false);
        var record = TreeFilesRecord.Read(site.Home, slot);
        List<string> kept = [], outputs = [], intermediates = [], copies = [], own = [];
        foreach (var top in ignored)
        {
            var file = rules.Rules.For(top, rules.Floor);
            var list = file.Kind switch
            {
                FileKind.Dependency when record?.Of(top) is { Installing: false } || IsCompanion(record, top) => kept,
                FileKind.Output => outputs,
                FileKind.Intermediate or FileKind.Cache => intermediates,
                FileKind.Local when TreeCopies.IsOwn(record, slot, top) => copies,
                _ => own,
            };
            list.Add(top);
        }

        return new SlotIgnored(kept, outputs, intermediates, copies, own);
    }

    private static bool IsCompanion(TreeFilesState? record, string top) =>
        record is not null && record.Placements.Any(each => each.Companions.Any(companion => string.Equals(companion.Path, top, StringComparison.Ordinal)));
}
