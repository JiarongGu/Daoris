namespace Daoris.Driver;

/// <summary>
/// The tops of a take's plan inside which the start tracks a path, listed again after the switch: git's ignored entries inside each,
/// sorted one by one by the plan's rules, so a tracked file the switch wrote stays and only the previous line's entries beside it move.
/// An entry whose rule keeps it stays; an intermediate moves only where the toolchain moved; every other, a <c>base</c> among them, moves aside.
/// </summary>
internal static class PlanRelisting
{
    internal static async Task<IReadOnlyList<string>> MovingAsync(string home, string slot, TakePlan plan, bool toolchainMoved, CancellationToken ct)
    {
        if (plan.Relisted.Count == 0 || plan.Rules is not { } rules) return [];
        var ignored = await MachinePorts.Repository.IgnoredAsync(slot, plan.Relisted, ct).ConfigureAwait(false);
        var record = TreeFilesRecord.Read(home, slot);
        var moving = new List<string>();
        foreach (var path in ignored)
        {
            switch (SlotIgnored.SortOf(rules, record, slot, path))
            {
                case SlotIgnored.Sort.Kept:
                    break;
                case SlotIgnored.Sort.Intermediate when !toolchainMoved:
                    break;
                default:
                    moving.Add(path);
                    break;
            }
        }

        return moving;
    }
}
