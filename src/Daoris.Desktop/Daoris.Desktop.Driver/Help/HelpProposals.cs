using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>One of Ask Daoris's proposals, as the connector wrote it.</summary>
/// <param name="Kind">Which kind it is, one <see cref="HelpProposalKinds"/> registers: `setting`, `ask`, `agent`, `go`, `plugin`, `browser`, `sync`, `repository`, `session`, `pause`, `clear`, `workflow`, `setup` or `role`.</param>
/// <param name="Door">Which of its kind's changes it is, as that kind's class says: a `daoris place`, `daoris settings`, `daoris quest` or `daoris git` verb for a setting, `update` or `pin` for an agent, and so on.</param>
/// <param name="Target">What the change is for, as its kind's class says: a repository, an agent, a record's id, a view, a plugin or a session.</param>
/// <param name="Value">What it is set to, where its kind takes one: a setting's value, a pin's version, a move's workspace.</param>
/// <param name="Session">The conversation that proposed it.</param>
/// <param name="State">`proposed`, then `applied`, `dismissed` or `refused`.</param>
/// <remarks>A field only one kind carries is read by that kind's judge in its file; the fields stay in this one declaration, under a comment per kind.</remarks>
public sealed record HelpProposal(
    string Id, string Kind, string Door, string? Target, string? Workspace, string? Value, string? Sentence,
    string Why, string? Session, string State)
{
    /// <summary>
    /// The repository whose checkout holds a plugin to add, or null for a whole path the person gave.
    /// </summary>
    public string? Repository { get; init; }

    // The ask kind's.

    /// <summary>
    /// The review choice the person said for an ask's work, as the composer sends it: <c>on</c>, an environment's name
    /// or <c>off</c>; null where none is said, and each repository's rule decides.
    /// </summary>
    public string? Review { get; init; }

    /// <summary>The person's words with an ask's review choice, sent only with one.</summary>
    public string? ReviewWords { get; init; }

    /// <summary>An ask's short title, at most 40 characters, as the service's box writes it; null where none is given.</summary>
    public string? ShortTitle { get; init; }

    // The browser kind's.

    /// <summary>A favorite's title, for a browser proposal that keeps one, or null for the page's host.</summary>
    public string? Title { get; init; }

    // The clear kind's.

    /// <summary>
    /// The units a clear proposal's card listed, held in its file from its first draw: what its Apply sends.
    /// Null before the card listed anything.
    /// </summary>
    public IReadOnlyList<HistoryUnitName>? Units { get; init; }

    // The go kind's.

    /// <summary>A go's Settings domain, or null.</summary>
    public string? Domain { get; init; }

    /// <summary>A go's part of its domain or view — a card, a setup step, a drawer — or null.</summary>
    public string? Part { get; init; }

    /// <summary>A go's one quest or ask or session, as the service's box wrote it, or null.</summary>
    public string? Item { get; init; }

    // The plugin kind's.

    /// <summary>A plugin's folder to add from: from that checkout's root, or a whole path.</summary>
    public string? Folder { get; init; }

    /// <summary>One of the install's own plugins to add, by its id — never a path on this machine.</summary>
    public string? Offer { get; init; }

    /// <summary>
    /// The server an <c>approve</c> or a <c>withdraw</c> names, as the plugin declares it for Ask Daoris; null where
    /// the plugin offers it one only.
    /// </summary>
    public string? Server { get; init; }

    /// <summary>The writes an <c>approve</c> accepts, each by name; null or empty accepts none, its reads only.</summary>
    public IReadOnlyList<string>? Writes { get; init; }

    // The role kind's.

    /// <summary>A role proposal's own <c>press</c>: its names, work, scope, choices and the revision read; null for every other kind.</summary>
    public System.Text.Json.JsonElement? RolePress { get; init; }

    // The setup kind's.

    /// <summary>The place's quick set-up a <c>place</c> proposal asks, from the file's <c>place</c>; null for every other door and kind.</summary>
    public PlaceAsk? Place { get; init; }

    // The sync kind's.

    /// <summary>The rows a sync proposal's look listed, kept in its file; null before the person looked.</summary>
    public IReadOnlyList<HelpSyncRow>? Listed { get; init; }

    /// <summary>What a sync proposal's look kept beside its rows; nothing before the person looked.</summary>
    public HelpSyncBesides Besides { get; init; } = HelpSyncBesides.None;

    // The workflow kind's.

    /// <summary>A workflow proposal's repository or workspace, one word, which its judge finds which of.</summary>
    public string? Scope { get; init; }

    /// <summary>A workflow proposal's workflow, by its id.</summary>
    public string? Workflow { get; init; }

    /// <summary>The version a workflow proposal's edit is made on; null for a new workflow.</summary>
    public int? Base { get; init; }

    /// <summary>Whether a workflow proposal also chooses the workflow for its scope (the <c>use</c> door).</summary>
    public bool Choose { get; init; }

    /// <summary>A workflow proposal's whole steps, a JSON list, as the box wrote them; null where none are.</summary>
    public JsonElement? Steps { get; init; }

    /// <summary>A new workflow's name, the person's words; null where the box wrote none, and the id names it.</summary>
    public string? Name { get; init; }

    /// <summary>
    /// A workflow proposal's graph version, the graph's JSON object as the box wrote it;
    /// null where it carries steps or no version.
    /// </summary>
    public JsonElement? Graph { get; init; }

    /// <summary>
    /// The route a workflow proposal chooses its workflow for: <c>default</c>, <c>landed</c> or <c>kind:&lt;id&gt;</c>;
    /// null where it names none, and <see cref="Choose"/> asks the default.
    /// </summary>
    public string? Route { get; init; }

    /// <summary>
    /// The fingerprint of the draft open in the person's editor a workflow proposal changes, as the box wrote it: the
    /// <c>draft</c> door; null for every other.
    /// </summary>
    public string? Draft { get; init; }

    /// <summary>
    /// The changes of a <c>draft</c> card the person's press says were taken into the draft, and those left,
    /// by their ids: the press's, never the file's; null where the press names none, which opens the editor.
    /// </summary>
    public IReadOnlyList<string>? Taken { get; init; }

    /// <inheritdoc cref="Taken"/>
    public IReadOnlyList<string>? Left { get; init; }
}

