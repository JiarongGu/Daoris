using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The meeting at the look (AGENTROOM1a, D177 points 1, 2, 4 and 7): a request the person's verb wrote is frozen into its first
/// round's packet from the brainstorm's assembly and waits for their send; sent, each participant takes its turn in the person's
/// order, one at a time, in a room holding that round's packet and nothing else; Daoris reads each turn's message by its shape, and
/// relays it with its author from its own binding of the turn; and the round ends with the next round's packet frozen from the
/// transcript under a new digest, which goes only by the person's press, naming it. A meeting talks: it has no checkout, publishes
/// nothing and writes into no repository.
/// </summary>
/// <remarks>
/// <para><b>A turn is a brainstorm's pass</b> (<see cref="DriverBrainstorms.PassAsync{T}"/>): its slot under the cap, its own minutes
/// and the round's deadline (the policy's pass and request minutes, D176 point 6), its one repair, a limit's restart on the next
/// account, a crash, the person's stop and its record written as it starts. The turns go in sequence, never side by side, so a
/// meeting's account runs one of its turns at a time.</para>
///
/// <para><b>Within a round, no turn reads another's message of that round.</b> What goes to a maker is only a packet the person read
/// before it went (D177 point 2's show), so each message names exactly what it answered; the round's messages reach the others in
/// the next round's packet, which the person reads first.</para>
///
/// <para><b>A turn's answer is read once, at its turn's end</b>, and moved out of the answers: one found there at any other moment
/// (a second answer to a turn already read, or one written before its turn was granted) is refused, said, and reaches no one.</para>
///
/// <para><b>No real agent takes a turn yet</b>: no tool carries a meeting's message (the connector's <c>brainstorm_give</c> carries
/// proposals), so with no stand-in a turn is refused, <c>no-message-tool</c>, and nothing is started or spent.</para>
/// </remarks>
internal sealed class DriverMeetings(LoopContext context, DriverBrainstorms brainstorms)
{
    private readonly LoopContext _context = context;

    private readonly DriverBrainstorms _brainstorms = brainstorms;

    /// <summary>The code of a turn not started because no tool carries a meeting's message yet.</summary>
    internal const string NoMessageTool = "no-message-tool";

    /// <summary>Why an answer was refused: it was found outside its turn, before it was granted or after it was read.</summary>
    internal const string OutsideTurn = "outside-turn";

    private static string Claim(string id) => $"meeting:{id}";

    /// <summary>
    /// The meetings on file, at a look: each request's first packet frozen, each round the person sent run beside the look, one
    /// stopped before its round ran ended, and one left running by a driver that stopped ended as interrupted. Returns one for each
    /// meeting this look starts, so the planner leaves it under the cap; its turns' slots are the brainstorm's look's to count.
    /// </summary>
    public async Task<int> MeetingsAsync(Snapshot snapshot, int taken, List<string> events, CancellationToken ct)
    {
        var files = new MeetingFiles(_context.Home);
        var requests = files.Requests();
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
                        files.WriteState(new MeetingState(request.Id, BrainstormStates.Ended, state?.Round ?? 1, state?.Packet, BrainstormStates.Stopped, DateTimeOffset.UtcNow));
                        events.Add($"meeting `{request.Id}` stopped by the person before round {state?.Round ?? 1} ran: nothing of it went to any agent.");
                        break;
                    case null when _context.Beside:
                        // PARALLEL1b (D181 point 3): under the watch, the first round's packet frozen beside the look, once.
                        _brainstorms.Freezing(request.Id, token => FreezeAsync(request, snapshot, files, token), ct, kind: "meeting");
                        break;
                    case null:
                        events.Add(await FreezeAsync(request, snapshot, files, ct).ConfigureAwait(false));
                        break;
                    case BrainstormStates.Frozen when state.Packet is { } digest && files.Sent(request.Id, state.Round) == digest:
                        if (_context.Config.Cap - snapshot.Active.Count - taken - begun <= 0) break;
                        if (!_context.Runs.TryOpinion(Claim(request.Id))) break;
                        begun++;
                        files.WriteState(state with { State = BrainstormStates.Running, At = DateTimeOffset.UtcNow });
                        Beside(request, state.Round, digest, _brainstorms.RuleOf(request.Repository, snapshot), files, ct);
                        break;
                    case BrainstormStates.Running when _context.Runs.TryOpinion(Claim(request.Id)):
                        // Nothing in this driver runs it: the driver that did stopped. What was relayed is kept; nothing runs again by itself.
                        try
                        {
                            events.Add(Interrupted(request.Id, state, files));
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
                events.Add($"meeting `{request.Id}` could not be looked at this look, and is looked at again at the next: {error.Message}");
            }
        }

