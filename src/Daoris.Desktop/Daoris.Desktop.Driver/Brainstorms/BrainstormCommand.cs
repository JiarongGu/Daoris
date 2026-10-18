using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The brainstorm at the terminal (BRAINSTORM1a, D176 points 1, 2 and 8): <c>daoris-driver brainstorm "&lt;question&gt;"</c> writes a
/// request the driver's look takes, as a helper's ask does, so every pass runs in whichever process holds the loop; <c>show</c>
/// prints the packet whole, then the document; <c>send</c> is the person's press that lets the packet they saw go; <c>clear</c>
/// removes the records and the packet; <c>list</c> names each. Files only: the verb spawns nothing and asks no service.
/// </summary>
public static partial class BrainstormCommand
{
    public const string Usage =
        "usage: daoris-driver brainstorm \"<question>\" [--repository <name>] [--agents <a,b>] [--id <id>]\n"
        + "       daoris-driver brainstorm show|send|stop|clear <id>\n"
        + "       daoris-driver brainstorm list";

    /// <summary>Exit codes as every verb's: 0 done, 1 refused, 2 a line this verb does not read.</summary>
    public static int Run(string[] args, string home, TextWriter output, DateTimeOffset now)
    {
        var files = new BrainstormFiles(home);
        switch (args)
        {
            case ["list"]:
                return List(files, output);
            case ["show", var id]:
                return Show(files, id, output);
            case ["send", var id]:
                return Send(files, id, output, now);
            case ["stop", var id]:
                return Stop(files, id, output, now);
            case ["clear", var id]:
                return Clear(files, id, output);
            case [var question, .. var rest] when !question.StartsWith("--", StringComparison.Ordinal) && question is not ("show" or "send" or "stop" or "clear" or "list"):
                return Ask(files, question, rest, output, now);
            default:
                output.WriteLine(Usage);
                return 2;
        }
    }

