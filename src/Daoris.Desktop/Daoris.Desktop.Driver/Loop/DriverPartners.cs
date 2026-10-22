using System.Diagnostics;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What a partner's pass is asked with (COAGENT1b): the ask, the lead's record, the repository's checkout and the copy's named folder.</summary>
/// <param name="Root">The repository's registered checkout, where the ask's base is.</param>
/// <param name="Copy">The copy's folder, named in <see cref="PartnerStates.Preparing"/> before it is made.</param>
public sealed record PartnerPassAsk(PartnerAsk Ask, PriorSession Lead, string Root, string Copy)
{
    /// <summary>
    /// Told the partner's record and its job the moment the host opens it, before its turn: the driver writes
    /// <see cref="PartnerStates.Started"/> naming them then, which <c>partner_answer</c> finds its piece by.
    /// </summary>
    public Action<string, string>? Opened { get; init; }
}

/// <summary>What a partner's pass came to: its record if one opened, how its turn ended, and only once its job was waited empty.</summary>
/// <param name="Session">The partner's record; null where none opened, and nothing ran.</param>
/// <param name="Line">What a terminal and a look say, one line.</param>
public sealed record PartnerPassRun(string? Session, string Line)
{
    /// <summary>Why nothing ran: a code of <see cref="PartnerWhy"/>.</summary>
    public string? Code { get; init; }

    /// <summary>How the turn ended (<see cref="PartnerEnds"/>), once the job is empty.</summary>
    public string? End { get; init; }

    /// <summary>What the partner said last, where its turn completed: the fallback answer.</summary>
    public string? LastWords { get; init; }
}

/// <summary>
/// A partner's piece (COAGENT1b, D199 points 2 to 4; the co-agent design §2.3): the person's ask on file is judged, its agent chosen
/// and admitted, its slot reserved in the common admission, its copy made at the ask's exact base, its record opened through the host,
/// its one turn run through the launcher in its strict job, its end waited until the job is empty, its files kept and imported as one
/// commit frozen at <see cref="PieceRefs.Of"/>, and its answer and facts written. Each side effect is made before the state that names
/// it (<see cref="PartnerStates"/>); a piece no pass of this process holds is reconciled from what is observed (<see cref="PartnerReconcile"/>).
/// </summary>
/// <remarks>
/// <para><b>Not yet here</b> (D199's COAGENT1b notes, in the design's order): the hand to the lead and its word, the notes, the role's
/// allow-list on the server, the landing doors' refusal, the measurements table and the person's press and verb. Until the measurements
/// table holds a row, <see cref="PartnerChoice"/> admits no production start: every piece is refused <see cref="PartnerWhy.NotMeasured"/>
/// unless a test hands in its choice.</para>
/// </remarks>
internal sealed class DriverPartners(LoopContext context, Driver driver)
{
    private readonly LoopContext _context = context;

    private readonly Driver _driver = driver;

    private static string Claim(string piece) => $"partner:{piece}";

