using System.Text.Json;
using static Daoris.Driver.HelpProposals;

namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's <c>plugin</c> proposal (PLUG9): a plugin that has landed, added from its folder in the
/// checkout of the repository that holds it, or one installed here switched on or off — judged with the
/// catalogue's own reader, as <c>daoris plugin add</c> and the Plugins place's switch judge them. Its door is
/// <c>add</c>, <c>enable</c>, <c>disable</c> or <c>update</c>; its target the id of a plugin switched or
/// updated; and its own <c>folder</c> and <c>offer</c>, beside the proposal's <c>repository</c>, name what an
/// add copies.
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
///
/// <para><b>Since PLUG9 (c) and (d) (D103)</b> an add may name one of the install's own offers by its id,
/// never a path, and an <c>update</c> replaces an installed plugin from the source it recorded, judged by
/// <see cref="PluginInstall.PlanUpdate"/> and showing what changes before Apply.</para>
/// </remarks>
internal sealed class HelpPluginProposals : IHelpProposalKind
{
    public string Kind => "plugin";

    // PLUG9: adding a plugin that has landed, or switching one; making one is an ask, never this.
    public string Tool => "plugin_propose";

    public IReadOnlyList<string> Doors { get; } = ["add", "enable", "disable", "update"];

    public HelpProposal Read(HelpProposal proposal, JsonElement file) => proposal with
    {
        Folder = Text(file, "folder"),
        Offer = Text(file, "offer"),
    };

