using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.HelpProposals;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's <c>clear</c> proposal (HIST1f-b, D153, the history-clearing design §6.4): what this machine kept of finished work
/// cleared from it, as the pages' *Clear from this machine…* and *Clear history…* and the terminal's history clears do. Its doors
/// are <c>quest</c>, <c>ask</c>, <c>failed</c> and <c>workspace</c>, its target the quest's id, the ask's id or the workspace's name, as the
/// service's box writes it. It is judged by what <see cref="HistoryClearing.PlanAsync"/> answers, the screen's first press, and
/// applied through its second, <see cref="HistoryClearing.ClearAsync"/>.
/// </summary>
/// <remarks>
/// <para><b>The card is the first press, Apply the second</b> (§5, §6.4): the card says what would go, counted in the terminal's
/// words, every kept unit with its keep's own sentence, and that nothing brings it back. Drawing it holds the units it listed in
/// the proposal's file (<see cref="Drawn"/>, <see cref="HelpProposal.Units"/>), and Apply sends exactly those, never a fresh
/// plan's: the press judges each again, and keeps and counts one that changed since. A card drawn again says what changed since it
/// listed, what is gone, and what may go now that it did not list, which a new proposal lists. A card never drawn has listed
/// nothing, so its Apply sends nothing and leaves it standing.</para>
///
/// <para><b>A quest or an ask the clear would keep is refused</b> in its keep's sentence, as its page offers no press for it
/// (§6.1); a quest an ask asked names the ask, which clears it with every quest it became (§1.1). A workspace with nothing that
/// may go is said, never refused (D48 §6), and its Apply calls no door.</para>
///
/// <para><b>A closed quest's failed sessions</b> (HIST1f-d, §1.1, §4) are the <c>failed</c> door, its target the quest's id: the
/// card names them, and the quest and its other sessions stay. A quest still open or taken is refused in the keep's sentence; a
/// closed one with none of this machine's failed sessions is said, and its Apply calls no door.</para>
/// </remarks>
internal sealed class HelpClearProposals : IHelpProposalKind
{
    public string Kind => "clear";

    // HIST1f-b, HIST1f-d: a closed quest's work, an ask's, a closed quest's failed sessions and a workspace's.
    public string Tool => "clear_propose";

    public IReadOnlyList<string> Doors { get; } = ["quest", "ask", "failed", "workspace"];

    /// <summary>
    /// ASKSCOPE1c: a workspace's clear names the workspace as its target. ASKSCOPE1c2: a quest's, an ask's and a closed quest's
    /// failed sessions' name a record by id, filed under the workspace the plan read for it says.
    /// </summary>
    public HelpNames Named(HelpProposal proposal, HelpMachineFacts facts) =>
        new([proposal.Workspace, proposal.Door == "workspace" ? proposal.Target : null], [proposal.Repository])
        {
            Records = UnitOf(proposal, facts) is { } unit
                ? [unit.Kind == HistoryKinds.Ask ? HelpNames.Ask(unit.Id, unit.Workspace) : HelpNames.Quest(unit.Id, unit.Workspace)]
                : [],
        };