    /// <summary>
    /// The pieces on file at a look: each judged and started, or carried on from where it stands, beside the look. Returns how many this
    /// look reserved, which take slots under the cap before any quest's first start, as a helper's do (D130). Nothing is read where no
    /// piece is on file.
    /// </summary>
    /// <param name="taken">The slots this look's other passes already took.</param>
    public async Task<int> PiecesAsync(Snapshot snapshot, int taken, List<string> events, CancellationToken ct)
    {
        var files = new PartnerFiles(_context.Home);
        var asks = files.Asks();
        if (asks.Count == 0) return 0;

        var begun = 0;
        foreach (var ask in asks)
        {
            if (!_context.Runs.TryOpinion(Claim(ask.Id))) continue;
            var handedOff = false;
            try
            {
                var state = files.ReadState(ask.Id);
                var seen = new PartnerObserved(
                    StateReadable: !files.Unreadable(ask.Id, "state"),
                    CopyExists: state?.Copy is { } copy && Directory.Exists(copy),
                    RecordForPiece: null,
                    Job: state?.State == PartnerStates.Started ? PieceJobs.Observe(state.Job) : PartnerJob.Empty,
                    RefCommit: null);
                var step = PartnerReconcile.Next(ask.Id, state, seen, DateTimeOffset.UtcNow);
                if (step.Write is { } write)
                {
                    files.WriteState(write);
                    state = write;
                    if (write.State == PartnerStates.Uncertain)
                    {
                        events.Add(_context.Said.Say(Line(LookClass.Progress, $"partner  piece `{ask.Id}` is uncertain ({write.Code}): its copy is kept untouched for the person.")));
                    }
                }

                switch (step.Act)
                {
                    case PartnerAct.Judge:
                        if (await JudgeAsync(ask, state, snapshot, taken + begun, files, events, ct).ConfigureAwait(false) is { } go)
                        {
                            begun++;
                            handedOff = true;
                            Beside(ask.Id, token => RunPieceAsync(ask, go.Lead, go.Root, go.Choice, files, token), ct);
                        }

                        break;
                    case PartnerAct.Prepare or PartnerAct.RemoveCopyThenPrepare:
                        // Approved or preparing with nothing of this process's running it: a restart's. The choice is made again, never an
                        // agent run again unasked: nothing of the partner's ran before `started`.
                        handedOff = true;
                        Beside(ask.Id, token => PrepareAgainAsync(ask, state!, step.Act == PartnerAct.RemoveCopyThenPrepare, files, token), ct);
                        break;
                    case PartnerAct.Keep:
                        handedOff = true;
                        Beside(ask.Id, token => KeepAsync(ask, files, lastWords: null, token), ct);
                        break;
                    case PartnerAct.Answer:
                        handedOff = true;
                        Beside(ask.Id, token => Task.FromResult(Answer(ask, files.ReadState(ask.Id)!, files, null)), ct);
                        break;
                }
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or JsonException or IOException
                                              or UnauthorizedAccessException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                events.Add(_context.Said.Retry(
                    "partner", $"partner  piece `{ask.Id}` could not be looked at this look, and is looked at again at the next: {error.Message}"));
            }
            finally
            {
                if (!handedOff) _context.Runs.OpinionDone(Claim(ask.Id));
            }
        }

        return begun;
    }

    private sealed record Go(PriorSession Lead, string Root, ReviewerChoice Choice);

    /// <summary>
    /// An ask judged (the design §2.3 steps 1 to 4): its lead a working session of this machine's, its piece and paths sound, its agent
    /// chosen and admitted, then its slot and its place in the lead's allowance reserved in one step. A verdict that refuses writes
    /// <see cref="PartnerStates.Refused"/> with its code; one that waits writes <see cref="PartnerStates.Waiting"/>.
    /// </summary>
    private async Task<Go?> JudgeAsync(
        PartnerAsk ask, PartnerState? state, Snapshot snapshot, int taken, PartnerFiles files, List<string> events, CancellationToken ct)
    {
        var lead = await _context.Service.RecordAsync(ask.Asker, ct).ConfigureAwait(false);
        if (PartnerJudge.Refusal(ask, lead) is { } refused)
        {
            events.Add(_context.Said.Say(Refuse(ask, refused, null, files)));
            return null;
        }

        var root = lead!.Tree is { Length: > 0 } tree ? await RootAsync(tree, ct).ConfigureAwait(false) : null;
        if (root is null)
        {
            events.Add(_context.Said.Say(Refuse(ask, PartnerWhy.CopyFailed, null, files)));
            return null;
        }

        var choice = _context.PartnerChoices is { } choose
            ? await choose(ask, lead, ct).ConfigureAwait(false)
            : await PartnerChoice.ChooseAsync(_context.Harnesses, ask, lead.Adapter, _context.Config, lead.Workspace, ct).ConfigureAwait(false);
        if (!choice.Chosen)
        {
            // A cool-off or a sign-in waits, tried again at a later look; a refusal by the sheet is said and ends the piece.
            if (choice.Code is PartnerWhy.NotMeasured or PartnerWhy.NoConnector or PartnerWhy.NotListed)
            {
                events.Add(_context.Said.Say(Refuse(ask, choice.Code, ask.Agent, files)));
                return null;
            }

            Wait(files, ask, state, choice.Code ?? ReviewerUnavailable.Refused);
            events.Add(_context.Said.Say(Line(LookClass.Retry, $"partner  piece `{ask.Id}` waits: {choice.Sentence}")));
            return null;
        }

        var fromOf = await FromOfAsync(ask.Asker, lead, ct).ConfigureAwait(false);
        switch (PartnerReservations.TryReserve(files, ask.Id, snapshot.Active, fromOf, _context.Config.Cap, taken, DateTimeOffset.UtcNow))
        {
            case PieceAdmitted.Reserved:
                return new Go(lead, root, choice);
            case PieceAdmitted.TooMany:
                events.Add(_context.Said.Say(Refuse(ask, PartnerWhy.TooMany, choice.Reviewer, files)));
                return null;
            case PieceAdmitted.CapFull:
                Wait(files, ask, state, "cap");
                return null;
            default:
                return null;
        }
    }

