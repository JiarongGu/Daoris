using Daoris.Driver;
using static Daoris.Desktop.DriverArguments;
using static Daoris.Desktop.TreesRoutes;

namespace Daoris.Desktop;

/// <summary>
/// How a reviewed session's work lands behind the landing doors: what a press would do, said before it, and the press, which is the
/// person's Accept and lands nothing itself. A run the runtime lands is waited for, a chat run's press is the driver's one reserved
/// operation, and work no run's <c>land</c> performs is held.
/// </summary>
internal sealed class TreesLandDesk(DriverLoop loop)
{
    /// <summary>What the landing's plan or its press asked: the plan with <c>Said</c> for the verb's words, the press with its answers and follow.</summary>
    internal sealed record LandAsk(string Id, string? Branch, bool Press, bool Said = false, string? Answers = null, bool Follow = false);

    private readonly DriverLoop _loop = loop;

    public async Task<object?> LandAsync(LandAsk asked, CancellationToken cancellationToken)
    {
        var id = asked.Id;
        var service = _loop.Service ?? throw NotReady();

        // Every record read once: this session's tree and quest, its line's other parts and a chat's run are all read from it.
        var recordsJson = await service.SessionRecordsJsonAsync(cancellationToken);
        var records = SessionRecords.Parse(recordsJson);
        var mine = records.FirstOrDefault(record => string.Equals(record.Id, id, StringComparison.OrdinalIgnoreCase));
        var tree = mine?.Tree;
        var press = asked.Press;
        // `trees land --plan` asks for the verb's words (`said`) in place of the page's fields, and gets them for the two
        // cases the verb answers before it plans, as its alone path does: a landing its review reads as landed, and no tree to land.
        var terminal = !press && asked.Said;
        LandedReview? earlier = null;
        if (terminal)
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

        // the verb's two refusals are words on the press, exit 1, where the plan keeps the page's refusal. A landing the review
        // already reads as landed lands nothing again, and a session with no tree names nothing to land.
        if (press && await TreesWords.LandedBeforeAsync(new SessionTrees(_loop.Home), service, id, cancellationToken) is { } before
            && before.ReadsAsLanded(TreeFolders.TreeGone(tree)))
        {
            return Refused(id, TreesWords.NotAgain(id, before, TreeFolders.TreeGone(tree)));
        }

        if (string.IsNullOrWhiteSpace(tree))
        {
            if (press) return Refused(id, await TreeNamedRefusals.SaidAsync(service, id, TreesWords.NoTreeToLand(id), cancellationToken));
            throw await TreeNamedRefusals.RefusalAsync(service, id,
                "this session's record names no working tree on this machine, so there is nothing here to land.", cancellationToken);
        }

        // a chat that took a quest is gated by that quest's chain, read as the view reads it.
        var questId = mine?.Quest ?? ChatRuns.TakenOf(id, recordsJson);
        // a partner's record lands nothing, at the press, the plan and the verb alike.
        if (TreesWords.PartnerLands(records, id) is { } partner)
        {
            if (press || terminal) return Refused(id, partner);
            throw Refusals.Because(Refusals.SessionNotReviewable, partner.Error[0]["trees: ".Length..], ("session", id));
        }
        // The gate, the plan and the press read through trees with no plugin wire: no landing is made here, only a run's
        // `land` lands, which Daoris performs at its pass.
        var trees = new SessionTrees(_loop.Home);

        // a chat in a tree of its own is gated by its chat run's process. A press on a run
        // not bound yet binds it first; one the home cannot keep refuses the press (`bind-failed`), never falling back to Current. A graph
        // choice binds its version; a plan binds nothing.
        var chatRun = questId is null ? ChatRuns.ChatOf(id, recordsJson, trees.Holds) : null;
        if (press && chatRun is not null && new ChatAccepts(_loop).BindFailed(chatRun, tree) is { } failed) return Refused(id, TreesWords.BindFailed(id, failed));
        if (press && chatRun is not null)
        {
            return await ChatPressAsync(asked, service, id, tree, chatRun, recordsJson, cancellationToken);
        }

        // The landing gate: the second opinion, then the review. What holds the press says so before it,
        // and the press refuses it.
        // Said to every window while the press is under way; a plan is no press and says it to nobody.
        PressSteps.Reach(PressSteps.Gate);
        var gate = await trees.GateAsync(tree, questId, new ServiceReviewWorld(service), id, cancellationToken, run: chatRun);
        // Named for the chain's first quest, and a part of an entry with others of its run for the run's first part, on the entry's one
        // branch: the call every door that lands one session makes.
        // A name the person entered still lands as entered. Its quests read once, the press made on its own.
        var quests = new PressQuests(service.FindQuestAsync);
        var subject = Entering(await trees.OwnSubjectAsync(
            tree, id, questId, records, quest => quests.FindAsync(quest, cancellationToken),
            named => _loop.Events.Openings([named]).GetValueOrDefault(named), gate.Process, cancellationToken), asked.Branch);
        var lead = await RunParts.LeadOfAsync(records, id, quest => quests.FindAsync(quest, cancellationToken));

        if (!press)
        {
            // How it lands, by the process the gate read: a named workflow's landing step, or the rule.
            var plan = await trees.PlanAsync(tree, subject, cancellationToken, gate.Process);
            if (terminal)
            {
                return new
                {
                    Session = id,
                    Said = (await TreesPlanWords.LandPlanAsync(trees, _loop.Home, id, tree, plan, gate, earlier, [], cancellationToken)).ToWire(),
                };
            }

            // Accept refuses until the tree's uncommitted paths are committed: the page says so before the press.
            var uncommitted = TreesReviewWires.UncommittedCount(await trees.UncommittedAsync(tree, cancellationToken));
            // only a part of an entry with others can have been left out.
            return Planned(id, plan, uncommitted, gate, lead is null ? null : LeftOutOfEntry(id), new CarriedAccepts(_loop.Home).Of(id),
                LandingDelivery.Newest(_loop.Home, id));
        }

        // The press answers what it showed beside Accept: a dispute or commits nobody read,
        // drawn with the gate's token, which the press sends back; a gate that moved since answers nothing. With its answer kept, a run's graph that holds it for a judgement not made yet asks the loop for that pass and waits for it
        // within a bound, so it lands at this press where the pass makes it ready; past the bound it is refused as read.
        var shown = gate;
        // where the run's graph waits on its review, this press is the person's Accept, kept on the run's record for the pass.
        var accepted = false;
        // on its way to the review, the press is kept as that review's Accept.
        var kept = false;
        gate = await GraphPasses.PressAsync(
            token => new OpinionPresses(_loop.Home, service).PressedAsync(id, shown, asked.Answers, tree, questId, token),
            token => trees.GateAsync(tree, questId, new ServiceReviewWorld(service), id, token, run: chatRun), _loop.PassWholeAsync, cancellationToken,
            (review, _) => Task.FromResult(accepted = RunNodeWords.Word(new NodeRecordFiles(_loop.Home), review.Run, review.Node!, NodeWordActs.Accept, null).Done),
            (graph, _) => Task.FromResult(kept = EarlyAccepts.Keep(new NodeRecordFiles(_loop.Home), graph.Run, graph.Commit).Done));
        // A run whose `land` the runtime performs is landed by that node, never by this press: the
        // press is the Accept (the review's word, above), and it waits within the bound for the landing the pass starts.
        if (gate.Process is WorkflowProcess { LandsByRuntime: true } performed)
        {
            if (kept && !gate.LetsGo && LandPresses.Kept(gate) is { } early) return Pressed(id, early, gate, TreesWords.Land(early, []), []);
            return await PerformedPressAsync(asked, id, gate, performed, accepted, cancellationToken);
        }

        // No run's `land` performs this work, so the press lands nothing: what holds it, or why nothing lands it.
        var held = RunLandHolds.Of(gate);
        if (_loop.Log is { } log && LandingGateWords.Held(id, trees.Owner(tree), gate, held, ReviewDoors.Screen) is { } line)
        {
            SessionLog.WriteLanding(log, line);
        }

        return Pressed(id, held, gate, TreesWords.Land(held, []), []);
    }

