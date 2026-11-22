using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The brainstorm at the look: a request the person's verb wrote is frozen into one packet from
/// Daoris's own knowledge and waits for the person's send; sent, each named agent's generator answers in a room holding that
/// packet as files, Daoris validates every answer with no model and gives a failed one its one repair, the synthesis ranks the
/// proposals under anonymous ids, and Daoris re-attaches who gave which and keeps the document. Nothing is published or travels.
/// </summary>
/// <remarks>
/// <para><b>The state file is the driver's alone</b> (<see cref="BrainstormFiles"/>): <c>frozen</c> naming the packet's digest,
/// <c>running</c>, and <c>ended</c>. The person's send and stop are files of their own.</para>
///
/// <para><b>Bounded, and never hanging</b>: the policy names the agents and the minutes, fitted by
/// <see cref="BrainstormDeadlines"/>. The generators run side by side, each taking a slot under the cap and waiting for one within the deadline; the
/// generators end at the request's deadline less the synthesis's and the challenge's minutes, reserved from the start; a pass past
/// its own minutes, or past that deadline, is stopped, and what answered is kept. A crash fails its pass alone. A limit cools its
/// account and starts the pass once more on the next ready account, as its one repair. The person's stop ends every pass,
/// and a restart ends what ran as <c>interrupted</c>; nothing runs again by itself.</para>
/// </remarks>
/// <param name="driver">The driver whose run machinery a turn goes through: the spawn's preparation, the rules handed and the hold.</param>
internal sealed class DriverBrainstorms(LoopContext context, Driver driver)
{
    private readonly LoopContext _context = context;

    private readonly Driver _driver = driver;

    /// <summary>The most generators one request runs (at most four makers).</summary>
    internal const int MostAgents = DriverBrainstormLimits.MostAgents;

    /// <summary>The code of a turn not started because this machine has no connector to hand it, so no result tool.</summary>
    internal const string NoConnector = "no-connector";

    /// <summary>A pass stopped at its own minutes.</summary>
    internal const string TimedOut = "timed-out";

    /// <summary>A pass stopped at the request's deadline, before its own minutes ran out.</summary>
    internal const string Deadline = "deadline";

    /// <summary>A pass that found no slot under the cap before its deadline: never started.</summary>
    internal const string CapFull = "cap-full";

    /// <summary>A turn whose account met its limit: the account cools, and the pass starts once more on the next one.</summary>
    internal const string Limit = "limit";

    /// <summary>The cool-offs one pass waits for within its deadline.</summary>
    internal const int MostWaits = 3;

    /// <summary>A turn that ended by a failure of its own, with no answer.</summary>
    internal const string Crashed = "crashed";

    /// <summary>An agent the request named that the policy does not list: never run.</summary>
    internal const string NotInPolicy = "not-in-policy";

    /// <summary>Guards the slots this driver's brainstorm passes hold, and what else held the cap at the last look.</summary>
    private readonly object _slots = new();

    private int _holding;

    private int _busy;

    /// <summary>A brainstorm's minute, and a meeting's: a minute, unless a test shortens it.</summary>
    internal TimeSpan Minute => _context.BrainstormMinute ?? TimeSpan.FromMinutes(1);

    internal TimeProvider Clock => _context.Clock;

    /// <summary>How often a waiting pass asks for a slot again, and a running brainstorm looks for the person's stop.</summary>
    internal TimeSpan Tick => TimeSpan.FromMilliseconds(Math.Clamp(Minute.TotalMilliseconds / 60, 20, 1000));

    private static string Claim(string id) => $"brainstorm:{id}";