/// <summary>What the machine holds that a proposal is judged against: names only.</summary>
/// <remarks>A fact only one kind reads is read by that kind's judge in its file; the facts stay in this one declaration, under a comment per kind.</remarks>
public sealed record HelpMachineFacts(
    IReadOnlyCollection<string> Repositories, IReadOnlyCollection<string> Workspaces, IReadOnlyCollection<string> Agents)
{
    // Every kind's.

    /// <summary>
    /// The workspace the conversation that proposed it belongs to, read from its record (<see cref="HelpCircle.OfRecord"/>): the
    /// focus of what a list shows first, no limit on what a proposal names. Null for <c>default</c>'s room, the whole machine.
    /// </summary>
    public string? Circle { get; init; }

    /// <summary>The git identities on this machine (labels and hosts, never a key or a token), for a <c>git-use</c> door's judge.</summary>
    public IReadOnlyList<HelpGitIdentity> GitIdentities { get; init; } = [];

    /// <summary>This machine's plugins, for a landing rule that names one and a plugin proposal.</summary>
    public PluginCatalog Plugins { get; init; } = PluginCatalog.None;

    // The browser kind's.

    /// <summary>What Daoris's browser's files hold, read by the desktop for a browser proposal; null where none is read.</summary>
    public HelpBrowserFacts? Browser { get; init; }

    // The clear kind's.

    /// <summary>
    /// What this machine keeps of each quest, ask and workspace a clear proposal names, as <c>HISTORY_PLAN</c> answers it
    /// (<see cref="HistoryClearing.PlanAsync"/>): what a clear proposal is judged by. Read only for one.
    /// </summary>
    public IReadOnlyList<HistoryPlan> History { get; init; } = [];

    // The go kind's.

    /// <summary>The quests, closed ones included, which a go naming one is judged against; read only for one.</summary>
    public IReadOnlyList<HelpQuestFacts> Quests { get; init; } = [];

    /// <summary>The asks, closed ones included, which a go naming one is judged against; read only for one.</summary>
    public IReadOnlyList<HelpAskFacts> Asks { get; init; } = [];

    /// <summary>
    /// This machine's own session records, closed ones included, as the Sessions list reads them: what a go naming a session
    /// is judged against; read only for one.
    /// </summary>
    public IReadOnlyList<SessionRecord> Sessions { get; init; } = [];

    // The plugin kind's.

    /// <summary>The Daoris home, which a plugin is never added from; null where none is named.</summary>
    public string? Home { get; init; }

    /// <summary>Each registered repository's checkout on this machine, or null where it has none.</summary>
    public IReadOnlyDictionary<string, string?> Checkouts { get; init; } = new Dictionary<string, string?>();

    /// <summary>The harness names this build carries, which a plugin may not declare.</summary>
    public IReadOnlyCollection<string> Reserved { get; init; } = [];

    /// <summary>The install's offers folder, where an offer is found and an offer's update reads; null is none.</summary>
    public string? OffersFolder { get; init; }

    /// <summary>The install's offers, as the Plugins place lists them under Daoris's own plugins.</summary>
    public IReadOnlyList<PluginOffer> Offers { get; init; } = [];

    // The rule views'.

    /// <summary>
    /// The quests the loop's last tick parked by their failed sessions, which a retry must name: the verdict
    /// the quest drawer shows its Retry by. Empty before any tick, and while another loop holds the home.
    /// </summary>
    public IReadOnlyList<ParkedQuest> Parked { get; init; } = [];

    /// <summary>
    /// The quests the loop's last look held by the person's stop, each with the session they stopped: a retry of
    /// one releases that stop. Empty before any look, and while another loop holds the home.
    /// </summary>
    public IReadOnlyList<HeldQuest> Held { get; init; } = [];

    /// <summary>
    /// Each registered repository with its workspace and its checkout here, or none: what a <c>review</c>'s
    /// procedure is looked for in, the one repository it names or each of the workspace's, and where a go or a workflow finds a repository's workspace. Empty where not asked.
    /// </summary>
    public IReadOnlyList<(string Repository, string? Workspace, string? Root)> Registered { get; init; } = [];

    // The setup kind's.

    /// <summary>
    /// A place's plan for each <c>place</c> proposal, by the proposal's id: <see cref="PlacePlans.PlanAsync"/> over its ask,
    /// as <c>daoris place … --plan</c> prints it. Read only for one; nothing is written.
    /// </summary>
    public IReadOnlyDictionary<string, PlacePlanned> Places { get; init; } = new Dictionary<string, PlacePlanned>();

    // The workflow kind's.

    /// <summary>
    /// The named workflows saved under the home, each as found (<see cref="WorkflowStore.List"/>): what a workflow proposal's base
    /// is held to. Read only for one.
    /// </summary>
    public IReadOnlyList<WorkflowFileFound> Workflows { get; init; } = [];

    /// <summary>
    /// The runs bound on this machine (<see cref="WorkflowRunBindings"/>): how many keep the version they started on, which a
    /// workflow card says. Read only for a workflow proposal.
    /// </summary>
    public IReadOnlyList<WorkflowRunBinding> WorkflowRuns { get; init; } = [];

    /// <summary>
    /// The gates each registered repository's accepted integration declaration names, by repository: what a workflow card
    /// that chooses a graph asking with <c>done</c> or <c>steward</c> is judged by (<see cref="IntegrateChoice.ReadAcceptedAsync"/>). Read
    /// only for one; a repository with no entry is unread, and the card holds on that.
    /// </summary>
    public IReadOnlyDictionary<string, AcceptedGates> AcceptedGates { get; init; } = new Dictionary<string, AcceptedGates>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The workflow drafts the proposing conversation's record says it has been told, by fingerprint (<see cref="WorkflowDraftTold.Told"/>):
    /// what a <c>draft</c> proposal is judged against. Read only for one.
    /// </summary>
    public IReadOnlyDictionary<string, WorkflowDraftTold> Drafts { get; init; } = new Dictionary<string, WorkflowDraftTold>(StringComparer.Ordinal);
}

