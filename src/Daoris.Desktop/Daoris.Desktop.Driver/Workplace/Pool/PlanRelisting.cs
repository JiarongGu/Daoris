namespace Daoris.Driver;

/// <summary>
/// The tops of a take's plan inside which the start tracks a path, listed again after the switch (the tree-pool design §37.3, 1): git's
/// ignored entries inside each, sorted one by one by the plan's rules, so a tracked file the switch wrote stays and only the previous
/// line's entries beside it move. An entry whose rule keeps it stays; an intermediate moves only where the toolchain moved; every other,
/// a <c>base</c> among them since an entry has no producer of its own read, moves aside.
/// </summary>
internal static class PlanRelisting
{
    internal static async Task<IReadOnlyList<string>> MovingAsync(string home, string slot, TakePlan plan, bool toolchainMoved, CancellationToken ct)
    {
        if (plan.Relisted.Count == 0 || plan.Rules is not { } rules) return [];
        var (code, said, error) = await WorkingTree.GitAsync(
            slot, ["--no-optional-locks", "status", "--porcelain", "-z", "--untracked-files=all", "--ignored=matching", "--", .. plan.Relisted], ct).ConfigureAwait(false);
        if (code != 0) throw new DriverException($"git could not list the slot's ignored entries again: {TreeGit.FirstLine(error)}");

        var record = TreeFilesRecord.Read(home, slot);
        var moving = new List<string>();
        foreach (var entry in said.Split('\0', StringSplitOptions.RemoveEmptyEntries).Where(entry => entry.StartsWith("!! ", StringComparison.Ordinal)))
        {
            var path = entry[3..];
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
