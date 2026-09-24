using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// The intake half of the loop (D65 §1b): an ask the declarations left to a person is answered by a
/// SESSION this driver opens in the circle's room — the driver's brain is a session, not a model (D24).
/// </summary>
/// <remarks>
/// <para><b>The same spawn as a quest's, on the driven capture path</b> (§1b's trap): an intake is one
/// turn — the ask is its target — so it is framed as one prompt on either door, never fed raw lines the
/// way a chat's stdin is.</para>
///
/// <para><b>Observed, never self-reported</b> (D46 §4): the record moves on the exit code and on what
/// became of the ASK. Published onto it by the intake is done; a clean exit that published nothing is
/// the intake asking the person, which parks the record until the person answers the ask.</para>
///
/// <para><b>It publishes; it never edits</b> (D32). Its room is Daoris's own directory, its allow-list
/// reads the family and publishes, and it holds no repository's tree.</para>
/// </remarks>
public sealed partial class Driver
{
    /// <summary>What an intake with no connector can and cannot do — said on its transcript.</summary>
    private const string NoConnectorForIntake =
        "— no daoris-knowledge on this machine, so this intake has no connector: it can decide, but "
        + "cannot publish what it decides, and will end by asking you. `npm run publish:service -- "
        + "--install` lands one.";

    /// <summary>
    /// Why a person's line is refused for a running intake, and where their answer goes instead — the
    /// sentence the bridge carries verbatim, so a stale page is told the truth rather than "it ended".
    /// </summary>
    internal static string TakesNoMessages(string ask) =>
        $"the intake for ask #{ask} takes no messages: an intake is one turn, and where the "
        + "declarations do not settle it, it asks you by parking. The answer is on the ask — publish it "
        + "to a repository, or close it.";

