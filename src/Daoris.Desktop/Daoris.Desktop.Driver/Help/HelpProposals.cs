using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>One of Ask Daoris's proposals, as the connector wrote it (HELP1c, D89; HELP6; PLUG9; WSR5b; HELP10).</summary>
/// <param name="Kind">Which kind it is, one <see cref="HelpProposalKinds"/> registers: `setting`, `ask`, `agent`, `delete`, `account`, `go`, `plugin`, `hand`, `browser`, `sync`, `accept`, `repository`, `session`, `pause`, `clear`, `workflow` or `setup`.</param>
/// <param name="Door">Which of its kind's changes it is, as that kind's class says: a `daoris driver` verb for a setting, `update` or `pin` for an agent, and so on.</param>
/// <param name="Target">What the change is for, as its kind's class says: a repository, an agent, a record's id, a view, a plugin, a session or a branch.</param>
/// <param name="Value">What it is set to, where its kind takes one: a setting's value, a pin's version, a hand-off's plugin, a move's workspace.</param>
/// <param name="Session">The conversation that proposed it.</param>
/// <param name="State">`proposed`, then `applied`, `dismissed` or `refused`.</param>
/// <remarks>MOD6: a field only one kind carries is read by that kind's judge in its file; PART4 keeps the fields in this one declaration, under a comment per kind.</remarks>
public sealed record HelpProposal(
    string Id, string Kind, string Door, string? Target, string? Workspace, string? Value, string? Sentence,
    string Why, string? Session, string State)
{
    /// <summary>
    /// The repository whose checkout holds a plugin to add (PLUG9), or null for a whole path the person gave;
    /// for a hand-off (WSR5b), the repository its branch is in, where a name alone is in several.
    /// </summary>
    public string? Repository { get; init; }

    // The account kind's.

    /// <summary>An account's name, for an account's settings (HELP6).</summary>
    public string? Account { get; init; }

    /// <summary>The model an account's settings would set — `unset` for the tool's own default — or null to leave it.</summary>
    public string? Model { get; init; }

    /// <summary>The effort an account's settings would set — `unset` for the tool's own default — or null to leave it.</summary>
    public string? Effort { get; init; }

    // The agent kind's: the <c>use</c> door's fields (TOOL4g), as <see cref="HelpAgentProposals.Read"/> reads them from a file.

    /// <summary>How a scope's list is used, as the proposal names it; null where it names nothing of it.</summary>
    public UseChange? AccountUse { get; init; }

    /// <summary>
    /// The <c>use</c> door's curve where the file's breaks a rule (SWITCH1d): the twins' sentence, which its card refuses with;
    /// null where it names no curve or a curve that reads.
    /// </summary>
    public string? AccountCurveRefused { get; init; }

    /// <summary>The <c>order</c> door's list or clear, as the file names them; null for any other door (TOOL4l-b).</summary>
    public AccountOrder? AccountOrder { get; init; }

    /// <summary>The <c>ready</c> door's account: an id, or <c>own</c> for the tool's own sign-in; null for any other door (TOOL4l-b).</summary>
    public string? ReadyAccount { get; init; }

    // The ask kind's.

    /// <summary>
    /// ENTRY1c: the review choice the person said for an ask's work, as the composer sends it: <c>on</c>, an environment's name
    /// or <c>off</c>; null where none was said, and each repository's rule decides.
    /// </summary>
    public string? Review { get; init; }

    /// <summary>ENTRY1c: the person's words with an ask's review choice, sent only with one.</summary>
    public string? ReviewWords { get; init; }

    /// <summary>HEAD2e: an ask's short title, at most 40 characters, as the service's box wrote it; null where none was given.</summary>
    public string? ShortTitle { get; init; }

    // The browser kind's.

    /// <summary>A favorite's title, for a browser proposal that keeps one (HELP10), or null for the page's host.</summary>
    public string? Title { get; init; }

    // The clear kind's.

    /// <summary>
    /// The units a clear proposal's card listed, held in its file from its first draw (HIST1f-b, design §5): what its Apply sends.
    /// Null before the card listed anything.
    /// </summary>
    public IReadOnlyList<HistoryUnitName>? Units { get; init; }

    // The go kind's.

    /// <summary>A go's Settings domain, or null.</summary>
    public string? Domain { get; init; }

    /// <summary>A go's part of its domain or view — a card, a setup step, a drawer — or null.</summary>
    public string? Part { get; init; }

    /// <summary>A go's one quest or ask (ENTRY1f1) or session (ENTRY1f2), as the service's box wrote it, or null.</summary>
    public string? Item { get; init; }

    // The plugin kind's.

    /// <summary>A plugin's folder to add from: from that checkout's root, or a whole path (PLUG9).</summary>
    public string? Folder { get; init; }

    /// <summary>One of the install's own plugins to add, by its id (PLUG9 d) — never a path on this machine.</summary>
    public string? Offer { get; init; }

    /// <summary>
    /// The server an <c>approve</c> or a <c>withdraw</c> names (HELPPLUG1c), as the plugin declares it for Ask Daoris; null where
    /// the plugin offers it one only.
    /// </summary>
    public string? Server { get; init; }

    /// <summary>The writes an <c>approve</c> accepts, each by name (HELPPLUG1c); null or empty accepts none, its reads only.</summary>
    public IReadOnlyList<string>? Writes { get; init; }

    // The sync kind's.

    /// <summary>The rows a sync proposal's look listed (HELP10), kept in its file; null before the person looked.</summary>
    public IReadOnlyList<HelpSyncRow>? Listed { get; init; }

    /// <summary>What a sync proposal's look kept beside its rows (LEFT3 b); nothing before the person looked.</summary>
    public HelpSyncBesides Besides { get; init; } = HelpSyncBesides.None;

    // The workflow kind's.

    /// <summary>A workflow proposal's repository or workspace, one word (WORKFLOW1h-a's box), which its judge finds which of.</summary>
    public string? Scope { get; init; }

    /// <summary>A workflow proposal's workflow, by its id.</summary>
    public string? Workflow { get; init; }

    /// <summary>The version a workflow proposal's edit is made on; null for a new workflow.</summary>
    public int? Base { get; init; }

    /// <summary>Whether a workflow proposal also chooses the workflow for its scope (the <c>use</c> door).</summary>
    public bool Choose { get; init; }

    /// <summary>A workflow proposal's whole steps, the design's JSON list (§2.5), as the box wrote them; null where none are.</summary>
    public JsonElement? Steps { get; init; }

    /// <summary>A new workflow's name, the person's words (WORKFLOW1h-name); null where the box wrote none, and the id names it.</summary>
    public string? Name { get; init; }
}

