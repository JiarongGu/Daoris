using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// How a build before POOL1a read a scope of <c>harnesses.json</c> (D130 §2 rule 1), kept here when <c>HarnessSettings.ResolveScope</c>
/// left with its last callers in POOL1b (D197): the workspace's scope where it names a default or a list of its own for the agent,
/// else the machine's; a scope's settings read only with its list, its kept account only within it, and it begins at its default
/// where its list holds it, else at its first. The fold reads the raw file by this rule, and an older build reads a folded file so.
/// </summary>
internal static class OlderScope
{
    public static RotationScope Of(HarnessSettings settings, string agent, string? workspace)
    {
        var circle = workspace?.Trim();
        if (!string.IsNullOrEmpty(circle))
        {
            var own = settings.Workspaces.TryGetValue(circle, out var defaults) && defaults.TryGetValue(agent, out var named)
                && !string.IsNullOrWhiteSpace(named) ? named.Trim() : null;
            IReadOnlyList<string> list = settings.WorkspaceRotation.TryGetValue(circle, out var orders) && orders.TryGetValue(agent, out var order)
                ? order : [];
            if (own is not null || list.Count > 0)
            {
                return ScopeOf(ChoiceFrom.Workspace, own, list,
                    settings.WorkspaceUses.TryGetValue(circle, out var uses) ? uses.GetValueOrDefault(agent) : null);
            }
        }

        var machine = settings.Defaults.TryGetValue(agent, out var held) && !string.IsNullOrWhiteSpace(held) ? held.Trim() : null;
        return ScopeOf(ChoiceFrom.Machine, machine, settings.Rotation.TryGetValue(agent, out var mine) ? mine : [], settings.Uses.GetValueOrDefault(agent));
    }

    private static RotationScope ScopeOf(ChoiceFrom from, string? own, IReadOnlyList<string> list, UseEntry? entry)
    {
        if (list.Count == 0) return new RotationScope(from, own, [], own, RotationUse.Default, []);

        var (use, unknown) = RotationUse.Read(entry);
        if (use.Keep is { } keep && !list.Contains(keep, StringComparer.Ordinal)) use = use with { Keep = null };
        var begins = own is not null && list.Contains(own, StringComparer.Ordinal) ? own : list[0];
        return new RotationScope(from, own, list, begins, use, unknown);
    }
}
