namespace Daoris.Driver;

/// <summary>
/// The intake as the lead of an ask's team. Where the workspace has more than one repository the intake starts as plan work, on the Lead
/// role's order, holds <c>team_plan</c> beside <c>quest_publish</c> and decides first whether the work is one part or several; in a
/// workspace with one repository it starts as intake work, as before. The session is one kind either way (an intake, by its marks), so
/// there is no second way to start an ask's first session.
/// </summary>
public static class IntakeLead
{
    /// <summary>Whether the intake answering an ask leads its team: the workspace has more than one repository that declares itself.</summary>
    public static bool Leads(IReadOnlyCollection<string> repositories) => repositories.Count > 1;

    /// <summary>The work the intake starts as: plan work for a lead, intake work otherwise.</summary>
    public static string WorkOf(bool leads) => leads ? ParticipantWork.Plan : ParticipantWork.Intake;

    /// <summary>What the lead is told beside the intake's job: decide the split, then write it through the plan door.</summary>
    public const string Instruction =
        "This workspace has more than one repository, so you also lead the ask's team. Before you publish, decide whether the work is\n"
        + "one part or several. One part is a valid plan: publish it with `quest_publish` as above. Where it is several, write the plan\n"
        + "with `team_plan`, whole, in one call, and publish nothing else: each part is a thing a person sees working (`outcome`), in\n"
        + "one repository and lane, with its `claims` (the path globs it will change), its `proof` (each point naming its check or\n"
        + "test) and its `requirements` quoted from the person's words; each becomes a quest under its id, so writing a part again\n"
        + "returns its quest. An edge is `after` only for a true build dependency, and `orders` where two parts share a file and\n"
        + "either order works, each with its reason. Where several parts read one contract, write a first small part that lands only\n"
        + "that contract and make the others `after` it. Say in `why` why these parts. Read the plan back with `team_plan` and no\n"
        + "`parts`; a flag it names is for you to resolve in the next revision. Never write what the repositories' integrator writes:\n"
        + "the backlog, the archive, the changelog, the areas and generated files are no part's claim.\n\n";
}
