using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The brainstorm at the terminal (BRAINSTORM1a, D176 points 1, 2 and 8; ONEWAY1t): <c>daoris brainstorm "&lt;question&gt;"</c>
/// asks the running service's desk, the one writer of a request, which the driver's look takes, so every pass runs in whichever
/// process holds the loop. <c>send</c>, <c>stop</c> and <c>clear</c> are the desk's presses; <c>show</c> and <c>list</c> read the
/// records here, which the look and the desk share. <c>--kind meeting</c> asks a meeting of the same desk (D177). With no service
/// running the verb says so and writes nothing.
/// </summary>
[CommandGroup("brainstorm", "show", "send", "stop", "clear", "list", "measure", Takes = "\"<question>\"")]
public static class BrainstormCommand
{
    public const string Usage =
        "usage: daoris brainstorm \"<question>\" [--repository <name>] [--agents <a,b>] [--source <path>]... [--id <id>]\n"
        + "       daoris brainstorm show|send|stop|clear <id>\n"
        + "       daoris brainstorm list\n"
        + "       daoris brainstorm \"<question>\" --kind meeting --agents <a,b> [--repository <name>] [--id <id>]\n"
        + "       daoris brainstorm send <id> --kind meeting [--round <n>]";

    /// <summary>
    /// Exit codes as every verb's: 0 done, 1 refused or no service, 2 a line this verb does not read. <paramref name="doors"/> null
    /// finds the service as a terminal does; <paramref name="now"/> is unused, since the desk stamps what it writes.
    /// </summary>
    public static int Run(string[] args, string home, TextWriter output, DateTimeOffset now, IServiceDoors? doors = null)
    {
        _ = now;
        if (Kind(args) is not { } kinded)
        {
            output.WriteLine(Usage);
            return 2;
        }

        if (kinded.Meeting) return MeetingCommand.Run(kinded.Args, home, output, now, doors);
        args = kinded.Args;
        var files = new BrainstormFiles(home);
        switch (args)
        {
            case ["list"]:
                return List(files, output);
            // The folder that holds every meeting is no brainstorm (AGENTROOM1a): the desk refuses it a press, and a read says so.
            case ["show", var id] when BrainstormFiles.Reserved(id):
                output.WriteLine($"daoris-driver: there is no brainstorm `{id}` on this machine.");
                return 1;
            case ["show", var id]:
                return Show(files, id, output);
            case ["send", var id]:
                return BrainstormDoor.Press(doors, ServiceVerb.Post, BrainstormDoor.PathOf(id, "send", meeting: false), "{}", output);
            case ["stop", var id]:
                return BrainstormDoor.Press(doors, ServiceVerb.Post, BrainstormDoor.PathOf(id, "stop", meeting: false), "{}", output);
            case ["clear", var id]:
                return BrainstormDoor.Press(doors, ServiceVerb.Delete, BrainstormDoor.PathOf(id, null, meeting: false), null, output);
            case [var question, .. var rest] when !question.StartsWith("--", StringComparison.Ordinal) && question is not ("show" or "send" or "stop" or "clear" or "list"):
                return Ask(question, rest, output, doors);
            default:
                output.WriteLine(Usage);
                return 2;
        }
    }

    /// <summary>
    /// The line without its <c>--kind</c> pair, and whether it names a meeting; null where the kind is none of the two or is named
    /// twice. A meeting's line is a brainstorm's with the kind named, which the meeting verb reads as it always has.
    /// </summary>
    private static (string[] Args, bool Meeting)? Kind(string[] args)
    {
        var at = Array.IndexOf(args, "--kind");
        if (at < 0) return (args, false);
        if (at + 1 >= args.Length || args.Count(word => word == "--kind") > 1) return null;
        var rest = args.Where((_, index) => index != at && index != at + 1).ToArray();
        switch (args[at + 1])
        {
            case "brainstorm":
                return (rest, false);
            case "meeting":
                return rest switch
                {
                    [] => (rest, true),
                    ["list" or "show" or "send" or "stop" or "clear", ..] => (rest, true),
                    [var question, .. var more] when !question.StartsWith("--", StringComparison.Ordinal) => (["ask", question, .. more], true),
                    _ => (rest, true),
                };
            default:
                return null;
        }
    }

