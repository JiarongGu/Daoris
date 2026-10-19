using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The brainstorm at the look (BRAINSTORM1a, D176 points 1-4 and 8): a request the person's verb wrote is frozen into one packet from
/// Daoris's own knowledge and waits for the person's send; sent, each named agent's generator answers once in a room holding that
/// packet, Daoris validates every answer with no model and gives a failed one its one repair, the synthesis ranks the proposals
/// under anonymous ids, and Daoris re-attaches who gave which and keeps the document. Nothing is published, nothing travels, and
/// nothing is written into any repository.
/// </summary>
/// <remarks>
/// <para><b>The state file is the driver's alone</b> (<see cref="BrainstormFiles"/>): <c>frozen</c> naming the packet's digest,
/// <c>running</c>, and <c>ended</c>. The person's send and stop are files of their own.</para>
///
/// <para><b>Bounded, and never hanging</b> (BRAINSTORM1b, D176 points 6, 7 and 9): the person's policy names the agents and the
/// minutes. The generators run side by side, each taking a slot under the cap and waiting for one within the deadline; the
/// generators end at the request's deadline less the synthesis's and the challenge's minutes, reserved from the start; a pass past
/// its own minutes, or past that deadline, is stopped, and what answered is kept. A crash fails its pass alone. A limit cools its
/// account (D125) and starts the pass once more on the next ready account, as its one repair. The person's stop ends every pass,
/// and a restart ends what ran as <c>interrupted</c>; nothing runs again by itself.</para>
/// </remarks>
/// <param name="driver">The driver whose run machinery a turn goes through: the spawn's preparation, the rules handed and the hold.</param>
internal sealed class DriverBrainstorms(LoopContext context, Driver driver)
{
    private readonly LoopContext _context = context;

    private readonly Driver _driver = driver;

    /// <summary>The most generators one request runs (D176 point 9's proposed default: at most four makers).</summary>
    internal const int MostAgents = DriverBrainstormLimits.MostAgents;

    /// <summary>The code of a turn not started because this machine has no connector to hand it, so no result tool (BRAINSTORM1a2).</summary>
    internal const string NoConnector = "no-connector";

    /// <summary>A pass stopped at its own minutes.</summary>
    internal const string TimedOut = "timed-out";

    /// <summary>A pass stopped at the request's deadline, before its own minutes ran out.</summary>
    internal const string Deadline = "deadline";

    /// <summary>A pass that found no slot under the cap before its deadline: never started.</summary>
    internal const string CapFull = "cap-full";

    /// <summary>A turn whose account met its limit (D125): the account cools, and the pass starts once more on the next one.</summary>
    internal const string Limit = "limit";

    /// <summary>A turn that ended by a failure of its own, with no answer.</summary>
    internal const string Crashed = "crashed";

    /// <summary>An agent the request named that the policy does not list (D176 point 9): never run.</summary>
    internal const string NotInPolicy = "not-in-policy";

    /// <summary>Guards the slots this driver's brainstorm passes hold, and what else held the cap at the last look.</summary>
    private readonly object _slots = new();

    private int _holding;

    private int _busy;

    /// <summary>A brainstorm's minute, and a meeting's (AGENTROOM1a): a minute, unless a test shortens it.</summary>
    internal TimeSpan Minute => _context.BrainstormMinute ?? TimeSpan.FromMinutes(1);