    public HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts) => proposal.Door switch
    {
        "add" when proposal.Offer?.Trim() is { Length: > 0 } => OfferAdd(proposal, facts),
        "add" => PluginAdd(proposal, facts),
        "enable" or "disable" => PluginSwitch(proposal, facts),
        "update" => PluginUpdate(proposal, facts),
        var door => new HelpPlan($"`{door}` is not a plugin's change — `add`, `enable`, `disable` or `update`.", "", "", null),
    };

    public async Task<HelpApplied> ApplyAsync(HelpApplying applying, CancellationToken ct)
    {
        var (proposal, plan, doors, id) = (applying.Proposal, applying.Plan, applying.Doors, applying.Id);

        // PLUG9: a copy, a swap or a row, and nothing started — the loop starts what it runs at its next look.
        try
        {
            if (proposal.Door == "add" && proposal.Offer?.Trim() is { Length: > 0 }) doors.AddOffer(plan.Plugin!.Id);
            else if (proposal.Door == "add") doors.AddPlugin(plan.Source!);
            else if (proposal.Door == "update") await doors.UpdatePluginAsync(plan.Plugin!.Id, ct).ConfigureAwait(false);
            else doors.SwitchPlugin(plan.Plugin!.Id, proposal.Door == "enable");
        }
        catch (DriverException error)
        {
            return applying.Settled(false, $"Not applied: `#{id}` (`{plan.Terminal}`) — {error.Message}", error.Message);
        }

        return applying.Settled(true, $"Applied: `#{id}` — {plan.Describe} (`{plan.Terminal}`)", null);
    }

    /// <summary>
    /// An add of one of the install's offers, by its id (PLUG9 d): one the install carries, sound, and not
    /// installed here — <see cref="PluginInstall.AddOffer"/>'s own refusals, said before the card is drawn.
    /// </summary>
    private static HelpPlan OfferAdd(HelpProposal proposal, HelpMachineFacts facts)
    {
        var wanted = proposal.Offer!.Trim();
        var terminal = $"daoris plugin add --offer {wanted}";
        static HelpPlan Refused(string why, string terminal) => new(why, "", terminal, null);
        if (facts.Offers.FirstOrDefault(each => string.Equals(each.Id, wanted, StringComparison.OrdinalIgnoreCase)) is not { } offer)
        {
            return Refused($"this install offers no plugin `{wanted}` — "
                + (facts.Offers.Count > 0 ? $"it offers {Names(facts.Offers.Select(each => each.Id))}." : "it offers none."), terminal);
        }

        terminal = $"daoris plugin add --offer {offer.Id}";
        if (offer.Problem is { } problem) return Refused($"Daoris's own `{offer.Id}` cannot be installed as it stands: {problem}", terminal);
        if (offer.Installed || facts.Plugins.Plugins.Any(entry => string.Equals(entry.Manifest.Id, offer.Id, StringComparison.OrdinalIgnoreCase)))
        {
            return Refused($"plugin `{offer.Id}` is already installed on this machine. Ask Daoris adds a plugin and never replaces "
                + $"one: an update (`daoris plugin update {offer.Id}`) takes the install's newer copy, keeping what it kept.", terminal);
        }

        var view = View(offer.Manifest) with { Copied = true, Needs = offer.Needs };
        var needs = offer.Needs.Count > 0 ? $" It needs: {string.Join("; ", offer.Needs)}" : "";
        return new HelpPlan(null,
            $"Install Daoris's own plugin `{offer.Id}`{Titled(offer.Manifest)}, which this install offers, copied into Daoris's "
            + $"home under its id; the driver starts what it runs at its next look. {Runs(view)}{needs}",
            terminal, null) { Plugin = view };
    }

    /// <summary>
    /// An update of an installed plugin from the source it recorded (PLUG9 c): <see cref="PluginInstall.PlanUpdate"/>'s
    /// judgement, in the words <c>daoris plugin update</c> refuses with, and what changes before Apply.
    /// </summary>
    private static HelpPlan PluginUpdate(HelpProposal proposal, HelpMachineFacts facts)
    {
        var id = proposal.Target?.Trim() ?? "";
        var terminal = $"daoris plugin update {id} --yes";
        if (facts.Home is not { Length: > 0 } home) return new HelpPlan("there is no Daoris home here to update a plugin in.", "", terminal, null);
        var (plan, refusal) = PluginInstall.PlanUpdate(home, id, facts.Reserved, facts.OffersFolder);
        if (plan is null) return new HelpPlan(refusal, "", terminal, null);

        terminal = $"daoris plugin update {plan.Id} --yes";
        var (manifest, _) = PluginCatalog.ReadAsWritten(plan.Id, Path.Combine(plan.From, PluginCatalog.ManifestName));
        var view = View(manifest) with { Replaced = true, Changes = plan.Changes };
        // The folder a plugin came from is a path on this machine, which the conversation is never told.
        var from = plan.Source.Offer is { } offer ? $"the install's own `{offer}`" : "the folder it was added from";
        var changes = plan.Changes.Count > 0
            ? "It changes " + string.Join("; ", plan.Changes.Select(change => $"its {change.What} from {Said(change.Was)} to {Said(change.Now)}")) + "."
            : "What it declares does not change; its files are replaced.";
        return new HelpPlan(null,
            $"Update plugin `{plan.Id}` from {from}: its folder is replaced from there, what it kept stays, and the driver starts "
            + $"it again at its next look. {changes} {Runs(view)}",
            terminal, null) { Plugin = view };
    }

    /// <summary>One side of a change, as the card says it: code, or none.</summary>
    private static string Said(string value) => value.Length > 0 ? $"`{value}`" : "none";

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
                return Refused($"`{repository}` is not registered on this machine — use a repository's name as Repositories lists it.");
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
                + $"an update (`daoris plugin update {id}`) takes a newer copy from where it came from, and "
                + "`daoris plugin add <folder>` at a terminal replaces it wholesale; either keeps what it kept.",
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

        said.AddRange(view.Harnesses.Select(harness => $"It declares agent `{harness.Name}` (`{Line(harness.Command)}`)."));
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

