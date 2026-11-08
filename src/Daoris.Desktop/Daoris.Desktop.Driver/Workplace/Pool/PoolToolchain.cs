namespace Daoris.Driver;

/// <summary>
/// The key of the effective rules' <c>toolchain</c> outputs in a slot at its commit: what each command a dependency, intermediate or cache
/// rule names prints there, hashed together. A take compares it with the key the last let-go wrote, so intermediates built under another
/// toolchain are moved aside and not built over.
/// </summary>
internal static class PoolToolchain
{
    internal static async Task<string> KeyAsync(PoolSite site, string slot, CancellationToken ct)
    {
        var head = await WorkingTree.HeadAsync(slot, ct).ConfigureAwait(false);
        if (head is null) return "";
        var read = await StoreDependencyRules.ReadAsync(site.Home, slot, site.Repository, head, ct).ConfigureAwait(false);
        var lines = new List<string>();
        var asked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rule in read.Rules.Rules.Select(each => each.Rule).Where(rule => rule.Kind is FileKind.Dependency or FileKind.Intermediate or FileKind.Cache))
        {
            foreach (var command in rule.Toolchain ?? [])
            {
                if (!asked.Add(command)) continue;
                var answer = await DependencyCommands.RunAsync(site.Home, slot, command, slot, InstallEnvironment.Of(rule), DependencyCommands.ReadBound, ct).ConfigureAwait(false);
                lines.Add($"{command}\t{answer.Code}\t{DependencyKeys.Hash(DependencyKeys.Tokened(answer.Output, slot))}");
            }
        }

        return lines.Count == 0 ? "" : DependencyKeys.Hash(string.Join("\n", lines.Order(StringComparer.Ordinal)));
    }
}