    /// <summary>
    /// The asks due an intake this tick: none unless a harness is named for it; otherwise the oldest
    /// unserved ask in each circle, as many as the slots allow.
    /// </summary>
    private async Task<IReadOnlyList<AskView>> IntakesDueAsync(int slots, List<string> events, CancellationToken ct)
    {
        if (config.IntakeAdapter is not { Length: > 0 }) return [];

        IReadOnlyList<AskView> asks;
        try
        {
            asks = await service.AsksAsync(ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
        {
            // A host older than the asks answers no ask door; the quests still drive, and this says why
            // no ask is being answered rather than looking like a machine with none.
            events.Add($"intake  the asks could not be read, so none is answered this tick: {error.Message}");
            return [];
        }

        // Proposed and never served: what the declarations left to a person and no intake has had.
        // One per circle, because the room runs one intake at a time — the OLDEST in each, which the
        // service's newest-first order puts last.
        var due = asks
            .Where(ask => ask.State == "Proposed" && ask.Intake is null)
            .GroupBy(ask => ask.Workspace, StringComparer.OrdinalIgnoreCase)
            .Select(circle => circle.Last())
            .ToList();
        if (due.Count == 0) return [];

        if (slots <= 0)
        {
            // Sitting must say why (D46 §3) — for an ask as for a quest.
            events.Add(
                $"intake  {string.Join(", ", due.Select(ask => $"ask #{ask.Id}"))} waiting: the concurrency cap "
                + $"({config.Cap}) is spent — it frees as sessions finish.");
            return [];
        }

        return [.. due.Take(slots)];
    }

    /// <returns>
    /// The console line, whether a session actually opened, and — when one ENDED here — what it ended
    /// as. A park is not an ending: the record waits for the person, and the tick that finds it parked
    /// is what tells them (SURF5b), once, from the one place parks are seen.
    /// </returns>
    private async Task<(string Line, bool Opened, SessionEnded? Ended)> RunIntakeAsync(
        AskView ask, List<TrustHold> untrusted, CancellationToken ct)
    {
        var adapterName = config.IntakeAdapter!;
        var named = $"ask #{ask.Id} in {ask.Workspace}";
        (string, bool, SessionEnded?) Hold(string why) => ($"held  intake for {named}: {why}", false, null);

        // Asked BEFORE the record, like every hold a driven spawn asks: a record for a spawn that could
        // never happen would mark the ask served and explain nothing.
        ISessionAdapter adapter;
        HarnessSelection selection;
        try
        {
            adapter = _adapters.Resolve(adapterName);
            selection = await _harnesses.SelectAsync(adapterName, config, ask.Workspace, chosen: null, ct)
                .ConfigureAwait(false);
        }
        catch (DriverException error)
        {
            return Hold(error.Message);
        }

        if (!selection.Allowed) return Hold(selection.Refusal!);

        // The room, written from the circle's declarations as they stand now.
        string room;
        try
        {
            room = IntakeRoom.Prepare(
                home, ask.Workspace, await service.DeclarationsAsync(ask.Workspace, ct).ConfigureAwait(false));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException
                                          or HttpRequestException or System.Text.Json.JsonException)
        {
            return Hold($"its room could not be written — {error.Message}");
        }

        // The same trust question a driven spawn asks (DEPLOY1): in a room the harness has not been
        // told to trust, the room's allow-list is inert — so it is asked only where the rules handed
        // over at spawn would not let the intake publish (D73), exactly as for a quest's session.
        if (adapter.Toolchain is { TrustFile: { Length: > 0 } trustFile }
            && !HandedConnector(adapter, ask.Workspace, repository: null, "quest_publish"))
        {
            var configHome = selection.ProfileHome ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var trustPath = Path.Combine(configHome, trustFile);
            if (ClaudeTrust.Accepted(trustPath, room) == false)
            {
                // The room is Daoris's own folder under the home, and trusting it is still the person's
                // grant: the fact goes to the screen beside the sentence, as a quest's does (D73).
                lock (untrusted) untrusted.Add(new TrustHold(room, trustPath, Ask: ask.Id));
                return Hold(ClaudeTrust.Refusal(room, selection.ProfileHome is null ? null : selection.Profile));
            }
        }

        var (sessionId, message) = await service
            .OpenIntakeAsync(ask.Id, adapterName, room, selection.Version, selection.Profile, ct)
            .ConfigureAwait(false);
        if (sessionId is null)
        {
            return ($"refused  intake for {named}: {message}", false, null);
        }

        try
        {
            var scope = IntakeRoom.Scope(ask.Id, sessionId);
            var target = new SessionTarget(
                QuestId: "", ask.Sentence.Split('\n', 2)[0].Trim(), ask.Sentence, ask.Asker ?? "the person",
                $"ask #{ask.Id}", room, service.BaseUrl)
            {
                Links = ask.Links,
                Attachments = ask.Attachments,
                Ask = ask.Id,
                Session = sessionId,
                Prompt = IntakePrompt.Compose(ask),
            };
            var (info, harnessNotice) = Prepare(adapter, target, selection);

            await service.AdvanceAsync(sessionId, "starting", ct: ct).ConfigureAwait(false);

            var transcript = Path.Combine(home, "sessions", $"{sessionId}.log");
            Directory.CreateDirectory(Path.GetDirectoryName(transcript)!);

            // 🔴 The connector is handed, never assumed (§1b's first trap): a chat is given none, and
            // the room has no `.mcp.json` of its own. The protocol door carries it on the wire below;
            // the pipe door takes a file under the home, the knowledge host first, with the ask and
            // this session in its environment so what it publishes is asked by the ask.
            string? handed = null;
            string? preamble = null;
            if (adapter.Wire == SessionWire.Pipe)
            {
                var connector = Connector(scope);
                if (connector is null) preamble = NoConnectorForIntake;
                handed = SpawnServers.Write(home, sessionId, connector is null ? _servers : [connector, .. _servers]);
                if (handed is not null) adapter.HandServers(info, handed);
            }

            // What the intake may do (PERM1, D72): its circle's rules and the machine's — it serves an
            // ask, and belongs to no repository.
            var rules = HandRules(adapter, info, sessionId, ask.Workspace, repository: null);

            using var process = Process.Start(info)
                ?? throw new DriverException($"the {adapter.Name} adapter's process did not start");
            // 🔴 One turn takes no messages (INT4h), on either door: the pipe door gives it no stdin,
            // and the protocol door's stdin is the driver's own frames — a person's line written
            // there would land in the middle of the JSON-RPC stream.
            using var tracked = _processes.Track(sessionId, process, refusesInput: TakesNoMessages(ask.Id));
            using var _ = new Disposer(() => SpawnServers.Remove(handed));
            using var ruled = new Disposer(() => SpawnSettings.Remove(rules.File));

            // Held as its own type for the same reason a quest's is: the usage lives in the outcome.
            var acp = adapter.Wire == SessionWire.Acp
                ? CaptureAcpAsync(
                    process, transcript, sessionId, room, TargetPrompt.Compose(target), adapter.AcpPosture,
                    harnessNotice, ct, scope, rules.Meta)
                : null;
            Task capture = acp ?? CaptureAsync(process, transcript, sessionId, ct, preamble);

            await service.AdvanceAsync(sessionId, "working", transcript: transcript, ct: ct).ConfigureAwait(false);

            var exitCode = await WaitAsync(process, ct).ConfigureAwait(false);
            await capture.ConfigureAwait(false);

            if (acp is not null && (await acp.ConfigureAwait(false))?.Usage is { } used)
            {
                usage?.Record(new UsageEntry(
                    sessionId, $"ask #{ask.Id}", adapter.Name, selection.Profile, used.Used, used.Size, DateTimeOffset.UtcNow));
            }

            // What became of the ask is the observation — the intake's own account of it is not.
            var after = await service.FindAskAsync(ask.Id, ct).ConfigureAwait(false);
            var byPerson = _processes.WasStopRequested(sessionId);
            var conclusion = byPerson
                ? new SessionConclusion("stopped", "the person stopped it.")
                : exitCode is int code
                    ? IntakeObservation.Conclude(code, ask.Quests.Count, after)
                    : new SessionConclusion("failed", $"timed out after {config.TimeoutMinutes} minutes and was killed.");
            conclusion = AccountRefused(conclusion, adapter, selection, transcript);

            await service.AdvanceAsync(sessionId, conclusion.State, note: conclusion.Note, ct: ct).ConfigureAwait(false);

            return (
                $"{conclusion.State}  intake {sessionId} ({named}): {conclusion.Note}",
                true,
                SessionStates.IsParked(conclusion.State)
                    ? null
                    : new SessionEnded(
                        sessionId, $"ask #{ask.Id}", conclusion.State, byPerson, conclusion.Note,
                        Adapter: adapter.Name, Account: selection.Profile));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The person is closing the driver; the record says so rather than sit at "working".
            try
            {
                await service.AdvanceAsync(
                    sessionId, "stopped",
                    note: "the driver was stopped while this ran; the session's process was ended with it.",
                    ct: CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort by construction: the host may already be gone on the same shutdown.
            }

            return (
                $"stopped  intake {sessionId} ({named}): the driver was stopped.",
                true,
                new SessionEnded(sessionId, $"ask #{ask.Id}", "stopped", ByPerson: true, Adapter: adapterName));
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            try
            {
                await service.AdvanceAsync(sessionId, "failed", note: error.Message, ct: ct).ConfigureAwait(false);
            }
            catch (DriverException)
            {
                // The terminal write is best-effort by construction: the first failure is the report.
            }

            return (
                $"failed  intake {sessionId} ({named}): {error.Message}",
                true,
                new SessionEnded(sessionId, $"ask #{ask.Id}", "failed", ByPerson: false, error.Message, Adapter: adapterName));
        }
        finally
        {
            output?.Close(sessionId);
        }
    }

    /// <summary>
    /// End every parked intake whose ask the person has since answered — published or closed. A
    /// parked intake has no process left: it asked and ended, so the answer to its question is the
    /// only thing that can close its record. The person's own act, so nothing interrupts them for it.
    /// </summary>
    private async Task ConcludeAnsweredAsync(
        IReadOnlyList<SessionView> active, List<string> events, List<SessionEnded> concluded, CancellationToken ct)
    {
        foreach (var parked in active.Where(session => session.Ask is not null && SessionStates.IsParked(session.State)))
        {
            AskView? ask;
            try
            {
                ask = await service.FindAskAsync(parked.Ask!, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
            {
                continue; // The host did not answer; the next tick asks again.
            }

            if (ask is null || IntakeObservation.Answered(ask) is not { } answered) continue;

            try
            {
                await service.AdvanceAsync(parked.Id, answered.State, note: answered.Note, ct: ct).ConfigureAwait(false);
            }
            catch (DriverException error)
            {
                events.Add($"held  intake {parked.Id} could not be ended: {error.Message}");
                continue;
            }

            events.Add($"{answered.State}  intake {parked.Id} (ask #{ask.Id} in {ask.Workspace}): {answered.Note}");
            concluded.Add(new SessionEnded(parked.Id, parked.Repository, answered.State, ByPerson: true, answered.Note));
        }
    }
}
