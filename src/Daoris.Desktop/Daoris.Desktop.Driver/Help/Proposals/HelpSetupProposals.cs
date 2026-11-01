using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's <c>setup</c> proposal (LAYOUT8, D170 point 1, D117 §6.1): a registered repository set up for every agent — its
/// door <c>setup</c>, its target the repository as the registry names it — judged by the set-up press's own plan, the one
/// <c>daoris-driver setup &lt;repository&gt; --plan</c> prints, and applied as that press: the rule into the repository's scope,
/// then one ask to it, as the person's (D124 §2).
/// </summary>
/// <remarks>
/// <para><b>Why a door and not a shell</b> (D170 point 1): the owner asked Ask Daoris to include a registered, not adopted
/// repository, and it could only name the terminal command. A helper that ran commands could run any driver verb, a change
/// nobody confirmed (D89, HELP4), so the act is a card the person applies, as every other.</para>
///
/// <para><b>The press's own refusals</b>, every one at once in its order (D124 §2.1): a proposal the press would refuse is never
/// shown, and the helper hears each refusal with the door that answers it. The plan is read where a proposal is pending
/// (<see cref="HelpMachineFacts.Setups"/>), since it reads the line with git and asks the doctrine tool its version.</para>
///
/// <para><b>Room for WSSETUP7</b> (D124 §4.5): its <c>register</c> door and a workspace's plan with its pause, resume and stop
/// are doors of this kind, each refused here until built.</para>
/// </remarks>
internal sealed class HelpSetupProposals : IHelpProposalKind
{
    public string Kind => "setup";

    public string Tool => "setup_propose";

    public IReadOnlyList<string> Doors { get; } = ["setup", "place"];

    /// <summary>The <c>place</c> door's ask, from the file's <c>place</c>; the target stands for the repositories where it names none.</summary>
    public HelpProposal Read(HelpProposal proposal, JsonElement file) => proposal with
    {
        Place = file.TryGetProperty("place", out var place) && place.ValueKind == JsonValueKind.Object ? AskOf(place, proposal.Target) : null,
    };

    private static PlaceAsk AskOf(JsonElement place, string? target)
    {
        var repositories = Json.Strings(place, "repositories");
        if (repositories.Count == 0 && target?.Trim() is { Length: > 0 } one) repositories = [one];
        return new PlaceAsk(repositories, Json.Text(place, "preset") ?? "")
        {
            Opinion = Json.Flag(place, "opinion"),
            Look = Json.Flag(place, "look"),
            Adoption = Json.Flag(place, "adoption"),
            Builds = Json.NonEmpty(place, "builds"),
            Drive = Json.Flag(place, "drive"),
        };
    }

    /// <summary>ASKSCOPE1c: its target is the repository it sets up; a place's ask names each of its repositories.</summary>
    public HelpNames Named(HelpProposal proposal, HelpMachineFacts facts) =>
        new([proposal.Workspace], [proposal.Target, proposal.Repository, .. proposal.Place?.Repositories ?? []]);

    /// <summary>
    /// STEWARDROLE1a-ii (D209 §5.4): the set-up acts on its repository, which its Apply marks the owner's. A place's Apply writes nothing, but
    /// the repositories it opens a set-up for are the ones the room may name, so they are marked as the press's is.
    /// </summary>
    public HelpOwned Owned(HelpProposal proposal)
    {
        var repositories = new[] { proposal.Target }.Concat(proposal.Place?.Repositories ?? [])
            .Select(each => each?.Trim()).OfType<string>().Where(each => each.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return repositories.Count > 0 ? new HelpOwned(repositories, [], []) : HelpOwned.Nothing;
    }

    public HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts)
    {
        if (proposal.Door == "place") return PlanPlace(proposal, facts);
        var name = proposal.Target?.Trim() ?? "";
        if (proposal.Door != "setup") return new($"`{proposal.Door}` is not a door of a set-up — `setup`, or `place`.", "", "", null);
        if (facts.Setups.FirstOrDefault(each => string.Equals(each.Repository, name, StringComparison.OrdinalIgnoreCase)) is not { } setup)
        {
            return new($"what a set-up of `{name}` would ask was not read here.", "", $"daoris-driver setup {name}", null);
        }

        // The registry's spelling, as the press and the terminal say it.
        var terminal = $"daoris-driver setup {setup.Repository}";
        if (!setup.Pressable)
        {
            return new(setup.Refusals.Count == 0
                ? $"there is nothing to ask of `{setup.Repository}`."
                : string.Join(" ", setup.Refusals.Select(refusal => refusal.Sentence)), "", terminal, null);
        }

        var carried = setup.Adapter is { } adapter ? $", carried by its own session on `{adapter}`" : ", carried by its own session";
        var lands = setup.Landing is { } landing ? $" and landed {SetupPressRules.LandsAs(landing)}" : "";
        return new HelpPlan(null,
            (setup.Index is null ? $"Set `{setup.Repository}` up for every agent" : $"Give `{setup.Repository}` an index of where things are")
            + $": publish one quest to it in workspace `{setup.Workspace}`, as your ask, "
            + $"“{setup.Title}”{carried}{lands}; and add the {SetupPressRules.RulesOf(setup).Count} "
            + $"{(setup.Index is null ? "doctrine commands" : "commands, the doctrine tool's and its index generator's,")} its session runs to "
            + $"`{setup.Repository}`'s rules. Daoris writes nothing into `{setup.Repository}`: its session does the work on a branch "
            + "of its own.",
            terminal, null)
        {
            Setup = setup,
        };
    }

