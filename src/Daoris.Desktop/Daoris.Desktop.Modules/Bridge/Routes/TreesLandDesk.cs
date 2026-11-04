using Daoris.Driver;
using static Daoris.Desktop.DriverArguments;
using static Daoris.Desktop.TreesRoutes;

namespace Daoris.Desktop;

/// <summary>
/// How a reviewed session's work lands behind the landing doors: what a press would do, said before it, and the press, which applies the
/// repository's rule. A run the runtime lands is waited for, and a chat run's press is the driver's one reserved operation.
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

        var (tree, _) = await service.SessionGroundAsync(id, cancellationToken);
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

            if (string.IsNullOrWhiteSpace(tree)) return new { Session = id, Said = TreesWords.NoTreeToLand(id).ToWire() };
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
            if (press) return Refused(id, TreesWords.NoTreeToLand(id));
            throw Refusals.Because(
                Refusals.SessionNotReviewable,
                "this session's record names no working tree on this machine, so there is nothing here to land.",
                ("session", id));
        }

        var recordsJson = await service.SessionRecordsJsonAsync(cancellationToken);
        // a chat that took a quest is gated by that quest's chain, read as the view reads it.
        var questId = await service.SessionQuestAsync(id, cancellationToken) ?? ChatRuns.TakenOf(id, recordsJson);
        var records = SessionRecords.Parse(recordsJson);
        // a partner's record lands nothing, at the press, the plan and the verb alike.
        if (TreesWords.PartnerLands(records, id) is { } partner)
        {
            if (press || terminal) return Refused(id, partner);
            throw Refusals.Because(Refusals.SessionNotReviewable, partner.Error[0]["trees: ".Length..], ("session", id));
        }
        // A rule's plugin says its lines on the console under its name, as a hook's do; what the
        // landing did with it goes to the machine log and the loop's record of its health.
        // the lines also go into the answer (`said`), so a terminal that sent the press prints them as the alone path does.
        var pluginLines = new List<string>();
        // WORKFLOWCLEAN1 1c: the gate, the plan and the press that is only a run's Accept read through trees with no plugin wire; the wire is built
        // where a landing is made here (a chat run's press, or the transitional in-place landing), never for a run whose `land` Daoris performs.
        var trees = new SessionTrees(_loop.Home);
        SessionTrees Landing() => new(_loop.Home, new LandingPlugins(_loop.Home, say: SayTo(_loop, pluginLines), log: _loop.Log, health: _loop.Health));

        // a chat in a tree of its own is gated by its chat run's process. A press on a run
        // not bound yet binds it first; one the home cannot keep refuses the press (`bind-failed`), never falling back to Current. A graph
        // choice binds its version; a plan binds nothing.
        var chatRun = questId is null ? ChatRuns.ChatOf(id, recordsJson, trees.Holds) : null;
        if (press && chatRun is not null && ChatRunBound(chatRun, tree) is { Failed: { } failed }) return Refused(id, TreesWords.BindFailed(id, failed));
        // a chat run's press is the driver's one operation, which reserves the conversation, judges
        // every part at one captured tip, asks the opinion where it is not asked, and lands that tip pinned.
        if (press && chatRun is not null)
        {
            return await ChatPressAsync(asked, service, id, tree, chatRun, records, recordsJson, Landing(), pluginLines, cancellationToken);
        }

        // The landing gate: the second opinion, then the review. What holds the press says so before it,
        // and the press refuses it.
        // Said to every window while the press is under way; a plan is no press and says it to nobody.
        PressSteps.Reach(PressSteps.Gate);
        var gate = await trees.GateAsync(tree, questId, new ServiceReviewWorld(service), id, cancellationToken, run: chatRun);
        // Named for the chain's first quest, and a part of an entry with others for its entry's first part, on the entry's one
        // branch: the call every door that lands one session makes.
        // A name the person entered still lands as entered. FABRIC-CLOSE1h3: its quests read once, the press made on its own.
        var quests = new PressQuests(service.FindQuestAsync);
        var subject = Entering(await trees.OwnSubjectAsync(
            tree, id, questId, records, quest => quests.FindAsync(quest, cancellationToken),
            named => _loop.Events.Openings([named]).GetValueOrDefault(named), gate.Process, cancellationToken), asked.Branch);
        var lead = EntryLead(records, id);

        if (!press)
        {
            // How it lands, by the process the gate read: a named workflow's landing step, or the rule.
            var plan = await trees.PlanAsync(tree, subject, cancellationToken, gate.Process);
            if (terminal)
            {
                return new
                {
                    Session = id,
                    Said = (await TreesPlanWords.LandPlanAsync(trees, _loop.Home, id, tree, plan, gate, earlier, pluginLines, cancellationToken)).ToWire(),
                };
            }

            // Accept refuses until the tree's uncommitted paths are committed: the page says so before the press.
            var uncommitted = TreesReviewWires.UncommittedCount(await trees.UncommittedAsync(tree, cancellationToken));
            // only a part of an entry with others can have been left out.
            return Planned(id, plan, uncommitted, gate, lead is null ? null : LeftOutOfEntry(id), new CarriedAccepts(_loop.Home).Of(id),
                LandingDelivery.Newest(_loop.Home, id));
        }

        // The press answers what it showed beside Accept (the second-agent design §8.2–§8.3): a dispute or commits nobody read,
        // drawn with the gate's token, which the press sends back; a gate that moved since answers nothing. FABRIC-CUTOVER1d (the cutover
        // design §4): its answer kept, a run's graph that holds it for a judgement not made yet asks the loop for that pass and waits for it
        // within a bound, so it lands at this press where the pass makes it ready; past the bound it is refused as read.
        var shown = gate;
        // where the run's graph waits on its review, this press is the person's Accept, kept on the run's record for the pass.
        gate = await GraphPasses.PressAsync(
            token => new OpinionPresses(_loop.Home, service).PressedAsync(id, shown, asked.Answers, tree, questId, token),
            token => trees.GateAsync(tree, questId, new ServiceReviewWorld(service), id, token, run: chatRun), _loop.PassWholeAsync, cancellationToken,
            (review, _) => Task.FromResult(RunNodeWords.Word(new NodeRecordFiles(_loop.Home), review.Run, review.Node!, NodeWordActs.Accept, null).Done));
        // WORKFLOWCLEAN1 1c (the cleanup design §2.2): a run whose `land` the runtime performs is landed by that node, never by this press: the
        // press was the Accept (the review's word, above), and it waits within the bound for the landing the pass starts.
        if (gate.Process is WorkflowProcess { LandsByRuntime: true } performed)
        {
            return await PerformedPressAsync(asked, id, gate, performed, cancellationToken);
        }

        // FABRIC-CLOSE1h3: the result the press is made on, its quest as read for its name, judged again under the tree's
        // hold and before the push, so a press that waited behind another landing makes nothing where that result moved meanwhile.
        var pressed = await quests.PressedAsync(id, questId, ct: cancellationToken);

        // The sessions in use, asked when the rule's tidy reaches the other session branches the work holds.
        PressSteps.Reach(PressSteps.Landing);
        // a landing of this tree still under way holds the press for up to five minutes; the page is told it waits.
        using var waiting = PressSteps.WhileWaiting(PressSteps.Landing);
        var landed = await Landing().LandAsync(tree, subject, cancellationToken, async token => await LinesRoutes.InUseAsync(service, token), gate: gate,
            joins: subject.Joins, judged: pressed?.Judged, beforePush: pressed?.BeforePush, beside: token => BranchTidying.BesideAsync(service, token));
        // Kept where the conversation is kept, so the landing and the plugin's word outlast the press.
        if (landed.Landed) _loop.Events.Keep(id, LandingRules.Note(landed), line => _loop.Output.Append(id, line));
        if (_loop.Log is { } log && LandingGateWords.Held(id, trees.Owner(tree), gate, landed, ReviewDoors.Screen) is { } held)
        {
            SessionLog.WriteLanding(log, held);
        }

        // LANDBACK1 part 1: uncommitted work or a conflict is the session's to clear and the press is the person's yes already,
        // so the session is carried on in Daoris's own words through the say door (never the person's, never a card) and the yes kept
        // on it; the press answers that it was carried on and lands when its tree is clean. Words the door keeps nothing of leave the refusal.
        var carry = LandCarry.WhyOf(landed) is null ? null : await LandCarry.CarryAsync(
            async (named, text, key, token) => (await WordsHead.FollowAsync(
                named, at => service.SayAsync(at, text, null, token, from: DaorisWords.From, key: key)).ConfigureAwait(false)).Said,
            _loop.Home, id, tree, landed, records.FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.OrdinalIgnoreCase)),
            DateTimeOffset.UtcNow, cancellationToken);

        _loop.Nudge();
        // the lines this landing moved are registered at once where the verb asks (`follow`), as the page's next look would
        //; the lines it says ride in `followed` and after the sentence in `said`.
        var followed = asked.Follow
            ? await TreesWords.FollowMovedAsync(service, _loop.Home, () => DriverConfig.Load(_loop.ConfigPath), _loop.Log, cancellationToken)
            : null;
        return carry is null
            ? Pressed(id, landed, gate, TreesWords.Land(landed, pluginLines).Followed(followed), followed?.Out ?? [])
            : Pressed(id, carry.Told ? landed with { Message = carry.Sentence } : landed, gate,
                TreesWords.Carried(landed, carry, pluginLines).Followed(followed), followed?.Out ?? [], carried: carry);
    }

    /// <summary>
    /// The press on a run whose <c>land</c> the runtime performs: its Accept is the review's word,
    /// kept above, and it lands nothing and builds no plugin wire. It forgets a refusal nothing changed since (the press is the person's try
    /// again), asks a whole pass, and waits within <see cref="GraphPasses.Bound"/> for the landing that pass starts; it answers what the landing
    /// came to, or <i>accepted; landing</i> past the bound. A workflow with no review needs no Accept, and this press is only that try.
    /// </summary>
    private async Task<object?> PerformedPressAsync(LandAsk asked, string id, LandingGate gate, WorkflowProcess process, CancellationToken ct)
    {
        PressSteps.Reach(PressSteps.Landing);
        var unit = RunUnits.IdOf(process.BoundRun!);
        // What holds it, said as every press says it: the graph's part, the opinion, the look.
        if (gate.Refusal is { } held) return Pressed(id, held, gate, TreesWords.Land(held, []), []);
        if ((gate.Tip ?? gate.GraphPart()?.Commit) is not { } tip) return Refused(id, TreesWords.NotJudged(id));

        var outcome = await PerformedWaits.AwaitAsync(_loop, id, unit, tip, ct);
        var carried = outcome is { Landed: false } && process.Graph?.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.Review) is { } review
                      && RunRepairs.Of(new NodeRecordFiles(_loop.Home), unit, review.Id) is not null;
        var landed = outcome is null ? new TreeLanding(false, "Accepted; landing. Daoris lands it at its pass, and this window shows it when it has.")
            : carried ? outcome with { Message = "Accepted; its session was carried on to clear its tree, and it lands when the tree is clean." }
            : outcome;
        _loop.Nudge();
        var followed = asked.Follow && landed.Landed
            ? await TreesWords.FollowMovedAsync(_loop.Service ?? throw NotReady(), _loop.Home, () => DriverConfig.Load(_loop.ConfigPath), _loop.Log, ct)
            : null;
        return Pressed(id, landed, gate, TreesWords.Land(landed, []).Followed(followed), followed?.Out ?? []);
    }

    /// <summary>
    /// What a chat run's press waits for where the runtime performs its <c>land</c>: the landing the pass starts at the
    /// press's tip, within <see cref="GraphPasses.Bound"/>, or <i>accepted; landing</i> past it. As <see cref="PerformedPressAsync"/>, for the
    /// conversation's press, which keeps its own reservation and answer.
    /// </summary>
    private async Task<TreeLanding> PerformedLandingAsync(string id, LandingGate gate, WorkflowProcess process, CancellationToken ct)
    {
        if (gate.Refusal is { } held) return held;
        if ((gate.Tip ?? gate.GraphPart()?.Commit) is not { } tip) return new TreeLanding(false, TreesWords.NotJudged(id).Error.FirstOrDefault() ?? "nothing was judged.");

        var unit = RunUnits.IdOf(process.BoundRun!);
        var outcome = await PerformedWaits.AwaitAsync(_loop, id, unit, tip, ct);
        var carried = outcome is { Landed: false } && process.Graph?.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.Review) is { } review
                      && RunRepairs.Of(new NodeRecordFiles(_loop.Home), unit, review.Id) is not null;
        return outcome is null ? new TreeLanding(false, "Accepted; landing. Daoris lands it at its pass, and this window shows it when it has.")
            : carried ? outcome with { Message = "Accepted; its session was carried on to clear its tree, and it lands when the tree is clean." }
            : outcome;
    }

    /// <summary>
    /// The person's press on a chat run's work: <see cref="ChatPresses.PressAsync"/>, reserved in this
    /// window's runner, its gate keyed by the run and read at the captured tip, a press's answer to what it showed kept by the run, and the
    /// landing pinned to that tip. It answers as every press does.
    /// </summary>
    private async Task<object?> ChatPressAsync(
        LandAsk asked, ServiceClient service, string id, string tree, string chatRun, IReadOnlyList<SessionRecord> records, string recordsJson,
        SessionTrees trees, List<string> pluginLines, CancellationToken cancellationToken)
    {
        var (workspace, repository) = trees.Owner(tree);
        var (line, live) = ChatRuns.PressOf(id, recordsJson);
        var world = new ServiceReviewWorld(service);
        var presses = new OpinionPresses(_loop.Home, service) { Holder = _loop.Chat };
        var quests = new PressQuests(service.FindQuestAsync);
        PressSteps.Reach(PressSteps.Gate);
        var pressed = await ChatPresses.PressAsync(
            _loop.Home, new ChatPressAsk(id, tree, chatRun, repository, workspace, live), _loop.Chat,
            // a graph-bound run's gate is the quest's, its graph part judged by the pass this press asks
            // after the candidate it submitted, and the press's Accept the review's word.
            async (tip, token) => await GraphPasses.PressAsync(
                async held => await presses.PressedAsync(
                    id, await trees.GateAsync(tree, null, world, id, held, tip: tip, run: chatRun), asked.Answers, tree, null, held, chatRun),
                held => trees.GateAsync(tree, null, world, id, held, tip: tip, run: chatRun), _loop.PassWholeAsync, token,
                (review, _) => Task.FromResult(RunNodeWords.Word(new NodeRecordFiles(_loop.Home), review.Run, review.Node!, NodeWordActs.Accept, null).Done)),
            async (gate, token) =>
            {
                // WORKFLOWCLEAN1 row 1d (the cleanup design §2.7): a run whose `land` the runtime performs is landed by that node, and this press waits.
                if (gate.Process is WorkflowProcess { LandsByRuntime: true } performed) return await PerformedLandingAsync(id, gate, performed, token);
                var subject = Entering(await trees.OwnSubjectAsync(
                    tree, id, null, records, quest => quests.FindAsync(quest, token),
                    named => _loop.Events.Openings([named]).GetValueOrDefault(named), gate.Process, token), asked.Branch);
                PressSteps.Reach(PressSteps.Landing);
                // a landing of this tree still under way holds the press for up to five minutes, and the conversation with it.
                using var waiting = PressSteps.WhileWaiting(PressSteps.Landing);
                return await trees.LandAsync(tree, subject, token, async held => await LinesRoutes.InUseAsync(service, held), gate: gate, joins: subject.Joins,
                    beside: held => BranchTidying.BesideAsync(service, held));
            },
            () => ChatPresses.TurnsOf(_loop.Events, line), cancellationToken);

        var landed = pressed.Landed;
        var gate = pressed.Gate ?? LandingGate.Open(repository);
        if (landed.Landed) _loop.Events.Keep(id, LandingRules.Note(landed), line => _loop.Output.Append(id, line));
        if (_loop.Log is { } log && LandingGateWords.Held(id, (workspace, repository), gate, landed, ReviewDoors.Screen) is { } held)
        {
            SessionLog.WriteLanding(log, held);
        }

        _loop.Nudge();
        var followed = asked.Follow && landed.Landed
            ? await TreesWords.FollowMovedAsync(service, _loop.Home, () => DriverConfig.Load(_loop.ConfigPath), _loop.Log, cancellationToken)
            : null;
        return Pressed(id, landed, gate, TreesWords.Land(landed, pluginLines).Followed(followed), followed?.Out ?? [], pressed.Code);
    }

    /// <summary>
    /// A chat run bound before its press is gated, by its tree's owner: bound already, bound now, a
    /// graph choice that binds nothing, or why the home could not keep it.
    /// </summary>
    private ChatRunBound ChatRunBound(string run, string tree) => ChatRuns.BindFirst(
        DriverConfig.Load(_loop.ConfigPath), _loop.Home, run, tree, WorkflowSteps.PluginsOf(PluginCatalog.Load(_loop.Home, AdapterSet.Built().Names)),
        DateTimeOffset.UtcNow);

    /// <summary>
    /// A press the verb refuses before it lands anything: the page's fields as a refusal reads them, nothing landed, and the
    /// verb's words, with its code, beside them.
    /// </summary>
    private static object Refused(string session, TreesSaid said) => new
    {
        Session = session, Done = false, Message = Sentence(said), Branch = (string?)null, Plugin = (object?)null, Withheld = (string?)null,
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
    /// nothing accepted it since (<see cref="LandingRules.LeftOutOf"/>). A record that does not read says nothing left out, and the
    /// review offers Accept as before.
    /// </summary>
    private string? LeftOutOfEntry(string id)
    {
        try
        {
            return File.Exists(_loop.Events.PathOf(id)) ? LandingRules.LeftOutOf(_loop.Events.After(id, 0).Events) : null;
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