/// <summary>
/// A proposal judged: what it changes, the terminal command that does the same, and the edit to
/// make — or the route's refusal, and nothing to make.
/// </summary>
/// <param name="Apply">The edit to the driver's file, for a setting the route takes; null for every other kind or a refusal.</param>
/// <param name="Terminal">The command that does the same; empty for a go, which changes nothing.</param>
/// <remarks>What only one kind's card or Apply needs is read by that kind in its file; the members stay in this one declaration, under a comment per kind.</remarks>
public sealed record HelpPlan(string? Refusal, string Describe, string Terminal, Func<DriverConfig, DriverConfig>? Apply)
{
    // The clear kind's.

    /// <summary>What a clear proposal's Apply sends; null where the card changes nothing, and for every other kind.</summary>
    public HelpClearPlan? Clear { get; init; }

    // The go kind's.

    /// <summary>Where a go takes the person, for one the window has; null for every other kind.</summary>
    public HelpPlace? Go { get; init; }

    // The plugin kind's.

    /// <summary>What a plugin proposal's plugin runs, as its manifest writes it; null for every other kind.</summary>
    public HelpPluginView? Plugin { get; init; }

    /// <summary>The folder a plugin is added from, resolved on this machine; null for every other plan.</summary>
    public string? Source { get; init; }

