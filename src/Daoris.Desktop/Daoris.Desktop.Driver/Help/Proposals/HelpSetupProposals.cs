using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's <c>setup</c> proposal (D170 point 1; WORKFLOWCLEAN1 1f): the one door is <c>place</c>, whose ask carries adoption for the
/// repositories it names, judged by the place's plan and opened on the page's <i>Set up…</i>, where the person presses Apply. The
/// repository-alone <c>setup</c> door went with its press; a helper that ran commands could run any driver verb (D89, HELP4), so the act
/// stays a card the person applies.
/// </summary>
internal sealed class HelpSetupProposals : IHelpProposalKind
{
    public string Kind => "setup";

    public string Tool => "setup_propose";

    public IReadOnlyList<string> Doors { get; } = ["place"];

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

    public HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts) =>
        proposal.Door == "place"
            ? PlanPlace(proposal, facts)
            : new($"`{proposal.Door}` is not a door of a set-up: the one door is `place`, with adoption in its ask (`setup_propose`).", "", "", null);

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

    // A place's Apply plans and presses nothing: it takes the person to the page, which plans again and has its own Apply.
    public Task<HelpApplied> ApplyAsync(HelpApplying applying, CancellationToken ct) =>
        Task.FromResult(applying.Settled(true, $"Applied: `#{applying.Id}` — {applying.Plan.Describe} Nothing else changed.", null) with { Go = applying.Plan.Go });
}

/// <summary>A place's plan as its card shows it (AGENTSPLACE1k): the ask, its workspace, each change said once and the digest the card is revised by.</summary>
public sealed record HelpPlaceSetUp(PlaceAsk Asked, string Workspace, IReadOnlyList<PlaceChanged> Changes, string Digest);
