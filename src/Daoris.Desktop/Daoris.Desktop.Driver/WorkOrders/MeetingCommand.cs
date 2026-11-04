using System.Globalization;
using System.Text;
using Daoris.Contracts.Brainstorms;

namespace Daoris.Driver;

/// <summary>
/// A meeting at the terminal (AGENTROOM1a, D177 points 1, 2, 4 and 7; ONEWAY1t): <c>daoris brainstorm "&lt;question&gt;" --kind meeting --agents a,b</c>
/// asks the running service's brainstorm desk for a meeting, which writes the request the driver's look takes, so every turn runs in
/// whichever process holds the loop; <c>send</c> is the person's press for a round, the first by itself and a later one only named;
/// <c>stop</c> ends the meeting at once; <c>clear</c> removes it, each the desk's. <c>show</c> prints the round's packet whole, what a
/// refreshed packet adds beyond the one the person sent, and the transcript; <c>list</c> names each, read here.
/// </summary>
public static class MeetingCommand
{
    public const string Usage =
        "usage: daoris brainstorm \"<question>\" --kind meeting --agents <a,b> [--repository <name>] [--id <id>]\n"
        + "       daoris brainstorm show|stop|clear <id> --kind meeting\n"
        + "       daoris brainstorm send <id> --kind meeting [--round <n>]\n"
        + "       daoris brainstorm list --kind meeting";

    /// <summary>The fewest agents a meeting names: one agent alone is no meeting.</summary>
    public const int FewestAgents = 2;

    /// <summary>
    /// Exit codes as every verb's: 0 done, 1 refused or no service, 2 a line this verb does not read. The asks and presses go to the
    /// brainstorm desk's meeting kind (<see cref="BrainstormCommand"/>, <c>--kind meeting</c>), the one writer; <c>show</c> and
    /// <c>list</c> read the records here.
    /// </summary>
    public static int Run(string[] args, string home, TextWriter output, DateTimeOffset now, IServiceDoors? doors = null)
    {
        _ = now;
        var files = new MeetingFiles(home);
        switch (args)
        {
            case ["list"]:
                return List(files, output);
            case ["show", var id]:
                return Show(files, id, output);
            case ["send", var id]:
                return BrainstormDoor.Press(doors, new SendBrainstorm(id, BrainstormDoor.Meeting), output);
            case ["send", var id, "--round", var round] when int.TryParse(round, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= 1:
                return BrainstormDoor.Press(doors, new SendBrainstorm(id, BrainstormDoor.Meeting, number), output);
            case ["stop", var id]:
                return BrainstormDoor.Press(doors, new StopBrainstorm(id, BrainstormDoor.Meeting), output);
            case ["clear", var id]:
                return BrainstormDoor.Press(doors, new ClearBrainstorm(id, BrainstormDoor.Meeting), output);
            case ["ask", var question, .. var rest] when !question.StartsWith("--", StringComparison.Ordinal):
                return Ask(question, rest, output, doors);
            default:
                output.WriteLine(Usage);
                return 2;
        }
    }

    private static int Ask(string question, IReadOnlyList<string> rest, TextWriter output, IServiceDoors? doors)
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
                // The caller's own id, as the brainstorm's (D176 point 6), which the desk judges.
                case "--id":
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

        return BrainstormDoor.Press(
            doors, BrainstormDoor.Ask(question, repository is { Length: > 0 } ? repository : null, agents, [], named, meeting: true), output);
    }

    private static int Show(MeetingFiles files, string id, TextWriter output)
    {
        if (files.Request(id) is null)
        {
            output.WriteLine($"daoris: there is no meeting `{id}` on this machine.");
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
        BrainstormStates.Frozen when state.Round == 1 => $"Nothing has gone to any agent. Send it with `daoris brainstorm send {id} --kind meeting`, or clear it.",
        BrainstormStates.Frozen => $"Round {state.Round} has not gone to any agent. Send it with `daoris brainstorm send {id} --kind meeting --round {state.Round}`, "
                                   + $"or stop the meeting with `daoris brainstorm stop {id} --kind meeting`.",
        _ => $"{state.State}.",
    };

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