    /// <summary>The lead's <c>from</c> links up its chain, as the records say them, for its two open pieces (<see cref="PartnerAdmission.Chain"/>).</summary>
    private async Task<IReadOnlyDictionary<string, string?>> FromOfAsync(string lead, PriorSession record, CancellationToken ct)
    {
        var fromOf = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { [lead] = record.From };
        for (var (at, hops) = (record.From, 0); at is { Length: > 0 } && hops < 16 && !fromOf.ContainsKey(at); hops++)
        {
            var earlier = await _context.Service.RecordAsync(at, ct).ConfigureAwait(false);
            fromOf[at] = earlier?.From;
            at = earlier?.From;
        }

        return fromOf;
    }

    // The lead's repository's checkout is read with git, unless a test hands in a stand-in, since a real read spawns git (MOD8).
    private async Task<string?> RootAsync(string tree, CancellationToken ct) =>
        _context.PartnerTrees is { } read ? await read(tree, ct).ConfigureAwait(false) : await TreeGates.CheckoutOfTreeAsync(tree, ct).ConfigureAwait(false);

    private static void Wait(PartnerFiles files, PartnerAsk ask, PartnerState? state, string code)
    {
        if (state is { State: PartnerStates.Waiting } kept && kept.Code == code) return;
        files.WriteState(new PartnerState(ask.Id, PartnerStates.Waiting, code, DateTimeOffset.UtcNow));
    }

    /// <summary>A piece refused: its state <see cref="PartnerStates.Refused"/> with the code, which releases any reservation it held.</summary>
    private static LookLine Refuse(PartnerAsk ask, string code, string? agent, PartnerFiles files, string? detail = null)
    {
        var kept = files.ReadState(ask.Id);
        files.WriteState((kept ?? new PartnerState(ask.Id, PartnerStates.Refused, code, DateTimeOffset.UtcNow)) with
        {
            State = PartnerStates.Refused, Code = code, Agent = agent ?? kept?.Agent, At = DateTimeOffset.UtcNow,
        });
        return Line(LookClass.Progress, detail is { Length: > 0 }
            ? $"partner  piece `{ask.Id}` refused ({code}): {detail}"
            : $"partner  piece `{ask.Id}` refused ({code}).");
    }

    private static LookLine Line(LookClass @class, string line) => new(line, new LookClassed(@class, "partner"));

