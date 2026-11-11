using Daoris.Driver;
using static Daoris.Desktop.DriverArguments;
using static Daoris.Desktop.TreesRoutes;

namespace Daoris.Desktop;

/// <summary>
/// How a reviewed session's work would land, said before its review's Accept: the plan, what holds it, and the review record the Accept is
/// pressed on (<see cref="ReviewDrawn"/>). The Accept is that record's word at the node door, and the run's <c>land</c> lands it; nothing
/// lands here.
/// </summary>
internal sealed class TreesLandDesk(DriverLoop loop)
{
    private readonly DriverLoop _loop = loop;

    /// <param name="said">The verb's words (`trees land --plan`) in place of the page's fields.</param>
    public async Task<object?> PlanAsync(string id, string? branch, bool said, CancellationToken cancellationToken)
    {
        var service = _loop.Service ?? throw NotReady();

        // Every record read once: this session's tree and quest, its line's other parts and a chat's run are all read from it.
        var recordsJson = await service.SessionRecordsJsonAsync(cancellationToken);
        var records = SessionRecords.Parse(recordsJson);
        var mine = records.FirstOrDefault(record => string.Equals(record.Id, id, StringComparison.OrdinalIgnoreCase));
        var tree = mine?.Tree;
        // The verb's words answer the two cases it says before it plans, as its alone path does: a landing its review reads as landed,
        // and no tree to land.
        LandedReview? earlier = null;
        if (said)
        {
            earlier = await TreesWords.LandedBeforeAsync(new SessionTrees(_loop.Home), service, id, cancellationToken);
            if (earlier is not null && earlier.ReadsAsLanded(TreeFolders.TreeGone(tree)))
            {
                return new { Session = id, Said = TreesPlanWords.LandedPlan(id, earlier).ToWire() };
            }

            if (string.IsNullOrWhiteSpace(tree))
            {
                return new { Session = id, Said = (await TreeNamedRefusals.SaidAsync(service, id, TreesWords.NoTreeToLand(id), cancellationToken)).ToWire() };
            }
        }

        if (string.IsNullOrWhiteSpace(tree))
        {
            throw await TreeNamedRefusals.RefusalAsync(service, id,
                "this session's record names no working tree on this machine, so there is nothing here to land.", cancellationToken);
        }

        // a chat that took a quest is gated by that quest's chain, read as the view reads it.
        var questId = mine?.Quest ?? ChatRuns.TakenOf(id, recordsJson);
        // a partner's record lands nothing.
        if (TreesWords.PartnerLands(records, id) is { } partner)
        {
            if (said) return new { Session = id, Said = partner.ToWire() };
            throw Refusals.Because(Refusals.SessionNotReviewable, partner.Error[0]["trees: ".Length..], ("session", id));
        }

        // The gate and the plan read through trees with no plugin wire: no landing is made here.
        var trees = new SessionTrees(_loop.Home);
        var chatRun = questId is null ? ChatRuns.ChatOf(id, recordsJson, trees.Holds) : null;
        var gate = await trees.GateAsync(tree, questId, new ServiceReviewWorld(service), id, cancellationToken, run: chatRun);
        // Named for the chain's first quest, and a part of an entry with others of its run for the run's first part, on the entry's one
        // branch. A name the person entered still lands as entered. Its quests read once.
        var quests = new PressQuests(service.FindQuestAsync);
        var subject = Entering(await trees.OwnSubjectAsync(
            tree, id, questId, records, quest => quests.FindAsync(quest, cancellationToken),
            named => _loop.Events.Openings([named]).GetValueOrDefault(named), gate.Process, cancellationToken), branch);
        var lead = await RunParts.LeadOfAsync(records, id, quest => quests.FindAsync(quest, cancellationToken));

        // How it lands, by the process the gate read: a named workflow's landing step, or the rule.
        var plan = await trees.PlanAsync(tree, subject, cancellationToken, gate.Process);
        if (said)
        {
            return new
            {
                Session = id,
                Said = (await TreesPlanWords.LandPlanAsync(trees, _loop.Home, id, tree, plan, gate, earlier, [], cancellationToken)).ToWire(),
            };
        }

        // The Accept is kept with uncommitted paths and carries the session on to commit them: the page says so before the press.
        var uncommitted = TreesReviewWires.UncommittedCount(await trees.UncommittedAsync(tree, cancellationToken));
        // only a part of an entry with others can have been left out.
        return Planned(id, plan, uncommitted, gate, lead is null ? null : LeftOutOfEntry(id), new CarriedAccepts(_loop.Home).Of(id),
            LandingDelivery.Newest(_loop.Home, id), ReviewDrawn.Of(_loop.Home, gate.Process is WorkflowProcess { BoundRun: { } bound } ? bound : chatRun));
    }

    /// <summary>
    /// Where the person left this part out of its entry's Accept: the branch its conversation's newest note names, while
    /// nothing accepted it since (<see cref="LandingNotes.LeftOutOf"/>). A record that does not read says nothing left out, and the
    /// review offers Accept as before.
    /// </summary>
    private string? LeftOutOfEntry(string id)
    {
        try
        {
            return File.Exists(_loop.Events.PathOf(id)) ? LandingNotes.LeftOutOf(_loop.Events.After(id, 0).Events) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return null;
        }
    }
}