    /// <summary>ASKSCOPE1c2: the unit a quest's, an ask's or a failed clear names, as <see cref="OnePlan"/> finds it; null for none.</summary>
    private static HistoryUnitPlan? UnitOf(HelpProposal proposal, HelpMachineFacts facts)
    {
        HistoryScope? scope = proposal.Door switch { "quest" => HistoryScope.Quest, "ask" => HistoryScope.Ask, "failed" => HistoryScope.Failed, _ => null };
        if (scope is not { } named) return null;
        var id = proposal.Target?.Trim().TrimStart('#').Trim() ?? "";
        return facts.History.FirstOrDefault(plan => plan.Scope == named && string.Equals(plan.Id, id, StringComparison.OrdinalIgnoreCase))?
            .Units.FirstOrDefault(unit => unit.Kind == KindOf(named) && string.Equals(unit.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    private static string KindOf(HistoryScope scope) =>
        scope switch { HistoryScope.Ask => HistoryKinds.Ask, HistoryScope.Failed => HistoryKinds.Failed, _ => HistoryKinds.Quest };

    public HelpProposal Read(HelpProposal proposal, JsonElement file)
    {
        if (!file.TryGetProperty("units", out var units) || units.ValueKind != JsonValueKind.Array) return proposal with { Units = null };

        var held = new List<HistoryUnitName>();
        foreach (var unit in units.EnumerateArray())
        {
            if (Text(unit, "kind") is { } kind && Text(unit, "id") is { } id) held.Add(new HistoryUnitName(kind, id));
        }

        return proposal with { Units = held };
    }

    public HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts)
    {
        if (proposal.Door is not ("quest" or "ask" or "failed" or "workspace"))
        {
            return Refused($"`{proposal.Door}` is not a clear's door — `quest`, `ask`, `failed` or `workspace`; nothing else clears history.");
        }

        var scope = proposal.Door switch
        {
            "ask" => HistoryScope.Ask, "failed" => HistoryScope.Failed, "workspace" => HistoryScope.Workspace, _ => HistoryScope.Quest,
        };
        var target = scope == HistoryScope.Workspace ? proposal.Target?.Trim() ?? "" : proposal.Target?.Trim().TrimStart('#').Trim() ?? "";
        if (target.Length == 0) return Refused("a clear proposal names one quest or one ask by its id, or one workspace by its name.");

        var read = facts.History.FirstOrDefault(plan => plan.Scope == scope && string.Equals(plan.Id, target, StringComparison.OrdinalIgnoreCase));
        return scope == HistoryScope.Workspace
            ? WorkspacePlan(target, read, proposal.Units, facts)
            : OnePlan(scope, target, read, proposal.Units);
    }

    /// <summary>
    /// §5: the card's units held in the proposal's file, once, from when the list opened, beside every field the service wrote. A
    /// card that changes nothing holds nothing, so a later draw that lists something holds that.
    /// </summary>
    public void Drawn(string home, HelpProposal proposal, HelpPlan plan)
    {
        if (proposal.Units is not null || plan.Clear is not { Held: false } clear) return;
        var path = Path.Combine(FolderOf(home), $"{Path.GetFileName(proposal.Id)}.json");
        if (!File.Exists(path)) return;

        var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        // A second draw keeps the first list: the card the person read first is the one its Apply sends.
        if (node.ContainsKey("units")) return;
        node["units"] = new JsonArray([.. clear.Units.Select(unit => (JsonNode)new JsonObject { ["kind"] = unit.Kind, ["id"] = unit.Id })]);
        JsonFile.Write(path, node);
    }

    public async Task<HelpApplied> ApplyAsync(HelpApplying applying, CancellationToken ct)
    {
        var plan = applying.Plan;
        if (plan.Clear is not { } clear)
        {
            return applying.Settled(true, $"Applied: `#{applying.Id}` — nothing changed: {plan.Describe}", null);
        }

        // §5: Apply is the second press. A card never drawn has listed nothing, so nothing is sent, and it stands, unsettled.
        if (!clear.Held)
        {
            throw new DriverException($"the card for `#{applying.Id}` has not listed what it clears yet: Apply clears only what its card "
                + "lists, so it is drawn first, then applied.");
        }

        var outcome = await applying.Doors.ClearHistoryAsync(clear.Scope, clear.Id, clear.Units, ct).ConfigureAwait(false);
        return clear.Scope == HistoryScope.Workspace ? WorkspaceCleared(applying, outcome) : OneCleared(applying, clear, outcome);
    }

    /// <summary>
    /// A quest's work, an ask's or a quest's failed sessions, one unit: refused where it would be kept, else what it takes (§6.1's
    /// ask). A failed-sessions unit that takes nothing is said, never refused (HIST1f-d, D48 §6).
    /// </summary>
    private static HelpPlan OnePlan(HistoryScope scope, string id, HistoryPlan? read, IReadOnlyList<HistoryUnitName>? held)
    {
        var kind = KindOf(scope);
        var unit = read?.Units.FirstOrDefault(each => each.Kind == kind && string.Equals(each.Id, id, StringComparison.OrdinalIgnoreCase));
        if (unit is null) return Refused($"there is no {(scope == HistoryScope.Ask ? "ask" : "quest")} `#{id}` on this machine.");
        if (unit.Keep is { Word: HistoryWords.Unknown } unknown) return Refused(unknown.Message);
        if (unit.Keep is { } keep) return Refused($"{Name(unit)} stay{(scope == HistoryScope.Failed ? "" : "s")} on this machine: {keep.Message}");
        if (scope == HistoryScope.Failed && !HistoryCommand.Takes(unit))
        {
            return new HelpPlan(null, $"{Capital(Name(unit))}: this machine keeps none of them, so applying changes nothing.",
                HistoryCommand.Door(scope, unit.Id).Replace(" --yes", "", StringComparison.Ordinal), null);
        }

        var said = new List<string>
        {
            $"Clears {Name(unit)} from this machine: {HistoryCommand.Contents(unit)}, with what this machine kept of them "
            + $"({HistoryCommand.Size(unit.Bytes.Total)}): their words, transcripts and files. Nothing brings it back.",
        };
        if (unit.Forgotten.Count > 0)
        {
            said.Add($"The remote for `{RemoteTarget.Workspace(unit.Workspace)}` keeps the team's copy; this machine will not fetch it again.");
        }

        said.AddRange(Pieces(unit));
        return new HelpPlan(null, string.Join(' ', said), HistoryCommand.Door(scope, unit.Id), null)
        {
            Clear = new HelpClearPlan(scope, unit.Id, held ?? [unit.Name]) { Held = held is not null },
        };
    }

    /// <summary>
    /// §5 step 1, §6.1: what a workspace's clear takes, counted by kind and each unit named, then every unit it keeps with its
    /// reason. Once held, the units the card listed, each as it stands now, and what may go that it did not list.
    /// </summary>
    private static HelpPlan WorkspacePlan(string name, HistoryPlan? read, IReadOnlyList<HistoryUnitName>? held, HelpMachineFacts facts)
    {
        var known = facts.Workspaces.Contains(name, StringComparer.OrdinalIgnoreCase);
        var unknown = $"there is no workspace `{name}` on this machine — use a workspace's name as Repositories lists it.";
        if (read is null) return Refused(known ? $"what this machine keeps of `{name}`'s finished history was not read." : unknown);

        var reading = read.Reading ?? new HistoryReading(read.Id);
        var keeps = read.Units.Count > 0 || reading.Quests + reading.Asks + reading.Sessions + reading.Teammates > 0;
        if (!known && !keeps) return Refused(unknown);

        var workspace = read.Id;
        var terminal = HistoryCommand.Door(HistoryScope.Workspace, workspace);
        if (!keeps && reading.LeftOver == 0)
        {
            return new HelpPlan(null, $"`{workspace}` keeps no finished work on this machine, so applying changes nothing.", terminal, null);
        }

        var listed = held ?? [.. read.Units.Where(unit => unit.Clearable).Select(unit => unit.Name)];
        HistoryUnitPlan? Now(HistoryUnitName unit) => read.Units.FirstOrDefault(each => Same(each.Name, unit));
        bool Listed(HistoryUnitPlan unit) => listed.Any(each => Same(each, unit.Name));
        var going = listed.Select(Now).OfType<HistoryUnitPlan>().Where(unit => unit.Clearable).ToList();
        var more = read.Units.Where(unit => unit.Clearable && !Listed(unit)).ToList();
        var stays = new List<string>();
        stays.AddRange(listed.Select(Now).OfType<HistoryUnitPlan>().Where(unit => !unit.Clearable)
            .Select(unit => $"{Capital(Name(unit))} changed since this card listed it and is kept now: {Sentence(unit.Keep!.Message)}"));
        stays.AddRange(listed.Where(unit => Now(unit) is null).Select(unit => $"{Capital(Name(unit))} is no longer on this machine."));
        stays.AddRange(read.Units.Where(unit => !unit.Clearable && !Listed(unit)).Select(unit => $"It keeps {Name(unit)}: {Sentence(unit.Keep!.Message)}"));

        var room = HistoryCommand.TakesRoom(read);
        if (going.Count == 0 && reading.LeftOver == 0 && !room)
        {
            return new HelpPlan(null, string.Join(' ',
            [
                $"Nothing of `{workspace}`'s finished history may be cleared now, so applying changes nothing.", .. stays, .. More(more),
            ]), terminal, null);
        }

        var takes = HistoryCommand.Join(
        [
            HistoryCommand.Count(going.Sum(unit => unit.Quests.Count), "quest"), HistoryCommand.Count(going.Sum(unit => unit.Asks.Count), "ask"),
            HistoryCommand.Count(going.Sum(unit => unit.Sessions.Count), "session"), HistoryCommand.Copies(going.Sum(unit => unit.Teammates.Count)),
            reading.LeftOver > 0 ? HistoryCommand.Count(reading.LeftOver, "left-over file") : null, room ? "the intake's room" : null,
        ]);
        var bytes = going.Sum(unit => unit.Bytes.Total) + reading.LeftOverBytes + (room ? reading.Intake : 0);
        var said = new List<string>
        {
            $"Clears `{workspace}`'s finished history from this machine: {takes}, {HistoryCommand.Size(bytes)}. Nothing brings it back.",
        };
        if (going.Count > 0) said.Add($"It takes {HistoryCommand.Join(going.Select(Name))}.");
        said.AddRange(stays);
        said.AddRange(going.SelectMany(Pieces));
        var forgotten = going.Sum(unit => unit.Forgotten.Count);
        if (forgotten > 0)
        {
            said.Add($"The remote for `{workspace}` keeps the team's copy of {forgotten} of these quests; this machine will not fetch "
                + $"{(forgotten == 1 ? "it" : "them")} again.");
        }

        said.AddRange(More(more));
        said.Add("Apply clears what this card lists, each judged again as it goes.");
        return new HelpPlan(null, string.Join(' ', said), terminal + " --yes", null)
        {
            Clear = new HelpClearPlan(HistoryScope.Workspace, workspace, listed) { Held = held is not null },
        };
    }

    /// <summary>The press's answer for one quest's or one ask's work, as the terminal says it: what went, or why it stayed.</summary>
    private static HelpApplied OneCleared(HelpApplying applying, HelpClearPlan clear, HistoryOutcome outcome)
    {
        var named = Name(new HistoryUnitName(
            clear.Scope switch { HistoryScope.Ask => HistoryKinds.Ask, HistoryScope.Failed => HistoryKinds.Failed, _ => HistoryKinds.Quest }, clear.Id));
        var (id, terminal) = (applying.Id, applying.Plan.Terminal);
        if (outcome.Cleared is [var cleared])
        {
            var said = string.Join(' ',
            [
                $"cleared {named} from this machine: {HistoryCommand.Contents(cleared)}, {HistoryCommand.Size(outcome.Bytes)}.",
                .. outcome.Forgotten > 0
                    ? [$"The remote for `{RemoteTarget.Workspace(cleared.Workspace)}` keeps the team's copy; this machine will not fetch it again."]
                    : Array.Empty<string>(),
                .. Failed(outcome.Failed),
            ]);
            return applying.Settled(true, $"Applied: `#{id}` (`{terminal}`) — {said}", outcome.Failed > 0 ? said : null);
        }

        var kept = Sentence(outcome.Changed.FirstOrDefault()?.Keep?.Message ?? "the service did not clear it");
        return applying.Settled(false, $"Not applied: `#{id}` (`{terminal}`) — kept {named}, which changed since the list: {kept}", kept);
    }

    /// <summary>The press's answer for a workspace (§5): how many of those listed went, what, and each that changed since and stayed.</summary>
    private static HelpApplied WorkspaceCleared(HelpApplying applying, HistoryOutcome outcome)
    {
        var (id, terminal, workspace) = (applying.Id, applying.Plan.Terminal, outcome.Id);
        var went = HistoryCommand.Join(
        [
            HistoryCommand.Count(outcome.Quests, "quest"), HistoryCommand.Count(outcome.Asks, "ask"), HistoryCommand.Count(outcome.Sessions, "session"),
            HistoryCommand.Copies(outcome.Teammates), outcome.LeftOver > 0 ? HistoryCommand.Count(outcome.LeftOver, "left-over file") : null,
            outcome.Intake ? "the intake's room" : null,
        ]);
        var listed = outcome.Listed > 0 ? $"{outcome.Cleared.Count} of {outcome.Listed} listed, " : "";
        var changes = outcome.Changed.Count switch
        {
            0 => ".",
            1 => "; 1 changed since the list and was kept.",
            var n => $"; {n} changed since the list and were kept.",
        };
        var said = new List<string>
        {
            outcome.Took
                ? $"cleared `{workspace}`'s finished history from this machine: {listed}{(went.Length == 0 ? "nothing" : went)}, "
                  + $"{HistoryCommand.Size(outcome.Bytes)}{changes}"
                : $"nothing of `{workspace}`'s finished history was cleared{changes}",
        };
        if (outcome.Forgotten > 0)
        {
            said.Add(outcome.Forgotten == 1
                ? $"1 of the quests was forgotten here; the remote for `{workspace}` keeps the team's copy, and this machine will not fetch it again."
                : $"{outcome.Forgotten} of the quests were forgotten here; the remote for `{workspace}` keeps the team's copy, and this machine "
                  + "will not fetch them again.");
        }

        said.AddRange(outcome.Changed.Select(unit =>
            $"Kept {Name(unit)}, which changed since the list: {Sentence(unit.Keep?.Message ?? "the service did not clear it")}"));
        said.AddRange(Failed(outcome.Failed));
        var text = string.Join(' ', said);
        return outcome.Took
            ? applying.Settled(true, $"Applied: `#{id}` (`{terminal}`) — {text}", outcome.Changed.Count + outcome.Failed > 0 ? text : null)
            : applying.Settled(false, $"Not applied: `#{id}` (`{terminal}`) — {text}", text);
    }

    private static HelpPlan Refused(string why) => new(why, "", "", null);

    /// <summary>What may go now that the card did not list: never sent, so said, for a new proposal to list.</summary>
    private static IEnumerable<string> More(IReadOnlyList<HistoryUnitPlan> more) => more.Count == 0
        ? []
        : [$"This card did not list {HistoryCommand.Join(more.Select(Name))}, which may be cleared now too: a new proposal lists "
           + $"{(more.Count == 1 ? "it" : "them")}."];

    /// <summary>The pieces a unit lists and keeps while it goes, each in its keep's sentence: a teammate's failed session.</summary>
    private static IEnumerable<string> Pieces(HistoryUnitPlan unit) => unit.Kept.Select(piece =>
        $"It keeps {(piece.Session is { } session ? $"`{session}`" : piece.Quest is { } quest ? $"`#{quest}`" : Name(unit))}: {Sentence(piece.Message)}");

    private static IEnumerable<string> Failed(int failed) => failed > 0
        ? [$"{HistoryCommand.Count(failed, "file")} could not be removed and {(failed == 1 ? "is" : "are")} left over; the next clear of a "
           + $"workspace takes {(failed == 1 ? "it" : "them")}."]
        : [];

    /// <summary>A unit as the card names it: <c>`#q1`</c>, <c>ask `#a1`</c>, <c>`#q1`'s failed sessions</c>.</summary>
    private static string Name(HistoryUnitPlan unit) => Name(unit.Name);

    private static string Name(HistoryUnitName unit) => unit.Kind switch
    {
        HistoryKinds.Ask => $"ask `#{unit.Id}`",
        HistoryKinds.Failed => $"`#{unit.Id}`'s failed sessions",
        _ => $"`#{unit.Id}`",
    };

    private static bool Same(HistoryUnitName one, HistoryUnitName other) =>
        string.Equals(one.Kind, other.Kind, StringComparison.Ordinal) && string.Equals(one.Id, other.Id, StringComparison.OrdinalIgnoreCase);

    private static string Capital(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    private static string Sentence(string text) => text.EndsWith('.') ? text : text + ".";
}

/// <summary>What a clear card's Apply sends (HIST1f-b): the scope and its id as this machine spells them, and the units its card listed.</summary>
/// <param name="Units">The units the card listed, each <c>quest</c> or <c>ask</c> by id, as <c>HISTORY_CLEAR</c>'s <c>units</c> takes them.</param>
public sealed record HelpClearPlan(HistoryScope Scope, string Id, IReadOnlyList<HistoryUnitName> Units)
{
    /// <summary>Whether the units are the ones the drawn card held in its file; false before it was drawn, when Apply sends nothing.</summary>
    public bool Held { get; init; }
}

public interface IHelpClearDoors
{
    /// <summary>
    /// The screen's second press of a clear (<c>HISTORY_CLEAR</c>, HIST1c, D153; design §5): <see cref="HistoryClearing.ClearAsync"/>
    /// with exactly <paramref name="units"/>, each judged again by the service's half and this machine's as it goes, and for a
    /// workspace its left-over files and its intake's room. What went and what changed since comes back whole; a service that
    /// did not answer throws, before anything was removed.
    /// </summary>
    Task<HistoryOutcome> ClearHistoryAsync(HistoryScope scope, string id, IReadOnlyList<HistoryUnitName> units, CancellationToken ct);
}
