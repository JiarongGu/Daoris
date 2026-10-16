namespace Daoris.Driver;

/// <summary>
/// The second opinion's pass (XAGENT1d, D155 points 5 and 6; the second-agent design §4–§5): another agent, the one the walk
/// chose (XAGENT1b), reads a session's work in a copy of its own and says its opinion through its connector (XAGENT1c).
/// </summary>
/// <remarks>
/// <para><b>The same spawn as an intake's</b>, on the driven capture path: a pass is one turn framed as one prompt on either
/// door, taking no person's line and never resumed, since every pass is a fresh conversation (INT4i, §5.6). Its account is the
/// one the walk already chose, never walked again.</para>
///
/// <para><b>Nothing is taken back.</b> The copy is made before the record and removed after the run however it ended; the only
/// thing a pass takes is the opinion the reviewer said through its tool, which the local host keeps. The record moves on the
/// exit code, never on what the reviewer said of itself (D46 §4): whether it gave its opinion is the host's to derive.</para>
///
/// <para><b>The look asks it</b> (XAGENT1f, <see cref="OpinionGateLook.OpinionsAsync"/>): for an opinion owed by itself and for the
/// person's ask, beside the look, its slot taken before any quest's first start, and its findings delivered after (XAGENT1e).</para>
/// </remarks>
/// <param name="driver">
/// The driver whose run machinery a pass goes through (PART10m, D166): the spawn's preparation, the connector, the rules handed,
/// the hold of the process and the account sayings, which Continue, Helper and Intake call as well.
/// </param>
internal sealed class DriverOpinions(LoopContext context, Driver driver)
{
    private readonly LoopContext _context = context;

    private readonly Driver _driver = driver;

    /// <summary>
    /// Read <paramref name="ask"/>'s work with the reviewer <paramref name="choice"/> names, on the account its walk chose: the
    /// candidate read, the copy made, the opinion asked of the local host, the reviewer run in its copy under the opinion's
    /// rules and bound, and the copy removed. A choice with no reviewer starts nothing and says so, tier <c>none</c>.
    /// </summary>
    public async Task<OpinionPassRun> PassAsync(OpinionPassAsk ask, ReviewerChoice choice, CancellationToken ct = default)
    {
        if (choice is not { Chosen: true, Selection: { Allowed: true } selection, Reviewer: { } reviewer, Label: { } label })
        {
            return OpinionPassRun.Unavailable(choice);
        }

        var named = $"`{ask.Repository}` for session {ask.Working}";
        OpinionPassRun Held(string why) => new($"held  second opinion on {named}: {why}", false) { Code = choice.Code };

        // Asked BEFORE anything is made, like every hold a driven spawn asks: the adapter, then the candidate as git reads it.
        ISessionAdapter adapter;
        OpinionCandidateRead candidate;
        try
        {
            adapter = _context.Adapters.Resolve(reviewer);
            candidate = await OpinionPackets.ReadAsync(ask.Root, ask.Repository, ask.Base, ask.Tip, ct).ConfigureAwait(false);
        }
        catch (DriverException error)
        {
            return Held(error.Message);
        }

        // Its own copy, at the candidate's tip (§5.1), made before the record for the reason a session's tree is: a record for
        // a run that could never happen would explain nothing.
        OpinionTreeOpened copy;
        try
        {
            copy = await OpinionTree.OpenAsync(_context.Home, ask.Root, ask.Repository, ask.Workspace, candidate.Tip, ct).ConfigureAwait(false);
        }
        catch (DriverException error)
        {
            return Held(error.Message);
        }

        OpinionPassRun run;
        string? left;
        try
        {
            run = await ReadInCopyAsync(ask, choice, adapter, selection, reviewer, label, candidate, copy, named, ct).ConfigureAwait(false);
        }
        finally
        {
            // Removed however the run ended, the person's stop and the driver's own included: nothing in it is read back.
            left = await OpinionTree.RemoveAsync(_context.Home, copy.Path).ConfigureAwait(false);
        }

        return left is null ? run : run with { TreeLeft = left, Line = $"{run.Line} {left}" };
    }