    /// <summary>What an approval or a withdrawal names, every tool; null for every other plan.</summary>
    public HelpGrantView? Grant { get; init; }

    // The role kind's.

    /// <summary>For a role change the judge takes: the change, its choices and the revision read; null for every other kind.</summary>
    public HelpRoleChange? Role { get; init; }

    /// <summary>For the supervisor's rules at a place: the rules before and after as fields, which the page words from its catalogue; null for every other plan.</summary>
    public HelpRulesView? Rules { get; init; }

    /// <summary>For a supervisor set: its kinds, acts, holder and window as fields, which the page words from its catalogue; null for every other plan.</summary>
    public HelpSupervisorView? Supervisor { get; init; }

    // The setup kind's.

    /// <summary>For a <c>place</c> the plan allows: the ask, the workspace and what the plan changes, for the card; null for a refusal and every other kind.</summary>
    public HelpPlaceSetUp? Place { get; init; }

    // The sync kind's.

    /// <summary>What a sync card shows; null for every other kind.</summary>
    public HelpSyncPlan? Sync { get; init; }

    // The workflow kind's.

    /// <summary>A workflow proposal's card data, what its Apply saves and chooses; null for every other kind.</summary>
    public HelpWorkflowCard? Workflow { get; init; }
}

/// <summary>What the person's Apply did: whether it applied, and what the conversation is told.</summary>
public sealed record HelpApplied(bool Applied, string Told)
{
    // The go kind's.

    /// <summary>Where a go takes the person: the page navigates there, as its starters' doors do.</summary>
    public HelpPlace? Go { get; init; }

    // The sync kind's.

    /// <summary>
    /// The proposal still waits for the person: a sync card's look that listed rows, which settles nothing, so
    /// the card stays for its press and the page logs no settlement. False for every Apply that settles.
    /// </summary>
    public bool Stands { get; init; }
}

/// <summary>
/// The doors a proposal is applied through: each the code the screen's own route uses, so an
/// Apply is what the screen would have done, never a second path.
/// </summary>
/// <remarks>Each kind's file declares the doors its Apply goes through as its own interface, gathered here.</remarks>
public interface IHelpDoors :
    IHelpAskDoors, IHelpBrowserDoors, IHelpClearDoors,
    IHelpPluginDoors, IHelpConfigDoors, IHelpSyncDoors
{
}

