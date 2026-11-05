namespace Daoris.Driver;

/// <summary>One partner pass: admitted, its copy, its strict job, its record and its one turn, the job ended and waited empty after.</summary>
internal sealed class PartnerPassRunner(LoopContext context, Driver driver)
{
    private readonly LoopContext _context = context;

    private readonly Driver _driver = driver;

    /// <summary>
    /// The partner's pass (the design §2.3 steps 5 and 6): the copy at the ask's exact base, the strict job, the record, its one turn
    /// through the launcher under the partner's bound, then the job ended and waited empty. A copy or a job that cannot be made, or a
    /// record the host refuses, starts nothing.
    /// </summary>
    internal async Task<PartnerPassRun> PartnerPassAsync(PartnerPassAsk ask, ReviewerChoice choice, CancellationToken ct = default)
    {
        var reask = ask.Reask is { } walk ? new OneTurnReask(choice, walk) : null;
        var run = await PartnerAttemptAsync(ask, choice, reask, makeCopy: true, ct).ConfigureAwait(false);
        // D205 §3.5(f): its agent refused the entry's model at the open, so the refused record has ended `choice-not-offered` and this call
        // asks its walk again before it publishes an outcome. Named by its ask (D199), the walk is the named agent's other accounts, then
        // its other entries; never another agent. Its job has ended and waited empty, its copy untouched, and the replacement opens in both.
        while (run is { NotOffered: true } && reask is not null && !ct.IsCancellationRequested)
        {
            var next = await reask.NextAsync().ConfigureAwait(false);
            if (next is not { Chosen: true, Selection.Allowed: true })
            {
                return new PartnerPassRun(null, $"held  partner for piece `{ask.Ask.Id}`: {next.Sentence}") { Code = PartnerWhy.NotMeasured };
            }

            ask.Replaced?.Invoke(next);
            run = await PartnerAttemptAsync(ask, next, reask, makeCopy: false, ct).ConfigureAwait(false);
        }

        return run;
    }

    /// <summary>One try of the partner's pass with <paramref name="choice"/>: admitted, its copy (the first try's), its job and its record.</summary>
    private async Task<PartnerPassRun> PartnerAttemptAsync(PartnerPassAsk ask, ReviewerChoice choice, OneTurnReask? reask, bool makeCopy, CancellationToken ct)
    {
        var piece = ask.Ask.Id;
        if (choice is not { Chosen: true, Selection: { Allowed: true } selection, Reviewer: { } agent })
        {
            return new PartnerPassRun(null, $"held  partner for piece `{piece}`: {choice.Sentence}") { Code = PartnerWhy.NotMeasured };
        }

        ISessionAdapter adapter;
        try
        {
            adapter = _context.Adapters.Resolve(agent);
        }
        catch (DriverException error)
        {
            return new PartnerPassRun(null, $"held  partner for piece `{piece}`: {error.Message}") { Code = PartnerWhy.NotMeasured };
        }

        // Admission at the start (D199 point 4): the whole effective configuration this start would hand, held to a measured row, before
        // anything of the partner's is made. A production start reads no stand-in's row.
        var (rules, hardDeny, guarded) = _driver.PartnerHanded(ask.Lead.Workspace, ask.Lead.Repository ?? "");
        var configuration = PartnerConfigurations.Of(adapter, selection.Version, ask.Copy, rules, hardDeny, guarded, ThisSystem());
        if (PartnerMeasurements.Admitting(_context.PartnerMeasured ?? PartnerMeasurements.Rows, configuration, production: _context.PartnerMeasured is null) is null)
        {
            return new PartnerPassRun(null, $"refused  partner for piece `{piece}`: {PartnerMeasurements.Refused(configuration)}") { Code = PartnerWhy.NotMeasured };
        }

        if (makeCopy && await PieceCopy.MakeAsync(ask.Root, ask.Copy, ask.Ask.Base, ct).ConfigureAwait(false) is { } failed)
        {
            return new PartnerPassRun(null, $"held  partner for piece `{piece}`: {failed.Why}") { Code = failed.Code };
        }

        using var job = PieceJobs.Create(piece);
        var launcher = PieceStarts.LauncherPath(_context.Home);
        if (job is null || launcher is null)
        {
            return new PartnerPassRun(null, $"held  partner for piece `{piece}`: {PieceStarts.Missing(launcher, job, piece)}")
            {
                Code = PartnerWhy.NoJob,
            };
        }

        var service = _context.Service;
        var opened = await service.Partners.OpenAsync(
            ask.Ask.Asker, piece, agent, ask.Copy, selection.Profile, selection.Version, ask.Ask.Base, ct).ConfigureAwait(false);
        if (!opened.Opened) return new PartnerPassRun(null, $"refused  partner for piece `{piece}`: {opened.Message}") { Code = PartnerWhy.CopyFailed };

        var sessionId = opened.Session!;
        _context.Processes.MarkOpening(sessionId);
        ask.Opened?.Invoke(sessionId, PieceJobs.NameOf(piece));
        try
        {
            return await TurnAsync(ask, choice, adapter, selection, agent, sessionId, configuration, reask, ct).ConfigureAwait(false);
        }
        finally
        {
            // 🔴 Whatever ended the turn, nothing of the partner's outlives it: the job ended and waited empty before `ending` is written.
            job.Terminate();
            await job.WaitEmptyAsync().ConfigureAwait(false);
        }
    }