    /// <summary>
    /// The brainstorms on file, at a look: each request frozen, each sent one run beside the look, one stopped before it ran ended,
    /// and one left running by a driver that stopped ended as interrupted. Returns the slots its passes hold now and one for each
    /// brainstorm this look starts, so the planner leaves them under the cap.
    /// </summary>
    public async Task<int> BrainstormsAsync(Snapshot snapshot, int taken, List<string> events, CancellationToken ct)
    {
        int held;
        lock (_slots)
        {
            _busy = snapshot.SlotsClaimed + taken;
            held = _holding;
        }

        var files = new BrainstormFiles(_context.Home);
        var requests = files.Requests();
        if (requests.Count == 0) return held;

        var begun = 0;
        foreach (var request in requests)
        {
            try
            {
                var state = files.ReadState(request.Id);
                switch (state?.State)
                {
                    case null when files.Stopped(request.Id):
                    case BrainstormStates.Frozen when files.Stopped(request.Id):
                        files.WriteState(new BrainstormState(request.Id, BrainstormStates.Ended, state?.Packet, BrainstormStates.Stopped, Clock.GetUtcNow()));
                        events.Add(_context.Said.Own("brainstorm", $"brainstorm `{request.Id}` stopped by the person before it ran: nothing went to any agent."));
                        break;
                    case null when _context.Beside:
                        // Under the watch, as the `freeze` topic's judge beside the look, a request frozen
                        // once however many looks meet it while it freezes, so a checkout or a search slow to answer holds no start.
                        Freezing(request.Id, token => FreezeAsync(request, snapshot, files, token), ct);
                        break;
                    case null:
                        // A packet frozen waits for the person's send.
                        events.Add(_context.Said.Wait("brainstorm", await FreezeAsync(request, snapshot, files, ct).ConfigureAwait(false)));
                        break;
                    case BrainstormStates.Frozen when files.Sent(request.Id) == state.Packet:
                        // A sent brainstorm's turns are sessions the drain waits on; it stays frozen and sent, and runs at the first
                        // look that does not drain.
                        if (_context.Config.Draining)
                        {
                            _context.DrainHolds(events, "brainstorm", request.Id, $"`{request.Id}`");
                            break;
                        }

                        if (_context.Config.Cap - snapshot.SlotsClaimed - taken - begun - held <= 0) break;
                        if (!_context.Runs.TryOpinion(Claim(request.Id))) break;
                        begun++;
                        files.WriteState(state with { State = BrainstormStates.Running, At = Clock.GetUtcNow() });
                        Beside(request, state.Packet!, RuleOf(request.Repository, snapshot), files, WorkspaceOf(request.Repository, snapshot), ct);
                        break;
                    case BrainstormStates.Running when _context.Runs.TryOpinion(Claim(request.Id)):
                        // Nothing in this driver runs it: the driver that did stopped. What answered is kept; nothing runs again by itself.
                        try
                        {
                            events.Add(_context.Said.News("brainstorm", new LookNews(LookSaid.BrainstormCut, request.Id), Interrupted(request, state, files)));
                        }
                        finally
                        {
                            _context.Runs.OpinionDone(Claim(request.Id));
                        }

                        break;
                }
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or JsonException or IOException
                                              or UnauthorizedAccessException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                events.Add(_context.Said.Retry(
                    "brainstorm", $"brainstorm `{request.Id}` could not be looked at this look, and is looked at again at the next: {error.Message}"));
            }
        }

        return held + begun;
    }

    /// <summary>How many packets are frozen at once beside the looks (a topic bounded).</summary>
    internal const int FreezesAtOnce = 2;

    /// <summary>
    /// A request's packet frozen as the <c>freeze</c> topic's judge beside the look, a brainstorm's or a meeting's: one
    /// judgement per request, its line joining a later look's report, and what stops it said as the look would have said it.
    /// </summary>
    internal void Freezing(string id, Func<CancellationToken, Task<string>> freeze, CancellationToken ct, string kind = "brainstorm") =>
        _context.Runs.Judge("freeze", $"{kind}:{id}", FreezesAtOnce, async token =>
        {
            try
            {
                var said = await _driver.Stages.TimeAsync("freeze", () => freeze(token), $"{kind}:{id}", beside: true).ConfigureAwait(false);
                _context.Runs.Facts.Judged($"freeze/{kind}:{id}");
                return [said];
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or JsonException or IOException
                                              or UnauthorizedAccessException
                                              || (error is OperationCanceledException && !token.IsCancellationRequested))
            {
                // Said once until it is frozen: its line wakes the next look, which asks it again, so a freeze that keeps
                // failing would spin the loop (LookFacts.Failed).
                return _context.Runs.Facts.Failed($"freeze/{kind}:{id}")
                    ? [$"{kind} `{id}` could not be looked at this look, and is looked at again at the next: {error.Message}"]
                    : [];
            }
        }, ct);

