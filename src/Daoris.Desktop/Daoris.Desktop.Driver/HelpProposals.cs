using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>One of Ask Daoris's proposals, as the connector wrote it (HELP1c, D89; HELP6; PLUG9; WSR5b).</summary>
/// <param name="Kind">`setting`, `ask`, `agent`, `delete`, `account`, `go`, `plugin` or `hand`.</param>
/// <param name="Door">
/// The `daoris driver` verb for a setting; `ask` for an ask; `update` or `pin` for an agent; `quest` or
/// `ask` for a delete; `settings` for an account; `go` for a go; `add`, `enable` or `disable` for a plugin;
/// `hand` for a hand-off.
/// </param>
/// <param name="Target">
/// The repository a setting is for; the agent of an agent or an account; the id a delete names; a go's
/// view; the id of a plugin switched on or off; the session or branch a hand-off names.
/// </param>
/// <param name="Value">A setting's value; a pin's version; the plugin a hand-off names, or null for the rule's.</param>
/// <param name="Session">The conversation that proposed it.</param>
/// <param name="State">`proposed`, then `applied`, `dismissed` or `refused`.</param>
public sealed record HelpProposal(
    string Id, string Kind, string Door, string? Target, string? Workspace, string? Value, string? Sentence,
    string Why, string? Session, string State)
{
    /// <summary>An account's name, for an account's settings (HELP6).</summary>
    public string? Account { get; init; }

    /// <summary>The model an account's settings would set — `unset` for the tool's own default — or null to leave it.</summary>
    public string? Model { get; init; }

    /// <summary>The effort an account's settings would set — `unset` for the tool's own default — or null to leave it.</summary>
    public string? Effort { get; init; }

    /// <summary>A go's Settings domain, or null.</summary>
    public string? Domain { get; init; }

    /// <summary>A go's part of its domain or view — a card, a setup step, a drawer — or null.</summary>
    public string? Part { get; init; }

    /// <summary>
    /// The repository whose checkout holds a plugin to add (PLUG9), or null for a whole path the person gave;
    /// for a hand-off (WSR5b), the repository its branch is in, where a name alone is in several.
    /// </summary>
    public string? Repository { get; init; }

    /// <summary>A plugin's folder to add from: from that checkout's root, or a whole path (PLUG9).</summary>
    public string? Folder { get; init; }
}

/// <summary>What the machine holds that a proposal is judged against: names only.</summary>
public sealed record HelpMachineFacts(
    IReadOnlyCollection<string> Repositories, IReadOnlyCollection<string> Workspaces, IReadOnlyCollection<string> Agents)
{
    /// <summary>Each door as the Agents screen's roster reads it (HELP6).</summary>
    public IReadOnlyList<HelpDoorFacts> Doors { get; init; } = [];

    /// <summary>The quests, closed ones included, each with the service's own reading of whether it may be deleted.</summary>
    public IReadOnlyList<HelpQuestFacts> Quests { get; init; } = [];

    /// <summary>The asks, closed ones included, each with the service's own reading of whether it may be deleted.</summary>
    public IReadOnlyList<HelpAskFacts> Asks { get; init; } = [];

    /// <summary>This machine's plugins, for a landing rule that names one (HELP8, D100), and a plugin proposal (PLUG9).</summary>
    public PluginCatalog Plugins { get; init; } = PluginCatalog.None;

    /// <summary>The Daoris home, which a plugin is never added from (PLUG9); null where none was named.</summary>
    public string? Home { get; init; }

    /// <summary>Each registered repository's checkout on this machine, or null where it has none (PLUG9).</summary>
    public IReadOnlyDictionary<string, string?> Checkouts { get; init; } = new Dictionary<string, string?>();

    /// <summary>The harness names this build carries, which a plugin may not declare (PLUG9, D64 §5).</summary>
    public IReadOnlyCollection<string> Reserved { get; init; } = [];

    /// <summary>The branches this machine's landings made and recorded, for a hand-off (WSR5b).</summary>
    public IReadOnlyList<LandedBranch> Landed { get; init; } = [];
}

/// <summary>One door as the Agents screen reads it (HELP6): what its Update does, whether it pins, whose accounts it runs as.</summary>
/// <param name="Name">The harness, as `daoris agent` spells it.</param>
public sealed record HelpDoorFacts(string Name)
{
    public bool Present { get; init; }

    /// <summary>The roster's `updates`: `pin`, `tool`, or null for none (USE1a).</summary>
    public string? Updates { get; init; }

    /// <summary>This machine's pin for it, or null.</summary>
    public string? Pinned { get; init; }

    /// <summary>The package a pin installs from, or null.</summary>
    public string? Package { get; init; }

    /// <summary>The maker's release channel a pin installs from, or null (AGT2b).</summary>
    public string? Channel { get; init; }

    public string? Product { get; init; }

    /// <summary>Whose accounts it runs as (AGT7); null is itself.</summary>
    public string? Owner { get; init; }

    /// <summary>The owner's accounts on this machine, by name.</summary>
    public IReadOnlyList<string> Accounts { get; init; } = [];

    /// <summary>Whether Daoris knows the tool's own settings file, and so offers its model and effort (D98).</summary>
    public bool SettingsKnown { get; init; }

    /// <summary>The name its accounts live under.</summary>
    public string AccountsOf => Owner is { Length: > 0 } owner ? owner : Name;
}