/// <summary>
/// Ask Daoris's proposals, the driver's half: the files the connector's <c>*_propose</c> tools
/// wrote under the home, judged with the route's own code and settled by the person's press.
/// </summary>
/// <remarks>
/// <para><b>Judged by the route, not by the door that wrote it.</b> The service checks a proposal's
/// shape; whether the route takes it — a repository registered here, a line git accepts, a pattern
/// naming one branch per session, an agent this machine has — is the config's own edits and this
/// machine's names, run here. A proposal the route would refuse is never shown to the person.</para>
///
/// <para><b>By kind.</b> Each kind is a class of its own, registered in <see cref="HelpProposalKinds"/>;
/// this reads, dispatches and settles, and knows no kind.</para>
///
/// <para><b>THE FILE is the contract</b>, the twin of the service's <c>HelpProposalBox</c>: they share no
/// code, and each side's tests hold the same shape.</para>
/// </remarks>
public static class HelpProposals
{
    public static string FolderOf(string home) => DaorisHome.Entry(home, HomeEntry.HelpProposals);

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
                using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
                var root = document.RootElement;
                var proposal = Read(root, Path.GetFileNameWithoutExtension(path));
                if (proposal.State != "proposed" || !string.Equals(proposal.Session, session, StringComparison.OrdinalIgnoreCase)) continue;
                var at = Moments.Read(root, "proposed") ?? DateTimeOffset.MinValue;
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
        var node = JsonNode.Parse(AtomicFile.ReadText(path))!.AsObject();
        node["state"] = state;
        node["note"] = note;
        JsonFile.Write(path, node);
    }

    /// <summary>
    /// The person's press beside a card's Apply: the proposal settled <c>dismissed</c>, so no window draws its card again,
    /// and what the conversation is told of it, or null for nothing. A change's <i>Not now</i> is the person's answer, told as their
    /// next message so the agent knows. A go changes nothing, so its <i>Dismiss</i> only takes the card out of the
    /// conversation and is told to nobody: said as the person's words, it started a turn in which the agent could take
    /// it for their word on the quest or ask the card named, and act on that.
    /// </summary>
    public static string? Dismiss(string home, HelpProposal proposal)
    {
        Settle(home, proposal.Id, "dismissed", null);
        return proposal.Kind == "go" ? null : $"Not now: the person did not apply `#{proposal.Id}`.";
    }

    /// <summary>How long a claim is believed: a press longer than this is not one (a sync's replay is bounded far below it).</summary>
    private static readonly TimeSpan ClaimLife = TimeSpan.FromHours(1);

    /// <summary>
    /// Claim a proposal for its Apply: a file made only if none is there, so of two Applies at once, from two windows,
    /// one holds it and runs the proposal and the other gets null. Dispose lets it go, whether the proposal settled or stands for
    /// another press. A claim older than <see cref="ClaimLife"/> is left by a run that died, and is taken over.
    /// </summary>
    public static IDisposable? Claim(string home, string id)
    {
        var path = Path.Combine(FolderOf(home), $"{Path.GetFileName(id)}.claim");
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                Directory.CreateDirectory(FolderOf(home));
                var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 1, FileOptions.DeleteOnClose);
                return stream;
            }
            catch (IOException) when (File.Exists(path))
            {
                try
                {
                    if (DateTime.UtcNow - File.GetLastWriteTimeUtc(path) < ClaimLife) return null;
                    File.Delete(path);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    return null;
                }
            }
            catch (UnauthorizedAccessException) when (File.Exists(path))
            {
                // A claim being deleted by the run that let it go: held, for this press.
                return null;
            }
        }

        return null;
    }

    /// <summary>One proposal, found by its id, whatever its state — what Apply and Not now read.</summary>
    public static HelpProposal? Find(string home, string id)
    {
        var path = Path.Combine(FolderOf(home), $"{Path.GetFileName(id)}.json");
        if (!File.Exists(path)) return null;
        using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
        return Read(document.RootElement, id);
    }

    /// <summary>
    /// One file as the service's box writes it. A field a kind does not carry reads as null, and so does
    /// one an older file lacks: absence is "not named".
    /// </summary>
    /// <remarks>Every kind reads its own fields from every file, whatever the file's kind, so that stays so.</remarks>
    private static HelpProposal Read(JsonElement root, string id)
    {
        var read = new HelpProposal(
            Json.Text(root, "id") ?? id, Json.Text(root, "kind") ?? "", Json.Text(root, "door") ?? "", Json.Text(root, "target"),
            Json.Text(root, "workspace"), Json.Text(root, "value"), Json.Text(root, "sentence"), Json.Text(root, "why") ?? "",
            root.TryGetProperty("by", out var by) ? Json.Text(by, "session") : null, Json.Text(root, "state") ?? "")
        {
            Repository = Json.Text(root, "repository"),
        };
        return HelpProposalKinds.All.Aggregate(read, (proposal, kind) => kind.Read(proposal, root));
    }

    /// <summary>
    /// What the person's Apply of <paramref name="proposal"/> acts on, by its kind's door: what the Apply
    /// marks the owner's for the run before its edit.
    /// </summary>
    public static HelpOwned Owned(HelpProposal proposal) => HelpProposalKinds.Owned(proposal);

    /// <summary>The doors <paramref name="kind"/> takes, as its file spells them; none for a kind none judges. What the owner's census sweep reads.</summary>
    public static IReadOnlyList<string> DoorsOf(string kind) => HelpProposalKinds.Find(kind)?.Doors ?? [];

    /// <summary>Judge a proposal with the route's own code, against the machine as it stands, by its kind; the workspace it names is its kind's to judge.</summary>
    public static HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts) =>
        HelpProposalKinds.Find(proposal.Kind) is not { } kind
            ? new HelpPlan(Unknown(proposal.Kind), "", "", null)
            : kind.Plan(proposal, config, facts);

    /// <summary>
    /// A card drawn for the person: <c>HELP_PROPOSALS</c> calls this for each proposal it shows, with the plan it
    /// showed, so a kind whose Apply sends exactly what its card listed holds that in the proposal's file. A refused plan is
    /// never shown, and holds nothing.
    /// </summary>
    public static void Drawn(string home, HelpProposal proposal, HelpPlan plan)
    {
        if (plan.Refusal is null) HelpProposalKinds.Find(proposal.Kind)?.Drawn(home, proposal, plan);
    }

    /// <summary>
    /// The person's Apply: the plan made through the door the screen's own route uses, the
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

    /// <summary>
    /// Keep what an Apply published with the conversation that proposed it (<see cref="HelpPublished"/>), by the ids the
    /// service's answer named, as soon as it answered and before the card settles (<see cref="HelpApplying.Settled"/>). A keep that
    /// fails loses the list's entry, never the Apply, which happened: the quest and the ask stand, and Quests lists them.
    /// </summary>
    internal static void KeepPublished(string home, HelpProposal proposal, HelpPublishing made)
    {
        if (proposal.Session is not { } session || !SessionEventText.IsId(session)) return;
        try
        {
            new HelpPublished(home).Keep(session, new HelpPublishedEntry(DateTimeOffset.UtcNow, proposal.Id, proposal.Kind, made.Workspace)
            {
                Ask = made.Ask,
                Quest = made.Quest,
            });
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Only this conversation's list is short by one, as a file that does not read is.
        }
    }

    private static async Task<HelpApplied> ApplyOnceAsync(
        string home, HelpProposal proposal, HelpPlan plan, IHelpDoors doors, Action<string> later, CancellationToken ct)
    {
        var applying = new HelpApplying(home, proposal, plan, doors, later);
        if (plan.Refusal is { } refused) return applying.Settled(false, $"Not applied: `#{proposal.Id}` — the route refuses it: {refused}", refused);

        // A kind no class judges is refused by Plan, so it never arrives here unrefused; were it to, it is settled refused, never applied.
        if (HelpProposalKinds.Find(proposal.Kind) is not { } kind)
        {
            var unknown = Unknown(proposal.Kind);
            return applying.Settled(false, $"Not applied: `#{proposal.Id}` — the route refuses it: {unknown}", unknown);
        }

        return await kind.ApplyAsync(applying, ct).ConfigureAwait(false);
    }

    private static string Unknown(string kind) => $"`{kind}` is not a change Ask Daoris proposes.";

    internal static string Names(IEnumerable<string> names) =>
        string.Join(", ", names.OrderBy(name => name, StringComparer.Ordinal).Select(name => $"`{name}`"));
}
