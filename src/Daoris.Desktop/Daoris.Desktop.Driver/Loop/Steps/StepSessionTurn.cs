using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// The loop's start of the session that performs a plugin's step (<see cref="IStepSessions"/>), a supervisor's turn's shape: staffed as the
/// place's builds are (its role's order and the account walk), its record a part of the run (<see cref="StepSessionDoors"/>) saying which
/// step it performs while it works, at the place's checkout with no tree of its own under the step's read-only rules
/// (<see cref="StepPermissions"/>), handed its one server, its first words the step's ask; it ends completed where the step is answered
/// reached and failed otherwise, its note the step's outcome.
/// </summary>
/// <param name="driverOf">A driver for one session, built as a look's is.</param>
internal sealed class StepSessionTurn(Func<Driver> driverOf) : IStepSessions
{
    /// <summary>The bound of one step's session, in minutes.</summary>
    public const int Minutes = 30;

    private readonly ConcurrentDictionary<string, (string Session, SessionProcesses Processes)> _running = new(StringComparer.Ordinal);

    public void Stop(string invocation)
    {
        if (_running.TryGetValue(invocation, out var held)) held.Processes.Stop(held.Session);
    }

    public async Task RunAsync(StepAgentAsk ask, Func<string, ServerEntry> tools, CancellationToken ct)
    {
        var driver = driverOf();
        var context = driver.Context;
        var choice = await ChooseAsync(context, ask, ct).ConfigureAwait(false);
        if (choice is not { Chosen: true, Selection: { Allowed: true } selection, Reviewer: { } agent })
        {
            context.Log?.Info("step.unstaffed", ("invocation", ask.Invocation), ("why", choice.Sentence));
            return;
        }

        AgentDoor adapter;
        try
        {
            adapter = context.Adapters.Door(agent);
        }
        catch (DriverException error)
        {
            context.Log?.Info("step.unstaffed", ("invocation", ask.Invocation), ("why", error.Message));
            return;
        }

        var opened = await context.Service.StepSessions.OpenAsync(ask, agent, selection.Profile, selection.Version, ct).ConfigureAwait(false);
        if (!opened.Opened)
        {
            context.Log?.Info("step.unopened", ("invocation", ask.Invocation), ("why", opened.Message));
            return;
        }

        var sessionId = opened.Session!;
        _running[ask.Invocation] = (sessionId, context.Processes);
        try
        {
            await TurnAsync(driver, ask, tools(sessionId), adapter, selection, choice, agent, sessionId, ct).ConfigureAwait(false);
        }
        finally
        {
            _running.TryRemove(ask.Invocation, out _);
        }
    }

    private static Task<ReviewerChoice> ChooseAsync(LoopContext context, StepAgentAsk ask, CancellationToken ct)
    {
        var catalogue = RoleCatalog.ReadHome(context.Home);
        IReadOnlyList<ActorEffective> order;
        try
        {
            order = new StartPlan(
                ParticipantWork.Build, context.Config, context.Harnesses, context.Adapters, new PassMemory(), catalogue: catalogue,
                place: new RolePlace(ask.Workspace, ask.Repository)) { Workspace = ask.Workspace, Subject = ParticipantWork.Step }.Order;
        }
        catch (Exception error) when (error is ActorUnreadableException or DriverException)
        {
            return Task.FromResult(new ReviewerChoice(null, null, ReviewerUnavailable.Refused, error.Message));
        }

        return ReviewerChoice.ChooseAsync(
            context.Harnesses, OpinionRule.None, [], context.Config, ask.Workspace, ct, actor: ParticipantWork.Step, order: order, catalogue: () => catalogue);
    }