/// <summary>A quest as the service answered it, with its own reading of whether it may be deleted (D95).</summary>
/// <param name="Status">`Open`, `Taken`, `Done` or `Declined`, as the service spells it.</param>
public sealed record HelpQuestFacts(string Id, string Title, string To, string Status, bool Deletable);

/// <summary>An ask as the service answered it, with the quests it became and whether it may be deleted (D95).</summary>
public sealed record HelpAskFacts(string Id, string Sentence, string Workspace, string State, IReadOnlyList<string> Quests, bool Deletable);

/// <summary>A place on the window a go takes the person to (HELP6): a view, a Settings domain, a part of it.</summary>
public sealed record HelpPlace(string View, string? Domain, string? Part);

/// <summary>
/// A proposal judged: what it changes, the terminal command that does the same (D50), and the edit to
/// make — or the route's refusal, and nothing to make.
/// </summary>
/// <param name="Apply">The edit to the driver's file, for a setting the route takes; null for every other kind or a refusal.</param>
/// <param name="Terminal">The command that does the same; empty for a go, which changes nothing.</param>
public sealed record HelpPlan(string? Refusal, string Describe, string Terminal, Func<DriverConfig, DriverConfig>? Apply)
{
    /// <summary>Where a go takes the person, for one the window has; null for every other kind.</summary>
    public HelpPlace? Go { get; init; }

    /// <summary>What a plugin proposal's plugin runs, as its manifest writes it (PLUG9); null for every other kind.</summary>
    public HelpPluginView? Plugin { get; init; }

    /// <summary>The folder a plugin is added from, resolved on this machine (PLUG9); null for every other plan.</summary>
    public string? Source { get; init; }

    /// <summary>The landed branch a hand-off gives its plugin (WSR5b); null for every other plan.</summary>
    public HelpHandOff? Hand { get; init; }
}

/// <summary>What a hand-off's Apply hands on (WSR5b): the recorded branch, and the plugin the card named — null for the rule's.</summary>
public sealed record HelpHandOff(string Repository, string Branch, string? Plugin);

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
}

/// <summary>A harness a plugin declares, or a server it hands every session: its name, and the command that runs it.</summary>
public sealed record HelpPluginPart(string Name, IReadOnlyList<string> Command);

/// <summary>What the person's Apply did: whether it was applied, what the conversation is told, and a go's place.</summary>
public sealed record HelpApplied(bool Applied, string Told)
{
    public HelpPlace? Go { get; init; }
}

/// <summary>
/// The doors a proposal is applied through (HELP6): each the code the screen's own route runs, so an
/// Apply is what the screen would have done, never a second path.
/// </summary>
public interface IHelpDoors
{
    /// <summary>An edit to the driver's file, as the `SET_*` routes make it.</summary>
    void Change(Func<DriverConfig, DriverConfig> edit);

    /// <summary>An ask, through the local host's ask door.</summary>
    Task<AskAnswer> AskAsync(string workspace, string sentence, CancellationToken ct);

    /// <summary>The local host's <c>DELETE /api/quests/{id}</c>, the quest drawer's route.</summary>
    Task<(bool Ok, string Message)> DeleteQuestAsync(string id, CancellationToken ct);

    /// <summary>The local host's <c>DELETE /api/asks/{id}</c>, the ask's record's route.</summary>
    Task<(bool Ok, string Message)> DeleteAskAsync(string id, CancellationToken ct);

    /// <summary>
    /// <c>HARNESS_ACTION</c>'s own start of an update or a pin: answered once the process has started,
    /// its end told to <paramref name="ended"/> with its exit code and any problem.
    /// </summary>
    Task StartAgentActionAsync(string harness, string action, string? version, Action<int, string?> ended, CancellationToken ct);

    /// <summary><c>SET_AGENT_SETTINGS</c>'s own write to an account's settings file.</summary>
    AgentSettingsRead SetAgentSettings(string harness, string account, AgentSettingEdit? model, AgentSettingEdit? effort);

    /// <summary>
    /// <c>daoris plugin add</c>'s copy, the driver's twin (<see cref="PluginInstall.Add"/>, PLUG9): the
    /// folder copied into the home under its manifest's id. 🔴 Nothing the plugin declares is started here.
    /// </summary>
    void AddPlugin(string folder);

    /// <summary><c>PLUGIN_ACTION</c>'s own enable or disable: a row in <c>plugins.json</c> (PLUG9).</summary>
    void SwitchPlugin(string id, bool on);

    /// <summary>
    /// The review's own hand-off (<c>HANDOFF</c>, WSR5b): the recorded branch given to the plugin named, else the
    /// repository's rule's, which pushes it and opens the pull request. A refusal is an answer, never thrown.
    /// </summary>
    Task<TreeHand> HandAsync(string repository, string branch, string? plugin, CancellationToken ct);
}

/// <summary>
/// The places on the window a go may name (HELP6): the views, Settings' domains, and the parts of them a
/// door already opens — the setup guide's steps, a domain's cards, Projects' drawers.
/// </summary>
/// <remarks>
/// A twin (`.claude/knowledge/twins.md`) of the page's <c>help/places.ts</c>, which navigates to them:
/// they share no code, each side's test holds the same table, and they change together. Every name is
/// the one the window's own label shows.
/// </remarks>
public static class HelpPlaces
{
    public static readonly IReadOnlyList<(string Id, string Name)> Views =
    [
        ("overview", "Overview"), ("sessions", "Sessions"), ("quests", "Quests"), ("projects", "Projects"),
        ("map", "Map"), ("convergence", "Convergence"), ("search", "Search"), ("settings", "Settings"),
    ];