    private static int Ask(string question, IReadOnlyList<string> rest, TextWriter output, IServiceDoors? doors)
    {
        string? repository = null;
        string? named = null;
        IReadOnlyList<string> agents = [];
        var sources = new List<string>();
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
                case "--source":
                    sources.Add(rest[at + 1]);
                    break;
                // The caller's own id (BRAINSTORM1b, D176 point 6), which the desk judges: asked again under it, the same brainstorm.
                case "--id":
                    named = rest[at + 1];
                    break;
                default:
                    output.WriteLine(Usage);
                    return 2;
            }
        }

        return BrainstormDoor.Press(
            doors, ServiceVerb.Post, "/api/brainstorms",
            BrainstormDoor.AskBody(question, repository is { Length: > 0 } ? repository : null, agents, sources, named, meeting: false), output);
    }

    private static int Show(BrainstormFiles files, string id, TextWriter output)
    {
        if (files.Request(id) is not { } request)
        {
            output.WriteLine($"daoris-driver: there is no brainstorm `{id}` on this machine.");
            return 1;
        }

        var state = files.ReadState(id);
        if (state is null || files.PacketBytes(id) is not { } bytes || BrainstormPackets.Read(bytes) is not { } packet)
        {
            output.WriteLine($"Brainstorm `{id}` waits for Daoris's next look, which freezes its packet.");
            return 0;
        }

        if (state.State == BrainstormStates.Ended && files.DocumentText(id) is { } document)
        {
            output.Write(document);
            return 0;
        }

        output.Write(Packet(id, packet));
        output.WriteLine(state.State switch
        {
            BrainstormStates.Frozen when files.Sent(id) is null => $"Nothing has gone to any agent. Send it with `daoris brainstorm send {id}`, or clear it.",
            BrainstormStates.Frozen => "Sent: it goes at Daoris's next look.",
            BrainstormStates.Running => "Sent, and running.",
            _ => $"Ended ({state.Code ?? "no document"}): {(state.Code == BrainstormStates.NoAgent ? "no agent could answer, and the packet is kept" : "no document was kept")}.",
        });
        // Where no document was kept, the passes are what there is to read: each with its state, its codes and the walk's sentence.
        if (state.State == BrainstormStates.Ended)
        {
            foreach (var pass in files.ReadPasses(id))
            {
                output.WriteLine($"  {pass.By.Pass} on `{pass.By.Adapter}` (maker {pass.By.Maker}): {pass.State}"
                    + (pass.Codes.Count > 0 ? $" ({string.Join(", ", pass.Codes)})" : "") + (pass.Why is { } why ? $": {why}" : ""));
            }
        }

        _ = request;
        return 0;
    }

    /// <summary>The packet whole, as the person reads it before it goes: the question, each source with where it came from, and what was left out.</summary>
    internal static string Packet(string id, BrainstormPacket packet)
    {
        var text = new StringBuilder();
        text.Append("Brainstorm `").Append(id).Append("`, packet `").Append(packet.Digest).Append("`\n\n");
        foreach (var line in packet.Question.Split('\n')) text.Append("> ").Append(line.TrimEnd('\r')).Append('\n');
        Sources(text, packet.Repository, packet.Tier, packet.Sources, packet.LeftOut);
        text.Append('\n');
        return text.ToString();
    }

    /// <summary>
    /// A packet's sources as the person reads them, each with where it came from, and what was left out: a brainstorm's, and a
    /// meeting's, whose sources are the brainstorm's assembly (AGENTROOM1a, D177 point 2).
    /// </summary>
    internal static void Sources(
        StringBuilder text, string? repository, string? tier, IReadOnlyList<BrainstormSource> sources, IReadOnlyList<BrainstormLeftOut> leftOut)
    {
        text.Append('\n').Append(repository is null
            ? "No repository: the question alone.\n"
            : $"{sources.Count} sources of `{repository}`, found by {tier}.\n");
        foreach (var source in sources)
        {
            text.Append("\n").Append(source.Id).Append("  ").Append(source.Kind).Append("  ").Append(source.Path)
                .Append(source.First is { } first ? $":{first}-{source.Last ?? first}" : "")
                .Append(source.Revision is { } revision ? $" at {revision[..Math.Min(12, revision.Length)]}" : " at a revision not read")
                .Append($"  ({source.Kept} of {source.Of} characters; found by \"{source.Words}\", {source.Tier}")
                .Append(source.Reason is { } reason ? $"; in as {reason})\n" : ")\n");
            foreach (var line in source.Text.Split('\n')) text.Append("    ").Append(line.TrimEnd('\r')).Append('\n');
        }

        foreach (var left in leftOut) text.Append("\nLeft out: ").Append(left.Path).Append(" (").Append(left.Why).Append(")\n");
    }

    private static int List(BrainstormFiles files, TextWriter output)
    {
        var requests = files.Requests();
        if (requests.Count == 0) output.WriteLine("No brainstorm on this machine.");
        foreach (var request in requests)
        {
            var state = files.ReadState(request.Id)?.State ?? "asked";
            var line = request.Question.Split('\n')[0];
            output.WriteLine($"{request.Id}  {state}  {(line.Length > 80 ? line[..80] + "…" : line)}");
        }

        return 0;
    }
}

/// <summary>The limits a brainstorm request is held to before the policy names its own (D176 point 9's proposed defaults).</summary>
public static class DriverBrainstormLimits
{
    /// <summary>At most four makers: the policy's own bound (<see cref="BrainstormRules.MostAgents"/>).</summary>
    public const int MostAgents = BrainstormRules.MostAgents;

    /// <summary>At most twelve named sources: half the packet's sources, so the search keeps room beside them.</summary>
    public const int MostSources = 12;
}