    /// <summary>How often a waiting pass asks for a slot again, and a running brainstorm looks for the person's stop.</summary>
    private TimeSpan Tick => TimeSpan.FromMilliseconds(Math.Clamp(Minute.TotalMilliseconds / 60, 20, 1000));

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
            _busy = snapshot.Active.Count + taken;
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
                        files.WriteState(new BrainstormState(request.Id, BrainstormStates.Ended, state?.Packet, BrainstormStates.Stopped, DateTimeOffset.UtcNow));
                        events.Add($"brainstorm `{request.Id}` stopped by the person before it ran: nothing went to any agent.");
                        break;
                    case null when _context.Beside:
                        // PARALLEL1b (D181 point 3): under the watch, as the `freeze` topic's judge beside the look, a request frozen
                        // once however many looks meet it while it freezes, so a checkout or a search slow to answer holds no start.
                        Freezing(request.Id, token => FreezeAsync(request, snapshot, files, token), ct);
                        break;
                    case null:
                        events.Add(await FreezeAsync(request, snapshot, files, ct).ConfigureAwait(false));
                        break;
                    case BrainstormStates.Frozen when files.Sent(request.Id) == state.Packet:
                        if (_context.Config.Cap - snapshot.Active.Count - taken - begun - held <= 0) break;
                        if (!_context.Runs.TryOpinion(Claim(request.Id))) break;
                        begun++;
                        files.WriteState(state with { State = BrainstormStates.Running, At = DateTimeOffset.UtcNow });
                        Beside(request, state.Packet!, RuleOf(request.Repository, snapshot), files, ct);
                        break;
                    case BrainstormStates.Running when _context.Runs.TryOpinion(Claim(request.Id)):
                        // Nothing in this driver runs it: the driver that did stopped. What answered is kept; nothing runs again by itself.
                        try
                        {
                            events.Add(Interrupted(request, state, files));
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
                events.Add($"brainstorm `{request.Id}` could not be looked at this look, and is looked at again at the next: {error.Message}");
            }
        }

        return held + begun;
    }

    /// <summary>How many packets are frozen at once beside the looks (PARALLEL1b; a topic bounded, the events design §A3).</summary>
    internal const int FreezesAtOnce = 2;

    /// <summary>
    /// A request's packet frozen as the <c>freeze</c> topic's judge beside the look (PARALLEL1b), a brainstorm's or a meeting's: one
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
                // Said once until it is frozen (PARALLEL1b2): its line wakes the next look, which asks it again, so a freeze that keeps
                // failing would spin the loop (LookFacts.Failed).
                return _context.Runs.Facts.Failed($"freeze/{kind}:{id}")
                    ? [$"{kind} `{id}` could not be looked at this look, and is looked at again at the next: {error.Message}"]
                    : [];
            }
        }, ct);

    /// <summary>
    /// The policy standing for a request (D176 point 9): its repository's workspace's whole, else this machine's. A meeting's is the
    /// same (AGENTROOM1a, D177 point 6: the brainstorm's bounds and policy are the meeting's).
    /// </summary>
    internal BrainstormRule RuleOf(string? asked, Snapshot snapshot)
    {
        var workspace = asked is { } repository
            ? snapshot.Repositories.FirstOrDefault(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase))?.Workspace
              ?? RemoteTarget.DefaultWorkspace
            : null;
        return BrainstormRules.Resolve(_context.Config, workspace).Rule;
    }

    /// <summary>
    /// The packet frozen (D176 point 2): the question, and for a repository its sources from the knowledge search in more than one
    /// wording, else from its declared index's files; written once, named by its digest, and shown before anything goes.
    /// </summary>
    private async Task<string> FreezeAsync(BrainstormRequest request, Snapshot snapshot, BrainstormFiles files, CancellationToken ct)
    {
        var packet = await AssembleAsync(request.Question, request.Repository, snapshot, ct).ConfigureAwait(false);
        files.WritePacket(request.Id, BrainstormPackets.Bytes(packet));
        files.WriteState(new BrainstormState(request.Id, BrainstormStates.Frozen, packet.Digest, null, DateTimeOffset.UtcNow));
        var from = request.Repository is null ? "the question alone" : $"{packet.Sources.Count} sources of `{request.Repository}` by {packet.Tier}";
        return $"brainstorm `{request.Id}` frozen: packet `{packet.Digest[..19]}`, {from}. Read it with `daoris-driver brainstorm show {request.Id}`, "
            + $"and send it with `daoris-driver brainstorm send {request.Id}`.";
    }

    /// <summary>
    /// The packet's assembly (D176 point 2): the question, and for a repository its sources from the knowledge search in more than
    /// one wording, else from its declared index's files. A meeting's first packet is this assembly (AGENTROOM1a, D177 point 2).
    /// </summary>
    internal async Task<BrainstormPacket> AssembleAsync(string question, string? asked, Snapshot snapshot, CancellationToken ct)
    {
        var found = new List<BrainstormFound>();
        string? tier = null;
        string? revision = null;
        if (asked is { } repository)
        {
            var (root, commit) = await CheckoutAsync(repository, snapshot, ct).ConfigureAwait(false);
            revision = commit;
            try
            {
                foreach (var words in BrainstormWordings.Of(question))
                {
                    var search = await SearchAsync(words, repository, ct).ConfigureAwait(false);
                    found.AddRange(search.Found);
                    tier = tier is null or BrainstormPackets.Meaning ? search.Tier : tier;
                }
            }
            catch (Exception error) when (error is HttpRequestException or JsonException or DriverException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                // No service: the declared index's files answer (D151 §6).
                found.Clear();
                tier = BrainstormPackets.Files;
                if (root is not null) found.AddRange(IndexFiles(root, question));
            }
        }

        return BrainstormPackets.Freeze(question, asked, tier, revision, found, BrainstormBounds.First);
    }

    private async Task<(string? Root, string? Revision)> CheckoutAsync(string repository, Snapshot snapshot, CancellationToken ct)
    {
        if (_context.BrainstormCheckouts is { } read) return await read(repository, ct).ConfigureAwait(false);
        var root = snapshot.Repositories.FirstOrDefault(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase))?.Root;
        return root is null ? (null, null) : (root, await ReviewGit.HeadAsync(root, ct).ConfigureAwait(false));
    }

    private Task<BrainstormSearch> SearchAsync(string words, string repository, CancellationToken ct) =>
        _context.BrainstormSearches is { } search ? search(words, repository, ct) : _context.Service.BrainstormSearchAsync(words, repository, ct);

    /// <summary>The declared index's files, whole, each a source: what answers where no service does (D151 §6).</summary>
    private static IEnumerable<BrainstormFound> IndexFiles(string root, string question)
    {
        foreach (var index in RepositoryIndexes.Find(root))
        {
            var path = Path.Combine(root, index);
            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            yield return new BrainstormFound("index", index, null, null, text, question);
        }
    }

    /// <summary>
    /// A brainstorm a driver that stopped left running (D176 point 6): each pass it was running ends as <c>interrupted</c>, what
    /// answered is kept as the unranked document, and nothing runs again by itself.
    /// </summary>
    private static string Interrupted(BrainstormRequest request, BrainstormState state, BrainstormFiles files)
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

        files.WriteState(new BrainstormState(id, BrainstormStates.Ended, state.Packet, BrainstormStates.Interrupted, DateTimeOffset.UtcNow));
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
            var turn = pass.Turns > 1 ? $"{pass.By.Pass}r" : pass.By.Pass;
            if (BrainstormAnswers.Check(files.ReadAnswer(id, turn), sources).Answer is { } answer) answered.Add((pass.By, answer));
        }

        return answered;
    }

    private static List<string> Needs(IEnumerable<(BrainstormBy By, BrainstormAnswer Answer)> answered) =>
        [.. answered.SelectMany(each => each.Answer.Needs).Distinct(StringComparer.Ordinal)];

    /// <summary>A brainstorm run beside the look, as a helper's pass is: told opened at once, its line joining a later look's report.</summary>
    private void Beside(BrainstormRequest request, string digest, BrainstormRule rule, BrainstormFiles files, CancellationToken ct) =>
        _ = _context.Runs.StartAsync(async opened =>
        {
            opened();
            try
            {
                return new StartRun(await RunAsync(request, digest, rule, files, ct).ConfigureAwait(false), Opened: true);
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or JsonException or IOException
                                              or UnauthorizedAccessException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                files.WriteState(new BrainstormState(request.Id, BrainstormStates.Ended, digest, "failed", DateTimeOffset.UtcNow));
                files.RemoveRooms(request.Id);
                return new StartRun($"brainstorm `{request.Id}` stopped: {error.Message}", Opened: true);
            }
            finally
            {
                _context.Runs.OpinionDone(Claim(request.Id));
            }
        });

    /// <summary>
    /// The generators a request runs, by pass, in its order (D176 point 9: a request's flags choose within the policy, never beyond
    /// it): the agents it names that the policy lists, or every one it names where the policy lists none; with none named, the
    /// policy's, else this machine's agent. One the policy does not list keeps its pass, never run.
    /// </summary>
    private List<(string Pass, string Agent, bool Allowed)> Generators(BrainstormRequest request, BrainstormRule rule)
    {
        var named = request.Agents.Select(agent => agent.Trim()).Where(agent => agent.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var agents = named.Count > 0 ? named : rule.Agents.Count > 0 ? [.. rule.Agents] : [_context.Config.Adapter];
        var plan = new List<(string Pass, string Agent, bool Allowed)>();
        var running = 0;
        foreach (var agent in agents)
        {
            var allowed = rule.Agents.Count == 0 || rule.Agents.Contains(agent, StringComparer.OrdinalIgnoreCase);
            if (allowed && ++running > MostAgents) break;
            plan.Add(($"g{plan.Count + 1}", agent, allowed));
        }

        return plan;
    }

    /// <summary>
    /// The passes of one sent brainstorm: the generators side by side within the generators' deadline, then the synthesis within
    /// its own minutes, the challenge's kept in reserve, then the document; the person's stop watched throughout, and the rooms
    /// removed however it ended.
    /// </summary>
    private async Task<string> RunAsync(BrainstormRequest request, string digest, BrainstormRule rule, BrainstormFiles files, CancellationToken ct)
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
            var minutes = rule.Minutes;
            var started = DateTimeOffset.UtcNow;
            var deadline = started + minutes.Request * Minute;
            var generatorsEnd = started + minutes.Generators * Minute;
            var sources = packet.Sources.Select(source => source.Id).ToHashSet(StringComparer.Ordinal);
            var plan = Generators(request, rule);
            var generators = plan.Select(each => each.Allowed
                ? PassAsync<BrainstormAnswer>(
                    id, each.Pass, each.Agent, BrainstormPassRecord.Generate, bytes, BrainstormInstruction.Generator(digest), null,
                    text =>
                    {
                        var check = BrainstormAnswers.Check(text, sources);
                        return (check.Answer, check.Codes);
                    }, files, minutes.Pass * Minute, generatorsEnd, stop.Token, ct)
                : Task.FromResult(Refused(id, each.Pass, each.Agent, files))).ToList();
            var results = await Task.WhenAll(generators).ConfigureAwait(false);
            var passes = results.Select(result => result.Record).ToList();
            var answered = results.Where(result => result.Valid is not null).Select(result => (result.Record.By, Answer: result.Valid!)).ToList();
            var needs = Needs(answered);
            var ran = plan.Count(each => each.Allowed);

            if (stop.IsCancellationRequested) return Stopped(id, packet, passes, answered, needs, files, digest);
            if (answered.Count == 0)
            {
                files.WriteState(new BrainstormState(id, BrainstormStates.Ended, digest, BrainstormStates.NoAgent, DateTimeOffset.UtcNow));
                return $"brainstorm `{id}` ended: no agent could answer, and its packet is kept.";
            }

            var handed = BrainstormSyntheses.Number(answered);
            BrainstormDocument document;
            if (handed.Count == 0)
            {
                document = BrainstormDocuments.Unranked(id, packet, passes, handed, needs, "every answer said it had no proposal");
            }
            else
            {
                // The synthesizer: the first agent of the request whose generator answered (D176 point 9's proposed default), within its
                // own minutes and before the challenge's reserve.
                var synthesizer = answered[0].By.Adapter;
                var (record, synthesis) = await PassAsync<BrainstormSynthesized>(
                    id, "s1", synthesizer, BrainstormPassRecord.Synthesize, bytes, BrainstormInstruction.Synthesizer(digest),
                    BrainstormSyntheses.Anonymous(handed),
                    text =>
                    {
                        var check = BrainstormSyntheses.Check(text, handed);
                        return (check.Synthesis, check.Codes);
                    }, files, minutes.Synthesis * Minute, deadline - minutes.Challenge * Minute, stop.Token, ct).ConfigureAwait(false);
                passes.Add(record);
                if (stop.IsCancellationRequested && synthesis is null) return Stopped(id, packet, passes, answered, needs, files, digest);
                document = synthesis is not null
                    ? BrainstormDocuments.Ranked(id, packet, passes, handed, synthesis, needs)
                    : BrainstormDocuments.Unranked(id, packet, passes, handed, needs, $"{BrainstormDocuments.NoSynthesis}: {string.Join(", ", record.Codes)}");
            }

            files.WriteDocument(id, document);
            files.WriteState(new BrainstormState(
                id, BrainstormStates.Ended, digest, document.Unranked is null ? null : BrainstormStates.Unranked, DateTimeOffset.UtcNow));
            return $"brainstorm `{id}` ended: {document.Rows.Count} {(document.Unranked is null ? "ranked" : "unranked")} rows from "
                + $"{answered.Count} of {ran} generators. Read it with `daoris-driver brainstorm show {id}`.";
        }
        finally
        {
            await done.CancelAsync().ConfigureAwait(false);
            await watching.ConfigureAwait(false);
            files.RemoveRooms(id);
        }
    }

    /// <summary>The person's stop (D176 point 6): every pass ended, and what answered kept as the unranked document.</summary>
    private static string Stopped(
        string id, BrainstormPacket packet, IReadOnlyList<BrainstormPassRecord> passes, IReadOnlyList<(BrainstormBy By, BrainstormAnswer Answer)> answered,
        IReadOnlyList<string> needs, BrainstormFiles files, string digest)
    {
        if (answered.Count > 0)
        {
            files.WriteDocument(id, BrainstormDocuments.Unranked(id, packet, passes, BrainstormSyntheses.Number(answered), needs, BrainstormDocuments.StoppedByPerson));
        }

        files.WriteState(new BrainstormState(id, BrainstormStates.Ended, digest, BrainstormStates.Stopped, DateTimeOffset.UtcNow));
        return $"brainstorm `{id}` stopped by the person: {answered.Count} answers kept, and nothing runs again by itself.";
    }

    /// <summary>
    /// The person's stop, looked for while a brainstorm runs: its file, written by their verb, ends every pass at once. A meeting's
    /// round is watched the same, through its turns' files (AGENTROOM1a).
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

                await Task.Delay(Tick, done).ConfigureAwait(false);
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

    /// <summary>A slot under the machine's cap (D176 point 7, D130), taken by a pass as a reviewer's is; false where the cap is full.</summary>
    private bool TrySlot()
    {
        lock (_slots)
        {
            if (_context.Config.Cap - _busy - _holding <= 0) return false;
            _holding++;
            return true;
        }
    }

    private void FreeSlot()
    {
        lock (_slots) _holding--;
    }

    /// <summary>
    /// One pass (D176 points 3, 6 and 7): a slot under the cap, waited for until <paramref name="until"/>; then its turn in a fresh
    /// room within its own minutes from its slot, cut at <paramref name="until"/>; its answer validated by <paramref name="check"/>,
    /// and where that failed its one repair, handed the codes, within the same minutes. A limit starts it once more instead, as that
    /// repair; a crash is a turn with no answer; the person's stop ends it. Its record is written at each turn's start, so a restart
    /// knows what ran, and whatever it came to. A meeting's turn is one such pass (AGENTROOM1a, D177 point 4), run by
    /// <paramref name="run"/>: a meeting's runner, never the brainstorm's.
    /// </summary>
    /// <param name="run">How its turns run; null for a brainstorm's: its test stand-in, else <see cref="TurnAsync"/>.</param>
    internal async Task<(BrainstormPassRecord Record, T? Valid)> PassAsync<T>(
        string id, string pass, string agent, string role, byte[] packet, string instruction, byte[]? proposals,
        Func<string?, (T? Valid, IReadOnlyList<string> Codes)> check, BrainstormFiles files, TimeSpan own, DateTimeOffset until,
        CancellationToken stop, CancellationToken ct, Func<BrainstormTurn, CancellationToken, Task<BrainstormTurnRun>>? run = null)
        where T : class
    {
        run ??= _context.BrainstormTurns ?? TurnAsync;
        var family = AgentFamily.Of(_context.Adapters, agent);
        var maker = family.Maker ?? family.Owner;
        string? account = null;
        (BrainstormPassRecord, T?) Ended(string state, IReadOnlyList<string> codes, int turns, T? valid = null)
        {
            var record = new BrainstormPassRecord(new BrainstormBy(pass, maker, agent, account), role, state, codes, turns);
            files.WritePass(id, record);
            return (record, valid);
        }

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (stop.IsCancellationRequested) return Ended(BrainstormPassRecord.Stopped, [BrainstormStates.Stopped], 0);
            // The deadline first: a slot let go at the deadline starts nothing.
            if (DateTimeOffset.UtcNow >= until) return Ended(BrainstormPassRecord.Unavailable, [CapFull], 0);
            if (TrySlot()) break;
            try
            {
                await Task.Delay(Tick, stop).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // The person's stop: said at the loop's top.
            }
        }

        try
        {
            var ownEnd = DateTimeOffset.UtcNow + own;
            var end = ownEnd < until ? ownEnd : until;
            var cutCode = until < ownEnd ? Deadline : TimedOut;
            IReadOnlyList<string> codes = [];
            var restart = false;
            for (var turns = 1; turns <= 2; turns++)
            {
                ct.ThrowIfCancellationRequested();
                if (stop.IsCancellationRequested) return Ended(BrainstormPassRecord.Stopped, [BrainstormStates.Stopped], turns - 1);
                var left = end - DateTimeOffset.UtcNow;
                if (left <= TimeSpan.Zero) return Ended(BrainstormPassRecord.Failed, [.. codes, cutCode], turns - 1);

                var turn = turns == 1 ? pass : $"{pass}r";
                var said = turns == 1 || restart ? instruction : BrainstormInstruction.Repair(instruction, codes);
                Ended(BrainstormPassRecord.Running, [], turns);
                var room = files.OpenRoom(id, turn, packet, said, proposals);
                using var window = CancellationTokenSource.CreateLinkedTokenSource(stop);
                window.CancelAfter(left);
                BrainstormTurnRun ran;
                var cut = false;
                try
                {
                    ran = await run(
                        new BrainstormTurn(id, turn, agent, room, said, files.AnswerPath(id, turn), Math.Max(1, (int)Math.Ceiling(left.TotalMinutes))),
                        window.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (window.IsCancellationRequested && !ct.IsCancellationRequested)
                {
                    cut = true;
                    ran = new BrainstormTurnRun(true, null, null, $"stopped  brainstorm turn {turn}");
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    // A crash ends the turn with no answer: its pass alone fails, after its repair, never the brainstorm.
                    ran = new BrainstormTurnRun(true, null, Crashed, $"crashed  brainstorm turn {turn}: {error.Message}");
                }

                account = ran.Account ?? account;
                if (!ran.Ran)
                {
                    return Ended(BrainstormPassRecord.Unavailable, restart ? [Limit, ran.Code ?? "not-started"] : [ran.Code ?? "not-started"], turns);
                }

                if (ran.Code == Limit && !restart && !cut)
                {
                    // D125: the account cooled where the turn ran, no strike is counted, and the pass starts once more on the next
                    // ready account of that agent's walk, as its one repair, within its minutes.
                    codes = [Limit];
                    restart = true;
                    continue;
                }

                var (valid, failed) = check(files.ReadAnswer(id, turn));
                if (valid is not null) return Ended(BrainstormPassRecord.Answered, [], turns, valid);
                if (cut || window.IsCancellationRequested)
                {
                    ct.ThrowIfCancellationRequested();
                    return stop.IsCancellationRequested
                        ? Ended(BrainstormPassRecord.Stopped, [BrainstormStates.Stopped], turns)
                        : Ended(BrainstormPassRecord.Failed, [cutCode], turns);
                }

                codes = ran.Code is { } code ? [.. failed, code] : failed;
                restart = false;
            }

            return Ended(BrainstormPassRecord.Failed, codes, 2);
        }
        finally
        {
            FreeSlot();
        }
    }

    /// <summary>
    /// One real turn (BRAINSTORM1a, BRAINSTORM1a2): the agent's account by the walk, its spawn prepared as any session's, the
    /// <c>brainstorm</c> posture held to its room, one turn framed as one prompt, its connector its only one and carrying the turn so
    /// its result tool writes the answer file, and its own minutes. Its record is the brainstorm's own, under the home's data: no
    /// session record is opened on the service, and nothing of it syncs. A limit its door reports cools the account (D125,
    /// BRAINSTORM1b), and the pass starts once more on the next.
    /// </summary>
    internal async Task<BrainstormTurnRun> TurnAsync(BrainstormTurn turn, CancellationToken ct)
    {
        var sessionId = $"brainstorm-{turn.Id}-{turn.Turn}";
        // Its connector, and no plugin's server beside it (D176 point 2): the turn whole on it, so `brainstorm_give` or
        // `brainstorm_synthesize` writes the file the driver reads and judges it against the room. A pass answers through that tool
        // alone, so on a machine with no connector it is not started, and no account is spent on a turn that cannot answer.
        var scope = BrainstormVariables.Scope(turn);
        var connector = _driver.Connector(sessionId, scope);
        if (connector is null)
        {
            return new BrainstormTurnRun(false, null, NoConnector,
                $"held  brainstorm turn {turn.Turn}: this machine has no connector, and a pass answers only through its tool");
        }

        ISessionAdapter adapter;
        HarnessSelection selection;
        try
        {
            adapter = _context.Adapters.Resolve(turn.Agent);
            (selection, _) = await _context.Harnesses
                .SelectHandedAsync(turn.Agent, _context.Config, null, null, StartKind.Driven, ct).ConfigureAwait(false);
        }
        catch (DriverException error)
        {
            return new BrainstormTurnRun(false, null, "not-started", $"held  brainstorm turn {turn.Turn}: {error.Message}");
        }

        if (!selection.Allowed)
        {
            return new BrainstormTurnRun(false, null, selection.Cooling is not null ? "cooling" : "unavailable", $"held  brainstorm turn {turn.Turn}: {selection.Refusal}");
        }

        var files = new BrainstormFiles(_context.Home);
        var target = new SessionTarget(QuestId: "", "Brainstorm", "", "Daoris", "", turn.Room, "")
        {
            Brainstorm = turn.Id,
            BrainstormTurn = turn.Turn,
            BrainstormAnswer = turn.Answer,
            Session = sessionId,
            Prompt = turn.Instruction,
        };
        var (info, notice) = _driver.Prepare(adapter, target, selection);
        var transcript = files.PathOf(turn.Id, "passes", $"{turn.Turn}.log");
        Directory.CreateDirectory(Path.GetDirectoryName(transcript)!);
        // The protocol door carries the connector on the wire, with the same scope; the pipe door takes a file under the home.
        var handed = SpawnServers.Hand(adapter, info, _context.Home, sessionId, [connector]);
        var rules = _driver.HandRules(
            adapter, info, sessionId, null, null, tree: turn.Room, kept: null, brainstorm: BrainstormPermissions.ToolOf(turn.Turn));
        try
        {
            return await _driver.HoldAsync(
                adapter, info, target, sessionId, transcript, turn.Room, notice, rules, handed: handed,
                refusesInput: "a brainstorm's pass is one turn, and takes no words",
                conclude: (exitCode, used, turnFailed, _) =>
                {
                    if (used is not null)
                    {
                        _context.Usage?.Record(new UsageEntry(sessionId, "", adapter.Name, selection.Profile, used.Used, used.Size, DateTimeOffset.UtcNow));
                    }

                    var code = turnFailed is { Length: > 0 } failure && Cooled(adapter, selection, failure, sessionId) ? Limit
                        : turnFailed is { Length: > 0 } ? "turn-failed"
                        : exitCode is null ? TimedOut
                        : exitCode != 0 ? "exited" : null;
                    return Task.FromResult(new BrainstormTurnRun(true, selection.Profile, code, $"{code ?? "completed"}  brainstorm turn {turn.Turn}"));
                },
                ct, scope: scope, handedServers: [], minutes: turn.Minutes,
                noConnector: "— this machine has no connector, so this pass cannot give its answer.", movedFirst: true)
                .ConfigureAwait(false);
        }
        finally
        {
            _context.Output?.Close(sessionId);
        }
    }

    /// <summary>
    /// A limit the turn's door reported, read as the roster reads one (D125 §2): the account cools for the time the agent named or
    /// the machine's cool-off, and no strike is counted. False where the words are no limit, or the cool-off could not be kept.
    /// </summary>
    private bool Cooled(ISessionAdapter adapter, HarnessSelection selection, string failure, string sessionId)
    {
        try
        {
            return _context.Harnesses.Limited(adapter.Name, selection.Profile, failure, sessionId, _context.Config.CoolOff) is not null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
