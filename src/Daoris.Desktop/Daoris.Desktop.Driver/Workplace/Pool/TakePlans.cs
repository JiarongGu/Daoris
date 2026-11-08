namespace Daoris.Driver;

/// <summary>
/// The take's plan, made in the claim after the judgement and before the switch (the tree-pool design §36.3, 1, §37.3, 1, §38.2, 1):
/// the effective rules read once at the take's start, the repository's layer from git's objects, over the ignored tops the judgement
/// listed. A top inside which the start tracks a path is listed again after the switch, never moved whole. A <c>base</c> top beside one
/// project file keeps the folders of its declared configurations as bases, recorded and marked here, and sets every other entry under
/// it aside; one beside none or several is set aside whole, said. A top whose rule at the start is the line's own is the previous
/// line's, and moves aside with the outputs.
/// </summary>
internal static class TakePlans
{
    internal static async Task<TakePlan> WriteAsync(PoolSite site, PoolGit git, string slot, TakeFacts facts, SlotIgnored judged, CancellationToken ct)
    {
        var aside = Path.Combine(PoolClearing.AsideRoot(site), $"{facts.From}-{DateTimeOffset.UtcNow.ToString(TreeSetAside.SetAsideStamp, System.Globalization.CultureInfo.InvariantCulture)}");
        IReadOnlyList<string> tops = [.. judged.Kept, .. judged.Outputs, .. judged.Intermediates, .. judged.Based];
        if (tops.Count == 0) return TakePlan.None with { Aside = aside };

        var rules = await StoreDependencyRules.ReadAsync(site.Home, slot, site.Repository, facts.Start, ct).ConfigureAwait(false);
        var record = TreeFilesRecord.Read(site.Home, slot);
        var tracked = await TrackedAsync(git, slot, facts.Start, tops, ct).ConfigureAwait(false);
        List<string> outputs = [], intermediates = [], relisted = [], said = [];
        List<TreeBase> bases = [];
        foreach (var top in tops)
        {
            if (tracked.Any(path => TreeBases.Under(path, top)))
            {
                relisted.Add(top);
                continue;
            }

            switch (SlotIgnored.SortOf(rules, record, slot, top))
            {
                case SlotIgnored.Sort.Kept:
                    break;
                case SlotIgnored.Sort.Intermediate:
                    intermediates.Add(top);
                    break;
                case SlotIgnored.Sort.Based when top.EndsWith('/'):
                    var producer = await BaseProducers.ReadAsync(git, slot, facts.Start, top, ct).ConfigureAwait(false);
                    if (producer.Project is null)
                    {
                        outputs.Add(top);
                        said.Add(producer.Why!);
                        break;
                    }

                    Divide(slot, top, producer, record, facts with { Aside = aside }, outputs, bases);
                    break;
                default:
                    outputs.Add(top);
                    break;
            }
        }

        TreeBases.Write(site.Home, slot, bases);
        return new TakePlan(outputs, intermediates, relisted, bases, rules) { Aside = aside, Said = said };
    }

    /// <summary>Each declared configuration's folder under the top kept as a base, every other entry set aside.</summary>
    private static void Divide(string slot, string top, BaseProducer producer, TreeFilesState? record, TakeFacts facts, List<string> outputs, List<TreeBase> bases)
    {
        var full = BaseMarker.FolderOf(slot, top);
        if (!Directory.Exists(full)) return;
        foreach (var entry in Directory.EnumerateFileSystemEntries(full).Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileName(entry);
            var folder = Directory.Exists(entry);
            var inward = folder && !File.GetAttributes(entry).HasFlag(FileAttributes.ReparsePoint);
            var path = top + name + (folder ? "/" : "");
            if (!inward || !producer.Configurations.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                outputs.Add(path);
                continue;
            }

            bases.Add(new TreeBase(path, facts.Previous, facts.Head, facts.At, producer.Project!, name)
            {
                Job = facts.Job, Aside = facts.Aside, Was = record?.Bases.FirstOrDefault(each => each.Path == path),
            });
        }
    }

    /// <summary>The paths the start tracks under the tops, one listing of git's objects.</summary>
    private static async Task<IReadOnlyList<string>> TrackedAsync(PoolGit git, string slot, string start, IReadOnlyList<string> tops, CancellationToken ct)
    {
        var (code, listed, error) = await git.GitAsync(slot, ["ls-tree", "-r", "--name-only", "-z", start, "--", .. tops], ct).ConfigureAwait(false);
        if (code != 0) throw new DriverException($"git could not list what the start tracks: {TreeGit.FirstLine(error)}");
        return listed.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }
}

/// <summary>What a take's plan stamps a base with: the start it reads at, the slot's line and HEAD before the switch, the claim's job and the moment.</summary>
internal sealed record TakeFacts(string Start, string From, string Previous, string Head, string Job, DateTimeOffset At)
{
    internal string Aside { get; init; } = "";
}
