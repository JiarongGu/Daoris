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
/// <c>running</c>, and <c>ended</c>. The person's send is a file of its own, naming the digest they saw.</para>
///
/// <para><b>The passes run in sequence, beside the look,</b> on one slot under the cap. Running them side by side within one
/// deadline, the policy's makers and the stop are BRAINSTORM1b's.</para>
/// </remarks>
/// <param name="driver">The driver whose run machinery a turn goes through: the spawn's preparation, the rules handed and the hold.</param>
internal sealed class DriverBrainstorms(LoopContext context, Driver driver)
{
    private readonly LoopContext _context = context;

    private readonly Driver _driver = driver;

    /// <summary>The most generators one request runs (D176 point 9's proposed default: at most four makers).</summary>
    internal const int MostAgents = 4;

    /// <summary>Each turn's minutes, until the policy names them (D176 point 9's proposed default: ten).</summary>
    internal const int TurnMinutes = 10;

    private static string Claim(string id) => $"brainstorm:{id}";

    /// <summary>
    /// The brainstorms on file, at a look: each request frozen, each sent one run beside the look, and one left running by a driver
    /// that stopped ended as interrupted. Returns how many this look starts, each taking a slot under the cap as a pass does.
    /// </summary>
    public async Task<int> BrainstormsAsync(Snapshot snapshot, int taken, List<string> events, CancellationToken ct)
    {
        var files = new BrainstormFiles(_context.Home);
        var requests = files.Requests();
        if (requests.Count == 0) return 0;

        var begun = 0;
        foreach (var request in requests)
        {
            try
            {
                var state = files.ReadState(request.Id);
                switch (state?.State)
                {
                    case null:
                        events.Add(await FreezeAsync(request, snapshot, files, ct).ConfigureAwait(false));
                        break;
                    case BrainstormStates.Frozen when files.Sent(request.Id) == state.Packet:
                        if (_context.Config.Cap - snapshot.Active.Count - taken - begun <= 0) break;
                        if (!_context.Runs.TryOpinion(Claim(request.Id))) break;
                        begun++;
                        files.WriteState(state with { State = BrainstormStates.Running, At = DateTimeOffset.UtcNow });
                        Beside(request, state.Packet!, files, ct);
                        break;
                    case BrainstormStates.Running when _context.Runs.TryOpinion(Claim(request.Id)):
                        // Nothing in this driver runs it: the driver that did stopped. What answered is kept; nothing runs again by itself.
                        try
                        {
                            files.WriteState(state with { State = BrainstormStates.Ended, Code = "interrupted", At = DateTimeOffset.UtcNow });
                            files.RemoveRooms(request.Id);
                            events.Add($"brainstorm `{request.Id}` was running when its driver stopped: ended as interrupted, and what answered is kept.");
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

        return begun;
    }

    /// <summary>
    /// The packet frozen (D176 point 2): the question, and for a repository its sources from the knowledge search in more than one
    /// wording, else from its declared index's files; written once, named by its digest, and shown before anything goes.
    /// </summary>
    private async Task<string> FreezeAsync(BrainstormRequest request, Snapshot snapshot, BrainstormFiles files, CancellationToken ct)
    {
        var found = new List<BrainstormFound>();
        string? tier = null;
        string? revision = null;
        if (request.Repository is { } repository)
        {
            var (root, commit) = await CheckoutAsync(repository, snapshot, ct).ConfigureAwait(false);
            revision = commit;
            try
            {
                foreach (var words in BrainstormWordings.Of(request.Question))
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
                if (root is not null) found.AddRange(IndexFiles(root, request.Question));
            }
        }

        var packet = BrainstormPackets.Freeze(request.Question, request.Repository, tier, revision, found, BrainstormBounds.First);
        files.WritePacket(request.Id, BrainstormPackets.Bytes(packet));
        files.WriteState(new BrainstormState(request.Id, BrainstormStates.Frozen, packet.Digest, null, DateTimeOffset.UtcNow));
        var from = request.Repository is null ? "the question alone" : $"{packet.Sources.Count} sources of `{request.Repository}` by {packet.Tier}";
        return $"brainstorm `{request.Id}` frozen: packet `{packet.Digest[..19]}`, {from}. Read it with `daoris-driver brainstorm show {request.Id}`, "
            + $"and send it with `daoris-driver brainstorm send {request.Id}`.";
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

    /// <summary>A brainstorm run beside the look, as a helper's pass is: told opened at once, its line joining a later look's report.</summary>
    private void Beside(BrainstormRequest request, string digest, BrainstormFiles files, CancellationToken ct) =>
        _ = _context.Runs.StartAsync(async opened =>
        {
            opened();
            try
            {
                return new StartRun(await RunAsync(request, digest, files, ct).ConfigureAwait(false), Opened: true);
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
    /// The passes of one sent brainstorm, in order: each generator once with its one repair, then the synthesis with its one repair,
    /// then the document; the rooms removed however it ended.
    /// </summary>
    private async Task<string> RunAsync(BrainstormRequest request, string digest, BrainstormFiles files, CancellationToken ct)
    {
        var id = request.Id;
        var bytes = files.PacketBytes(id);
        if (bytes is null || BrainstormPackets.Read(bytes) is not { } packet || packet.Digest != digest)
        {
            throw new DriverException($"its packet is not the one the person sent ({digest}).");
        }

        try
        {
            var sources = packet.Sources.Select(source => source.Id).ToHashSet(StringComparer.Ordinal);
            var agents = request.Agents.Count > 0 ? request.Agents.Take(MostAgents).ToList() : [_context.Config.Adapter];
            var passes = new List<BrainstormPassRecord>();
            var answered = new List<(BrainstormBy By, BrainstormAnswer Answer)>();
            for (var at = 0; at < agents.Count; at++)
            {
                var turn = $"g{at + 1}";
                var (record, answer) = await PassAsync<BrainstormAnswer>(
                    id, turn, agents[at], BrainstormPassRecord.Generate, bytes, BrainstormInstruction.Generator(digest), null,
                    text =>
                    {
                        var check = BrainstormAnswers.Check(text, sources);
                        return (check.Answer, check.Codes);
                    }, files, ct).ConfigureAwait(false);
                passes.Add(record);
                if (answer is not null) answered.Add((record.By, answer));
            }

            var needs = answered.SelectMany(each => each.Answer.Needs).Distinct(StringComparer.Ordinal).ToList();
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
                // The synthesizer: the first agent of the request whose generator answered (D176 point 9's proposed default).
                var synthesizer = answered[0].By.Adapter;
                var (record, synthesis) = await PassAsync<BrainstormSynthesized>(
                    id, "s1", synthesizer, BrainstormPassRecord.Synthesize, bytes, BrainstormInstruction.Synthesizer(digest),
                    BrainstormSyntheses.Anonymous(handed),
                    text =>
                    {
                        var check = BrainstormSyntheses.Check(text, handed);
                        return (check.Synthesis, check.Codes);
                    }, files, ct).ConfigureAwait(false);
                passes.Add(record);
                document = synthesis is not null
                    ? BrainstormDocuments.Ranked(id, packet, passes, handed, synthesis, needs)
                    : BrainstormDocuments.Unranked(id, packet, passes, handed, needs, BrainstormDocuments.NoSynthesis);
            }

            files.WriteDocument(id, document);
            files.WriteState(new BrainstormState(
                id, BrainstormStates.Ended, digest, document.Unranked is null ? null : BrainstormStates.Unranked, DateTimeOffset.UtcNow));
            return $"brainstorm `{id}` ended: {document.Rows.Count} {(document.Unranked is null ? "ranked" : "unranked")} rows from "
                + $"{answered.Count} of {agents.Count} generators. Read it with `daoris-driver brainstorm show {id}`.";
        }
        finally
        {
            files.RemoveRooms(id);
        }
    }

    /// <summary>
    /// One pass: its turn in a fresh room, its answer validated by <paramref name="check"/>, and where that failed its one repair,
    /// handed the codes (D176 point 3). The pass's record is written whatever it came to.
    /// </summary>
    private async Task<(BrainstormPassRecord Record, T? Valid)> PassAsync<T>(
        string id, string pass, string agent, string role, byte[] packet, string instruction, byte[]? proposals,
        Func<string?, (T? Valid, IReadOnlyList<string> Codes)> check, BrainstormFiles files, CancellationToken ct)
        where T : class
    {
        var family = AgentFamily.Of(_context.Adapters, agent);
        var maker = family.Maker ?? family.Owner;
        IReadOnlyList<string> codes = [];
        string? account = null;
        for (var turns = 1; turns <= 2; turns++)
        {
            var turn = turns == 1 ? pass : $"{pass}r";
            var said = turns == 1 ? instruction : BrainstormInstruction.Repair(instruction, codes);
            var room = files.OpenRoom(id, turn, packet, said, proposals);
            var run = await (_context.BrainstormTurns ?? TurnAsync)(
                new BrainstormTurn(id, turn, agent, room, said, files.AnswerPath(id, turn), TurnMinutes), ct).ConfigureAwait(false);
            account = run.Account ?? account;
            var by = new BrainstormBy(pass, maker, agent, account);
            if (!run.Ran)
            {
                var unavailable = new BrainstormPassRecord(by, role, BrainstormPassRecord.Unavailable, [run.Code ?? "not-started"], turns);
                files.WritePass(id, unavailable);
                return (unavailable, null);
            }

            var (valid, failed) = check(files.ReadAnswer(id, turn));
            if (valid is not null)
            {
                var record = new BrainstormPassRecord(by, role, BrainstormPassRecord.Answered, [], turns);
                files.WritePass(id, record);
                return (record, valid);
            }

            codes = run.Code is { } ended ? [.. failed, ended] : failed;
        }

        var refused = new BrainstormPassRecord(new BrainstormBy(pass, maker, agent, account), role, BrainstormPassRecord.Failed, codes, 2);
        files.WritePass(id, refused);
        return (refused, null);
    }

    /// <summary>
    /// One real turn (BRAINSTORM1a): the agent's account by the walk, its spawn prepared as any session's, the reviewer's read-only
    /// rules held to its room, one turn framed as one prompt, no connector beyond its result, and its own minutes. Its record is the
    /// brainstorm's own, under the home's data: no session record is opened on the service, and nothing of it syncs.
    /// </summary>
    internal async Task<BrainstormTurnRun> TurnAsync(BrainstormTurn turn, CancellationToken ct)
    {
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
        var sessionId = $"brainstorm-{turn.Id}-{turn.Turn}";
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
        var rules = _driver.HandRules(adapter, info, sessionId, null, null, tree: turn.Room, kept: null, opinion: true);
        try
        {
            return await _driver.HoldAsync(
                adapter, info, target, sessionId, transcript, turn.Room, notice, rules, handed: null,
                refusesInput: "a brainstorm's pass is one turn, and takes no words",
                conclude: (exitCode, used, turnFailed, _) =>
                {
                    if (used is not null)
                    {
                        _context.Usage?.Record(new UsageEntry(sessionId, "", adapter.Name, selection.Profile, used.Used, used.Size, DateTimeOffset.UtcNow));
                    }

                    var code = turnFailed is { Length: > 0 } ? "turn-failed"
                        : exitCode is null ? "timed-out"
                        : exitCode != 0 ? "exited" : null;
                    return Task.FromResult(new BrainstormTurnRun(true, selection.Profile, code, $"{code ?? "completed"}  brainstorm turn {turn.Turn}"));
                },
                ct, handedServers: [], minutes: turn.Minutes, noConnector: "a brainstorm's pass is handed no connector here", movedFirst: true)
                .ConfigureAwait(false);
        }
        finally
        {
            _context.Output?.Close(sessionId);
        }
    }
}