    public static readonly IReadOnlyList<(string Id, string Name)> Domains =
    [
        ("start", "Get started"), ("appearance", "Appearance"), ("ai", "Daoris's own AI"), ("workspace", "Workspace"),
        ("driver", "Driver"), ("agents", "Agents & accounts"), ("permissions", "Permissions"), ("plugins", "Plugins"),
        ("browser", "Browser"), ("logs", "Logs"),
    ];

    /// <summary>The parts, each within a view (Projects) or a Settings domain.</summary>
    public static readonly IReadOnlyList<(string Within, string Id, string Name)> Parts =
    [
        ("projects", "add", "Add repository"), ("projects", "import", "Import a folder"),
        ("start", "agent", "step 1, an agent"), ("start", "helper", "step 2, Daoris's own agent"),
        ("start", "repositories", "step 3, a workspace and its repositories"), ("start", "driven", "step 4, what is driven"),
        ("start", "landing", "step 5, how work lands"), ("start", "rules", "step 6, what agents may do"),
        ("workspace", "wiring", "Wiring"), ("workspace", "lines", "Lines"), ("workspace", "landing", "How work lands"),
        ("workspace", "sweep", "Session branches"),
        ("agents", "usage", "Usage"), ("permissions", "proposals", "Proposed by agents"),
    ];
}

/// <summary>
/// Ask Daoris's proposals, the driver's half (HELP1c, D89): the files the connector's
/// <c>setting_propose</c> and <c>ask_propose</c> wrote under the home, judged with the route's own code
/// and settled by the person's press.
/// </summary>
/// <remarks>
/// <para><b>Judged by the route, not by the door that wrote it.</b> The service checks a proposal's
/// shape; whether the route takes it — a repository registered here, a line git accepts, a pattern
/// naming one branch per session, an agent this machine has — is the config's own edits and this
/// machine's names, run here. A proposal the route would refuse is never shown to the person.</para>
///
/// <para><b>THE FILE is the contract</b>, the twin of the service's <c>HelpProposalBox</c>: they share no
/// code, and each side's tests hold the same shape.</para>
/// </remarks>
public static partial class HelpProposals
{
    public static string FolderOf(string home) => Path.Combine(home, "help", "proposals");

    /// <summary>The proposals one conversation made that wait for the person, oldest first. A file that does not read is skipped.</summary>
    public static IReadOnlyList<HelpProposal> Pending(string home, string session)
    {
        var folder = FolderOf(home);
        if (!Directory.Exists(folder)) return [];

        var found = new List<(DateTimeOffset At, HelpProposal Proposal)>();
        foreach (var path in Directory.EnumerateFiles(folder, "*.json"))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var root = document.RootElement;
                var proposal = Read(root, Path.GetFileNameWithoutExtension(path));
                if (proposal.State != "proposed" || !string.Equals(proposal.Session, session, StringComparison.OrdinalIgnoreCase)) continue;
                var at = DateTimeOffset.TryParse(Text(root, "proposed"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var when) ? when : DateTimeOffset.MinValue;
                found.Add((at, proposal));
            }
            catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
            {
                // Half-written or not a proposal: nothing a person could press on.
            }
        }

