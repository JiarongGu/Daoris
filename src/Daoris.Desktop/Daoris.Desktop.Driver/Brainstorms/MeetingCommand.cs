using System.Globalization;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// A meeting at the terminal (AGENTROOM1a, D177 points 1, 2, 4 and 7): <c>daoris-driver meeting ask "&lt;question&gt;" --agents a,b</c>
/// writes a request the driver's look takes, as the brainstorm's verb does, so every turn runs in whichever process holds the loop;
/// <c>show</c> prints the round's packet whole, what a refreshed packet adds beyond the one the person sent, and the transcript;
/// <c>send</c> is the person's press for a round, the first by itself and a later one only named; <c>stop</c> ends the meeting at
/// once; <c>clear</c> removes it; <c>list</c> names each. Files only: the verb spawns nothing and asks no service.
/// </summary>
public static class MeetingCommand
{
    public const string Usage =
        "usage: daoris-driver meeting ask \"<question>\" --agents <a,b> [--repository <name>] [--id <id>]\n"
        + "       daoris-driver meeting show|stop|clear <id>\n"
        + "       daoris-driver meeting send <id> [--round <n>]\n"
        + "       daoris-driver meeting list";

    /// <summary>The fewest agents a meeting names: one agent alone is no meeting.</summary>
    public const int FewestAgents = 2;

