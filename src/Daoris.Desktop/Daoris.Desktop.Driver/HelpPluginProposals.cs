namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's <c>plugin</c> proposal (PLUG9): a plugin that has landed, added from its folder in the
/// checkout of the repository that holds it, or one installed here switched on or off — judged with the
/// catalogue's own reader, as <c>daoris plugin add</c> and Settings → Plugins' switch judge them.
/// </summary>
/// <remarks>
/// <para><b>Making a plugin is work, and installing one is the person's press.</b> A plugin runs on this
/// machine as the person, so the room sends its making to the repository that holds plugins as an ask,
/// and this kind only installs what landed there. The card shows what will run, as the manifest writes
/// it, before Apply.</para>
///
/// <para><b>It adds, and never replaces.</b> An id already installed is refused: replacing one is the
/// terminal's <c>daoris plugin add</c>, which replaces it wholesale (<see cref="PluginInstall"/>).</para>
///
/// <para>🔴 <b>Nothing runs</b> at the proposal, the judgement or the Apply: an add copies a folder and a
/// switch writes a row; the loop starts what a plugin runs at its next look, as it does any plugin.</para>
/// </remarks>
public static partial class HelpProposals
{
    private static HelpPlan Plugin(HelpProposal proposal, HelpMachineFacts facts) => proposal.Door switch
    {
        "add" => PluginAdd(proposal, facts),
        "enable" or "disable" => PluginSwitch(proposal, facts),
        var door => new HelpPlan($"`{door}` is not a plugin's change — `add`, `enable` or `disable`.", "", "", null),
    };

    /// <summary>
    /// An add: the folder resolved inside the checkout it names (or the whole path the person gave), never
    /// in or around Daoris's home, read by the catalogue's own reader as <c>plugin add</c> reads it, and an
    /// id not installed here.
    /// </summary>
    private static HelpPlan PluginAdd(HelpProposal proposal, HelpMachineFacts facts)
    {
        var folder = proposal.Folder?.Trim() ?? "";
        var repository = proposal.Repository?.Trim() is { Length: > 0 } holder ? holder : null;
        static HelpPlan Refused(string why, string terminal = "") => new(why, "", terminal, null);

        if (folder.Length == 0)
        {
            return Refused("an add names the plugin's folder: where it landed, in the checkout of the repository that holds it.");
        }

        string source;
        string from;
        if (repository is not null)
        {
            if (!facts.Repositories.Contains(repository, StringComparer.OrdinalIgnoreCase))
            {
                return Refused($"`{repository}` is not registered on this machine — use a repository's name as Projects lists it.");
            }

            if (!facts.Checkouts.TryGetValue(repository, out var root) || root is not { Length: > 0 })
            {
                return Refused($"`{repository}` has no checkout on this machine, so there is no folder here to add a plugin from.");
            }

            if (Path.IsPathRooted(folder))
            {
                return Refused($"a folder in `{repository}` is written from its checkout's root, like `plugins/quiet-hours`, never as a whole path.");
            }

            var checkout = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(checkout, folder)));
            if (!Inside(source, checkout))
            {
                return Refused($"`{folder}` leaves `{repository}`'s checkout — a plugin is added from a folder inside the repository that holds it.");
            }

