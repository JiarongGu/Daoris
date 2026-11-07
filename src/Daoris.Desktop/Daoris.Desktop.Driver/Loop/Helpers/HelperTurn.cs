using System.Text.Json;

namespace Daoris.Driver;

/// <summary>A helper's pass: one read-only turn in a clone of the asker's repository, the answer read from the clone's file.</summary>
internal sealed class HelperTurn(LoopContext context, Driver driver)
{
    private readonly LoopContext _context = context;

    private readonly Driver _driver = driver;

    /// <summary>
    /// The helper's pass (MIXAGENT2a-ii; the reviewer's machinery, <see cref="PassAsync"/>): a read-only clone of the asker's
    /// repository at its tree's last commit with the asker's captured work in progress laid over it (COAGENT1a), its record a chat
    /// that serves the asker, one turn under the reviewer's read-only rules with its one tool <c>helper_answer</c>, and the clone
    /// removed however it ended, nothing read back from it. A choice with no agent starts nothing.
    /// </summary>
    internal async Task<HelperPassRun> HelperPassAsync(HelperPassAsk ask, ReviewerChoice choice, CancellationToken ct = default)
    {
        if (choice is not { Chosen: true, Selection: { Allowed: true } selection, Reviewer: { } helper })
        {
            return new HelperPassRun(null, $"held  helper for ask `{ask.Ask.Id}`: {choice.Sentence}") { Code = HelperWhy.NotStarted };
        }

        AgentDoor adapter;
        try
        {
            adapter = _context.Adapters.Door(helper);
        }
        catch (DriverException error)
        {
            return new HelperPassRun(null, $"held  helper for ask `{ask.Ask.Id}`: {error.Message}") { Code = HelperWhy.NotStarted };
        }

        OpinionTreeOpened copy;
        try
        {
            copy = await OpinionTree.OpenAsync(_context.Home, ask.Root, ask.Repository, ask.Workspace, ask.Tip, ct).ConfigureAwait(false);
        }
        catch (DriverException error)
        {
            return new HelperPassRun(null, $"held  helper for ask `{ask.Ask.Id}`: {error.Message}") { Code = HelperWhy.Unreadable };
        }

        // COAGENT1a: the asker's work in progress laid over the fresh copy; a copy it cannot be laid over whole is removed, and no
        // helper reads part of it.
        if (ask.Capture is { } capture && HelperCapture.LayOver(copy.Path, capture) is { } unlaid)
        {
            var gone = await OpinionTree.RemoveAsync(_context.Home, copy.Path).ConfigureAwait(false);
            return new HelperPassRun(null, $"held  helper for ask `{ask.Ask.Id}`: its work could not be laid over its copy: {unlaid}{(gone is null ? "" : $" {gone}")}")
            {
                Code = HelperWhy.Unreadable,
            };
        }

        HelperPassRun run;
        string? left;
        try
        {
            var reask = ask.Reask is { } walk ? new OneTurnReask(choice, walk) : null;
            run = await ReadHelperInCopyAsync(ask, choice, adapter, selection, helper, copy, reask, ct).ConfigureAwait(false);
            // D205 §3.5(f): its agent refused the entry's model at the open, so the refused record has ended `choice-not-offered` and this
            // call walks its candidates on with it passed, then opens the replacement and calls `Opened` again with it, before it
            // publishes an ordinary outcome; with none left, `not-started` with the pass reasons.
            while (run is { NotOffered: true } && reask is not null && !ct.IsCancellationRequested)
            {
                var next = await reask.NextAsync().ConfigureAwait(false);
                if (next is not { Chosen: true, Selection: { Allowed: true } nextSelection, Reviewer: { } nextHelper })
                {
                    run = new HelperPassRun(null, $"held  helper for ask `{ask.Ask.Id}`: {next.Sentence}") { Code = HelperWhy.NotStarted };
                    break;
                }

                try
                {
                    adapter = _context.Adapters.Door(nextHelper);
                }
                catch (DriverException error)
                {
                    run = new HelperPassRun(null, $"held  helper for ask `{ask.Ask.Id}`: {error.Message}") { Code = HelperWhy.NotStarted };
                    break;
                }

                ask.Replaced?.Invoke(next);
                run = await ReadHelperInCopyAsync(ask, next, adapter, nextSelection, nextHelper, copy, reask, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            // Removed however the run ended, the person's stop and the driver's own included: nothing in it is read back.
            left = await OpinionTree.RemoveAsync(_context.Home, copy.Path).ConfigureAwait(false);
        }

        return left is null ? run : run with { Line = $"{run.Line} {left}" };
    }

    /// <summary>The helper's turn from its record's open to its record's end, in its copy.</summary>
    private async Task<HelperPassRun> ReadHelperInCopyAsync(
        HelperPassAsk ask, ReviewerChoice choice, AgentDoor adapter, HarnessSelection selection, string agent, OpinionTreeOpened copy,
        OneTurnReask? reask, CancellationToken ct)
    {
        var service = _context.Service;
        var home = _context.Home;
        var output = _context.Output;
        var named = $"ask `{ask.Ask.Id}`";
        var opened = await service.Helpers.OpenAsync(ask.Ask.Asker, agent, copy.Path, selection.Profile, selection.Version, ask.Tip, ct).ConfigureAwait(false);
        if (!opened.Opened)
        {
            return new HelperPassRun(null, $"refused  helper for {named}: {opened.Message}") { Code = HelperWhy.NotStarted };
        }

        var sessionId = opened.Session!;
        // Marked as this process's start until its spawn's marker replaces it (QUEUED1).
        _context.Processes.MarkOpening(sessionId);
        // The state names the helper now, before its turn (helper_answer finds its ask by it).
        ask.Opened?.Invoke(sessionId);
        HelperPassRun Ended(string state, string note, string? lastWords = null) =>
            new(sessionId, $"{state}  helper {sessionId} ({named}): {note}") { State = state, LastWords = lastWords };
        // The record says who asked: an opening's line, then the helper's own on another account of the order, as a quest's start does.
        RotatedOpening.Say(service, _context.Events, sessionId, agent, selection, carried: null);
        OneTurnReask.Say(_context.Events, sessionId, "helper", choice, _driver.Log);
        var bound = HelperPass.Bound(_context.Config);
        var posture = OpinionPosture.Of(adapter);
        SessionStop? stoppedFirst = null;

        try
        {
            var packet = new HelperPacket(ask.Ask.Asker, ask.Repository, ask.Tip, ask.Ask.Question, bound)
            {
                Capture = ask.Capture,
                // COWORKUX1d: a follow-up is handed the ask it follows as Daoris's record, and says where its answer goes.
                Earlier = new HelperFiles(home).EarlierOf(ask.Ask),
                ForPerson = ask.Ask.ForPerson,
            };
            var target = new SessionTarget(
                QuestId: "", $"Helper for session {ask.Ask.Asker}", "", "Daoris", ask.Repository, copy.Path, service.BaseUrl)
            {
                Helper = ask.Ask.Id,
                Session = sessionId,
                Prompt = HelperInstruction.Compose(packet),
                // CONTEXTONE1c: its standing block, its copy's facts and the person's choices for its repository.
                Block = StandingBlocks.OnMachine(StandingBlocks.OfTree(ask.Repository, copy.Path), ParticipantWork.Help, _context.Config, [], null),
                // MODELROLE1e (D205 §3.2, §4.4): the entry's model and effort, carried on either door, and the choice its record keeps.
                Asked = choice.Effective?.Asked,
                Choice = choice.Effective?.From,
            };
            var (info, harnessNotice) = _driver.Prepare(adapter, target, selection);
            await service.AdvanceAsync(sessionId, "starting", ct: ct).ConfigureAwait(false);

            var transcript = DaorisHome.Entry(home, HomeEntry.Sessions, $"{sessionId}.log");
            Directory.CreateDirectory(Path.GetDirectoryName(transcript)!);

            // Its connector and nothing else, as a reviewer's is: no plugin's server, which could write where a helper may not.
            string? handed = null;
            string? preamble = null;
            if (adapter.ServerHanding() is { } handing)
            {
                var connector = _driver.Connector(sessionId, scope: null);
                if (connector is null) preamble = HelperPass.NoConnector;
                handed = handing.Hand(info, home, ConnectorPlans.For(sessionId, ask.Workspace, connector is null ? [] : [connector], ConnectorPlans.ConnectorOnly));
            }

            // HELPPLUG1c (D170 point 4): the skills plugins hand sessions, as a reviewer's are handed them: words, never a tool.
            var (skills, skillPlugins) = SpawnSkills.Hand(_context.Adapters, adapter, info, home, sessionId, _context.Catalog, PluginServerTo.Sessions);
            using var skilled = new Disposer(() => SpawnFiles.RemoveFolder(skills));

            // What it may do: the person's composition under the reviewer's read-only shape with `helper_answer`, held to its copy
            // by the tree guard.
            var handedRules = _driver.HandRules(
                adapter, info, sessionId, ask.Workspace, ask.Repository, tree: copy.Path, kept: null, opinion: true, helper: true,
                plugins: skillPlugins);
            var opening = HelperPass.Opening(ask.Repository, ask.Tip, posture);

            return await _driver.Holds.HoldAsync(
                "helper", adapter, info, target, sessionId, transcript, copy.Path, Driver.JoinNotices(harnessNotice, opening), handedRules, handed,
                // One turn, and no words: nobody talks to a helper while it reads, on either door.
                refusesInput: HelperPass.TakesNoWords(ask.Ask.Id),
                ct: ct,
                preamble: Driver.JoinNotices(preamble, opening),
                handedPlan: ConnectorPlans.For(sessionId, ask.Workspace, [], ConnectorPlans.ConnectorOnly),
                said: _driver.Said(adapter, selection, sessionId), limits: _driver.NativeLimit(adapter),
                minutes: bound,
                transientWanted: TransientOwners.Helper(_context.Processes, sessionId, new HelperFiles(home), ask.Ask.Id),
                noConnector: HelperPass.NoConnector,
                stopMade: stop => stoppedFirst = stop,
                // D205 §3.5(f): its agent's refusal of the entry's model at the open is kept as the account's reading and asked again in
                // this call: a later entry of the same agent on the same account is set on this session, anything else ends the record.
                offers: new OpenOffers(selection.Version, selection.Profile, reask is null ? null : _ => reask.SameAccountAsync()),
                conclude: async (exitCode, used, turnFailed, ended) =>
                {
                    if (used is not null)
                    {
                        _context.Usage?.Record(new UsageEntry(
                            sessionId, ask.Repository, adapter.Id.Value, selection.Profile, used.Used, used.Size, DateTimeOffset.UtcNow));
                    }

                    // Refused at the open, before any prompt: the record ends `choice-not-offered`, its one count given back once its
                    // process has stopped, and the pass asks its walk again (above) before it publishes an outcome.
                    if (!_context.Processes.WasStopRequested(sessionId) && ended.NotOffered is { } notOffered)
                    {
                        var refusedEnd = Observation.ChoiceNotOffered(adapter.Id.Value, notOffered);
                        await service.AdvanceAsync(sessionId, refusedEnd.State, note: refusedEnd.Note, ct: ct, parts: refusedEnd.Parts).ConfigureAwait(false);
                        _context.Harnesses.Release(selection.Number, sessionId);
                        return Ended(refusedEnd.State, refusedEnd.Note) with { NotOffered = true };
                    }

                    var byPerson = _context.Processes.WasStopRequested(sessionId);
                    var conclusion = byPerson
                        ? SessionConclusion.Of("stopped", _context.Processes.StopNote(sessionId) ?? Observation.Stopped)
                        : exitCode is int code
                            ? ReadHelper(ask.Ask.Asker, code, turnFailed)
                            : SessionConclusion.Of("failed", Observation.TimedOut(bound));
                    conclusion = _driver.AccountRefused(conclusion, adapter, selection, ended);
                    conclusion = _driver.AccountSignedOut(conclusion, adapter, selection, turnFailed, ended);
                    (conclusion, var limited) = _driver.AccountLimited(conclusion, adapter, selection, turnFailed, sessionId, used);
                    var transient = !byPerson && !limited && TransientEnded.Of(ended, timedOut: exitCode is null) is not null;
                    if (transient) conclusion = TransientEnded.Over(conclusion, ended, TransientEnded.Helper, adapter.Id.Value, timedOut: exitCode is null);

                    await service.AdvanceAsync(
                        sessionId, conclusion.State, note: conclusion.Note, ct: ct, limit: limited, parts: conclusion.Parts).ConfigureAwait(false);
                    // Its last words stand as the answer only where its turn completed and it used no tool (design §3).
                    var lastWords = conclusion.State == "completed" ? SessionLastWords.ParkedWords(_context.Events, sessionId, transcript) : null;
                    return Ended(conclusion.State, conclusion.Note, lastWords) with { Limit = limited, Transient = transient };
                }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The person is closing the driver; the record says so rather than sit at "working".
            try
            {
                await service.AdvanceAsync(sessionId, "stopped", Observation.DriverClosed, ct: CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort by construction: the host may already be gone on the same shutdown.
            }

            return Ended("stopped", "the driver was stopped.");
        }
        catch (Exception) when (stoppedFirst is not null)
        {
            // A wire that broke after a stop is that stop's end, not a failure (STOPRACE1), as a quest's session reads it.
            var stopped = await _driver.EndStoppedAsync(sessionId, stoppedFirst).ConfigureAwait(false);
            return Ended(stopped.State, stopped.Note);
        }
        catch (Exception error)
        {
            // Everything but the shutdown above, a client timeout included, as a reviewer's twin.
            try
            {
                await service.AdvanceAsync(sessionId, "failed", Observation.Failure(error.Message), ct: CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The terminal write is best-effort by construction: the first failure is the report.
            }

            return Ended("failed", error.Message);
        }
        finally
        {
            output?.Close(sessionId);
        }
    }

    /// <summary>
    /// How a helper's turn ended, from its exit and its door (D46 §4): a clean exit is <c>completed</c>, whatever it said; a refused
    /// turn or another exit is <c>failed</c>. Whether it answered is the answer file's, which the settling reads.
    /// </summary>
    internal static SessionConclusion ReadHelper(string asker, int exitCode, string? turnFailed) =>
        turnFailed is { Length: > 0 } failed
            ? SessionConclusion.Of("failed", Observation.Failure(failed))
            : exitCode != 0
                ? SessionConclusion.Of("failed", Noted.Of(EndNoteCodes.EndedExit, $"it exited with code {exitCode}.", ("exit", exitCode)))
                : SessionConclusion.Of("completed", HelperNotes.Helping(asker));
}