/// <summary>
/// What a plugin proposal's card shows (PLUG9): the plugin's id, the command it starts, the points it
/// speaks on, the harnesses it declares and the servers it hands every session, each as its manifest
/// writes it, so the person sees what will run before Apply.
/// </summary>
/// <param name="Command">Its hook process, `${plugin}` as written; null where it speaks on no point.</param>
public sealed record HelpPluginView(
    string Id, string Name, string Version, IReadOnlyList<string>? Command, IReadOnlyList<string> Points,
    IReadOnlyList<HelpPluginPart> Harnesses, IReadOnlyList<HelpPluginPart> Servers)
{
    /// <summary>Whether Apply copies it into the home under its id (an add), rather than switching one installed.</summary>
    public bool Copied { get; init; }

    /// <summary>Why an installed plugin contributes nothing as it stands, in the catalogue's words; null when sound.</summary>
    public string? Problem { get; init; }

    /// <summary>What an offer says it needs on the machine, in its README's words (PLUG9 d); empty for every other plugin.</summary>
    public IReadOnlyList<string> Needs { get; init; } = [];

    /// <summary>What an update changes (PLUG9 c); empty for every other change, and for an update whose declarations stay.</summary>
    public IReadOnlyList<PluginChange> Changes { get; init; } = [];

    /// <summary>Whether Apply replaces the installed folder from its source, keeping what it kept (an update).</summary>
    public bool Replaced { get; init; }
}

/// <summary>A harness a plugin declares, or a server it hands every session: its name, and the command that runs it.</summary>
public sealed record HelpPluginPart(string Name, IReadOnlyList<string> Command);

public sealed partial record HelpProposal
{
    /// <summary>A plugin's folder to add from: from that checkout's root, or a whole path (PLUG9).</summary>
    public string? Folder { get; init; }

    /// <summary>One of the install's own plugins to add, by its id (PLUG9 d) — never a path on this machine.</summary>
    public string? Offer { get; init; }
}

public sealed partial record HelpMachineFacts
{
    /// <summary>The Daoris home, which a plugin is never added from (PLUG9); null where none was named.</summary>
    public string? Home { get; init; }

    /// <summary>Each registered repository's checkout on this machine, or null where it has none (PLUG9).</summary>
    public IReadOnlyDictionary<string, string?> Checkouts { get; init; } = new Dictionary<string, string?>();

    /// <summary>The harness names this build carries, which a plugin may not declare (PLUG9, D64 §5).</summary>
    public IReadOnlyCollection<string> Reserved { get; init; } = [];

    /// <summary>The install's offers folder (PLUG9 d), where an offer is found and an offer's update reads; null is none.</summary>
    public string? OffersFolder { get; init; }

    /// <summary>The install's offers, as the Plugins place lists them under Daoris's own plugins (PLUG9 d).</summary>
    public IReadOnlyList<PluginOffer> Offers { get; init; } = [];
}

public sealed partial record HelpPlan
{
    /// <summary>What a plugin proposal's plugin runs, as its manifest writes it (PLUG9); null for every other kind.</summary>
    public HelpPluginView? Plugin { get; init; }

    /// <summary>The folder a plugin is added from, resolved on this machine (PLUG9); null for every other plan.</summary>
    public string? Source { get; init; }
}

public partial interface IHelpDoors
{
    /// <summary>
    /// <c>daoris plugin add</c>'s copy, the driver's twin (<see cref="PluginInstall.Add"/>, PLUG9): the
    /// folder copied into the home under its manifest's id. 🔴 Nothing the plugin declares is started here.
    /// </summary>
    void AddPlugin(string folder);

    /// <summary><c>PLUGIN_ACTION</c>'s own enable or disable: a row in <c>plugins.json</c> (PLUG9).</summary>
    void SwitchPlugin(string id, bool on);

    /// <summary>
    /// <c>PLUGIN_INSTALL</c>'s own copy of one of the install's offers, by its id (PLUG9 d,
    /// <see cref="PluginInstall.AddOffer"/>). 🔴 Nothing the plugin declares is started here.
    /// </summary>
    void AddOffer(string id);

    /// <summary>
    /// <c>PLUGIN_UPDATE</c>'s own apply (PLUG9 c, <see cref="PluginInstall.Update"/>): the plugin's hook stopped,
    /// its install folder replaced from its source, what it kept untouched; the loop starts it again later.
    /// </summary>
    Task UpdatePluginAsync(string id, CancellationToken ct);
}
