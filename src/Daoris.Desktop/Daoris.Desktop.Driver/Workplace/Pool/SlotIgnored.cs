namespace Daoris.Driver;

/// <summary>
/// The ignored tops of a slot sorted by the effective rules at its commit: what the slot keeps (a dependency whose placement record
/// holds), what a take moves aside (runnable outputs, and intermediates and caches where the toolchain moved), Daoris's own copies of
/// machine-local files, and the line's own, which nothing here may take.
/// </summary>
internal sealed record SlotIgnored(
    IReadOnlyList<string> Kept, IReadOnlyList<string> Outputs, IReadOnlyList<string> Intermediates, IReadOnlyList<string> Copies, IReadOnlyList<string> Own)
{
    internal static readonly SlotIgnored None = new([], [], [], [], []);

    /// <summary>Every ignored top the rules and the floor leave to the slot or to Daoris, none of the line's own.</summary>
    internal IReadOnlyList<string> Pooled => [.. Kept, .. Outputs, .. Intermediates, .. Copies];

    /// <summary>The tops of <paramref name="status"/>'s ignored paths, sorted at the slot's present commit.</summary>
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