            from = $"`{folder}` in `{repository}`";
        }
        else
        {
            if (!Path.IsPathRooted(folder))
            {
                return Refused("name the repository whose checkout holds it, with its folder there — a whole path only when the person gave one.");
            }

            source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
            from = $"`{source}`";
        }

        if (facts.Home is { Length: > 0 } home && PluginInstall.Placement(home, source) is { } misplaced) return Refused(misplaced);
        if (!Directory.Exists(source))
        {
            return Refused(repository is null
                ? $"there is no folder `{source}` on this machine."
                : $"there is no folder `{folder}` in `{repository}`'s checkout.");
        }

        var terminal = $"daoris plugin add {Spelled(source)}";
        var (manifest, refusal) = PluginInstall.Read(source, facts.Reserved);
        if (refusal is not null) return Refused(refusal, terminal);
        var id = manifest!.Id;
        if (facts.Plugins.Plugins.Any(entry => string.Equals(entry.Manifest.Id, id, StringComparison.OrdinalIgnoreCase)))
        {
            return Refused(
                $"plugin `{id}` is already installed on this machine. Ask Daoris adds a plugin and never replaces one: "
                + "`daoris plugin add <folder>` at a terminal replaces it wholesale, keeping what it kept.",
                terminal);
        }

        var view = View(manifest) with { Copied = true };
        return new HelpPlan(null,
            $"Add plugin `{id}`{Titled(manifest)} from {from}, copied into Daoris's home under its id; the driver starts "
            + $"what it runs at its next look. {Runs(view)}",
            terminal, null) { Plugin = view, Source = source };
    }

    /// <summary>A switch: an installed plugin, not already where the switch would put it — the Plugins row's own switch.</summary>
    private static HelpPlan PluginSwitch(HelpProposal proposal, HelpMachineFacts facts)
    {
        var id = proposal.Target?.Trim() ?? "";
        var on = proposal.Door == "enable";
        if (facts.Plugins.Plugins.FirstOrDefault(entry => string.Equals(entry.Manifest.Id, id, StringComparison.OrdinalIgnoreCase))
            is not { } found)
        {
            var installed = facts.Plugins.Plugins.Select(entry => entry.Manifest.Id).ToList();
            return new HelpPlan(
                $"no plugin `{id}` on this machine — " + (installed.Count > 0 ? $"installed here: {Names(installed)}." : "none is installed here."),
                "", $"daoris plugin {proposal.Door} {id}", null);
        }

        var named = found.Manifest.Id;
        var terminal = $"daoris plugin {proposal.Door} {named}";
        if (found.Enabled == on)
        {
            return new HelpPlan($"`{named}` is already {(on ? "on" : "off")}, so there is nothing to switch.", "", terminal, null);
        }

        // What it runs as its manifest writes it: the catalogue's entry has the placeholders expanded, and a
        // refused plugin's declarations removed, where the card shows what the file says.
        var (manifest, _) = PluginCatalog.ReadAsWritten(named, Path.Combine(found.Folder, PluginCatalog.ManifestName));
        var view = View(manifest) with { Problem = found.Problem };
        var describe = on
            ? $"Switch plugin `{named}` on: the driver starts what it runs at its next look."
              + (found.Problem is { } problem ? $" As it stands it contributes nothing: {problem}" : "")
            : $"Switch plugin `{named}` off: it stays installed with what it kept, and the driver stops asking it at its next look.";
        return new HelpPlan(null, $"{describe} {Runs(view)}", terminal, null) { Plugin = view };
    }

    private static HelpPluginView View(PluginManifest manifest) => new(
        manifest.Id, manifest.Name, manifest.Version, manifest.Hooks?.Command, manifest.Hooks?.Points ?? [],
        [.. manifest.Harnesses.Select(harness => new HelpPluginPart(harness.Name, harness.Command))],
        [.. manifest.Servers.Select(server => new HelpPluginPart(server.Name, server.Command))]);

    /// <summary>What the plugin runs, said as its manifest writes it: its process and points, its harnesses, its servers.</summary>
    private static string Runs(HelpPluginView view)
    {
        var said = new List<string>();
        if (view.Command is { Count: > 0 } command)
        {
            said.Add($"It runs `{Line(command)}`, speaking on {string.Join(", ", view.Points.Select(point => $"`{point}`"))}.");
        }

        said.AddRange(view.Harnesses.Select(harness => $"It declares harness `{harness.Name}` (`{Line(harness.Command)}`)."));
        said.AddRange(view.Servers.Select(server => $"It hands every session server `{server.Name}` (`{Line(server.Command)}`)."));
        return said.Count > 0 ? string.Join(" ", said) : "It declares nothing and runs nothing.";
    }

    /// <summary>The name and version a manifest gives beside its id, where it gives either.</summary>
    private static string Titled(PluginManifest manifest)
    {
        var name = manifest.Name != manifest.Id ? manifest.Name : "";
        var titled = string.Join(" ", new[] { name, manifest.Version }.Where(part => part.Length > 0));
        return titled.Length > 0 ? $" ({titled})" : "";
    }

    private static string Line(IEnumerable<string> command) => string.Join(" ", command.Select(Spelled));

    /// <summary>A word as a terminal takes it: quoted where it holds a space.</summary>
    private static string Spelled(string word) => word.Contains(' ') ? $"\"{word}\"" : word;

    private static bool Inside(string path, string folder)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(path, folder, comparison) || path.StartsWith(folder + Path.DirectorySeparatorChar, comparison);
    }
}