        return begun;
    }

    /// <summary>
    /// The participants, in the person's order (D177 point 1): each agent the request names that the policy lists, or every one where
    /// it lists none (D176 point 9), at most the policy's four, each with its maker; and the ones the policy does not list, which take
    /// no turn.
    /// </summary>
    private (List<MeetingParticipant> Taking, List<string> Left) Participants(MeetingRequest request, BrainstormRule rule)
    {
        var taking = new List<MeetingParticipant>();
        var left = new List<string>();
        foreach (var agent in request.Agents.Select(agent => agent.Trim()).Where(agent => agent.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (rule.Agents.Count > 0 && !rule.Agents.Contains(agent, StringComparer.OrdinalIgnoreCase))
            {
                left.Add(agent);
                continue;
            }

            if (taking.Count >= DriverBrainstorms.MostAgents) break;
            var family = AgentFamily.Of(_context.Adapters, agent);
            taking.Add(new MeetingParticipant($"a{taking.Count + 1}", family.Maker ?? family.Owner, agent));
        }

        return (taking, left);
    }

    /// <summary>
    /// The first round's packet frozen (D177 point 2): the brainstorm's assembly of the question and its sources, and the participants
    /// in order; written once, named by its digest, and shown before anything goes. Fewer than two the policy lets take a turn is no
    /// meeting: it ends, and nothing goes.
    /// </summary>
    private async Task<string> FreezeAsync(MeetingRequest request, Snapshot snapshot, MeetingFiles files, CancellationToken ct)
    {
        var (taking, left) = Participants(request, _brainstorms.RuleOf(request.Repository, snapshot));
        var outside = left.Count == 0 ? "" : $" {string.Join(", ", left.Select(agent => $"`{agent}`"))} {(left.Count == 1 ? "is" : "are")} not in the policy, and take no turn.";
        if (taking.Count < MeetingCommand.FewestAgents)
        {
            files.WriteState(new MeetingState(request.Id, BrainstormStates.Ended, 1, null, DriverBrainstorms.NotInPolicy, DateTimeOffset.UtcNow));
            return $"meeting `{request.Id}` ended before its first round: fewer than two of its agents are in the policy, and nothing went to any agent.{outside}";
        }

        var basis = await _brainstorms.AssembleAsync(request.Question, request.Repository, snapshot, ct).ConfigureAwait(false);
        var packet = MeetingPackets.Freeze(basis, taking);
        files.WritePacket(request.Id, 1, MeetingPackets.Bytes(packet));
        files.WriteState(new MeetingState(request.Id, BrainstormStates.Frozen, 1, packet.Digest, null, DateTimeOffset.UtcNow));
        var from = request.Repository is null ? "the question alone" : $"{packet.Sources.Count} sources of `{request.Repository}` by {packet.Tier}";
        return $"meeting `{request.Id}` frozen: round 1's packet `{packet.Digest[..19]}`, {from}, for {Named(taking)}.{outside} "
            + $"Read it with `daoris-driver meeting show {request.Id}`, and send it with `daoris-driver meeting send {request.Id}`.";
    }

    private static string Named(IEnumerable<MeetingParticipant> participants) =>
        string.Join(", ", participants.Select(each => $"{each.Id} `{each.Adapter}`"));

    /// <summary>
    /// A meeting a driver that stopped left running (D177's trap, a restart around a dispatch and around an answer's write): each
    /// turn it was running, and each it recorded answered but never relayed, ends as <c>interrupted</c>; what was relayed is kept;
    /// and nothing runs again by itself.
    /// </summary>
    private static string Interrupted(string id, MeetingState state, MeetingFiles files)
    {
        var transcript = files.ReadTranscript(id);
        var relayed = transcript.Messages.Select(message => MeetingFiles.Turn(message.Round, message.Participant)).ToHashSet(StringComparer.Ordinal);
        foreach (var pass in files.Turns.ReadPasses(id))
        {
            if (pass.State == BrainstormPassRecord.Running || (pass.State == BrainstormPassRecord.Answered && !relayed.Contains(pass.By.Pass)))
            {
                files.Turns.WritePass(id, pass with { State = BrainstormPassRecord.Interrupted, Codes = [BrainstormStates.Interrupted] });
            }
        }

        files.WriteState(state with { State = BrainstormStates.Ended, Code = BrainstormStates.Interrupted, At = DateTimeOffset.UtcNow });
        files.RemoveRooms(id);
        return $"meeting `{id}` was running round {state.Round} when its driver stopped: ended as interrupted, its {transcript.Messages.Count} relayed "
            + "messages are kept, and nothing runs again by itself.";
    }

    /// <summary>A round run beside the look, as a brainstorm is: told opened at once, its line joining a later look's report.</summary>
    private void Beside(MeetingRequest request, int round, string digest, BrainstormRule rule, MeetingFiles files, CancellationToken ct) =>
        _ = _context.Runs.StartAsync(async opened =>
        {
            opened();
            try
            {
                return new StartRun(await RoundAsync(request, round, digest, rule, files, ct).ConfigureAwait(false), Opened: true);
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or JsonException or IOException
                                              or UnauthorizedAccessException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                files.WriteState(new MeetingState(request.Id, BrainstormStates.Ended, round, digest, "failed", DateTimeOffset.UtcNow));
                files.RemoveRooms(request.Id);
                return new StartRun($"meeting `{request.Id}` stopped in round {round}: {error.Message}", Opened: true);
            }
            finally
            {
                _context.Runs.OpinionDone(Claim(request.Id));
            }
        });

    /// <summary>
    /// One round the person sent (D177 point 4): each participant's turn in their order, one at a time, within its own minutes and the
    /// round's deadline; each message relayed as it is read; then the next round's packet frozen, or the meeting ended after its last.
    /// The person's stop is watched throughout, and the rooms removed however the round ended.
    /// </summary>
    private async Task<string> RoundAsync(MeetingRequest request, int round, string digest, BrainstormRule rule, MeetingFiles files, CancellationToken ct)
    {
        var id = request.Id;
        var bytes = files.PacketBytes(id, round);
        if (bytes is null || MeetingPackets.Read(bytes) is not { } packet || packet.Digest != digest)
        {
            throw new DriverException($"its round {round} packet is not the one the person sent ({digest}).");
        }

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var done = new CancellationTokenSource();
        var watching = _brainstorms.WatchAsync(id, files.Turns, stop, done.Token);
        try
        {
            var minute = _brainstorms.Minute;
            var deadline = DateTimeOffset.UtcNow + rule.Minutes.Request * minute;
            var kept = files.ReadTranscript(id);
            var messages = new List<MeetingMessage>(kept.Messages);
            var refused = new List<MeetingRefused>(kept.Refused);
            var said = new List<string>();
            var gaps = new List<string>();
            foreach (var participant in packet.Participants)
            {
                var turn = MeetingFiles.Turn(round, participant.Id);
                Refuse(id, files, refused, said);
                var (record, text) = await _brainstorms.PassAsync<string>(
                    id, turn, participant.Adapter, MeetingPackets.Say, bytes, MeetingInstruction.Say(digest, participant, round), null,
                    answer =>
                    {
                        var check = MeetingMessages.Check(answer);
                        return (check.Text, check.Codes);
                    }, files.Turns, rule.Minutes.Pass * minute, deadline, stop.Token, ct, _context.MeetingTurns ?? Unavailable).ConfigureAwait(false);
                // Read once, at its turn's end: what is written for this turn after is a second answer, and refused.
                files.Read(id, turn);
                files.Read(id, $"{turn}r");
                if (text is not null)
                {
                    // The author is Daoris's binding of the turn, never what the message says of itself.
                    messages.Add(new MeetingMessage(round, participant.Id, participant.Maker, participant.Adapter, text, digest));
                    files.WriteTranscript(id, new MeetingTranscript(messages, refused));
                }
                else if (record.State != BrainstormPassRecord.Stopped)
                {
                    gaps.Add($"{participant.Id} `{participant.Adapter}` did not answer: {string.Join(", ", record.Codes)}");
                }
            }

            Refuse(id, files, refused, said);
            files.WriteTranscript(id, new MeetingTranscript(messages, refused));
            var spoke = messages.Count(message => message.Round == round);
            var refusals = said.Count == 0 ? "" : " " + string.Join(" ", said);
            if (stop.IsCancellationRequested)
            {
                files.WriteState(new MeetingState(id, BrainstormStates.Ended, round, digest, BrainstormStates.Stopped, DateTimeOffset.UtcNow));
                return $"meeting `{id}` stopped by the person in round {round}: {spoke} of its messages kept, and nothing more goes to any agent.{refusals}";
            }

            var spoken = $"{spoke} of {packet.Participants.Count} participants spoke{(gaps.Count == 0 ? "" : "; " + string.Join("; ", gaps))}.";
            if (spoke == 0)
            {
                files.WriteState(new MeetingState(id, BrainstormStates.Ended, round, digest, BrainstormStates.NoAgent, DateTimeOffset.UtcNow));
                return $"meeting `{id}` ended: no agent spoke in round {round}, and its packets are kept. {spoken}{refusals}";
            }

            if (round >= MeetingPackets.MostRounds)
            {
                files.WriteState(new MeetingState(id, BrainstormStates.Ended, round, digest, null, DateTimeOffset.UtcNow));
                return $"meeting `{id}` ended after round {round}: {spoken} Read the transcript with `daoris-driver meeting show {id}`.{refusals}";
            }

            var next = MeetingPackets.Refresh(packet, messages);
            files.WritePacket(id, round + 1, MeetingPackets.Bytes(next));
            files.WriteState(new MeetingState(id, BrainstormStates.Frozen, round + 1, next.Digest, null, DateTimeOffset.UtcNow));
            return $"meeting `{id}` round {round} ended: {spoken} Round {round + 1}'s packet `{next.Digest[..19]}` adds their messages: read it with "
                + $"`daoris-driver meeting show {id}`, and send it with `daoris-driver meeting send {id} --round {round + 1}`.{refusals}";
        }
        finally
        {
            await done.CancelAsync().ConfigureAwait(false);
            await watching.ConfigureAwait(false);
            files.RemoveRooms(id);
        }
    }

    /// <summary>
    /// Every answer waiting outside its turn (D177's trap, an agent answering for another): one for a turn already read, or for a
    /// turn not yet granted, is refused, said, and removed unread, so it reaches no one.
    /// </summary>
    private static void Refuse(string id, MeetingFiles files, List<MeetingRefused> refused, List<string> said)
    {
        foreach (var turn in files.Waiting(id))
        {
            files.Refuse(id, turn);
            refused.Add(new MeetingRefused(turn, OutsideTurn));
            said.Add($"An answer for `{turn}` outside its turn was refused, and reached no one.");
        }
    }

    /// <summary>
    /// A real agent's meeting turn, refused and never started (AGENTROOM1a): its result tool is the connector's, and none carries a
    /// meeting's message yet (<c>brainstorm_give</c> carries a generator's proposals), so a started turn could not answer.
    /// </summary>
    private static Task<BrainstormTurnRun> Unavailable(BrainstormTurn turn, CancellationToken ct) =>
        Task.FromResult(new BrainstormTurnRun(false, null, NoMessageTool,
            $"held  meeting turn {turn.Turn}: no tool carries a meeting's message yet, so no agent is started"));
}