    private static async Task TurnAsync(
        Driver driver, StepAgentAsk ask, ServerEntry tools, AgentDoor adapter, HarnessSelection selection, ReviewerChoice choice, string agent,
        string sessionId, CancellationToken ct)
    {
        var context = driver.Context;
        var (service, home) = (context.Service, context.Home);
        context.Processes.MarkOpening(sessionId);
        RotatedOpening.Say(service, context.Events, sessionId, agent, selection, carried: null);
        var cwd = ask.Root ?? OpinionTree.PathFor(home, null, "step");
        Directory.CreateDirectory(cwd);
        var target = new SessionTarget(QuestId: "", $"Step `{ask.Step}` of `{ask.Plugin}` for {ask.Unit}", "", "Daoris", ask.Repository, cwd, service.BaseUrl)
        {
            Session = sessionId,
            Prompt = ask.Prompt(),
            Block = new StandingFacts
            {
                Repository = ask.Repository,
                Workspace = ask.Workspace,
                Knowledge = false,
                PluginBriefs = PluginBriefs.At(home, new PluginPlace(ask.Repository, ask.Workspace), [tools]),
            },
            Asked = choice.Effective?.Asked,
            Choice = choice.Effective?.From,
        };
        try
        {
            var (info, harnessNotice) = driver.Prepare(adapter, target, selection);
            await service.AdvanceAsync(sessionId, "starting", ct: ct).ConfigureAwait(false);
            var transcript = DaorisHome.Entry(home, HomeEntry.Sessions, $"{sessionId}.log");
            Directory.CreateDirectory(Path.GetDirectoryName(transcript)!);
            // Its one server, and nothing else: no connector and no other plugin's server.
            var plan = ConnectorPlans.For(sessionId, ask.Workspace, [tools], [ConnectorSource.Plugin]);
            var handed = adapter.ServerHanding() is { } handing ? handing.Hand(info, home, plan) : null;
            var rules = driver.HandRules(adapter, info, sessionId, ask.Workspace, ask.Repository, tree: cwd, kept: null, step: true);
            var performs = Noted.Of(
                StartNoteCodes.StepPerforms, $"Performs the step `{ask.Step}` of `{ask.Plugin}` for {ask.Unit}, attempt {ask.Attempt}.",
                ("step", ask.Step), ("plugin", ask.Plugin), ("unit", ask.Unit), ("attempt", ask.Attempt));
            await driver.Holds.HoldAsync(
                ParticipantWork.Build, adapter, info, target, sessionId, transcript, cwd, harnessNotice, rules, handed,
                refusesInput: "A step's session works its plugin's tools alone and answers the step: nobody talks to it while it runs.",
                ct: ct, handedPlan: plan, workingNote: performs, said: driver.Said(adapter, selection, sessionId), limits: driver.NativeLimit(adapter),
                minutes: Minutes, offers: new OpenOffers(selection.Version, selection.Profile, null),
                conclude: async (exitCode, used, turnFailed, ended) =>
                {
                    if (used is not null)
                    {
                        context.Usage?.Record(new UsageEntry(sessionId, ask.Repository, adapter.Id.Value, selection.Profile, used.Used, used.Size, DateTimeOffset.UtcNow));
                    }

                    var conclusion = context.Processes.WasStopRequested(sessionId)
                        ? SessionConclusion.Of("stopped", context.Processes.StopNote(sessionId) ?? Observation.Stopped)
                        : exitCode is null ? SessionConclusion.Of("failed", Observation.TimedOut(Minutes))
                        : StepConclusion.Of(ask, StepAnswer.Read(home, ask.Invocation), turnFailed);
                    conclusion = driver.AccountRefused(conclusion, adapter, selection, ended);
                    conclusion = driver.AccountSignedOut(conclusion, adapter, selection, turnFailed, ended);
                    (conclusion, var limited) = driver.AccountLimited(conclusion, adapter, selection, turnFailed, sessionId, used);
                    await service.AdvanceAsync(sessionId, conclusion.State, note: conclusion.Note, ct: CancellationToken.None, limit: limited, parts: conclusion.Parts)
                        .ConfigureAwait(false);
                    return conclusion.State;
                }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await driver.EndStoppedAsync(sessionId, null).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or IOException or HttpRequestException)
        {
            try
            {
                await service.AdvanceAsync(sessionId, "failed", Observation.Failure(error.Message), ct: CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The terminal write is best-effort by construction: the first failure is the report.
            }
        }
        finally
        {
            context.Output?.Close(sessionId);
        }
    }
}
