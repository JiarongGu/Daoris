using System.Text.Json;
using static Daoris.Driver.HelpProposals;
using static Daoris.Driver.Json;

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

    // HELPPLUG1c: `approve` and `withdraw`, the person's approval of what a plugin's server offers Ask Daoris, by their press.
    public IReadOnlyList<string> Doors { get; } = ["add", "enable", "disable", "update", "approve", "withdraw"];

    public HelpProposal Read(HelpProposal proposal, JsonElement file) => proposal with
    {
        Folder = Text(file, "folder"),
        Offer = Text(file, "offer"),
        Server = Text(file, "server"),
        Writes = file.TryGetProperty("writes", out var writes) && writes.ValueKind == JsonValueKind.Array
            ? [.. writes.EnumerateArray().Where(tool => tool.ValueKind == JsonValueKind.String).Select(tool => tool.GetString()!)]
            : null,
    };

    public HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts) => proposal.Door switch
    {
        "add" when proposal.Offer?.Trim() is { Length: > 0 } => OfferAdd(proposal, facts),
        "add" => PluginAdd(proposal, facts),
        "enable" or "disable" => PluginSwitch(proposal, facts),
        "update" => PluginUpdate(proposal, facts),
        "approve" or "withdraw" => GrantPlan(proposal, facts),
        var door => new HelpPlan(
            $"`{door}` is not a plugin's change — `add`, `enable`, `disable`, `update`, `approve` or `withdraw`.", "", "", null),
    };

    public async Task<HelpApplied> ApplyAsync(HelpApplying applying, CancellationToken ct)
    {
        var (proposal, plan, doors, id) = (applying.Proposal, applying.Plan, applying.Doors, applying.Id);

        // PLUG9: a copy, a swap or a row, and nothing started — the loop starts what it runs at its next look.
        // HELPPLUG1c: an approval writes exactly the tools its card named, through the page's own code; a withdrawal takes it back.
        try
        {
            if (plan.Grant is { Withdraw: true } withdrawn) doors.WithdrawHelp(withdrawn.Plugin, withdrawn.Server);
            else if (plan.Grant is { } grant) doors.ApproveHelp(grant.Plugin, grant.Server, grant.Accepted);
            else if (proposal.Door == "add" && proposal.Offer?.Trim() is { Length: > 0 }) doors.AddOffer(plan.Plugin!.Id);
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
    /// An approval of what a plugin's server offers Ask Daoris, or its withdrawal (HELPPLUG1c, D170's note), judged as the page's
    /// press is (<see cref="HelpGrants"/>): a plugin here, switched on and sound, a server it declares for Ask Daoris (the one it
    /// declares, where none is named), never one naming a generic execution tool (HELP4), each write accepted one it declares,
    /// and a change from what is on file. The card names every tool, its reads and its writes apart.
    /// </summary>
    private static HelpPlan GrantPlan(HelpProposal proposal, HelpMachineFacts facts)
    {
        var id = proposal.Target?.Trim() ?? "";
        var withdraw = proposal.Door == "withdraw";
        static HelpPlan Refused(string why, string terminal = "") => new(why, "", terminal, null);
        if (id.Length == 0) return Refused($"`{proposal.Door}` names the plugin by its id, as the room lists the plugins installed here.");
        if (facts.Plugins.Contributing.FirstOrDefault(entry => string.Equals(entry.Manifest.Id, id, StringComparison.OrdinalIgnoreCase))
            is not { } plugin)
        {
            var sound = facts.Plugins.Contributing.Select(entry => entry.Manifest.Id).ToList();
            return Refused($"no plugin `{id}` on this machine is switched on and sound — "
                + (sound.Count > 0 ? $"these are: {Names(sound)}." : "none is."));
        }

        var named = plugin.Manifest.Id;
        var offered = plugin.Manifest.Servers.Where(server => server.HandedTo(PluginServerTo.Help)).ToList();
        if (offered.Count == 0)
        {
            return Refused($"plugin `{named}` offers Ask Daoris no server: what it serves is handed to sessions only, as its manifest declares.");
        }

        var wanted = proposal.Server?.Trim() is { Length: > 0 } server ? server : null;
        if (wanted is null && offered.Count > 1)
        {
            return Refused($"plugin `{named}` offers Ask Daoris {Names(offered.Select(each => each.Name))}: name the server.");
        }

        if ((wanted is null ? offered[0] : offered.FirstOrDefault(each => string.Equals(each.Name, wanted, StringComparison.Ordinal)))
            is not { } declared)
        {
            return Refused($"plugin `{named}` offers Ask Daoris no server `{wanted}` — it offers {Names(offered.Select(each => each.Name))}.");
        }

        var grant = facts.Home is { Length: > 0 } home
            ? HelpGrants.Read(home, facts.Plugins).All.FirstOrDefault(each =>
                string.Equals(each.Plugin, named, StringComparison.OrdinalIgnoreCase) && each.Server.Name == declared.Name)
            : null;
        static string Tools(IEnumerable<string> tools) => string.Join(", ", tools.Select(tool => $"`{tool}`"));
        var reads = declared.Allow.Count > 0 ? $"reads with {Tools(declared.Allow)}" : "reads with none of its tools";
        if (withdraw)
        {
            var terminal = $"daoris-driver plugins withdraw {named} {declared.Name}";
            if (grant is not { State: HelpGrants.Approved })
            {
                return Refused($"`{declared.Name}` of plugin `{named}` is not approved for Ask Daoris, so there is nothing to withdraw.", terminal);
            }

            var lost = grant.Accepted.Count > 0 ? $"{reads}, and changes things outside Daoris with {Tools(grant.Accepted)}" : reads;
            return new HelpPlan(null,
                $"Withdraw Ask Daoris's approval of server `{declared.Name}` of plugin `{named}`: it is handed it no more, from its next "
                + $"conversation, so it no longer {lost}. Approving it again asks you again.",
                terminal, null)
            {
                Grant = new HelpGrantView(named, plugin.Manifest.Name, declared.Name, declared.Allow, declared.Writes, grant.Accepted) { Withdraw = true },
            };
        }

        var flags = "";
        var accepted = proposal.Writes ?? [];
        if (HelpGrants.Refusal(declared) is { } refusal) return Refused($"`{declared.Name}` is not approved: {refusal}");
        if (accepted.FirstOrDefault(tool => !declared.Writes.Contains(tool, StringComparer.Ordinal)) is { } unknown)
        {
            return Refused($"`{unknown}` is not a write `{declared.Name}` declares — it writes outside Daoris with "
                + (declared.Writes.Count > 0 ? Tools(declared.Writes) : "none of its tools") + ".");
        }

        // The declared order, as the approval keeps it; the terminal's flag spells what the card accepts.
        var kept = declared.Writes.Where(tool => accepted.Contains(tool, StringComparer.Ordinal)).ToList();
        var asked = declared.Writes.Except(kept, StringComparer.Ordinal).ToList();
        if (declared.Writes.Count > 0 && kept.Count == 0) flags = " --reads-only";
        else if (asked.Count > 0) flags = $" --writes {string.Join(",", kept)}";
        if (grant is { State: HelpGrants.Approved } && grant.Accepted.ToHashSet(StringComparer.Ordinal).SetEquals(kept))
        {
            return Refused($"`{declared.Name}` of plugin `{named}` is already approved for Ask Daoris with exactly these tools, so nothing would change.");
        }

        var said = new List<string> { reads };
        if (kept.Count > 0) said.Add($"changes things outside Daoris with {Tools(kept)}, as you, which you accept");
        if (asked.Count > 0) said.Add($"is refused {Tools(asked)}, writes you do not accept");
        var again = grant is { Changed: true } ? " It was approved before, and an update has changed its tools since." : "";
        return new HelpPlan(null,
            $"Approve server `{declared.Name}` of plugin `{named}` for Ask Daoris: from its next conversation it {string.Join("; it ", said)}. "
            + $"A plugin is code that runs as you; an update that adds or widens a tool asks you again.{again}",
            $"daoris-driver plugins approve {named} {declared.Name}{flags}", null)
        {
            Grant = new HelpGrantView(named, plugin.Manifest.Name, declared.Name, declared.Allow, declared.Writes, kept),
        };
    }

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
        [.. manifest.Servers.Select(server => new HelpPluginPart(server.Name, server.Command) { To = server.To, Allow = server.Allow, Writes = server.Writes })]);

    /// <summary>What the plugin runs, said as its manifest writes it: its process and points, its harnesses, its servers.</summary>
    private static string Runs(HelpPluginView view)
    {
        var said = new List<string>();
        if (view.Command is { Count: > 0 } command)
        {
            said.Add($"It runs `{Line(command)}`, speaking on {string.Join(", ", view.Points.Select(point => $"`{point}`"))}.");
        }

        said.AddRange(view.Harnesses.Select(harness => $"It declares agent `{harness.Name}` (`{Line(harness.Command)}`)."));
        said.AddRange(view.Servers.Select(Handed));
        return said.Count > 0 ? string.Join(" ", said) : "It declares nothing and runs nothing.";
    }

    /// <summary>
    /// A server as the card says it (HELPPLUG1a, D170 point 3): who it is handed to, and what it offers Ask Daoris, its reads and
    /// its writes outside Daoris, which reach the helper only once the person approves them on the plugin's page (D170's note).
    /// </summary>
    private static string Handed(HelpPluginPart server)
    {
        var sessions = server.To.Contains(PluginServerTo.Sessions, StringComparer.Ordinal);
        var help = server.To.Contains(PluginServerTo.Help, StringComparer.Ordinal);
        var line = $"server `{server.Name}` (`{Line(server.Command)}`)";
        if (!help) return $"It hands every session {line}.";

        static string Named(IEnumerable<string> tools) => string.Join(", ", tools.Select(tool => $"`{tool}`"));
        var uses = new List<string>();
        if (server.Allow.Count > 0) uses.Add($"to read with {Named(server.Allow)}");
        if (server.Writes.Count > 0) uses.Add($"to change things outside Daoris with {Named(server.Writes)}");
        if (sessions)
        {
            var tools = uses.Count > 0 ? string.Join(" and ", uses) : "with none of its tools";
            return $"It hands every session {line}, and offers it to Ask Daoris {tools}, once you approve it on its page.";
        }

        return uses.Count > 0
            ? $"It offers Ask Daoris {line}, {string.Join(" and ", uses)}, once you approve them on its page."
            : $"It offers Ask Daoris {line} with none of its tools, once you approve it on its page.";
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

/// <summary>
/// What a plugin proposal's approval or withdrawal names (HELPPLUG1c): the plugin, the server, each tool it reads with and each
/// it writes outside Daoris with, as the plugin declares them, and the writes accepted, as the approval keeps them.
/// </summary>
/// <param name="Name">The plugin's name, as its manifest gives it.</param>
/// <param name="Accepted">For an approval, the writes it accepts; for a withdrawal, those the approval had accepted.</param>
public sealed record HelpGrantView(
    string Plugin, string Name, string Server, IReadOnlyList<string> Reads, IReadOnlyList<string> Writes, IReadOnlyList<string> Accepted)
{
    /// <summary>Whether Apply takes the approval back, rather than writing it.</summary>
    public bool Withdraw { get; init; }
}

/// <summary>A harness a plugin declares, or a server it hands: its name, and the command that runs it.</summary>
public sealed record HelpPluginPart(string Name, IReadOnlyList<string> Command)
{
    /// <summary>A server's: who it is handed to (HELPPLUG1a, D170 point 3), sessions where its manifest names none.</summary>
    public IReadOnlyList<string> To { get; init; } = [PluginServerTo.Sessions];

    /// <summary>A server's: the tools of it that read, which Ask Daoris may call once the person approves them.</summary>
    public IReadOnlyList<string> Allow { get; init; } = [];

    /// <summary>A server's: the tools of it that change something outside Daoris, never allowed silently.</summary>
    public IReadOnlyList<string> Writes { get; init; } = [];
}

public interface IHelpPluginDoors
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

    /// <summary>
    /// <c>PLUGIN_ACTION</c>'s own <c>help-approve</c> (HELPPLUG1c, <see cref="HelpGrants.Approve(string, PluginCatalog, string, string, IReadOnlyList{string})"/>):
    /// the server's exact reads and the writes accepted, the others asked. Ask Daoris's next spawn is handed it.
    /// </summary>
    void ApproveHelp(string plugin, string server, IReadOnlyList<string> writes);

    /// <summary><c>PLUGIN_ACTION</c>'s own <c>help-withdraw</c> (HELPPLUG1c, <see cref="HelpGrants.Withdraw"/>).</summary>
    void WithdrawHelp(string plugin, string server);
}