    /// <summary>
    /// The policy standing for a request: its repository's workspace's whole, else this machine's. A meeting's is the
    /// same: the brainstorm's bounds and policy are the meeting's.
    /// </summary>
    internal BrainstormRule RuleOf(string? asked, Snapshot snapshot) => BrainstormRules.Resolve(_context.Config, WorkspaceOf(asked, snapshot)).Rule;

    /// <summary>
    /// The workspace of the repository a request asks about, as the registry says it, else the default one; null for a request on no repository.
    /// Its policy is read there, and each of its passes asks its accounts there, for the version it pins.
    /// </summary>
    private static string? WorkspaceOf(string? asked, Snapshot snapshot) =>
        asked is { } repository
            ? snapshot.Repositories.FirstOrDefault(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase))?.Workspace
              ?? RemoteTarget.DefaultWorkspace
            : null;

    /// <summary>
    /// The packet frozen: the question, and for a repository its sources from the knowledge search in more than one
    /// wording, else from its declared index's files; written once, named by its digest, and shown before anything goes.
    /// </summary>
    private async Task<string> FreezeAsync(BrainstormRequest request, Snapshot snapshot, BrainstormFiles files, CancellationToken ct)
    {
        var packet = DesignPasses.Is(request) ? DesignPasses.Packet(request)
            : await AssembleAsync(request.Question, request.Repository, snapshot, ct, request.Sources).ConfigureAwait(false);
        files.WritePacket(request.Id, BrainstormPackets.Bytes(packet));
        files.WriteState(new BrainstormState(request.Id, BrainstormStates.Frozen, packet.Digest, null, Clock.GetUtcNow()));
        var from = request.Repository is null ? "the question alone" : $"{packet.Sources.Count} sources of `{request.Repository}` by {packet.Tier}";
        return $"brainstorm `{request.Id}` frozen: packet `{packet.Digest[..19]}`, {from}. Read it with `daoris brainstorm show {request.Id}`, "
            + $"and send it with `daoris brainstorm send {request.Id}`.";
    }

    /// <summary>The packet's assembly, by <see cref="BrainstormAssembly"/>; a meeting's first packet is this assembly too.</summary>
    internal Task<BrainstormPacket> AssembleAsync(
        string question, string? asked, Snapshot snapshot, CancellationToken ct, IReadOnlyList<string>? named = null) =>
        new BrainstormAssembly(_context).AssembleAsync(question, asked, snapshot, ct, named);


    /// <summary>
    /// A brainstorm a driver that stopped left running: each pass still running ends as <c>interrupted</c>, what
    /// answered is kept as the unranked document, and nothing runs again by itself.
    /// </summary>
    private string Interrupted(BrainstormRequest request, BrainstormState state, BrainstormFiles files)
    {
        var id = request.Id;
        var passes = new List<BrainstormPassRecord>();
        foreach (var pass in files.ReadPasses(id))
        {
            if (pass.State != BrainstormPassRecord.Running)
            {
                passes.Add(pass);
                continue;
            }

            var ended = pass with { State = BrainstormPassRecord.Interrupted, Codes = [BrainstormStates.Interrupted] };
            files.WritePass(id, ended);
            passes.Add(ended);
        }

        if (files.PacketBytes(id) is { } bytes && BrainstormPackets.Read(bytes) is { } packet && Kept(id, packet, passes, files) is { Count: > 0 } answered)
        {
            files.WriteDocument(id, BrainstormDocuments.Unranked(
                id, packet, passes, BrainstormSyntheses.Number(answered), Needs(answered), BrainstormDocuments.Interrupted));
        }

        files.WriteState(new BrainstormState(id, BrainstormStates.Ended, state.Packet, BrainstormStates.Interrupted, Clock.GetUtcNow()));
        files.RemoveRooms(id);
        return $"brainstorm `{id}` was running when its driver stopped: ended as interrupted, what answered is kept, and nothing runs again by itself.";
    }

    /// <summary>The answers its generators gave, read again from their files and validated as they were when they answered.</summary>
    private static List<(BrainstormBy By, BrainstormAnswer Answer)> Kept(
        string id, BrainstormPacket packet, IEnumerable<BrainstormPassRecord> passes, BrainstormFiles files)
    {
        var sources = packet.Sources.Select(source => source.Id).ToHashSet(StringComparer.Ordinal);
        var answered = new List<(BrainstormBy By, BrainstormAnswer Answer)>();
        foreach (var pass in passes.Where(pass => pass is { Role: BrainstormPassRecord.Generate, State: BrainstormPassRecord.Answered })
                     .OrderBy(pass => pass.By.Pass, StringComparer.Ordinal))
        {
            // A retry that ended with none keeps the answer its first turn gave before its limit, in that turn's file.
            string[] turns = pass.Turns > 1 ? [$"{pass.By.Pass}r", pass.By.Pass] : [pass.By.Pass];
            if (turns.Select(turn => BrainstormAnswers.Check(files.ReadAnswer(id, turn), sources).Answer).FirstOrDefault(each => each is not null) is { } answer)
            {
                answered.Add((pass.By, answer));
            }
        }

        return answered;
    }

    private static List<string> Needs(IEnumerable<(BrainstormBy By, BrainstormAnswer Answer)> answered) =>
        [.. answered.SelectMany(each => each.Answer.Needs).Distinct(StringComparer.Ordinal)];

    /// <summary>A brainstorm run beside the look, as a helper's pass is: told opened at once, its line joining a later look's report.</summary>
    private void Beside(BrainstormRequest request, string digest, BrainstormRule rule, BrainstormFiles files, string? workspace, CancellationToken ct) =>
        _ = _context.Runs.StartAsync(async opened =>
        {
            opened();
            try
            {
                return await RunAsync(request, digest, rule, files, workspace, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or JsonException or IOException
                                              or UnauthorizedAccessException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                files.WriteState(new BrainstormState(request.Id, BrainstormStates.Ended, digest, "failed", Clock.GetUtcNow()));
                files.RemoveRooms(request.Id);
                // Stopped by a failure, not by the person: an end nobody pressed and nobody must answer.
                return new StartRun($"brainstorm `{request.Id}` stopped: {error.Message}", Opened: true)
                {
                    Look = LookClassed.Noticed("brainstorm", new LookNews(LookSaid.BrainstormFailed, request.Id)),
                };
            }
            finally
            {
                _context.Runs.OpinionDone(Claim(request.Id));
            }
        });

    /// <summary>
    /// The generators a request runs, by pass, in its order (a request's flags choose within the policy, never beyond
    /// it): the agents it names that the policy lists, or every one it names where the policy lists none; with none named, the
    /// policy's, else Brainstorm's voices (its actor's agents, at account default).
    /// One the policy does not list keeps its pass, never run.
    /// </summary>
    /// <param name="workspace">The request's workspace, which a scoped role is found by; null for the machine's.</param>
    /// <exception cref="DriverException">The role catalogue does not read, or two roles take brainstorming in one scope.</exception>
    internal List<(string Pass, string Agent, bool Allowed, ActorEffective Entry)> Generators(
        BrainstormRequest request, BrainstormRule rule, string? workspace = null)
    {
        // The role that takes brainstorming at the request's workspace; the seeded catalogue answers Brainstorm.
        var found = RoleCatalog.ReadHome(_context.Harnesses.Home).RoleFor(ParticipantWork.Brainstorm, new RolePlace(workspace));
        if (found.Held is { } held) throw new DriverException(held);
        var role = found.Role?.Id ?? "brainstorm";
        var named = request.Agents.Select(agent => agent.Trim()).Where(agent => agent.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        // The request's and the policy's agents decide who, each taking the actor's entry for it, else account
        // default (rule-default); with none named, the actor's entries are the voices, one each. The plan is read once and frozen for the
        // brainstorm: a later change of the actor never reaches a run begun, and a voice that cannot run is dropped, never replaced.
        var voices = found.Role is null ? []
            : named.Count > 0 ? ActorChoices.Effective(role, _context.Config, fence: named)
            : rule.Agents.Count > 0 ? ActorChoices.Effective(role, _context.Config, fence: rule.Agents)
            : ActorChoices.Effective(role, _context.Config);
        var plan = new List<(string Pass, string Agent, bool Allowed, ActorEffective Entry)>();
        var running = 0;
        foreach (var voice in voices.DistinctBy(each => each.Agent, StringComparer.OrdinalIgnoreCase))
        {
            var allowed = rule.Agents.Count == 0 || rule.Agents.Contains(voice.Agent, StringComparer.OrdinalIgnoreCase);
            if (allowed && ++running > MostAgents) break;
            plan.Add(($"g{plan.Count + 1}", voice.Agent, allowed, voice));
        }

        return plan;
    }

    /// <summary>
    /// The passes of one sent brainstorm: the generators side by side within the generators' deadline, then the synthesis within
    /// its own minutes, the challenge's kept in reserve, then the document; the person's stop watched throughout, and the rooms
    /// removed however it ended. Its line is classed here: the person's stop their own, an end its page says.
    /// </summary>
    /// <param name="workspace">The workspace of the repository it asks about, which each pass's accounts are asked for; null for none.</param>
    private async Task<StartRun> RunAsync(
        BrainstormRequest request, string digest, BrainstormRule rule, BrainstormFiles files, string? workspace, CancellationToken ct)
    {
        var id = request.Id;
        var bytes = files.PacketBytes(id);
        if (bytes is null || BrainstormPackets.Read(bytes) is not { } packet || packet.Digest != digest)
        {
            throw new DriverException($"its packet is not the one the person sent ({digest}).");
        }

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var done = new CancellationTokenSource();
        var watching = WatchAsync(id, files, stop, done.Token);
        try
        {
            var minutes = BrainstormDeadlines.Of(rule.Minutes, request);
            var started = Clock.GetUtcNow();
            var deadline = started + minutes.Request * Minute;
            var generatorsEnd = started + minutes.Generators * Minute;
            var sources = packet.Sources.Select(source => source.Id).ToHashSet(StringComparer.Ordinal);
            var laid = BrainstormRoomPacket.Lay(packet);
            var plan = Generators(request, rule, workspace);
            var generators = plan.Select(each => each.Allowed
                ? Passes.PassAsync<BrainstormAnswer>(
                    id, each.Pass, each.Agent, BrainstormPassRecord.Generate, laid, DesignPasses.Generator(request, digest, minutes.Pass), null,
                    text =>
                    {
                        var check = BrainstormAnswers.Check(text, sources);
                        return (check.Answer, check.Codes);
                    }, files, minutes.Pass * Minute, generatorsEnd, stop.Token, ct, entry: each.Entry, workspace: workspace)
                : Task.FromResult(Refused(id, each.Pass, each.Agent, files))).ToList();
            var results = await Task.WhenAll(generators).ConfigureAwait(false);
            var passes = results.Select(result => result.Record).ToList();
            var answered = results.Where(result => result.Valid is not null).Select(result => (result.Record.By, Answer: result.Valid!)).ToList();
            var needs = Needs(answered);
            var ran = plan.Count(each => each.Allowed);

            if (stop.IsCancellationRequested) return Ran(LookClass.Own, Stopped(id, packet, passes, answered, needs, files, digest));
            // A maker that cooled, met its limit twice or found the cap full is said, with the offer to ask again, where any answered or none did.
            var shortOf = passes.Where(pass => pass is { Role: BrainstormPassRecord.Generate, State: BrainstormPassRecord.Unavailable or BrainstormPassRecord.Failed }
                                             && pass.Codes.Any(code => code is StartPasses.Cooling or CapFull or Limit)).ToList();
            if (answered.Count == 0 && shortOf.Count == 0)
            {
                files.WriteState(new BrainstormState(id, BrainstormStates.Ended, digest, BrainstormStates.NoAgent, Clock.GetUtcNow()));
                return Ran(LookClass.Progress, $"brainstorm `{id}` ended: no agent could answer, and its packet is kept.");
            }

            var handed = BrainstormSyntheses.Number(answered);
            if (shortOf.Count > 0)
            {
                var why = BrainstormDocuments.ShortOfMakers(shortOf, answered.Count);
                files.WriteDocument(id, BrainstormDocuments.Unranked(id, packet, passes, handed, needs, why));
                files.WriteState(new BrainstormState(id, BrainstormStates.Ended, digest, BrainstormStates.ShortHanded, Clock.GetUtcNow()));
                return Ran(LookClass.Progress, $"brainstorm `{id}` ended with no synthesis: {why}. Read what answered with `daoris brainstorm show {id}`.");
            }

            BrainstormDocument document;
            if (handed.Count == 0)
            {
                document = BrainstormDocuments.Unranked(id, packet, passes, handed, needs, "every answer said it had no proposal");
            }
            else
            {
                // The synthesizer: the first agent of the request whose generator answered, within its
                // own minutes and before the challenge's reserve.
                var synthesizer = answered[0].By.Adapter;
                // The synthesis takes its generator's asked choice, not the actor as it stands now, and its record says so.
                var generated = plan.First(each => each.Pass == answered[0].By.Pass).Entry;
                var (record, synthesis) = await Passes.PassAsync<BrainstormSynthesized>(
                    id, "s1", synthesizer, BrainstormPassRecord.Synthesize, laid, DesignPasses.Synthesizer(request, digest),
                    BrainstormSyntheses.Anonymous(handed),
                    text =>
                    {
                        var check = DesignPasses.Check(request, text, handed);
                        return (check.Synthesis, check.Codes);
                    }, files, minutes.Synthesis * Minute, deadline - minutes.Challenge * Minute, stop.Token, ct, entry: generated, workspace: workspace)
                    .ConfigureAwait(false);
                passes.Add(record);
                if (stop.IsCancellationRequested && synthesis is null) return Ran(LookClass.Own, Stopped(id, packet, passes, answered, needs, files, digest));
                document = synthesis is not null
                    ? BrainstormDocuments.Ranked(id, packet, passes, handed, synthesis, needs)
                    : BrainstormDocuments.Unranked(id, packet, passes, handed, needs, $"{BrainstormDocuments.NoSynthesis}: {string.Join(", ", record.Codes)}");
            }

            files.WriteDocument(id, document);
            files.WriteState(new BrainstormState(
                id, BrainstormStates.Ended, digest, document.Unranked is null ? null : BrainstormStates.Unranked, Clock.GetUtcNow()));
            return Ran(LookClass.Progress, $"brainstorm `{id}` ended: {document.Rows.Count} {(document.Unranked is null ? "ranked" : "unranked")} rows from "
                + $"{answered.Count} of {ran} generators. Read it with `daoris brainstorm show {id}`.");
        }
        finally
        {
            await done.CancelAsync().ConfigureAwait(false);
            await watching.ConfigureAwait(false);
            files.RemoveRooms(id);
        }
    }

    /// <summary>A run's line, classed where it is made.</summary>
    private static StartRun Ran(LookClass @class, string line) => new(line, Opened: true) { Look = new LookClassed(@class, "brainstorm") };

    /// <summary>The person's stop: every pass ended, and what answered kept as the unranked document.</summary>
    private string Stopped(
        string id, BrainstormPacket packet, IReadOnlyList<BrainstormPassRecord> passes, IReadOnlyList<(BrainstormBy By, BrainstormAnswer Answer)> answered,
        IReadOnlyList<string> needs, BrainstormFiles files, string digest)
    {
        if (answered.Count > 0)
        {
            files.WriteDocument(id, BrainstormDocuments.Unranked(id, packet, passes, BrainstormSyntheses.Number(answered), needs, BrainstormDocuments.StoppedByPerson));
        }

        files.WriteState(new BrainstormState(id, BrainstormStates.Ended, digest, BrainstormStates.Stopped, Clock.GetUtcNow()));
        return $"brainstorm `{id}` stopped by the person: {answered.Count} answers kept, and nothing runs again by itself.";
    }

    /// <summary>
    /// The person's stop, looked for while a brainstorm runs: its file, written by their verb, ends every pass at once. A meeting's
    /// round is watched the same, through its turns' files.
    /// </summary>
    internal async Task WatchAsync(string id, BrainstormFiles files, CancellationTokenSource stop, CancellationToken done)
    {
        try
        {
            while (!done.IsCancellationRequested && !stop.IsCancellationRequested)
            {
                if (files.Stopped(id))
                {
                    await stop.CancelAsync().ConfigureAwait(false);
                    return;
                }

                await Task.Delay(Tick, Clock, done).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // The brainstorm ended: nothing more to watch.
        }
    }

    private (BrainstormPassRecord Record, BrainstormAnswer? Valid) Refused(string id, string pass, string agent, BrainstormFiles files)
    {
        var family = AgentFamily.Of(_context.Adapters, agent);
        var record = new BrainstormPassRecord(
            new BrainstormBy(pass, family.Maker ?? family.Owner, agent, null), BrainstormPassRecord.Generate, BrainstormPassRecord.Unavailable, [NotInPolicy], 0);
        files.WritePass(id, record);
        return (record, null);
    }

    /// <summary>A slot under the machine's cap, taken by a pass as a reviewer's is; false where the cap is full.</summary>
    internal bool TrySlot()
    {
        lock (_slots)
        {
            if (_context.Config.Cap - _busy - _holding <= 0) return false;
            _holding++;
            return true;
        }
    }

    internal void FreeSlot()
    {
        lock (_slots) _holding--;
    }

    private BrainstormTurnRunner Turns => new(this, _context, _driver);

    internal BrainstormPasses Passes => new(this, _context, _driver);

    /// <summary>A voice's start plan.</summary>
    internal static StartPlan Voice(BrainstormTurn turn, DriverConfig config, HarnessRoster roster) => BrainstormTurnRunner.Voice(turn, config, roster);

    internal static Task<HarnessSelection> VoiceAsync(BrainstormTurn turn, DriverConfig config, HarnessRoster roster, CancellationToken ct) =>
        BrainstormTurnRunner.VoiceAsync(turn, config, roster, ct);

    /// <summary>A room measurement of <paramref name="agent"/>, by <see cref="BrainstormTurnRunner"/>.</summary>
    internal Task<RoomMeasure> MeasureRoomAsync(string agent, bool spend, CancellationToken ct) => Turns.MeasureRoomAsync(agent, spend, ct);

    /// <summary>The code a pass is <c>unavailable</c> with where its voice's walk found no ready account.</summary>
    internal static string PassCode(HarnessSelection selection) => BrainstormTurnRunner.PassCode(selection);

    /// <summary>One real turn, by <see cref="BrainstormTurnRunner"/>.</summary>
    internal Task<BrainstormTurnRun> TurnAsync(BrainstormTurn turn, CancellationToken ct) => Turns.TurnAsync(turn, ct);
}