    /// <summary>A piece's work beside the look, as a helper's is: told opened at once, its line joining a later look's report, its claim released at its end.</summary>
    private void Beside(string piece, Func<CancellationToken, Task<LookLine>> work, CancellationToken ct) =>
        _ = _context.Runs.StartAsync(async opened =>
        {
            opened();
            try
            {
                var line = await work(ct).ConfigureAwait(false);
                return new StartRun(line.Text, Opened: true) { Look = line.Look };
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or JsonException or IOException
                                              or UnauthorizedAccessException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                return new StartRun($"partner  piece `{piece}` stopped, and is looked at again at the next look: {error.Message}", Opened: true)
                {
                    Look = new LookClassed(LookClass.Retry, "partner"),
                };
            }
            finally
            {
                _context.Runs.OpinionDone(Claim(piece));
            }
        });

    /// <summary>An approved or preparing piece a restart left: a partial copy removed, the lead read again, then prepared as at first.</summary>
    private async Task<LookLine> PrepareAgainAsync(PartnerAsk ask, PartnerState state, bool removeCopy, PartnerFiles files, CancellationToken ct)
    {
        if (removeCopy && state.Copy is { } copy && await OpinionTree.RemoveAsync(_context.Home, copy, PartnerPass.Prefix).ConfigureAwait(false) is { } left)
        {
            return Line(LookClass.Retry, $"partner  piece `{ask.Id}` waits for its partial copy to go: {left}");
        }

        var lead = await _context.Service.RecordAsync(ask.Asker, ct).ConfigureAwait(false);
        if (PartnerJudge.Refusal(ask, lead) is { } refused) return Refuse(ask, refused, state.Agent, files);
        var root = lead!.Tree is { Length: > 0 } tree ? await RootAsync(tree, ct).ConfigureAwait(false) : null;
        if (root is null) return Refuse(ask, PartnerWhy.CopyFailed, state.Agent, files);
        var choice = _context.PartnerChoices is { } choose
            ? await choose(ask, lead, ct).ConfigureAwait(false)
            : await PartnerChoice.ChooseAsync(_context.Harnesses, ask, lead.Adapter, _context.Config, lead.Workspace, ct).ConfigureAwait(false);
        if (!choice.Chosen) return Refuse(ask, choice.Code ?? PartnerWhy.NotMeasured, ask.Agent, files);
        return await RunPieceAsync(ask, lead, root, choice, files, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// One reserved piece carried to its answer (the design §2.3 steps 5 to 8): <c>preparing</c> naming the copy, the pass (the copy, the
    /// job, the record, <c>started</c>, the turn, the job waited empty), <c>ending</c> with the keeper's moment, then the keeper.
    /// </summary>
    private async Task<LookLine> RunPieceAsync(PartnerAsk ask, PriorSession lead, string root, ReviewerChoice choice, PartnerFiles files, CancellationToken ct)
    {
        var agent = choice.Reviewer!;
        var copy = PartnerPass.CopyFor(_context.Home, lead.Workspace, lead.Repository ?? "", ask.Id);
        var preparing = (files.ReadState(ask.Id) ?? new PartnerState(ask.Id, PartnerStates.Preparing, null, DateTimeOffset.UtcNow)) with
        {
            State = PartnerStates.Preparing, Code = null, Copy = copy, Agent = agent, Account = choice.Selection?.Profile, At = DateTimeOffset.UtcNow,
        };
        files.WriteState(preparing);

        var pass = new PartnerPassAsk(ask, lead, root, copy)
        {
            // 🔴 Started, naming the record and the job, BEFORE its turn: `partner_answer` finds its piece by this state.
            Opened = (partner, job) => files.WriteState(files.ReadState(ask.Id)! with
            {
                State = PartnerStates.Started, Partner = partner, Job = job, At = DateTimeOffset.UtcNow,
            }),
        };
        var run = await (_context.PartnerPasses ?? PartnerPassAsync)(pass, choice, ct).ConfigureAwait(false);
        if (run.Session is null)
        {
            // Nothing of the partner's ran: the copy, if any of it was made, is Daoris's own to remove.
            await OpinionTree.RemoveAsync(_context.Home, copy, PartnerPass.Prefix).ConfigureAwait(false);
            return Refuse(ask, run.Code ?? PartnerWhy.CopyFailed, agent, files, run.Line);
        }

        // The job is empty: no process of the partner's, nor a child, can still write the copy. The keeper's moment dates its commit.
        var now = DateTimeOffset.UtcNow;
        files.WriteState(files.ReadState(ask.Id)! with { State = PartnerStates.Ending, End = run.End ?? PartnerEnds.Answered, Kept = now, At = now });
        return await KeepAsync(ask, files, run.LastWords, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The keeper (the design §2.3 step 7, §7.9): the copy read as files and judged, one commit written onto the base in the repository,
    /// frozen create-only. <c>imported</c> removes the copy and goes on to the answer; <c>held</c> keeps it for the person; no work refuses
    /// the piece <see cref="PartnerWhy.NoWork"/>, its copy removed.
    /// </summary>
    private async Task<LookLine> KeepAsync(PartnerAsk ask, PartnerFiles files, string? lastWords, CancellationToken ct)
    {
        var state = files.ReadState(ask.Id)!;
        if (state.Copy is not { } copy || state.Kept is not { } moment)
        {
            files.WriteState(state with { State = PartnerStates.Uncertain, Code = PartnerWhy.StateUnreadable, At = DateTimeOffset.UtcNow });
            return Line(LookClass.Progress, $"partner  piece `{ask.Id}` is uncertain: its state names no copy or no keeper's moment.");
        }

        // A partner's last words are kept before the copy goes, since they stand as its answer where it gave none through its tool.
        if (lastWords is { Length: > 0 } && files.ReadAnswer(ask.Id) is null) files.WriteAnswer(new PartnerAnswer(ask.Id, lastWords, null, DateTimeOffset.UtcNow) { LastWords = true });

        var kept = await (_context.PartnerKeeps ?? KeepInRepositoryAsync)(ask, state, ct).ConfigureAwait(false);
        switch (kept.State)
        {
            case PartnerStates.Imported:
                files.WriteState(state with { State = PartnerStates.Imported, Code = null, Commit = kept.Commit, At = DateTimeOffset.UtcNow });
                var left = await OpinionTree.RemoveAsync(_context.Home, copy, PartnerPass.Prefix).ConfigureAwait(false);
                if (left is null) files.WriteState(files.ReadState(ask.Id)! with { CopyRemoved = true });
                return Answer(ask, files.ReadState(ask.Id)!, files, kept.Judged);
            case PartnerStates.Held:
                files.WriteState(state with { State = PartnerStates.Held, Code = kept.Code, At = DateTimeOffset.UtcNow });
                if (kept.Judged is { } held) files.WriteFacts(Facts(ask, "", held));
                return Line(LookClass.Progress, $"partner  piece `{ask.Id}` is held ({kept.Code}): its import was refused, and its copy is kept for the person.");
            case null:
                await OpinionTree.RemoveAsync(_context.Home, copy, PartnerPass.Prefix).ConfigureAwait(false);
                files.WriteState(state with { State = PartnerStates.Refused, Code = PartnerWhy.NoWork, CopyRemoved = !Directory.Exists(copy), At = DateTimeOffset.UtcNow });
                return Line(LookClass.Progress, $"partner  piece `{ask.Id}` left no work in its copy.");
            default:
                files.WriteState(state with { State = PartnerStates.Uncertain, Code = kept.Code, At = DateTimeOffset.UtcNow });
                return Line(LookClass.Progress, $"partner  piece `{ask.Id}` is uncertain ({kept.Code}): {kept.Why}");
        }
    }

    private async Task<PieceKept> KeepInRepositoryAsync(PartnerAsk ask, PartnerState state, CancellationToken ct)
    {
        var lead = await _context.Service.RecordAsync(ask.Asker, ct).ConfigureAwait(false);
        var root = lead?.Tree is { Length: > 0 } tree ? await RootAsync(tree, ct).ConfigureAwait(false) : null;
        if (root is null) return new(PartnerStates.Uncertain, null, PartnerWhy.CopyFailed, null, "the repository's checkout could not be read.");
        var index = DaorisHome.Entry(_context.Home, HomeEntry.Partners, $"{ask.Id}.index");
        return await PieceKeeper.KeepAsync(root, state.Copy!, ask, index, state.Kept!.Value, PartnerPass.Message(ask, state.Agent, state.Account), ct).ConfigureAwait(false);
    }

    /// <summary>The facts Daoris read itself, never the partner's words: the commit, its files, those beyond the piece, and what was left out.</summary>
    private static PartnerFacts Facts(PartnerAsk ask, string commit, PieceImport.Judged judged)
    {
        IReadOnlyList<string> touched = [.. judged.Changed.Select(change => change.Entry.Path).Concat(judged.Deleted).Order(StringComparer.Ordinal)];
        return new PartnerFacts(ask.Id, commit, touched, PieceImport.Beyond(touched, ask.Paths), judged.LeftOut, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// The answer (the design §2.3 step 8): the facts written beside the partner's own words, then <see cref="PartnerStates.Answered"/>. The
    /// hand to the lead is the next sub-feature's; until it lands, an answered piece waits at <c>answered</c>.
    /// </summary>
    private static LookLine Answer(PartnerAsk ask, PartnerState state, PartnerFiles files, PieceImport.Judged? judged)
    {
        if (judged is not null || files.ReadFacts(ask.Id) is null)
        {
            files.WriteFacts(judged is null
                ? new PartnerFacts(ask.Id, state.Commit ?? "", [], [], [], DateTimeOffset.UtcNow)
                : Facts(ask, state.Commit ?? "", judged));
        }

        files.WriteState(state with { State = PartnerStates.Answered, At = DateTimeOffset.UtcNow });
        var said = files.ReadAnswer(ask.Id) is { } answer ? (answer.LastWords ? "its last words stand as its answer" : "it answered through its tool") : "it gave no answer";
        return Line(LookClass.Progress,
            $"partner  piece `{ask.Id}` by {state.Agent ?? "a partner"} is answered: {TextBounds.ShortCommit(state.Commit ?? "", 7)} frozen at {PieceRefs.Of(ask.Id)}, {said}.");
    }

    /// <summary>
    /// The partner's pass (the design §2.3 steps 5 and 6): the copy at the ask's exact base, the strict job, the record, its one turn
    /// through the launcher under the partner's bound, then the job ended and waited empty. A copy or a job that cannot be made, or a
    /// record the host refuses, starts nothing.
    /// </summary>
    internal async Task<PartnerPassRun> PartnerPassAsync(PartnerPassAsk ask, ReviewerChoice choice, CancellationToken ct = default)
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

        if (await PieceCopy.MakeAsync(ask.Root, ask.Copy, ask.Ask.Base, ct).ConfigureAwait(false) is { } failed)
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
            return await TurnAsync(ask, adapter, selection, agent, sessionId, job, launcher, ct).ConfigureAwait(false);
        }
        finally
        {
            // 🔴 Whatever ended the turn, nothing of the partner's outlives it: the job ended and waited empty before `ending` is written.
            job.Terminate();
            await job.WaitEmptyAsync().ConfigureAwait(false);
        }
    }

    private async Task<PartnerPassRun> TurnAsync(
        PartnerPassAsk ask, ISessionAdapter adapter, HarnessSelection selection, string agent, string sessionId, ProcessJob job, string launcher,
        CancellationToken ct)
    {
        var service = _context.Service;
        var home = _context.Home;
        var output = _context.Output;
        var piece = ask.Ask.Id;
        var repository = ask.Lead.Repository ?? "";
        RotatedOpening.Say(service, _context.Events, sessionId, agent, selection, carried: null);
        var bound = PartnerPass.Bound(_context.Config);
        PartnerPassRun Ended(string state, string end, string note, string? lastWords = null) =>
            new(sessionId, $"{state}  partner {sessionId} (piece `{piece}`): {note}") { End = end, LastWords = lastWords };

        try
        {
            var packet = new PartnerPacket(ask.Ask.Asker, repository, ask.Ask.Base, ask.Ask.Piece, ask.Ask.Paths, ask.Ask.Uncommitted, bound);
            var target = new SessionTarget(QuestId: "", $"Partner for session {ask.Ask.Asker}", "", "Daoris", repository, ask.Copy, service.BaseUrl)
            {
                Piece = piece,
                Session = sessionId,
                Prompt = PartnerInstruction.Compose(packet),
            };
            var (info, harnessNotice) = _driver.Prepare(adapter, target, selection);
            // 🔴 The start made the launcher's (D199's note on the strict start): it joins the piece's job before the harness runs.
            if (PieceStarts.Through(info, launcher, job, piece) is { } unjoined) throw new DriverException(unjoined);
            await _driver.KeepHandedAsync(sessionId, info, ct).ConfigureAwait(false);
            await service.AdvanceAsync(sessionId, "starting", ct: ct).ConfigureAwait(false);

            var transcript = DaorisHome.Entry(home, HomeEntry.Sessions, $"{sessionId}.log");
            Directory.CreateDirectory(Path.GetDirectoryName(transcript)!);

            string? handed = null;
            string? preamble = null;
            if (adapter.Wire == SessionWire.Pipe)
            {
                var connector = _driver.Connector(sessionId, scope: null);
                if (connector is null) preamble = PartnerPass.NoConnector;
                handed = SpawnServers.Hand(adapter, info, home, sessionId, connector is null ? [] : [connector]);
            }

            // What it may do: the person's composition for its repository, held to its copy by the tree guard. The partner's own rules
            // (`PartnerPermissions`) and the server's allow-list are the role's sub-feature.
            var handedRules = _driver.HandRules(adapter, info, sessionId, ask.Lead.Workspace, repository, tree: ask.Copy, kept: null);
            var opening = PartnerPass.Opening(repository, ask.Ask.Base);

            return await _driver.HoldAsync(
                adapter, info, target, sessionId, transcript, ask.Copy, Driver.JoinNotices(harnessNotice, opening), handedRules, handed,
                refusesInput: PartnerPass.TakesNoWords(piece),
                ct: ct,
                preamble: Driver.JoinNotices(preamble, opening),
                handedServers: [],
                said: _driver.Said(adapter, selection, sessionId),
                minutes: bound,
                noConnector: PartnerPass.NoConnector,
                conclude: async (exitCode, used, turnFailed, ended) =>
                {
                    if (used is not null)
                    {
                        _context.Usage?.Record(new UsageEntry(sessionId, repository, adapter.Name, selection.Profile, used.Used, used.Size, DateTimeOffset.UtcNow));
                    }

                    var byPerson = _context.Processes.WasStopRequested(sessionId);
                    var conclusion = byPerson
                        ? SessionConclusion.Of("stopped", _context.Processes.StopNote(sessionId) ?? Observation.Stopped)
                        : exitCode is int code
                            ? DriverHelpers.ReadHelper(ask.Ask.Asker, code, turnFailed)
                            : SessionConclusion.Of("failed", Observation.TimedOut(bound));
                    conclusion = _driver.AccountRefused(conclusion, adapter, selection, ended);
                    conclusion = _driver.AccountSignedOut(conclusion, adapter, selection, turnFailed);
                    (conclusion, var limited) = _driver.AccountLimited(conclusion, adapter, selection, turnFailed, sessionId, used);

                    await service.AdvanceAsync(sessionId, conclusion.State, note: conclusion.Note, ct: ct, limit: limited, parts: conclusion.Parts).ConfigureAwait(false);
                    var end = PartnerTurns.End(byPerson, exitCode, limited);
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
}
