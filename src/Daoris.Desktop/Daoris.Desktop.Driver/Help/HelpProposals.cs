using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>One of Ask Daoris's proposals, as the connector wrote it (HELP1c, D89; HELP6; PLUG9; WSR5b; HELP10).</summary>
/// <param name="Kind">Which kind it is, one <see cref="HelpProposalKinds"/> registers: `setting`, `ask`, `agent`, `delete`, `account`, `go`, `plugin`, `hand` or `browser`.</param>
/// <param name="Door">Which of its kind's changes it is, as that kind's class says: a `daoris driver` verb for a setting, `update` or `pin` for an agent, and so on.</param>
/// <param name="Target">What the change is for, as its kind's class says: a repository, an agent, a record's id, a view, a plugin, a session or a branch.</param>
/// <param name="Value">What it is set to, where its kind takes one: a setting's value, a pin's version, a hand-off's plugin.</param>
/// <param name="Session">The conversation that proposed it.</param>
/// <param name="State">`proposed`, then `applied`, `dismissed` or `refused`.</param>
/// <remarks>MOD6: a field only one kind carries is declared in that kind's file, beside the judge that reads it.</remarks>
public sealed partial record HelpProposal(
    string Id, string Kind, string Door, string? Target, string? Workspace, string? Value, string? Sentence,
    string Why, string? Session, string State)
{
    /// <summary>
    /// The repository whose checkout holds a plugin to add (PLUG9), or null for a whole path the person gave;
    /// for a hand-off (WSR5b), the repository its branch is in, where a name alone is in several.
    /// </summary>
    public string? Repository { get; init; }
}

/// <summary>What the machine holds that a proposal is judged against: names only.</summary>
/// <remarks>MOD6: a fact only one kind reads is declared in that kind's file.</remarks>
public sealed partial record HelpMachineFacts(
    IReadOnlyCollection<string> Repositories, IReadOnlyCollection<string> Workspaces, IReadOnlyCollection<string> Agents)
{
    /// <summary>Each door as the Agents screen's roster reads it (HELP6), for an agent's update or pin and an account's settings.</summary>
    public IReadOnlyList<HelpDoorFacts> Doors { get; init; } = [];

    /// <summary>This machine's plugins, for a landing rule that names one (HELP8, D100), a plugin proposal (PLUG9) and a hand-off (WSR5b).</summary>
    public PluginCatalog Plugins { get; init; } = PluginCatalog.None;
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
/// <remarks>MOD6: what only one kind's card or Apply needs is declared in that kind's file.</remarks>
public sealed partial record HelpPlan(string? Refusal, string Describe, string Terminal, Func<DriverConfig, DriverConfig>? Apply);

/// <summary>What the person's Apply did: whether it was applied, and what the conversation is told.</summary>
public sealed partial record HelpApplied(bool Applied, string Told);

/// <summary>
/// The doors a proposal is applied through (HELP6): each the code the screen's own route uses, so an
/// Apply is what the screen would have done, never a second path.
/// </summary>
/// <remarks>MOD6: each kind's file adds the doors its Apply goes through.</remarks>
public partial interface IHelpDoors
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
    /// <remarks>Every kind reads its own fields from every file, whatever the file's kind, so that stays so.</remarks>
    private static HelpProposal Read(JsonElement root, string id)
    {
        var read = new HelpProposal(
            Text(root, "id") ?? id, Text(root, "kind") ?? "", Text(root, "door") ?? "", Text(root, "target"),
            Text(root, "workspace"), Text(root, "value"), Text(root, "sentence"), Text(root, "why") ?? "",
            root.TryGetProperty("by", out var by) ? Text(by, "session") : null, Text(root, "state") ?? "")
        {
            Repository = Text(root, "repository"),
        };
        return HelpProposalKinds.All.Aggregate(read, (proposal, kind) => kind.Read(proposal, root));
    }

    /// <summary>Judge a proposal with the route's own code, against the machine as it stands.</summary>
    public static HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts) =>
        HelpProposalKinds.Find(proposal.Kind) is { } kind
            ? kind.Plan(proposal, config, facts)
            : new HelpPlan(Unknown(proposal.Kind), "", "", null);

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

    internal static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
