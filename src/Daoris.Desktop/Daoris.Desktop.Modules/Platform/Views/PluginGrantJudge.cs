using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// An approval of what a plugin's server offers Ask Daoris, or its withdrawal, judged as <see cref="HelpGrants"/> takes the press: a sound
/// plugin here, on or off, a server it declares for Ask Daoris, never one naming a generic execution tool, each write accepted one it
/// declares, and a change from what is on file. The card names every tool, its reads and its writes apart.
/// </summary>
internal static class PluginGrantJudge
{
    public static PressViewed View(string home, PluginCatalog catalog, PluginEntry found, string action, string server, IReadOnlyList<string>? writes)
    {
        var named = found.Manifest.Id;
        if (found.Problem is { } problem) return PressViewed.Refused($"plugin `{named}` is not sound as it stands, so its servers reach Ask Daoris not at all: {problem}");
        var offered = found.Manifest.Servers.Where(each => each.HandedTo(PluginServerTo.Help)).ToList();
        if (offered.Count == 0) return PressViewed.Refused($"plugin `{named}` offers Ask Daoris no server: what it serves is handed to sessions only, as its manifest declares.");
        if (offered.FirstOrDefault(each => string.Equals(each.Name, server, StringComparison.Ordinal)) is not { } declared)
        {
            return PressViewed.Refused($"plugin `{named}` offers Ask Daoris no server `{server}` — it offers {PluginNamed.Names(offered.Select(each => each.Name))}.");
        }

        var grant = HelpGrants.Read(home, catalog, withSwitchedOff: true).All
            .FirstOrDefault(each => string.Equals(each.Plugin, named, StringComparison.OrdinalIgnoreCase) && each.Server.Name == declared.Name);
        var facts = PluginNamed.Facts(("id", named), ("server", declared.Name));
        if (action == "help-withdraw")
        {
            if (grant is not ({ State: HelpGrants.Approved } or { Changed: true }))
            {
                return PressViewed.Refused($"`{declared.Name}` of plugin `{named}` is not approved for Ask Daoris, so there is nothing to withdraw.");
            }

            return PressViewed.Shown(new PressView("plugin-withdraw", facts, $"daoris plugins withdraw {named} {declared.Name}")
            {
                Grant = new PressGrant(named, found.Manifest.Name, declared.Name, declared.Allow, declared.Writes, grant.Accepted) { Withdraw = true },
            });
        }

        if (HelpGrants.Refusal(declared) is { } refusal) return PressViewed.Refused($"`{declared.Name}` is not approved: {refusal}");
        if (writes?.FirstOrDefault(tool => !declared.Writes.Contains(tool, StringComparer.Ordinal)) is { } unknown)
        {
            return PressViewed.Refused($"`{unknown}` is not a write `{declared.Name}` declares — it writes outside Daoris with "
                + (declared.Writes.Count > 0 ? PluginNamed.Names(declared.Writes) : "none of its tools") + ".");
        }

        // The declared order, as the approval keeps it.
        var kept = writes is null ? declared.Writes : [.. declared.Writes.Where(tool => writes.Contains(tool, StringComparer.Ordinal))];
        if (grant is { State: HelpGrants.Approved } && grant.Accepted.ToHashSet(StringComparer.Ordinal).SetEquals(kept))
        {
            return PressViewed.Refused($"`{declared.Name}` of plugin `{named}` is already approved for Ask Daoris with exactly these tools, so nothing would change.");
        }

        var flags = writes is null ? "" : kept.Count == 0 ? " --reads-only" : $" --writes {string.Join(",", kept)}";
        return PressViewed.Shown(new PressView("plugin-approve", facts, $"daoris plugins approve {named} {declared.Name}{flags}")
        {
            Grant = new PressGrant(named, found.Manifest.Name, declared.Name, declared.Allow, declared.Writes, kept),
            Also = grant is { Changed: true } ? [new PressSaid("plugin-approved-before")] : null,
        });
    }
}