    private async Task<PartnerPassRun> TurnAsync(
        PartnerPassAsk ask, ReviewerChoice choice, ISessionAdapter adapter, HarnessSelection selection, string agent, string sessionId,
        PartnerConfiguration configuration, OneTurnReask? reask, CancellationToken ct)
    {
        var service = _context.Service;
        var home = _context.Home;
        var output = _context.Output;
        var piece = ask.Ask.Id;
        var repository = ask.Lead.Repository ?? "";
        RotatedOpening.Say(service, _context.Events, sessionId, agent, selection, carried: null);
        OneTurnReask.Say(_context.Events, sessionId, "partner", choice, _driver.Log);
        var bound = PartnerPass.Bound(_context.Config);
        PartnerPassRun Ended(string state, string end, string note, string? lastWords = null) =>
            new(sessionId, $"{state}  partner {sessionId} (piece `{piece}`): {note}") { End = end, LastWords = lastWords };

        SessionStop? stoppedFirst = null;

        try
        {
            var packet = new PartnerPacket(ask.Ask.Asker, repository, ask.Ask.Base, ask.Ask.Piece, ask.Ask.Paths, ask.Ask.Uncommitted, bound);
            var target = new SessionTarget(QuestId: "", $"Partner for session {ask.Ask.Asker}", "", "Daoris", repository, ask.Copy, service.BaseUrl)
            {
                Piece = piece,
                Session = sessionId,
                Prompt = PartnerInstruction.Compose(packet),
                // CONTEXTONE1c: its standing block, its copy's facts and the person's choices for its repository.
                Block = StandingBlocks.OnMachine(StandingBlocks.OfTree(repository, ask.Copy), ParticipantWork.Partner, _context.Config, [], null),
                // MODELROLE1e (D205 §3.2, §4.4): the entry's model and effort, carried on either door, and the choice its record keeps.
                Asked = choice.Effective?.Asked,
                Choice = choice.Effective?.From,
            };
            var (info, harnessNotice) = _driver.Prepare(adapter, target, selection);
            await service.AdvanceAsync(sessionId, "starting", ct: ct).ConfigureAwait(false);

            var transcript = DaorisHome.Entry(home, HomeEntry.Sessions, $"{sessionId}.log");
            Directory.CreateDirectory(Path.GetDirectoryName(transcript)!);

            string? handed = null;
            string? preamble = null;
            if (!adapter.Abilities().ServersOnWire)
            {
                var connector = _driver.Connector(sessionId, scope: null);
                if (connector is null) preamble = PartnerPass.NoConnector;
                handed = SpawnServers.Hand(adapter, info, home, sessionId, connector is null ? [] : [connector]);
            }

            // What it may do: the person's composition for its repository under the partner's rules (`PartnerPermissions`), held to its
            // copy by the tree guard; the connector's host refuses the same tools by the record's role whatever rules its agent takes.
            var handedRules = _driver.HandRules(adapter, info, sessionId, ask.Lead.Workspace, repository, tree: ask.Copy, kept: null, partner: true);
            // The effective configuration it was handed (§3.3), admitted by a measured row before the copy was made: what M2 measures in.
            _driver.Log?.Info(
                "partner.started", ("session", sessionId), ("piece", piece), ("agent", configuration.Agent),
                ("version", configuration.HarnessVersion ?? ""), ("posture", configuration.Posture ?? ""), ("roots", configuration.WritableRoots),
                ("network", configuration.Network), ("system", configuration.System), ("digest", configuration.SettingsDigest));
            var opening = PartnerPass.Opening(repository, ask.Ask.Base);

            return await _driver.Holds.HoldAsync(
                "partner", adapter, info, target, sessionId, transcript, ask.Copy, Driver.JoinNotices(harnessNotice, opening), handedRules, handed,
                refusesInput: PartnerPass.TakesNoWords(piece),
                ct: ct,
                // 🔴 The start made the launcher's at the start itself (D199's note on the strict start, TERMTRUST1a): it joins this
                // process's agents' job, then the piece's beneath it, before the harness runs, and is never started outside the piece's.
                piece: PieceJobs.NameOf(piece),
                preamble: Driver.JoinNotices(preamble, opening),
                handedServers: [],
                said: _driver.Said(adapter, selection, sessionId), limits: _driver.NativeLimit(adapter, selection, sessionId),
                minutes: bound,
                transientWanted: TransientEnded.Wanted(_context.Processes, sessionId),
                noConnector: PartnerPass.NoConnector,
                stopMade: stop => stoppedFirst = stop,
                // D205 §3.5(f): its agent's refusal of the entry's model at the open is kept as the account's reading and asked again in
                // this call: a later entry of the same agent on the same account is set on this session, anything else ends the record.
                offers: new OpenOffers(selection.Version, selection.Profile, reask is null ? null : _ => reask.SameAccountAsync()),
                conclude: async (exitCode, used, turnFailed, ended) =>
                {
                    if (used is not null)
                    {
                        _context.Usage?.Record(new UsageEntry(sessionId, repository, adapter.Name, selection.Profile, used.Used, used.Size, DateTimeOffset.UtcNow));
                    }

                    // Refused at the open, before any prompt: the record ends `choice-not-offered`, its one count given back once its
                    // process has stopped, and the pass asks its walk again (above) before it publishes an outcome.
                    if (!_context.Processes.WasStopRequested(sessionId) && ended.NotOffered is { } notOffered)
                    {
                        var refusedEnd = Observation.ChoiceNotOffered(adapter.Name, notOffered);
                        await service.AdvanceAsync(sessionId, refusedEnd.State, note: refusedEnd.Note, ct: ct, parts: refusedEnd.Parts).ConfigureAwait(false);
                        _context.Harnesses.Release(selection.Number, sessionId);
                        return Ended(refusedEnd.State, PartnerEnds.Answered, refusedEnd.Note) with { NotOffered = true };
                    }

                    var byPerson = _context.Processes.WasStopRequested(sessionId);
                    var conclusion = byPerson
                        ? SessionConclusion.Of("stopped", _context.Processes.StopNote(sessionId) ?? Observation.Stopped)
                        : exitCode is int code
                            ? DriverHelpers.ReadHelper(ask.Ask.Asker, code, turnFailed)
                            : SessionConclusion.Of("failed", Observation.TimedOut(bound));
                    conclusion = _driver.AccountRefused(conclusion, adapter, selection, ended);
                    conclusion = _driver.AccountSignedOut(conclusion, adapter, selection, turnFailed, ended);
                    (conclusion, var limited) = _driver.AccountLimited(conclusion, adapter, selection, turnFailed, sessionId, used);
                    var transient = !byPerson && !limited && TransientEnded.Of(ended, timedOut: exitCode is null) is not null;
                    if (transient) conclusion = TransientEnded.Over(conclusion, ended, TransientEnded.Partner, adapter.Name, timedOut: exitCode is null);

                    await service.AdvanceAsync(sessionId, conclusion.State, note: conclusion.Note, ct: ct, limit: limited, parts: conclusion.Parts).ConfigureAwait(false);
                    var end = transient ? PartnerEnds.Transient : PartnerTurns.End(byPerson, exitCode, limited);
                    var lastWords = conclusion.State == "completed" ? SessionLastWords.ParkedWords(_context.Events, sessionId, transcript) : null;
                    return Ended(conclusion.State, end, conclusion.Note, lastWords);
                }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            try
            {
                await service.AdvanceAsync(sessionId, "stopped", Observation.DriverClosed, ct: CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort by construction: the host may already be gone on the same shutdown.
            }

            return Ended("stopped", PartnerEnds.Stopped, "the driver was stopped.");
        }
        catch (Exception) when (stoppedFirst is not null)
        {
            // A wire that broke after a stop is that stop's end, not a failure (STOPRACE1), as a quest's session reads it.
            var stopped = await _driver.EndStoppedAsync(sessionId, stoppedFirst).ConfigureAwait(false);
            return Ended(stopped.State, PartnerEnds.Stopped, stopped.Note);
        }
        catch (Exception error)
        {
            try
            {
                await service.AdvanceAsync(sessionId, "failed", Observation.Failure(error.Message), ct: CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The terminal write is best-effort by construction: the first failure is the report.
            }

            return Ended("failed", PartnerEnds.Answered, error.Message);
        }
        finally
        {
            output?.Close(sessionId);
        }
    }

    /// <summary>
    /// The machine's system as a partner's configuration names it, read here so the configuration's domain stays pure; a test's
    /// stand-in row reads it too (<c>PartnerStandIns</c>).
    /// </summary>
    internal static string ThisSystem() =>
        $"{(OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : "linux")} {Environment.OSVersion.Version}";
}