    /// <summary>
    /// The press on a run whose <c>land</c> the runtime performs: its Accept is the review's word, kept above, and it lands nothing and builds no
    /// plugin wire. It is the person's try again, answered with what that try came to (<see cref="PerformedAnswers"/>); a workflow with no review
    /// needs no Accept. A gate held only by the review this press <paramref name="accepted"/> is read before the pass applies it, and holds nothing.
    /// </summary>
    private async Task<object?> PerformedPressAsync(LandAsk asked, string id, LandingGate gate, WorkflowProcess process, bool accepted, CancellationToken ct)
    {
        PressSteps.Reach(PressSteps.Landing);
        var unit = RunUnits.IdOf(process.BoundRun!);
        // What holds it, said as every press says it: the graph's part, the opinion, the look.
        if (gate.Refusal is { } held && !LandPresses.HeldByOwnAccept(gate, accepted)) return Pressed(id, held, gate, TreesWords.Land(held, []), []);
        if ((gate.Tip ?? gate.GraphPart()?.Commit) is not { } tip) return Refused(id, TreesWords.NotJudged(id));

        var landed = PerformedAnswers.Of(await PerformedWaits.AwaitAsync(_loop, id, unit, tip, ct));
        _loop.Nudge();
        var followed = asked.Follow && landed.Landed
            ? await TreesWords.FollowMovedAsync(_loop.Service ?? throw NotReady(), _loop.Home, () => DriverConfig.Load(_loop.ConfigPath), _loop.Log, ct)
            : null;
        return Pressed(id, landed, gate, TreesWords.Land(landed, []).Followed(followed), followed?.Out ?? []);
    }