    /// <summary>
    /// The <c>place</c> door (AGENTSPLACE1k): judged by <see cref="PlacePlans.PlanAsync"/> over the ask, read-only, every refusal said as the plan
    /// says it. What it allows is a go to the Setup page of the one workspace's repositories, with <i>Set up…</i> open on the ask.
    /// </summary>
    private static HelpPlan PlanPlace(HelpProposal proposal, HelpMachineFacts facts)
    {
        static HelpPlan Refused(string why, string terminal = "") => new(why, "", terminal, null);
        if (proposal.Place is not { } ask) return Refused("a place's set-up carries its ask: the delivery and the repositories.");
        if (!facts.Places.TryGetValue(proposal.Id, out var planned))
        {
            return Refused($"what a set-up of {Said(ask.Repositories)} would plan was not read here.", PlacePlans.Terminal(ask));
        }

        if (planned.Refusals.Count > 0) return Refused(string.Join(" ", planned.Refusals.Select(refusal => refusal.Sentence)), planned.Terminal);
        var workspaces = planned.Repositories.Select(row => row.Workspace).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (workspaces.Count != 1)
        {
            return Refused($"one set-up is of one workspace's repositories, and these are in {Said(workspaces)}: propose each workspace's alone.", planned.Terminal);
        }

        // The registry's spelling, as the page lists the places.
        var seeded = planned.Asked with { Repositories = [.. planned.Repositories.Select(row => row.Repository)] };
        var terminal = PlacePlans.Terminal(seeded);
        var changes = planned.Changes.Count == 0
            ? "The plan changes nothing now."
            : "The plan: " + string.Join("; ", planned.Changes.Select(each => $"{each.Change.Said} ({Said(each.Repositories)})")) + ".";
        return new HelpPlan(null,
            $"Open the Agents place's Setup for {Said(seeded.Repositories)} in workspace `{workspaces[0]}`, with *Set up…* open on this ask — "
            + $"{planned.Asked.Preset}{Riders(seeded)}. {changes} You read the plan there and press Apply yourself: nothing is written until you press Apply there.",
            terminal, null)
        {
            Go = new HelpPlace("agents", null, "roles")
            {
                Workspace = workspaces[0],
                Repository = seeded.Repositories.Count == 1 ? seeded.Repositories[0] : null,
                Setup = seeded,
            },
            Place = new HelpPlaceSetUp(seeded, workspaces[0], planned.Changes, planned.Digest),
        };
    }

    private static string Riders(PlaceAsk ask) =>
        string.Concat(
            ask.Opinion ? ", with a second opinion" : "",
            ask.Look ? ", with your look" : "",
            ask.Adoption ? ", asking its session to set up for agents" + (ask.Builds is { } builds ? $" ({builds})" : "") : "",
            ask.Drive ? ", with general driving" : "");

    private static string Said(IEnumerable<string> names) => string.Join(", ", names.Select(name => $"`{name}`"));

    public async Task<HelpApplied> ApplyAsync(HelpApplying applying, CancellationToken ct)
    {
        // A place's Apply plans and presses nothing: it takes the person to the page, which plans again and has its own Apply.
        if (applying.Proposal.Door == "place")
        {
            return applying.Settled(true, $"Applied: `#{applying.Id}` — {applying.Plan.Describe} Nothing else changed.", null) with { Go = applying.Plan.Go };
        }

        var setup = applying.Plan.Setup!;
        var outcome = await applying.Doors.SetUpAsync(setup, ct).ConfigureAwait(false);
        // ASKQUESTS1: the ask and the quest the press published, by id, for the conversation's own list.
        return applying.Settled(outcome.Published,
            $"{(outcome.Published ? "Applied" : "Not applied")}: `#{applying.Id}` (`{applying.Plan.Terminal}`) — {Said(setup, outcome)}",
            outcome.Message, outcome.Published ? new HelpPublishing(setup.Workspace, outcome.AskId, outcome.QuestId) : null);
    }

    /// <summary>What the press came to, as the terminal says it: the service's sentence, the ask and quest, the rules added.</summary>
    private static string Said(SetupPlan setup, SetupOutcome outcome)
    {
        if (!outcome.Published) return $"{outcome.Message} So nothing was published, and the rules this press added were taken back.";

        var made = new[] { outcome.AskId is { } ask ? $"ask `#{ask}`" : null, outcome.QuestId is { } quest ? $"quest `#{quest}`" : null }
            .OfType<string>()
            .ToList();
        var said = outcome.Message + (made.Count > 0 ? $" ({string.Join(", ", made)})" : "");
        // HELPSETUP1: a repository's rules are on its Setup → Reach since UX6f (D150 §3.1), as the terminal's press says.
        return outcome.Added.Count == 0
            ? said
            : $"{said}; added to `{setup.Repository}`'s rules: {string.Join(", ", outcome.Added.Select(rule => $"`{rule}`"))}, taken "
              + "back in Repositories → the repository's page → Setup → Reach, or "
              + $"`daoris agent rules remove <rule> --repository {setup.Repository}`.";
    }
}

/// <summary>A place's plan as its card shows it (AGENTSPLACE1k): the ask, its workspace, each change said once and the digest the card is revised by.</summary>
public sealed record HelpPlaceSetUp(PlaceAsk Asked, string Workspace, IReadOnlyList<PlaceChanged> Changes, string Digest);

public interface IHelpSetupDoors
{
    /// <summary>
    /// The set-up press (LAYOUT8), as <c>daoris-driver setup &lt;repository&gt;</c> presses the plan it read: the rule into the
    /// repository's scope, then one ask to it; a refusal from the service takes back the rules it added
    /// (<see cref="SetupPress.ApplyAsync"/>).
    /// </summary>
    Task<SetupOutcome> SetUpAsync(SetupPlan plan, CancellationToken ct);
}