    /// <summary>The pass from its record's open to its record's end, in its copy.</summary>
    private async Task<OpinionPassRun> ReadInCopyAsync(
        OpinionPassAsk ask, ReviewerChoice choice, ISessionAdapter adapter, HarnessSelection selection, string reviewer, string label,
        OpinionCandidateRead candidate, OpinionTreeOpened copy, string named, CancellationToken ct)
    {
        var service = _context.Service;
        var home = _context.Home;

        // The repository's own rules as they stand in the candidate, read before the reviewer starts and never after (§4).
        var rules = OpinionPackets.RulesIn(copy.Path);
        var posture = OpinionPosture.Of(adapter);
        var family = choice.Family ?? AgentFamily.Of(_context.Adapters, reviewer);

        OpinionAsked asked;
        try
        {
            asked = await service.Opinions.AskAsync(
                    new OpinionAskBody(
                        ask.Occasion, ask.Working, candidate, reviewer, label, OpinionPass.Families(choice.Working), posture,
                        ask.Rule.Bound, copy.Path)
                    {
                        // The one recheck reads the commits since its first pass's tip (XAGENT1e, design §6.5).
                        Pass = ask.Rechecks is null ? OpinionViews.FirstPass : OpinionViews.RecheckPass,
                        Rechecks = ask.Rechecks?.First.Id,
                        Product = family.Product,
                        Maker = family.Maker,
                        Account = selection.Profile,
                        HarnessVersion = selection.Version,
                    },
                    ct)
                .ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return new OpinionPassRun($"held  second opinion on {named}: the service did not answer ({error.Message}).", false) { Code = choice.Code };
        }

        if (!asked.Opened)
        {
            return new OpinionPassRun($"refused  second opinion on {named}: {asked.Message}", false) { Code = choice.Code };
        }

        var (opinion, sessionId) = (asked.Opinion!, asked.Session!);
        // The gate keeps the ask now (XAGENT1f), so every door reads the pass as being read while it runs.
        ask.Opened?.Invoke(opinion, sessionId);
        OpinionPassRun Ended(string state, string note) =>
            new($"{state}  second opinion {opinion} (session {sessionId}, {named}): {note}", true)
            {
                Opinion = opinion, Session = sessionId, State = state, Code = choice.Code, Posture = posture,
            };

        // An opinion on another account of the order says so, as a quest's start does (TOOL4f, D125 §3.2).
        RotatedOpening.Say(service, _context.Events, sessionId, reviewer, selection, carried: null);
        var bound = OpinionPass.Bound(ask.Rule, _context.Config);

        try
        {
            // The diff, in the opinion's own folder (§4): the reviewer is handed a read of it, as a resumed session is handed its
            // files (INT4j). Where git could not write it, the instruction says to read it from git in the copy.
            var folder = OpinionPackets.Folder(home, opinion);
            var diffFile = Path.Combine(folder, OpinionPackets.DiffName);
            var diff = await OpinionPackets.WriteDiffAsync(ask.Root, candidate, diffFile, ct).ConfigureAwait(false) ? diffFile : null;

            var packet = new OpinionPacket(ask.Occasion, candidate)
            {
                Quests = ask.Quests,
                Words = ask.Words,
                Rules = rules,
                Diff = diff,
                Verify = ask.Rule.Verify,
                Minutes = ask.Rule.Bound,
                Rechecks = ask.Rechecks,
            };
            var target = new SessionTarget(
                QuestId: "", $"Second opinion on {ask.Repository}", "", "Daoris", ask.Repository, copy.Path, service.BaseUrl)
            {
                Opinion = opinion,
                Session = sessionId,
                Prompt = OpinionInstruction.Compose(packet),
            };
            var (info, harnessNotice) = _driver.Prepare(adapter, target, selection);
            await service.AdvanceAsync(sessionId, "starting", ct: ct).ConfigureAwait(false);

            var transcript = DaorisHome.Entry(home, HomeEntry.Sessions, $"{sessionId}.log");
            Directory.CreateDirectory(Path.GetDirectoryName(transcript)!);

            // Its connector and nothing else (§5.4): no plugin's server, which could write where a reviewer may not. The
            // protocol door carries the connector on the wire; the pipe door takes a file under the home, as an intake's does.
            string? handed = null;
            string? preamble = null;
            if (adapter.Wire == SessionWire.Pipe)
            {
                var connector = _driver.Connector(sessionId, scope: null);
                if (connector is null) preamble = OpinionPass.NoConnector;
                handed = SpawnServers.Hand(adapter, info, home, sessionId, connector is null ? [] : [connector]);
            }

            // HELPPLUG1c (D170 point 4): the skills plugins hand sessions, though not their servers: a skill is words, and one that
            // allows itself tools is handed nowhere, so the reviewer's rules below stand. Gone with the pass.
            var (skills, skillPlugins) = SpawnSkills.Hand(adapter, info, home, sessionId, _context.Catalog, PluginServerTo.Sessions);
            using var skilled = new Disposer(() => SpawnSkills.Remove(skills));

            // What it may do (§5.2): the person's composition under the opinion's rules, held to its copy by the tree guard,
            // with a read of its folder outside it.
            var handedRules = _driver.HandRules(
                adapter, info, sessionId, ask.Workspace, ask.Repository, tree: copy.Path, kept: diff is null ? null : folder, opinion: true,
                plugins: skillPlugins);
            var opening = OpinionPass.Opening(candidate, posture);

            return await _driver.HoldAsync(
                adapter, info, target, sessionId, transcript, copy.Path, Driver.JoinNotices(harnessNotice, opening), handedRules, handed,
                // 🔴 One turn, and no words (§5.6): nobody talks to a reviewer while it reads, on either door.
                refusesInput: OpinionPass.TakesNoWords(opinion),
                ct: ct,
                preamble: Driver.JoinNotices(preamble, opening),
                handedServers: [],
                said: _driver.Said(adapter, selection, sessionId),
                minutes: bound,
                noConnector: OpinionPass.NoConnector,
                conclude: async (exitCode, used, turnFailed, ended) =>
                {
                    if (used is not null)
                    {
                        _context.Usage?.Record(new UsageEntry(
                            sessionId, ask.Repository, adapter.Name, selection.Profile, used.Used, used.Size, DateTimeOffset.UtcNow));
                    }

                    var byPerson = _context.Processes.WasStopRequested(sessionId);
                    var conclusion = byPerson
                        ? SessionConclusion.Of("stopped", _context.Processes.StopNote(sessionId) ?? Observation.Stopped)
                        : exitCode is int code
                            ? Read(opinion, code, turnFailed)
                            : SessionConclusion.Of("failed", Observation.TimedOut(bound));
                    conclusion = _driver.AccountRefused(conclusion, adapter, selection, ended);
                    conclusion = _driver.AccountSignedOut(conclusion, adapter, selection, turnFailed);
                    (conclusion, var limited) = _driver.AccountLimited(conclusion, adapter, selection, turnFailed, sessionId, used);

                    await service.AdvanceAsync(
                        sessionId, conclusion.State, note: conclusion.Note, ct: ct, limit: limited, parts: conclusion.Parts).ConfigureAwait(false);
                    return Ended(conclusion.State, conclusion.Note);
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
        catch (Exception error)
        {
            // Everything but the shutdown above, a client timeout included (REV3), as an intake's twin.
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
            _context.Output?.Close(sessionId);
        }
    }

    /// <summary>
    /// How a reviewer's run ended, from its exit and its door (D46 §4): a clean exit is <c>completed</c>, whatever it said; a
    /// refused turn or another exit is <c>failed</c>. Whether it gave its opinion is the opinion's, which the host derives.
    /// </summary>
    internal static SessionConclusion Read(string opinion, int exitCode, string? turnFailed) =>
        SessionConclusion.Of(
            turnFailed is { Length: > 0 } || exitCode != 0 ? "failed" : "completed",
            OpinionAnswers.Concluded(opinion, exitCode, turnFailed));
}