    /// <summary>The person's press on a chat run's work: the conversation's Accept (<see cref="ChatAccepts"/>), answered as every press is.</summary>
    private async Task<object?> ChatPressAsync(
        LandAsk asked, ServiceClient service, string id, string tree, string chatRun, string recordsJson, CancellationToken cancellationToken)
    {
        var (landed, gate, code) = await new ChatAccepts(_loop).PressAsync(id, tree, chatRun, recordsJson, asked.Answers, cancellationToken);
        var followed = asked.Follow && landed.Landed
            ? await TreesWords.FollowMovedAsync(service, _loop.Home, () => DriverConfig.Load(_loop.ConfigPath), _loop.Log, cancellationToken)
            : null;
        return Pressed(id, landed, gate, TreesWords.Land(landed, []).Followed(followed), followed?.Out ?? [], code);
    }

    /// <summary>
    /// A press the verb refuses before it lands anything: the page's fields as a refusal reads them, nothing landed, and the
    /// verb's words, with its code, beside them.
    /// </summary>
    private static object Refused(string session, TreesSaid said) => new
    {
        Session = session, Done = false, Message = Sentence(said), Branch = (string?)null,
        Review = (object?)null, Opinion = (object?)null, Workflow = (object?)null, LeftOut = (object?)null, Conflict = (object?)null,
        Said = said.ToWire(), Followed = Array.Empty<string>(), Carried = (object?)null,
    };

    /// <summary>The sentence a refusal's words carry without the verb's <c>trees: </c> lead, as the page shows it.</summary>
    private static string Sentence(TreesSaid said)
    {
        var line = said.Out.Concat(said.Error).FirstOrDefault() ?? "";
        return line.StartsWith("trees: ", StringComparison.Ordinal) ? line["trees: ".Length..] : line;
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

    /// <summary>
    /// A plugin's own lines, said as they come on the loop's output under its name and kept in <paramref name="lines"/> for the
    /// answer's words. A method, so the construction of a <c>LandingPlugins</c> reads whole to <c>PluginRecordWiringTests</c>.
    /// </summary>
    internal static Action<string, string> SayTo(DriverLoop loop, List<string> lines) => (plugin, line) =>
    {
        lines.Add(TreesWords.PluginLine(plugin, line));
        loop.Output.Append($"plugin:{plugin}", line);
    };
}