        return [.. found.OrderBy(entry => entry.At).Select(entry => entry.Proposal)];
    }

    /// <summary>Settle one: its state and why, written beside and renamed, every other field kept.</summary>
    public static void Settle(string home, string id, string state, string? note)
    {
        var path = Path.Combine(FolderOf(home), $"{id}.json");
        var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        node["state"] = state;
        node["note"] = note;
        AtomicFile.WriteText(path, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n") + "\n");
    }

    /// <summary>One proposal, found by its id, whatever its state — what Apply and Not now read.</summary>
    public static HelpProposal? Find(string home, string id)
    {
        var path = Path.Combine(FolderOf(home), $"{Path.GetFileName(id)}.json");
        if (!File.Exists(path)) return null;
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return Read(document.RootElement, id);
    }

    /// <summary>
    /// One file as the service's box writes it. A field a kind does not carry reads as null, and so does
    /// one a file from before HELP6 lacks: absence is "not named".
    /// </summary>
    private static HelpProposal Read(JsonElement root, string id) =>
        new(
            Text(root, "id") ?? id, Text(root, "kind") ?? "", Text(root, "door") ?? "", Text(root, "target"),
            Text(root, "workspace"), Text(root, "value"), Text(root, "sentence"), Text(root, "why") ?? "",
            root.TryGetProperty("by", out var by) ? Text(by, "session") : null, Text(root, "state") ?? "")
        {
            Account = Text(root, "account"),
            Model = Text(root, "model"),
            Effort = Text(root, "effort"),
            Domain = Text(root, "domain"),
            Part = Text(root, "part"),
            Repository = Text(root, "repository"),
            Folder = Text(root, "folder"),
        };

    /// <summary>
    /// The records a delete is judged against (HELP6): every quest and ask, closed ones included, each
    /// with the service's own reading of whether it may go — the answer the drawer's Delete is shown by.
    /// </summary>
    public static async Task<(IReadOnlyList<HelpQuestFacts> Quests, IReadOnlyList<HelpAskFacts> Asks)> RecordsAsync(
        ServiceClient service, CancellationToken ct)
    {
        var quests = await service.EveryQuestAsync(ct).ConfigureAwait(false);
        var asks = await service.EveryAskAsync(ct).ConfigureAwait(false);
        return (
            [.. quests.Select(quest => new HelpQuestFacts(quest.Id, quest.Title, quest.To, quest.Status, quest.Deletable))],
            [.. asks.Select(ask => new HelpAskFacts(ask.Id, ask.Sentence, ask.Workspace, ask.State, ask.Quests, ask.Deletable))]);
    }

    /// <summary>Judge a proposal with the route's own code, against the machine as it stands.</summary>
    public static HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts) => proposal.Kind switch
    {
        "setting" => Setting(proposal, config, facts),
        "ask" => Ask(proposal, facts),
        "agent" => Agent(proposal, facts),
        "delete" => Delete(proposal, facts),
        "account" => Account(proposal, facts),
        "go" => Go(proposal),
        "plugin" => Plugin(proposal, facts),
        "hand" => Hand(proposal, config, facts),
        var kind => new HelpPlan($"`{kind}` is not a change Ask Daoris proposes.", "", "", null),
    };

    private static HelpPlan Setting(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts)
    {
        var target = proposal.Target?.Trim();
        var workspace = proposal.Workspace?.Trim();
        var value = proposal.Value?.Trim() ?? "";
        HelpPlan Refused(string why, string describe, string terminal) => new(why, describe, terminal, null);

        // The names the route itself does not check, and a helper can invent: a repository, a circle, an agent.
        if (target is { Length: > 0 } && !facts.Repositories.Contains(target, StringComparer.OrdinalIgnoreCase))
        {
            return Refused($"`{target}` is not registered on this machine — use a repository's name as Projects lists it.", "", "");
        }

        if (workspace is { Length: > 0 } && !facts.Workspaces.Contains(workspace, StringComparer.OrdinalIgnoreCase))
        {
            return Refused($"there is no workspace `{workspace}` on this machine — one of {Names(facts.Workspaces)}.", "", "");
        }

        var scope = workspace is { Length: > 0 } ? $"--workspace {workspace}" : target ?? "";
        var whose = workspace is { Length: > 0 } ? $"workspace `{workspace}`" : $"`{target}`";
        (string Describe, string Terminal, Func<DriverConfig, DriverConfig> Edit) planned;
        switch (proposal.Door)
        {
            case "drive":
                planned = ($"Drive `{target}`: a quest addressed to it starts a session on this machine.", $"daoris driver drive {target}", c => c.WithDrivable(target!, true));
                break;
            case "undrive":
                planned = ($"Stop driving `{target}`.", $"daoris driver undrive {target}", c => c.WithDrivable(target!, false));
                break;
            case "hold":
                planned = ($"Hold `{target}`: nothing new starts there until it is resumed.", $"daoris driver hold {target}", c => c.WithHold(target!, true));
                break;
            case "resume":
                planned = ($"Resume `{target}`.", $"daoris driver resume {target}", c => c.WithHold(target!, false));
                break;
            case "trees":
                planned = value == "on"
                    ? ($"Sessions in `{target}` open their own tree.", $"daoris driver trees {target} on", c => c.WithTrees(target!, true))
                    : ($"Sessions in `{target}` work in its checkout.", $"daoris driver trees {target} off", c => c.WithTrees(target!, false));
                break;
            case "line":
            {
                var branch = value == "--clear" ? null : value;
                planned = (branch is null ? $"Clear {whose}'s line." : $"Set {whose}'s line to `{branch}`.",
                    $"daoris driver line {scope} {value}",
                    c => workspace is { Length: > 0 } ? c.WithWorkspaceLine(workspace, branch) : c.WithLine(target!, branch));
                break;
            }
            case "landing":
            {
                // As `daoris driver landing` reads its words (HELP8): the form, a branch's pattern, and
                // `--tidy` and `--plugin <id>` in either order. A pattern holds no space, since git takes none.
                var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
                var tidy = words.Remove("--tidy");
                string? plugin = null;
                if (words.IndexOf("--plugin") is var at and >= 0)
                {
                    if (at + 1 >= words.Count || words[at + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        return Refused("`--plugin` needs the id of an installed plugin — `--plugin <id>`; "
                            + "`daoris plugin list` shows what there is.", "", "");
                    }

                    plugin = words[at + 1];
                    words.RemoveRange(at, 2);
                }

                var bare = string.Join(' ', words);
                LandingRule? rule = bare switch
                {
                    "--clear" => null,
                    "merge" => new LandingRule("merge", null, tidy, plugin),
                    _ when bare.StartsWith("branch ", StringComparison.Ordinal) => new LandingRule("branch", bare["branch ".Length..].Trim(), tidy, plugin),
                    _ => new LandingRule(bare, null, tidy, plugin),
                };

                // The plugin must land work on THIS machine, asked as the screen's route asks it (D100),
                // once the rule's own shape is sound, so a merge naming one is told why in that shape's words.
                if (rule is { Plugin: { } named } && LandingRules.Problem(rule) is null
                    && LandingRules.PluginProblem(named, facts.Plugins) is { } unready)
                {
                    return Refused(unready, "", "");
                }

                var after = (plugin is null ? "" : $", plugin `{plugin}` pushes it and opens the pull request")
                    + (tidy ? ", its tree removed once landed" : "");
                var lands = rule is null ? $"Clear {whose}'s landing rule."
                    : rule.Form == "branch" ? $"Land {whose}'s accepted work on a branch `{rule.Pattern}`{after}."
                    : $"Land {whose}'s accepted work merged into its line{after}.";
                planned = (lands, $"daoris driver landing {scope} {value}",
                    c => workspace is { Length: > 0 } ? c.WithWorkspaceLanding(workspace, rule) : c.WithLanding(target!, rule));
                break;
            }
            case "intake" or "helper":
            {
                var agent = value == "off" ? null : value;
                if (agent is not null && !facts.Agents.Contains(agent, StringComparer.Ordinal))
                {
                    return Refused($"there is no agent `{agent}` on this machine — one of {Names(facts.Agents)}.", "", "");
                }

                var intake = proposal.Door == "intake";
                planned = (intake
                        ? agent is null ? "Stop answering asks with an agent: the declarations alone propose." : $"Answer asks with `{agent}`."
                        : agent is null ? "Turn Ask Daoris's agent off: it offers its starters only." : $"Run Ask Daoris on `{agent}`.",
                    $"daoris driver {proposal.Door} {value}",
                    c => intake ? c.WithIntake(agent) : c.WithHelper(agent));
                break;
            }
            case "strikes" when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var strikes):
                planned = (strikes == 0 ? "Never park a quest for its failed sessions." : $"Park a quest after {strikes} failed sessions.",
                    $"daoris driver strikes {strikes}", c => c.WithStrikes(strikes));
                break;
            case "timeout" when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) && minutes >= 1:
                planned = ($"Let one session run up to {minutes} minutes.", $"daoris driver timeout {minutes}", c => c with { TimeoutMinutes = minutes });
                break;
            case "notify" when value is "on" or "off":
                planned = (value == "on" ? "Say so when a session parks, or ends without being asked." : "Stop saying so when a session parks.",
                    $"daoris driver notify {value}", c => c.WithNotify(value == "on"));
                break;
            default:
                return Refused($"`{proposal.Door} {value}` is not a change the driver makes.", "", "");
        }

        // The route's own edits judge the rest — a branch git would not take, a pattern that is not one
        // branch per session — in their own words.
        try
        {
            planned.Edit(config);
        }
        catch (DriverException refused)
        {
            return Refused(refused.Message, planned.Describe, planned.Terminal);
        }

        return new HelpPlan(null, planned.Describe, planned.Terminal, planned.Edit);
    }

    private static HelpPlan Ask(HelpProposal proposal, HelpMachineFacts facts)
    {
        var workspace = proposal.Workspace?.Trim() ?? "";
        var sentence = proposal.Sentence?.Trim() ?? "";
        var terminal = $"daoris-driver ask --workspace {workspace} \"{sentence.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
        var describe = $"Ask at workspace `{workspace}`: “{sentence}”";
        if (sentence.Length == 0) return new HelpPlan("an ask needs its words.", describe, terminal, null);
        if (!facts.Workspaces.Contains(workspace, StringComparer.OrdinalIgnoreCase))
        {
            return new HelpPlan($"there is no workspace `{workspace}` on this machine — one of {Names(facts.Workspaces)}.", describe, terminal, null);
        }

        return new HelpPlan(null, describe, terminal, null);
    }

    /// <summary>
    /// An agent's Update or a pin (HELP6), judged by what the Agents screen offers and what
    /// <c>HARNESS_ACTION</c> refuses: Update where the roster's <c>updates</c> names one and the agent is
    /// installed, a pin where the door declares a package or a channel, of one exact release.
    /// </summary>
    private static HelpPlan Agent(HelpProposal proposal, HelpMachineFacts facts)
    {
        var name = proposal.Target?.Trim() ?? "";
        var version = proposal.Value?.Trim() ?? "";
        var update = proposal.Door == "update";
        var terminal = update ? $"daoris agent update {name}" : $"daoris agent pin {name} {version}";
        HelpPlan Refused(string why) => new(why, "", terminal, null);

        if (proposal.Door is not ("update" or "pin")) return Refused($"`{proposal.Door}` is not an agent's change — `update` or `pin`.");
        if (facts.Doors.FirstOrDefault(each => string.Equals(each.Name, name, StringComparison.Ordinal)) is not { } door)
        {
            return Refused($"there is no agent `{name}` on this machine — one of {Names(facts.Doors.Select(each => each.Name))}.");
        }

        if (update)
        {
            if (!door.Present)
            {
                return Refused($"`{name}` is not installed on this machine, so there is nothing to update — install it under "
                    + $"Settings → Agents & accounts, or with `daoris agent install {name}`.");
            }

            return door.Updates switch
            {
                "pin" => new HelpPlan(null,
                    $"Update `{name}`: move its pin from {door.Pinned} to the newest release, installed before the pin moves.", terminal, null),
                "tool" => new HelpPlan(null, $"Update `{name}` with its own updater.", terminal, null),
                _ => Refused(door.Pinned is { Length: > 0 } pinned
                    ? $"`{name}` offers no Update: it is pinned at {pinned} and declares no package or release channel to find a newer "
                      + $"version in. Pin another with `daoris agent pin {name} <version>`, or unpin it."
                    : $"`{name}` offers no Update: it declares no updater — it updates itself, or its package manager does."),
            };
        }

        if (version.Length == 0) return Refused("a pin names one exact release, such as 2.1.300.");
        if (door.Package is not { Length: > 0 } && door.Channel is not { Length: > 0 })
        {
            return Refused($"`{name}` declares no package or release channel, so Daoris has no sanctioned way to fetch a version of it. "
                + "Install it with its own tooling and Daoris will find it on PATH.");
        }

        if (door.Channel is { Length: > 0 } channel)
        {
            // The channel's own refusals, in its words: the one channel this build installs from, and a version
            // it can verify (AGT2b).
            if (channel != ClaudeReleases.Channel)
            {
                return Refused($"this build installs from no `{channel}` channel, so nothing would be fetched or pinned. "
                    + $"`daoris agent pin {name} {version}` in a terminal knows every channel Daoris does.");
            }

            try
            {
                ClaudeReleases.RefuseVersion(version);
            }
            catch (DriverException refused)
            {
                return Refused(refused.Message);
            }
        }
        else if (!ExactRelease().IsMatch(version))
        {
            // npm would take a pointer, and a pin naming one would change under a running arrangement (USE1a).
            return Refused($"`{version}` is not one exact release — a pin names one, like 1.2.3, never a pointer such as latest.");
        }

        return new HelpPlan(null,
            $"Pin `{name}` to {version}: install that version where Daoris keeps it, then run it"
            + (door.Pinned is { Length: > 0 } was && was != version ? $" in place of {was}." : "."),
            terminal, null);
    }

    /// <summary>
    /// A delete (HELP6), judged by the service's own reading of whether the record may go (D95) — the one
    /// the drawer's and the record's Delete are shown by. The card says what goes.
    /// </summary>
    private static HelpPlan Delete(HelpProposal proposal, HelpMachineFacts facts)
    {
        var id = proposal.Target?.Trim().TrimStart('#') ?? "";
        HelpPlan Refused(string why, string terminal) => new(why, "", terminal, null);
        string Quest(string quest) => facts.Quests.FirstOrDefault(each => each.Id == quest) is { } found
            ? $"`#{quest}` “{found.Title}”"
            : $"`#{quest}`";

        if (proposal.Door == "quest")
        {
            var terminal = $"daoris-driver quest delete {id}";
            if (facts.Quests.FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.OrdinalIgnoreCase)) is not { } quest)
            {
                return Refused($"there is no quest `#{id}` on this machine.", terminal);
            }

            if (!quest.Deletable) return Refused(Kept(quest), terminal);
            return new HelpPlan(null,
                $"Delete quest `#{quest.Id}` “{quest.Title}”, for `{quest.To}`: it goes from every list, and nothing brings it back.",
                terminal, null);
        }

        if (proposal.Door == "ask")
        {
            var terminal = $"daoris-driver ask --delete {id}";
            if (facts.Asks.FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.OrdinalIgnoreCase)) is not { } ask)
            {
                return Refused($"there is no ask `#{id}` on this machine.", terminal);
            }

            if (!ask.Deletable)
            {
                var standing = ask.Quests
                    .Select(quest => facts.Quests.FirstOrDefault(each => each.Id == quest))
                    .OfType<HelpQuestFacts>()
                    .Where(quest => !quest.Deletable)
                    .Select(quest => $"`#{quest.Id}` is {Spoken(quest)}")
                    .ToList();
                return Refused(
                    $"ask `#{ask.Id}` must stay: a quest it became has been started on"
                    + (standing.Count > 0 ? $" ({string.Join(", ", standing)})" : "")
                    + ", and an ask goes with every quest it became or not at all. Leave it, or close the ask instead, with the reason.",
                    terminal);
            }

            var goes = ask.Quests.Count switch
            {
                0 => ": it became no quest, so it goes alone.",
                1 => $", with the quest it became: {Quest(ask.Quests[0])}.",
                var count => $", with the {count} quests it became: {string.Join(", ", ask.Quests.Select(Quest))}.",
            };
            return new HelpPlan(null, $"Delete ask `#{ask.Id}` “{ask.Sentence}”{goes}", terminal, null);
        }

        return Refused($"`{proposal.Door}` is not a record Daoris deletes — a `quest` or an `ask`.", "");
    }

    /// <summary>Why a quest's record stays, with what to do instead — said as the service's refusal says it (D95).</summary>
    private static string Kept(HelpQuestFacts quest)
    {
        const string Decline = "Decline it instead, with the reason, and the asker hears why.";
        const string Closed = "A closed quest already leaves the list.";
        return Spoken(quest) switch
        {
            "taken" => $"quest `#{quest.Id}` is taken — someone is working it, and its record stays. {Decline}",
            "done" => $"quest `#{quest.Id}` is done — it is the record of work that was answered. {Closed}",
            "declined" => $"quest `#{quest.Id}` is declined — the decline is the trace of a decision, and its reason is the asker's to read. {Closed}",
            _ => $"quest `#{quest.Id}` was started on — a session's record names it, or a taken quest waits on it as its question. {Decline}",
        };
    }

    private static string Spoken(HelpQuestFacts quest) => quest.Status.ToLowerInvariant();

    /// <summary>
    /// An account's own model and effort (HELP6), judged by <c>SET_AGENT_SETTINGS</c>'s rules: a tool whose
    /// settings Daoris knows (D98), one of Daoris's accounts for it, and values the tool reads — its own
    /// aliases or a full model id, and an effort its settings keep, never `max`.
    /// </summary>
    private static HelpPlan Account(HelpProposal proposal, HelpMachineFacts facts)
    {
        var name = proposal.Target?.Trim() ?? "";
        var account = proposal.Account?.Trim() ?? "";
        var model = proposal.Model?.Trim() is { Length: > 0 } m ? m : null;
        var effort = proposal.Effort?.Trim() is { Length: > 0 } e ? e : null;
        if (facts.Doors.FirstOrDefault(each => string.Equals(each.Name, name, StringComparison.Ordinal)) is not { } door)
        {
            return new HelpPlan($"there is no agent `{name}` on this machine — one of {Names(facts.Doors.Select(each => each.Name))}.", "", "", null);
        }

        var owner = door.AccountsOf;
        var terminal = $"daoris agent settings {owner} --account {account}"
            + (model is null ? "" : $" model {model}") + (effort is null ? "" : $" effort {effort}");
        HelpPlan Refused(string why) => new(why, "", terminal, null);

        if (!door.SettingsKnown)
        {
            return Refused($"`{owner}` keeps its settings in files of its own that Daoris does not know the shape of, so Daoris "
                + "offers none — set its model with the tool itself.");
        }

        if (account.Length == 0)
        {
            return Refused($"name the account these settings are for — `{owner}`'s own configuration home is the tool's, and Daoris never touches it.");
        }

        if (!door.Accounts.Contains(account, StringComparer.Ordinal))
        {
            return Refused($"`{owner}` has no account `{account}` on this machine — accounts that exist: "
                + (door.Accounts.Count > 0 ? string.Join(", ", door.Accounts) : "(none)"));
        }

        if (model is null && effort is null) return Refused("the change sets a model, an effort, or both.");
        try
        {
            if (model is not null and not Unset) AgentSettings.JudgeModel(model);
            if (effort is not null and not Unset) AgentSettings.JudgeEffort(effort);
        }
        catch (DriverException refused)
        {
            return Refused(refused.Message);
        }

        static string To(string value) => value == Unset ? "the tool's own default" : $"`{value}`";
        var changes = new List<string>();
        if (model is not null) changes.Add($"model to {To(model)}");
        if (effort is not null) changes.Add($"effort to {To(effort)}");
        return new HelpPlan(null, $"Set `{owner}` account `{account}`'s {string.Join(" and its ", changes)}.", terminal, null);
    }

    /// <summary>The word that returns a key to the tool's own default, as `daoris agent settings` takes it.</summary>
    private const string Unset = "unset";

    /// <summary>A go (HELP6): a place the window has, by <see cref="HelpPlaces"/>. It changes nothing.</summary>
    private static HelpPlan Go(HelpProposal proposal)
    {
        var view = proposal.Target?.Trim().ToLowerInvariant() ?? "";
        var domain = proposal.Domain?.Trim().ToLowerInvariant() is { Length: > 0 } d ? d : null;
        var part = proposal.Part?.Trim().ToLowerInvariant() is { Length: > 0 } p ? p : null;
        HelpPlan Refused(string why) => new(why, "", "", null);

        // A tuple not found is its default, whose names are null.
        var shown = HelpPlaces.Views.FirstOrDefault(each => each.Id == view);
        if (shown.Id is null)
        {
            return Refused($"there is no view `{view}` — one of {Names(HelpPlaces.Views.Select(each => each.Id))}.");
        }

        var describe = $"Open {shown.Name}";
        if (domain is not null)
        {
            if (view != "settings") return Refused("a domain is a part of Settings — name `settings` as the view.");
            var named = HelpPlaces.Domains.FirstOrDefault(each => each.Id == domain);
            if (named.Id is null)
            {
                return Refused($"there is no Settings domain `{domain}` — one of {Names(HelpPlaces.Domains.Select(each => each.Id))}.");
            }

            describe += $" → {named.Name}";
        }

        if (part is not null)
        {
            var within = domain ?? view;
            var parts = HelpPlaces.Parts.Where(each => each.Within == within).ToList();
            var found = parts.FirstOrDefault(each => each.Id == part);
            if (found.Id is null)
            {
                return Refused(parts.Count > 0
                    ? $"there is no part `{part}` of `{within}` — one of {Names(parts.Select(each => each.Id))}."
                    : $"there is no part `{part}` of `{within}`" + (view == "settings" && domain is null ? " — name its Settings domain." : "."));
            }

            // A setup step is a place in the guide, not a card beneath a domain.
            describe += within == "start" ? $" at {found.Name}" : $" → {found.Name}";
        }

        return new HelpPlan(null, describe + ".", "", null) { Go = new HelpPlace(view, domain, part) };
    }

    /// <summary>
    /// The person's Apply (HELP6): the plan made through the door the screen's own route uses, the
    /// proposal settled, and what it did said into the conversation. A refused plan calls no door.
    /// </summary>
    /// <remarks>
    /// <para>A refusal a door raises before anything happened — a busy slot, which the page shows — is let
    /// through unsettled and unsaid, so the card stays for another press; a route's refusal in its own
    /// words settles it.</para>
    ///
    /// <para><b>An agent action's end is said after what the Apply did</b>, however soon it comes: a pin
    /// already installed ends before its start is answered, and its end must not arrive first.</para>
    /// </remarks>
    /// <param name="say">The conversation: what the Apply did, then an agent action's end.</param>
    public static async Task<HelpApplied> ApplyAsync(
        string home, HelpProposal proposal, HelpPlan plan, IHelpDoors doors, Action<string> say, CancellationToken ct)
    {
        var gate = new object();
        var told = false;
        var held = new List<string>();
        void Later(string text)
        {
            lock (gate)
            {
                if (!told)
                {
                    held.Add(text);
                    return;
                }
            }

            say(text);
        }

        var applied = await ApplyOnceAsync(home, proposal, plan, doors, Later, ct).ConfigureAwait(false);
        say(applied.Told);
        List<string> early;
        lock (gate)
        {
            told = true;
            early = [.. held];
        }

        foreach (var text in early) say(text);
        return applied;
    }

    private static async Task<HelpApplied> ApplyOnceAsync(
        string home, HelpProposal proposal, HelpPlan plan, IHelpDoors doors, Action<string> later, CancellationToken ct)
    {
        var id = proposal.Id;
        HelpApplied Settled(bool applied, string told, string? note)
        {
            Settle(home, id, applied ? "applied" : "refused", note);
            return new HelpApplied(applied, told);
        }

        if (plan.Refusal is { } refused) return Settled(false, $"Not applied: `#{id}` — the route refuses it: {refused}", refused);

        switch (proposal.Kind)
        {
            case "ask":
            {
                var answer = await doors.AskAsync(proposal.Workspace!.Trim(), proposal.Sentence!.Trim(), ct).ConfigureAwait(false);
                return Settled(answer.Ok, $"{(answer.Ok ? "Applied" : "Not applied")}: `#{id}` (`{plan.Terminal}`) — {answer.Message}", answer.Message);
            }

            case "delete":
            {
                var target = proposal.Target!.Trim().TrimStart('#');
                var (ok, message) = proposal.Door == "quest"
                    ? await doors.DeleteQuestAsync(target, ct).ConfigureAwait(false)
                    : await doors.DeleteAskAsync(target, ct).ConfigureAwait(false);
                return Settled(ok, $"{(ok ? "Applied" : "Not applied")}: `#{id}` (`{plan.Terminal}`) — {message}", message);
            }

            case "agent":
            {
                var harness = proposal.Target!.Trim();
                var doing = proposal.Door == "pin" ? $"pin of `{harness}` to {proposal.Value?.Trim()}" : $"update of `{harness}`";
                try
                {
                    await doors.StartAgentActionAsync(
                        harness, proposal.Door, proposal.Door == "pin" ? proposal.Value?.Trim() : null,
                        (code, problem) => later(problem is null && code == 0
                            ? $"The {doing} (`#{id}`) finished."
                            : $"The {doing} (`#{id}`) did not finish: {problem ?? $"it ended with exit code {code}"}."),
                        ct).ConfigureAwait(false);
                }
                catch (DriverException error)
                {
                    return Settled(false, $"Not applied: `#{id}` (`{plan.Terminal}`) — {error.Message}", error.Message);
                }

                return Settled(true, $"Started: `#{id}` — {plan.Describe} (`{plan.Terminal}`) Its end is said here when it finishes.", null);
            }

            case "account":
            {
                static AgentSettingEdit? Edit(string? value) =>
                    value?.Trim() is { Length: > 0 } set ? new AgentSettingEdit(set == Unset ? null : set) : null;
                try
                {
                    doors.SetAgentSettings(proposal.Target!.Trim(), proposal.Account!.Trim(), Edit(proposal.Model), Edit(proposal.Effort));
                }
                catch (DriverException error)
                {
                    return Settled(false, $"Not applied: `#{id}` (`{plan.Terminal}`) — {error.Message}", error.Message);
                }

                return Settled(true, $"Applied: `#{id}` — {plan.Describe} (`{plan.Terminal}`)", null);
            }

            case "go":
                return Settled(true, $"Applied: `#{id}` — {plan.Describe} Nothing else changed.", null) with { Go = plan.Go };

            case "plugin":
            {
                // PLUG9: a copy or a row, and nothing started — the loop starts what it runs at its next look.
                try
                {
                    if (proposal.Door == "add") doors.AddPlugin(plan.Source!);
                    else doors.SwitchPlugin(plan.Plugin!.Id, proposal.Door == "enable");
                }
                catch (DriverException error)
                {
                    return Settled(false, $"Not applied: `#{id}` (`{plan.Terminal}`) — {error.Message}", error.Message);
                }

                return Settled(true, $"Applied: `#{id}` — {plan.Describe} (`{plan.Terminal}`)", null);
            }

            case "hand":
            {
                // WSR5b: the review's own press; the plugin pushes, and what it answered is what the person reads.
                TreeHand handed;
                try
                {
                    handed = await doors.HandAsync(plan.Hand!.Repository, plan.Hand.Branch, plan.Hand.Plugin, ct).ConfigureAwait(false);
                }
                catch (DriverException error)
                {
                    return Settled(false, $"Not applied: `#{id}` (`{plan.Terminal}`) — {error.Message}", error.Message);
                }

                return Settled(handed.Handed, $"{(handed.Handed ? "Applied" : "Not applied")}: `#{id}` (`{plan.Terminal}`) — {handed.Message}",
                    handed.Handed ? null : handed.Message);
            }

            default:
                doors.Change(plan.Apply!);
                return Settled(true, $"Applied: `#{id}` — {plan.Describe} (`{plan.Terminal}`)", null);
        }
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$")]
    private static partial System.Text.RegularExpressions.Regex ExactRelease();

    private static string Names(IEnumerable<string> names) =>
        string.Join(", ", names.OrderBy(name => name, StringComparer.Ordinal).Select(name => $"`{name}`"));

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
