namespace Daoris.Driver;

/// <summary>
/// An agent supervisor's one read-only turn, the helper's turn's shape: a read-only copy of the subject's repository at its commit with
/// its work in progress laid over it, or an empty folder of its own where the subject names no tree; its record marked with the attempt
/// it decides for; the supervise rules (<see cref="SupervisePermissions"/>, the tree guard always, the person's tier, its handed file and
/// Daoris's connector alone); one turn, bounded; the copy removed however it ended; its last message its answer.
/// </summary>
internal sealed class SuperviseTurn(LoopContext context, Driver driver)
{
    public async Task<SuperviseRunEnd> RunAsync(SuperviseRunAsk ask, CancellationToken ct)
    {
        var (service, home) = (context.Service, context.Home);
        if (ask.Choice is not { Chosen: true, Selection: { Allowed: true } selection, Reviewer: { } agent }) return new("failed", null, null, OpinionPosture.CopyAlone);
        AgentDoor adapter;
        try
        {
            adapter = context.Adapters.Door(agent);
        }
        catch (DriverException)
        {
            return new("failed", null, null, OpinionPosture.CopyAlone);
        }

        var posture = OpinionPosture.Of(adapter);
        var record = await service.RecordAsync(ask.Waiting.Subject.Session, ct).ConfigureAwait(false);
        var (copy, workspace) = (await CopyAsync(record, ask.Waiting.Repository, ct).ConfigureAwait(false), record?.Workspace);
        if (copy is null) return new("failed", null, null, posture);
        try
        {
            return await TurnAsync(ask, adapter, selection, agent, copy, workspace, posture, ct).ConfigureAwait(false);
        }
        finally
        {
            await OpinionTree.RemoveAsync(home, copy).ConfigureAwait(false);
        }
    }

    // The subject's work as a read-only copy, or a folder of the turn's own; null where neither could be made.
    private async Task<string?> CopyAsync(PriorSession? record, string repository, CancellationToken ct)
    {
        var home = context.Home;
        if (record?.Tree is { } tree && Directory.Exists(tree))
        {
            try
            {
                var (_, root) = await driver.Helpers.ReadTreeAsync(tree, ct).ConfigureAwait(false);
                var read = await driver.Helpers.CaptureAsync(tree, ct).ConfigureAwait(false);
                if (root is null || read.Captured is not { } capture) return null;
                var opened = await OpinionTree.OpenAsync(home, root, repository, record.Workspace, capture.Commit, ct).ConfigureAwait(false);
                if (HelperCapture.LayOver(opened.Path, capture) is null) return opened.Path;
                await OpinionTree.RemoveAsync(home, opened.Path).ConfigureAwait(false);
                return null;
            }
            catch (DriverException)
            {
                return null;
            }
        }

        var own = OpinionTree.PathFor(home, null, "supervise");
        Directory.CreateDirectory(own);
        return own;
    }

    private async Task<SuperviseRunEnd> TurnAsync(
        SuperviseRunAsk ask, AgentDoor adapter, HarnessSelection selection, string agent, string copy, string? workspace, string posture, CancellationToken ct)
    {
        var (service, home) = (context.Service, context.Home);
        var waiting = ask.Waiting;
        var opened = await service.Supervising.OpenAsync(
            ask.Attempt, waiting.Role, waiting.Repository, workspace, agent, copy, selection.Profile, selection.Version, ct).ConfigureAwait(false);
        if (!opened.Opened) return new("failed", null, null, posture);
        var sessionId = opened.Session!;
        context.Processes.MarkOpening(sessionId);
        RotatedOpening.Say(service, context.Events, sessionId, agent, selection, carried: null);
        var target = new SessionTarget(QuestId: "", $"Supervisor `{waiting.Role}` for session {waiting.Subject.Session}", "", "Daoris", waiting.Repository, copy, service.BaseUrl)
        {
            Session = sessionId,
            Prompt = ask.Prompt,
            Asked = ask.Choice.Effective?.Asked,
            Choice = ask.Choice.Effective?.From,
        };
        var (info, harnessNotice) = driver.Prepare(adapter, target, selection);
        await service.AdvanceAsync(sessionId, "starting", ct: ct).ConfigureAwait(false);
        var transcript = DaorisHome.Entry(home, HomeEntry.Sessions, $"{sessionId}.log");
        Directory.CreateDirectory(Path.GetDirectoryName(transcript)!);
        string? handed = null;
        if (adapter.ServerHanding() is { } handing && driver.Connector(sessionId, scope: null) is { } connector)
        {
            handed = handing.Hand(info, home, ConnectorPlans.For(sessionId, workspace, [connector], ConnectorPlans.ConnectorOnly));
        }

        var rules = driver.HandRules(adapter, info, sessionId, workspace, waiting.Repository, tree: copy, kept: null, supervise: true);
        try
        {
            return await driver.Holds.HoldAsync(
                "supervise", adapter, info, target, sessionId, transcript, copy, harnessNotice, rules, handed,
                refusesInput: "A supervisor's turn reads and decides alone: nobody talks to it while it runs.",
                ct: ct, handedPlan: ConnectorPlans.For(sessionId, workspace, [], ConnectorPlans.ConnectorOnly),
                said: driver.Said(adapter, selection, sessionId), limits: driver.NativeLimit(adapter), minutes: SupervisePass.Minutes,
                noConnector: HelperPass.NoConnector, offers: new OpenOffers(selection.Version, selection.Profile, null),
                taken: (waiting.Role, ask.Revision),
                conclude: async (exitCode, used, turnFailed, ended) =>
                {
                    if (used is not null)
                    {
                        context.Usage?.Record(new UsageEntry(sessionId, waiting.Repository, adapter.Id.Value, selection.Profile, used.Used, used.Size, DateTimeOffset.UtcNow));
                    }

                    var byPerson = context.Processes.WasStopRequested(sessionId);
                    var conclusion = byPerson
                        ? SessionConclusion.Of("stopped", context.Processes.StopNote(sessionId) ?? Observation.Stopped)
                        : exitCode is int code
                            ? HelperTurn.ReadHelper(waiting.Subject.Session, code, turnFailed)
                            : SessionConclusion.Of("failed", Observation.TimedOut(SupervisePass.Minutes));
                    conclusion = driver.AccountRefused(conclusion, adapter, selection, ended);
                    conclusion = driver.AccountSignedOut(conclusion, adapter, selection, turnFailed, ended);
                    (conclusion, var limited) = driver.AccountLimited(conclusion, adapter, selection, turnFailed, sessionId, used);
                    var transient = !byPerson && !limited && TransientEnded.Of(ended, timedOut: exitCode is null) is not null;
                    await service.AdvanceAsync(sessionId, conclusion.State, note: conclusion.Note, ct: CancellationToken.None, limit: limited, parts: conclusion.Parts).ConfigureAwait(false);
                    var lastWords = conclusion.State == "completed" ? SessionLastWords.ParkedWords(context.Events, sessionId, transcript) : null;
                    var state = limited ? "limited" : transient ? "transient" : conclusion.State;
                    return new SuperviseRunEnd(state, lastWords, sessionId, posture, used?.Used, ask.Choice.Effective?.Model);
                }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await driver.EndStoppedAsync(sessionId, null).ConfigureAwait(false);
            return new("stopped", null, sessionId, posture);
        }
    }
}