    private static int Ask(BrainstormFiles files, string question, IReadOnlyList<string> rest, TextWriter output, DateTimeOffset now)
    {
        string? repository = null;
        string? named = null;
        IReadOnlyList<string> agents = [];
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
                // The caller's own id (BRAINSTORM1b, D176 point 6): asked again under it, it is the same brainstorm, never a second spend.
                case "--id" when IdShape().IsMatch(rest[at + 1]):
                    named = rest[at + 1];
                    break;
                default:
                    output.WriteLine(Usage);
                    return 2;
            }
        }

        question = question.Trim();
        if (question.Length == 0 || question.Length > BrainstormBounds.First.Question)
        {
            output.WriteLine($"daoris-driver: a brainstorm's question is 1 to {BrainstormBounds.First.Question} characters; this one is {question.Length}.");
            return 1;
        }

        if (agents.Count > DriverBrainstormLimits.MostAgents)
        {
            output.WriteLine($"daoris-driver: a brainstorm runs at most {DriverBrainstormLimits.MostAgents} agents; {agents.Count} were named.");
            return 1;
        }

        repository = repository is { Length: > 0 } ? repository : null;
        if (named is not null && files.Request(named) is { } asked)
        {
            if (asked.Question != question || !string.Equals(asked.Repository, repository, StringComparison.Ordinal) || !asked.Agents.SequenceEqual(agents, StringComparer.Ordinal))
            {
                output.WriteLine($"daoris-driver: brainstorm `{named}` was asked with other words; an id names one brainstorm.");
                return 1;
            }

            output.WriteLine($"Brainstorm `{named}` is already asked: the same brainstorm, never a second spend. Read it with `daoris-driver brainstorm show {named}`.");
            return 0;
        }

        var id = files.WriteRequest(question, repository, agents, now, named);
        output.WriteLine($"Brainstorm `{id}` asked. Daoris freezes its packet at its next look, from "
            + (repository is { Length: > 0 } ? $"its knowledge of `{repository}`" : "the question alone")
            + $"; with no Daoris running, it waits. Read it with `daoris-driver brainstorm show {id}` before you send it.");
        return 0;
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
            BrainstormStates.Frozen when files.Sent(id) is null => $"Nothing has gone to any agent. Send it with `daoris-driver brainstorm send {id}`, or clear it.",
            BrainstormStates.Frozen => "Sent: it goes at Daoris's next look.",
            BrainstormStates.Running => "Sent, and running.",
            _ => $"Ended ({state.Code ?? "no document"}): {(state.Code == BrainstormStates.NoAgent ? "no agent could answer, and the packet is kept" : "no document was kept")}.",
        });
        _ = request;
        return 0;
    }

    /// <summary>The packet whole, as the person reads it before it goes: the question, each source with where it came from, and what was left out.</summary>
    internal static string Packet(string id, BrainstormPacket packet)
    {
        var text = new StringBuilder();
        text.Append("Brainstorm `").Append(id).Append("`, packet `").Append(packet.Digest).Append("`\n\n");
        foreach (var line in packet.Question.Split('\n')) text.Append("> ").Append(line.TrimEnd('\r')).Append('\n');
        text.Append('\n').Append(packet.Repository is null
            ? "No repository: the question alone.\n"
            : $"{packet.Sources.Count} sources of `{packet.Repository}`, found by {packet.Tier}.\n");
        foreach (var source in packet.Sources)
        {
            text.Append("\n").Append(source.Id).Append("  ").Append(source.Kind).Append("  ").Append(source.Path)
                .Append(source.First is { } first ? $":{first}-{source.Last ?? first}" : "")
                .Append(source.Revision is { } revision ? $" at {revision[..Math.Min(12, revision.Length)]}" : " at a revision not read")
                .Append($"  ({source.Kept} of {source.Of} characters; found by \"{source.Words}\", {source.Tier})\n");
            foreach (var line in source.Text.Split('\n')) text.Append("    ").Append(line.TrimEnd('\r')).Append('\n');
        }

        foreach (var left in packet.LeftOut) text.Append("\nLeft out: ").Append(left.Path).Append(" (").Append(left.Why).Append(")\n");
        text.Append('\n');
        return text.ToString();
    }

    /// <summary>A caller's own id: a plain name of letters, digits and dashes, at most 32, so it is a folder's name and nothing more.</summary>
    [System.Text.RegularExpressions.GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9-]{0,31}$")]
    private static partial System.Text.RegularExpressions.Regex IdShape();

    /// <summary>
    /// The person's stop (BRAINSTORM1b, D176 point 6): a file the driver looks for while the brainstorm runs, and at its next look
    /// before it does; every pass ends, what answered is kept, and nothing runs again by itself.
    /// </summary>
    private static int Stop(BrainstormFiles files, string id, TextWriter output, DateTimeOffset now)
    {
        if (files.Request(id) is null)
        {
            output.WriteLine($"daoris-driver: there is no brainstorm `{id}` on this machine.");
            return 1;
        }

        if (files.ReadState(id) is { State: BrainstormStates.Ended } ended)
        {
            output.WriteLine($"Brainstorm `{id}` already ended ({ended.Code ?? "with its document"}).");
            return 0;
        }

        files.WriteStop(id, now);
        output.WriteLine($"Brainstorm `{id}`: stop asked. Every pass ends where Daoris runs it, what answered is kept, and nothing runs again by itself.");
        return 0;
    }

    private static int Send(BrainstormFiles files, string id, TextWriter output, DateTimeOffset now)
    {
        var state = files.ReadState(id);
        if (state is not { State: BrainstormStates.Frozen, Packet: { } digest })
        {
            output.WriteLine(files.Request(id) is null
                ? $"daoris-driver: there is no brainstorm `{id}` on this machine."
                : state is null
                    ? $"daoris-driver: brainstorm `{id}` has no packet yet: Daoris freezes it at its next look, and you read it before it goes."
                    : $"Brainstorm `{id}` was already sent ({state.State}).");
            return state is null || files.Request(id) is null ? 1 : 0;
        }

        if (files.Sent(id) == digest)
        {
            output.WriteLine($"Brainstorm `{id}` is already sent: the same brainstorm, never a second spend.");
            return 0;
        }

        files.WriteSent(id, digest, now);
        output.WriteLine($"Brainstorm `{id}` sent, packet `{digest}`: its passes start at Daoris's next look. Read the document with `daoris-driver brainstorm show {id}`.");
        return 0;
    }

    private static int Clear(BrainstormFiles files, string id, TextWriter output)
    {
        if (files.Request(id) is null && !Directory.Exists(files.Folder(id)))
        {
            output.WriteLine($"daoris-driver: there is no brainstorm `{id}` on this machine.");
            return 1;
        }

        if (files.ReadState(id) is { State: BrainstormStates.Running })
        {
            output.WriteLine($"daoris-driver: brainstorm `{id}` is running; it is cleared once it ends.");
            return 1;
        }

        if (files.Clear(id) is { } left)
        {
            output.WriteLine($"daoris-driver: brainstorm `{id}` could not be cleared whole: {left}");
            return 1;
        }

        output.WriteLine($"Brainstorm `{id}` cleared: its records and its packet are gone from this machine.");
        return 0;
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
}