/// <summary>What the machine holds that a proposal is judged against: names only.</summary>
/// <remarks>MOD6: a fact only one kind reads is read by that kind's judge in its file; PART4 keeps the facts in this one declaration, under a comment per kind.</remarks>
public sealed record HelpMachineFacts(
    IReadOnlyCollection<string> Repositories, IReadOnlyCollection<string> Workspaces, IReadOnlyCollection<string> Agents)
{
    // Every kind's.

    /// <summary>
    /// The workspace the conversation that proposed it is bound to (ASKSCOPE1c, D158's ASKSCOPE1 note point 5), read from its record
    /// (<see cref="HelpCircle.OfRecord"/>): a proposal naming another workspace, a repository of another, or every workspace at once
    /// is refused before its kind judges it (<see cref="HelpProposalScope"/>). Null for <c>default</c>'s room, the whole machine.
    /// </summary>
    public string? Circle { get; init; }

    /// <summary>Each door as the Agents screen's roster reads it (HELP6), for an agent's update or pin and an account's settings.</summary>
    public IReadOnlyList<HelpDoorFacts> Doors { get; init; } = [];

    /// <summary>This machine's plugins, for a landing rule that names one (HELP8, D100), a plugin proposal (PLUG9) and a hand-off (WSR5b).</summary>
    public PluginCatalog Plugins { get; init; } = PluginCatalog.None;

    // The accept kind's.

    /// <summary>
    /// Every quest as the service answers it, closed ones included, with what the person required, how each done answered
    /// and whether a departure holds it (DRIFT1d2): what an accept is judged against, read only when one is pending.
    /// </summary>
    public IReadOnlyList<QuestView> QuestRecords { get; init; } = [];

    // The agent kind's: the machine's wiring a proposal is judged against (TOOL4g), <c>harnesses.json</c> as the screen reads it.

    /// <summary>The lists, defaults and settings, for a default's scope and a use's; null where nothing was read.</summary>
    public HarnessSettings? Wiring { get; init; }

    /// <summary>The accounts cooling now, for a <c>ready</c>'s judge (TOOL4l-b); none where nothing was read.</summary>
    public IReadOnlyList<CoolingEntry> Cooling { get; init; } = [];

    // The browser kind's.

    /// <summary>What Daoris's browser's files hold (HELP10), read by the desktop for a browser proposal; null where none was read.</summary>
    public HelpBrowserFacts? Browser { get; init; }

    // The clear kind's.

    /// <summary>
    /// What this machine keeps of each quest, ask and workspace a clear proposal names, as <c>HISTORY_PLAN</c> answers it
    /// (<see cref="HistoryClearing.PlanAsync"/>, HIST1f-b): what a clear proposal is judged by. Read only for one.
    /// </summary>
    public IReadOnlyList<HistoryPlan> History { get; init; } = [];

    // The delete kind's.

    /// <summary>The quests, closed ones included, each with the service's own reading of whether it may be deleted.</summary>
    public IReadOnlyList<HelpQuestFacts> Quests { get; init; } = [];

    /// <summary>The asks, closed ones included, each with the service's own reading of whether it may be deleted.</summary>
    public IReadOnlyList<HelpAskFacts> Asks { get; init; } = [];

    /// <summary>
    /// The sessions the ledger would delete whose tree or landing this machine still holds (SESSUX1h2b, D126 §5.4): the
    /// machine's half, as <see cref="SessionDeletion.Kept"/> reads it. Read only for a session's delete.
    /// </summary>
    public IReadOnlySet<string> Kept { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// The ledger's judgement of deleting each session a delete names (SESSUX1h2b, D126 §5.4), by id: the record's half, its
    /// refusal in its own sentence, as <see cref="ServiceClient.JudgeSessionDeleteAsync"/> answers it. Read only for one.
    /// </summary>
    public IReadOnlyDictionary<string, SessionDeleteAnswer> SessionDeletes { get; init; } = new Dictionary<string, SessionDeleteAnswer>(StringComparer.Ordinal);

    // The go kind's.

    /// <summary>
    /// This machine's own session records, closed ones included, as the Sessions list reads them: what a go naming a session
    /// is judged against (ENTRY1f2), and since SESSUX1h2b a session proposal and a session's delete; read only for those.
    /// </summary>
    public IReadOnlyList<SessionRecord> Sessions { get; init; } = [];

    // The hand kind's.

    /// <summary>The branches this machine's landings made and recorded, for a hand-off (WSR5b).</summary>
    public IReadOnlyList<LandedBranch> Landed { get; init; } = [];

    // The pause kind's.

    /// <summary>
    /// The work of each ask and quest a pause proposal names, with what a pause would do with each piece (PAUSE1f-b, D132 §7.4):
    /// <see cref="WorkPausing.PlanAsync"/>'s answer for the id read as an ask's and as a quest's, each that this machine has.
    /// What a pause proposal is judged by, beside the pauses <c>driver.json</c> keeps. Read only for one.
    /// </summary>
    public IReadOnlyList<WorkPlan> Work { get; init; } = [];

    /// <summary>
    /// The workspaces whose quests this machine syncs with a remote (D48 §5), every one where the environment names the remote
    /// whole: where another machine may still take an open quest a pause holds here (design §2.6, §5.1), as the page's
    /// <c>wiredFor</c> reads the wiring. Read only for a pause proposal.
    /// </summary>
    public IReadOnlyCollection<string> Wired { get; init; } = [];

    // The plugin kind's.

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

    // The session kind's.

    /// <summary>
    /// Where Sessions' list places each of this machine's sessions, with its archive mark (SESSUX1h2b, D126 §2.4, §5.2): what a
    /// session proposal is judged by, as <see cref="SessionArchive"/> judges an archive. Read only for one.
    /// </summary>
    public IReadOnlyList<SessionGrouping> Placed { get; init; } = [];

    // The setting kind's.

    /// <summary>
    /// The quests the loop's last tick parked by their failed sessions (HELP10), which a retry must name: the verdict
    /// the quest drawer shows its Retry by. Empty before any tick, and while another loop holds the home (D104).
    /// </summary>
    public IReadOnlyList<ParkedQuest> Parked { get; init; } = [];

    /// <summary>
    /// The quests the loop's last look held by the person's stop (SESSUX1b), each with the session they stopped: a retry of
    /// one releases that stop. Empty before any look, and while another loop holds the home (D104).
    /// </summary>
    public IReadOnlyList<HeldQuest> Held { get; init; } = [];

    /// <summary>
    /// Each registered repository with its workspace and its checkout here, or none (REVIEWENV1a): what a <c>review</c>'s
    /// procedure is looked for in, the one repository it names or each of the workspace's. Empty where it was not asked.
    /// </summary>
    public IReadOnlyList<(string Repository, string? Workspace, string? Root)> Registered { get; init; } = [];

    // The setup kind's.

    /// <summary>
    /// What a press would do for each repository a set-up proposal names (LAYOUT8), <see cref="SetupPress.PlanAsync"/>'s plan as
    /// <c>daoris-driver setup &lt;repository&gt; --plan</c> prints it, refusals and all: what a set-up proposal is judged by and
    /// what its Apply presses. Read only for one, since a plan reads the line with git and asks the doctrine tool its version.
    /// </summary>
    public IReadOnlyList<SetupPlan> Setups { get; init; } = [];

    // The workflow kind's.

    /// <summary>
    /// The named workflows saved under the home, each as found (<see cref="WorkflowStore.List"/>): what a workflow proposal's base
    /// is held to (WORKFLOW1h-b). Read only for one.
    /// </summary>
    public IReadOnlyList<WorkflowFileFound> Workflows { get; init; } = [];

    /// <summary>
    /// The runs bound on this machine (<see cref="WorkflowRunBindings"/>): how many keep the version they started on, which a
    /// workflow card says (design §8.2). Read only for a workflow proposal.
    /// </summary>
    public IReadOnlyList<WorkflowRunBinding> WorkflowRuns { get; init; } = [];
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

/// <summary>
/// A proposal judged: what it changes, the terminal command that does the same (D50), and the edit to
/// make — or the route's refusal, and nothing to make.
/// </summary>
/// <param name="Apply">The edit to the driver's file, for a setting the route takes; null for every other kind or a refusal.</param>
/// <param name="Terminal">The command that does the same; empty for a go, which changes nothing.</param>
/// <remarks>MOD6: what only one kind's card or Apply needs is read by that kind in its file; PART4 keeps the members in this one declaration, under a comment per kind.</remarks>
public sealed record HelpPlan(string? Refusal, string Describe, string Terminal, Func<DriverConfig, DriverConfig>? Apply)
{
    // The accept kind's.

    /// <summary>For an accept the route takes (DRIFT1d2): the quest and its departures, for the card; null for every other kind.</summary>
    public HelpAcceptPlan? Accept { get; init; }

    // The clear kind's.

    /// <summary>What a clear proposal's Apply sends (HIST1f-b); null where the card changes nothing, and for every other kind.</summary>
    public HelpClearPlan? Clear { get; init; }

    // The go kind's.

    /// <summary>Where a go takes the person, for one the window has; null for every other kind.</summary>
    public HelpPlace? Go { get; init; }

    // The hand kind's.

    /// <summary>The landed branch a hand-off gives its plugin (WSR5b); null for every other plan.</summary>
    public HelpHandOff? Hand { get; init; }

    // The pause kind's.

    /// <summary>
    /// The pause a pause proposal's Apply makes or lifts (PAUSE1f-b): its scope and id as this machine spells them. Null where the
    /// card changes nothing, and for every other kind.
    /// </summary>
    public PausedBy? Pause { get; init; }

    // The plugin kind's.

    /// <summary>What a plugin proposal's plugin runs, as its manifest writes it (PLUG9); null for every other kind.</summary>
    public HelpPluginView? Plugin { get; init; }

    /// <summary>The folder a plugin is added from, resolved on this machine (PLUG9); null for every other plan.</summary>
    public string? Source { get; init; }

    /// <summary>What an approval or a withdrawal names, every tool (HELPPLUG1c); null for every other plan.</summary>
    public HelpGrantView? Grant { get; init; }

    // The repository kind's.

    /// <summary>For a move the route takes (ENTRY1d1): what moves, and where to; null for every other kind.</summary>
    public HelpRepositoryMove? Move { get; init; }

    // The session kind's.

    /// <summary>
    /// The sessions a session proposal's Apply archives or unarchives, or a delete's one session (SESSUX1h2b), as this machine's
    /// records spell them; empty where the card changes nothing, null for every other kind.
    /// </summary>
    public IReadOnlyList<string>? Sessions { get; init; }

    // The setup kind's.

    /// <summary>
    /// For a set-up the press would publish (LAYOUT8): the press's own plan, what the card shows (what was read on the line, the
    /// rule and the quest's whole text) and what Apply presses; null for a refusal and for every other kind.
    /// </summary>
    public SetupPlan? Setup { get; init; }

    // The sync kind's.

    /// <summary>What a sync card shows (HELP10); null for every other kind.</summary>
    public HelpSyncPlan? Sync { get; init; }

    // The workflow kind's.

    /// <summary>A workflow proposal's card data (WORKFLOW1h-b), what its Apply saves and chooses; null for every other kind.</summary>
    public HelpWorkflowCard? Workflow { get; init; }
}

/// <summary>What the person's Apply did: whether it was applied, and what the conversation is told.</summary>
public sealed record HelpApplied(bool Applied, string Told)
{
    // The go kind's.

    /// <summary>Where a go takes the person: the page navigates there, as its starters' doors do.</summary>
    public HelpPlace? Go { get; init; }

    // The sync kind's.

    /// <summary>
    /// The proposal still waits for the person (LEFT3 c): a sync card's look that listed rows, which settles nothing, so
    /// the card stays for its press and the page logs no settlement. False for every Apply that settles.
    /// </summary>
    public bool Stands { get; init; }
}

/// <summary>
/// The doors a proposal is applied through (HELP6): each the code the screen's own route uses, so an
/// Apply is what the screen would have done, never a second path.
/// </summary>
/// <remarks>MOD6: each kind's file declares the doors its Apply goes through as its own interface, gathered here (PART5).</remarks>
public interface IHelpDoors :
    IHelpAcceptDoors, IHelpAccountDoors, IHelpAgentDoors, IHelpAskDoors, IHelpBrowserDoors, IHelpClearDoors,
    IHelpDeleteDoors, IHelpHandDoors, IHelpPauseDoors, IHelpPluginDoors, IHelpRepositoryDoors, IHelpSessionDoors,
    IHelpSettingDoors, IHelpSetupDoors, IHelpSyncDoors
{
}

/// <summary>
/// Ask Daoris's proposals, the driver's half (HELP1c, D89): the files the connector's <c>*_propose</c> tools
/// wrote under the home, judged with the route's own code and settled by the person's press.
/// </summary>
/// <remarks>
/// <para><b>Judged by the route, not by the door that wrote it.</b> The service checks a proposal's
/// shape; whether the route takes it — a repository registered here, a line git accepts, a pattern
/// naming one branch per session, an agent this machine has — is the config's own edits and this
/// machine's names, run here. A proposal the route would refuse is never shown to the person.</para>
///
/// <para><b>By kind (MOD6).</b> Each kind is a class of its own, registered in <see cref="HelpProposalKinds"/>;
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
                using var document = JsonDocument.Parse(File.ReadAllText(path));
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
        var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        node["state"] = state;
        node["note"] = note;
        JsonFile.Write(path, node);
    }

    /// <summary>How long a claim is believed: a press longer than this is not one (a sync's replay is bounded far below it).</summary>
    private static readonly TimeSpan ClaimLife = TimeSpan.FromHours(1);

    /// <summary>
    /// Claim a proposal for its Apply (HELPAPPLY1): a file made only if none is there, so of two Applies at once, from two windows,
    /// one holds it and runs the proposal and the other gets null. Dispose lets it go, whether the proposal settled or stands for
    /// another press. A claim older than <see cref="ClaimLife"/> was left by a run that died, and is taken over.
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
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return Read(document.RootElement, id);
    }

    /// <summary>
    /// One file as the service's box writes it. A field a kind does not carry reads as null, and so does
    /// one a file from before HELP6 lacks: absence is "not named".
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
    /// Judge a proposal with the route's own code, against the machine as it stands: first against the workspace its conversation is
    /// bound to (ASKSCOPE1c, <see cref="HelpProposalScope"/>), then by its kind.
    /// </summary>
    public static HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts) =>
        HelpProposalKinds.Find(proposal.Kind) is not { } kind
            ? new HelpPlan(Unknown(proposal.Kind), "", "", null)
            : HelpProposalScope.Refusal(kind.Named(proposal, facts), facts) is { } outside
                ? new HelpPlan(outside, "", "", null)
                : kind.Plan(proposal, config, facts);

    /// <summary>
    /// A card drawn for the person (HIST1f-b): <c>HELP_PROPOSALS</c> calls this for each proposal it shows, with the plan it
    /// showed, so a kind whose Apply sends exactly what its card listed holds that in the proposal's file. A refused plan is
    /// never shown, and holds nothing.
    /// </summary>
    public static void Drawn(string home, HelpProposal proposal, HelpPlan plan)
    {
        if (plan.Refusal is null) HelpProposalKinds.Find(proposal.Kind)?.Drawn(home, proposal, plan);
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