    /// <summary>Exit codes as every verb's: 0 done, 1 refused, 2 a line this verb does not read.</summary>
    public static int Run(string[] args, string home, TextWriter output, DateTimeOffset now)
    {
        var files = new MeetingFiles(home);
        switch (args)
        {
            case ["list"]:
                return List(files, output);
            case ["show", var id]:
                return Show(files, id, output);
            case ["send", var id]:
                return Send(files, id, null, output, now);
            case ["send", var id, "--round", var round] when int.TryParse(round, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= 1:
                return Send(files, id, number, output, now);
            case ["stop", var id]:
                return Stop(files, id, output, now);
            case ["clear", var id]:
                return Clear(files, id, output);
            case ["ask", var question, .. var rest] when !question.StartsWith("--", StringComparison.Ordinal):
                return Ask(files, question, rest, output, now);
            default:
                output.WriteLine(Usage);
                return 2;
        }
    }

    private static int Ask(MeetingFiles files, string question, IReadOnlyList<string> rest, TextWriter output, DateTimeOffset now)
    {
        string? repository = null;
        string? named = null;
        IReadOnlyList<string>? agents = null;
        for (var at = 0; at < rest.Count; at += 2)
        {
            if (at + 1 >= rest.Count || rest[at + 1].StartsWith("--", StringComparison.Ordinal))
            {
                output.WriteLine(Usage);
                return 2;
            }

            switch (rest[at])
            {
                case "--repository":
                    repository = rest[at + 1].Trim();
                    break;
                case "--agents":
                    agents = [.. rest[at + 1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
                    break;
                // The caller's own id, as the brainstorm's (D176 point 6): asked again under it, it is the same meeting, never a second.
                case "--id" when BrainstormCommand.IdShape().IsMatch(rest[at + 1]):
                    named = rest[at + 1];
                    break;
                default:
                    output.WriteLine(Usage);
                    return 2;
            }
        }

        if (agents is null)
        {
            output.WriteLine(Usage);
            return 2;
        }

        question = question.Trim();
        if (question.Length == 0 || question.Length > BrainstormBounds.First.Question)
        {
            output.WriteLine($"daoris-driver: a meeting's question is 1 to {BrainstormBounds.First.Question} characters; this one is {question.Length}.");
            return 1;
        }

        var different = agents.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        if (different != agents.Count || different < FewestAgents || different > DriverBrainstormLimits.MostAgents)
        {
            output.WriteLine($"daoris-driver: a meeting names {FewestAgents} to {DriverBrainstormLimits.MostAgents} different agents, each once; "
                + $"{agents.Count} were named.");
            return 1;
        }

        repository = repository is { Length: > 0 } ? repository : null;
        if (named is not null && files.Request(named) is { } asked)
        {
            if (asked.Question != question || !string.Equals(asked.Repository, repository, StringComparison.Ordinal) || !asked.Agents.SequenceEqual(agents, StringComparer.Ordinal))
            {
                output.WriteLine($"daoris-driver: meeting `{named}` was asked with other words; an id names one meeting.");
                return 1;
            }

            output.WriteLine($"Meeting `{named}` is already asked: the same meeting, never a second spend. Read it with `daoris-driver meeting show {named}`.");
            return 0;
        }

        var id = files.WriteRequest(question, repository, agents, now, named);
        output.WriteLine($"Meeting `{id}` asked of {string.Join(", ", agents.Select(agent => $"`{agent}`"))}, in that order. Daoris freezes its first "
            + "round's packet at its next look, from " + (repository is null ? "the question alone" : $"its knowledge of `{repository}`")
            + $"; with no Daoris running, it waits. Read it with `daoris-driver meeting show {id}` before you send it.");
        return 0;
    }

    private static int Show(MeetingFiles files, string id, TextWriter output)
    {
        if (files.Request(id) is null)
        {
            output.WriteLine($"daoris-driver: there is no meeting `{id}` on this machine.");
            return 1;
        }

        var state = files.ReadState(id);
        if (state is null)
        {
            output.WriteLine($"Meeting `{id}` waits for Daoris's next look, which freezes its first round's packet.");
            return 0;
        }

        var text = new StringBuilder();
        if (files.Packet(id, state.Round) is { } packet)
        {
            text.Append(Packet(id, packet));
            if (state.State == BrainstormStates.Frozen && state.Round > 1 && files.Packet(id, state.Round - 1) is { } previous)
            {
                text.Append(Adds(previous, packet)).Append('\n');
            }

            text.Append(Transcript(files, id, packet, state));
        }

        text.Append(Standing(files, id, state)).Append('\n');
        output.Write(text.ToString());
        return 0;
    }

    /// <summary>A round's packet whole, as the person reads it before it goes: the question, the participants, and each source.</summary>
    internal static string Packet(string id, MeetingPacket packet)
    {
        var text = new StringBuilder();
        text.Append("Meeting `").Append(id).Append("`, round ").Append(packet.Round).Append(", packet `").Append(packet.Digest).Append("`\n\n");
        foreach (var line in packet.Question.Split('\n')) text.Append("> ").Append(line.TrimEnd('\r')).Append('\n');
        text.Append("\nParticipants, in their order: ")
            .Append(string.Join(", ", packet.Participants.Select(each => $"{each.Id} `{each.Adapter}` (maker {each.Maker})"))).Append(".\n");
        BrainstormCommand.Sources(text, packet.Repository, packet.Tier, packet.Sources, packet.LeftOut);
        text.Append('\n');
        return text.ToString();
    }

    /// <summary>
    /// What a refreshed packet adds beyond the one the person sent (D177 point 2's show): another maker's words, and whether anything
    /// else changed, which a refresh never does.
    /// </summary>
    internal static string Adds(MeetingPacket previous, MeetingPacket packet)
    {
        var added = MeetingPackets.Added(previous, packet);
        var rounds = added.Select(message => message.Round).Distinct().Order().ToList();
        var same = previous.Question == packet.Question && previous.Repository == packet.Repository
                   && previous.Sources.SequenceEqual(packet.Sources) && previous.Participants.SequenceEqual(packet.Participants);
        return $"Round {packet.Round}'s packet adds to round {previous.Round}'s, which you sent: {added.Count} {(added.Count == 1 ? "message" : "messages")}"
            + (rounds.Count == 0 ? "" : $" of round {string.Join(" and ", rounds)}")
            + ", another maker's words to each participant that did not say them. "
            + (same ? "Its question, its sources and its participants are as they were." : "Its question, its sources or its participants changed.")
            + "\n";
    }

    /// <summary>
    /// The conversation so far, round by round in the participants' order: each message with its author as Daoris's records bind
    /// it, each turn that did not answer with why, and each answer refused.
    /// </summary>
    private static string Transcript(MeetingFiles files, string id, MeetingPacket packet, MeetingState state)
    {
        var transcript = files.ReadTranscript(id);
        var passes = files.Turns.ReadPasses(id).ToDictionary(pass => pass.By.Pass, StringComparer.Ordinal);
        var last = Math.Max(state.Round, transcript.Messages.Select(message => message.Round).DefaultIfEmpty(0).Max());
        var text = new StringBuilder();
        for (var round = 1; round <= last; round++)
        {
            foreach (var participant in packet.Participants)
            {
                if (transcript.Messages.FirstOrDefault(each => each.Round == round && each.Participant == participant.Id) is { } message)
                {
                    text.Append($"[round {round}] {message.Participant} `{message.Adapter}` (maker {message.Maker}):\n");
                    foreach (var line in message.Text.Split('\n')) text.Append("    ").Append(line.TrimEnd('\r')).Append('\n');
                }
                else if (passes.TryGetValue(MeetingFiles.Turn(round, participant.Id), out var pass))
                {
                    text.Append(pass.State == BrainstormPassRecord.Running
                        ? $"[round {round}] {participant.Id} `{participant.Adapter}`: its turn runs.\n"
                        : $"[round {round}] {participant.Id} `{participant.Adapter}` did not answer: {string.Join(", ", pass.Codes)}.\n");
                }
            }
        }

        foreach (var refused in transcript.Refused)
        {
            text.Append($"Refused: an answer for `{refused.Turn}` ({refused.Why}), which reached no one.\n");
        }

        return text.Length == 0 ? "" : text.Append('\n').ToString();
    }

    private static string Standing(MeetingFiles files, string id, MeetingState state) => state.State switch
    {
        BrainstormStates.Ended => $"Ended ({state.Code ?? "after its last round"}).",
        _ when files.Stopped(id) => "Stopped: nothing more goes to any agent, and Daoris ends it at its next look.",
        BrainstormStates.Running => $"Round {state.Round} is running: one participant's turn at a time, in their order.",
        BrainstormStates.Frozen when files.Sent(id, state.Round) == state.Packet => $"Sent: round {state.Round} goes at Daoris's next look.",
        BrainstormStates.Frozen when state.Round == 1 => $"Nothing has gone to any agent. Send it with `daoris-driver meeting send {id}`, or clear it.",
        BrainstormStates.Frozen => $"Round {state.Round} has not gone to any agent. Send it with `daoris-driver meeting send {id} --round {state.Round}`, "
                                   + $"or stop the meeting with `daoris-driver meeting stop {id}`.",
        _ => $"{state.State}.",
    };

    /// <summary>
    /// The person's press for one round (D177 point 4): the first round's by itself; a later round's only named, so a send repeated
    /// from a terminal's history never spends another round; refused while a round runs, and after a stop.
    /// </summary>
    private static int Send(MeetingFiles files, string id, int? round, TextWriter output, DateTimeOffset now)
    {
        if (files.Request(id) is null)
        {
            output.WriteLine($"daoris-driver: there is no meeting `{id}` on this machine.");
            return 1;
        }

        var state = files.ReadState(id);
        if (state is null)
        {
            output.WriteLine($"daoris-driver: meeting `{id}` has no packet yet: Daoris freezes its first round's at its next look, and you read it before it goes.");
            return 1;
        }

        if (files.Stopped(id) || state.State == BrainstormStates.Ended)
        {
            output.WriteLine(files.Stopped(id) || state.Code == BrainstormStates.Stopped
                ? $"daoris-driver: meeting `{id}` was stopped: nothing more goes to any agent."
                : $"daoris-driver: meeting `{id}` ended ({state.Code ?? "after its last round"}): nothing more goes to any agent.");
            return 1;
        }

        var wanted = round ?? 1;
        if (state.State == BrainstormStates.Running)
        {
            if (wanted == state.Round)
            {
                output.WriteLine($"Meeting `{id}`'s round {wanted} is already sent, and running.");
                return 0;
            }

            output.WriteLine($"daoris-driver: meeting `{id}`'s round {state.Round} is running; round {wanted} can be sent once it ends and you read its packet.");
            return 1;
        }

        if (state is not { State: BrainstormStates.Frozen, Packet: { } digest })
        {
            output.WriteLine($"daoris-driver: meeting `{id}` has no round to send ({state.State}).");
            return 1;
        }

        if (wanted != state.Round)
        {
            output.WriteLine(round is null
                ? $"daoris-driver: meeting `{id}` waits for round {state.Round}: read its packet with `daoris-driver meeting show {id}`, then send it with "
                  + $"`daoris-driver meeting send {id} --round {state.Round}`."
                : $"daoris-driver: meeting `{id}` waits for round {state.Round}, not round {wanted}.");
            return 1;
        }

        if (files.Sent(id, state.Round) == digest)
        {
            output.WriteLine($"Meeting `{id}`'s round {state.Round} is already sent: the same round, never a second spend.");
            return 0;
        }

        files.WriteSent(id, state.Round, digest, now);
        output.WriteLine($"Meeting `{id}`'s round {state.Round} sent, packet `{digest}`: its turns start at Daoris's next look, one participant at a time. "
            + $"Read the transcript with `daoris-driver meeting show {id}`.");
        return 0;
    }

    /// <summary>The person's stop (D177's trap, the person leaving): the turn that runs ends, what was said is kept, and nothing more goes.</summary>
    private static int Stop(MeetingFiles files, string id, TextWriter output, DateTimeOffset now)
    {
        if (files.Request(id) is null)
        {
            output.WriteLine($"daoris-driver: there is no meeting `{id}` on this machine.");
            return 1;
        }

        if (files.ReadState(id) is { State: BrainstormStates.Ended } ended)
        {
            output.WriteLine($"Meeting `{id}` already ended ({ended.Code ?? "after its last round"}).");
            return 0;
        }

        files.WriteStop(id, now);
        output.WriteLine($"Meeting `{id}`: stop asked. The turn that runs ends where Daoris runs it, what was said is kept, and nothing more goes to any agent.");
        return 0;
    }

    private static int Clear(MeetingFiles files, string id, TextWriter output)
    {
        if (files.Request(id) is null && !Directory.Exists(files.Folder(id)))
        {
            output.WriteLine($"daoris-driver: there is no meeting `{id}` on this machine.");
            return 1;
        }

        if (files.ReadState(id) is { State: BrainstormStates.Running })
        {
            output.WriteLine($"daoris-driver: meeting `{id}` is running; it is cleared once its round ends.");
            return 1;
        }

        if (files.Clear(id) is { } left)
        {
            output.WriteLine($"daoris-driver: meeting `{id}` could not be cleared whole: {left}");
            return 1;
        }

        output.WriteLine($"Meeting `{id}` cleared: its records, its packets and its transcript are gone from this machine.");
        return 0;
    }

    private static int List(MeetingFiles files, TextWriter output)
    {
        var requests = files.Requests();
        if (requests.Count == 0) output.WriteLine("No meeting on this machine.");
        foreach (var request in requests)
        {
            var state = files.ReadState(request.Id) is { } stood
                ? $"{stood.State}{(stood.Code is null ? "" : $" ({stood.Code})")}, round {stood.Round}"
                : "asked";
            var line = request.Question.Split('\n')[0];
            output.WriteLine($"{request.Id}  {state}  {(line.Length > 80 ? line[..80] + "…" : line)}");
        }

        return 0;
    }
}
