namespace Daoris.Driver;

/// <summary>What <c>daoris quest done</c> asks: the quest, and the person's words, or null for none.</summary>
public sealed record QuestDoneAsk(string Quest, string? Note);

/// <summary>
/// The terminal's door to the person's done: <c>daoris quest done &lt;id&gt; [--note
/// "…"]</c> marks an open or taken quest done as the person's, through the service's own door for it, and prints the service's
/// sentence. The quest page's <i>Mark done…</i> is the other door, and a finish at a checkpoint names this one right after it
/// where the session's quest is still taken.
/// </summary>
/// <remarks>
/// <para><b>The person's done, never the agent's</b>: it answers none of the quest's requirements one by one, and the quest's
/// note says the done is theirs, with their words after it. That sentence is the service's, so every door writes the same.</para>
///
/// <para><b>Exit codes</b>: 0 closed, 1 refused (a closed quest, no such quest, a host older than the door), 2 the usage.</para>
/// </remarks>
[CommandGroup("quest", "pause", "resume", "abandon", "delete", "accept", "done", "reopen", "go-on", "check", "review", "workflow", "clear", "retry")]
public static class QuestDoneCommand
{
    public const string Usage =
        "usage: daoris quest done <id> [--note \"…\"]\n"
        + "       mark a quest done as yours: its record says you marked it done, with your words, and answers none of its\n"
        + "       requirements one by one. For a quest whose session here ended and left it taken, or any open or taken one.";

    /// <summary>Whether a <c>quest</c> line asks for the person's done.</summary>
    public static bool Asks(IReadOnlyList<string> args) => args is ["done", ..];

    /// <summary>The line read, or null with why not.</summary>
    public static QuestDoneAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        switch (args)
        {
            case ["done", var id] when Id(id):
                return new QuestDoneAsk(id.TrimStart('#'), null);
            case ["done", var id, "--note", var note] when Id(id) && note.Trim().Length > 0:
                return new QuestDoneAsk(id.TrimStart('#'), note.Trim());
            default:
                problem = "`quest done` takes one quest's id, and `--note` your words its record keeps.";
                return null;
        }
    }

    /// <summary>Post the person's done and say the service's sentence: 0 closed, 1 refused.</summary>
    public static async Task<int> RunAsync(QuestDoneAsk ask, ServiceClient service, TextWriter output, CancellationToken ct = default)
    {
        var (ok, message) = await service.PersonDoneAsync(ask.Quest, ask.Note, ct).ConfigureAwait(false);
        output.WriteLine($"daoris: {message}");
        // A refusal (a closed quest, no such quest) is an answer, not a tool error.
        return ok ? 0 : 1;
    }

    /// <summary>
    /// The verb: the person's press, sent to the running Daoris and confirmed in its window, its lines the route's; else
    /// <see cref="RunAsync"/>, the direct post.
    /// </summary>
    public static Task<int> PressAsync(QuestDoneAsk ask, ServiceClient service, TextWriter output, InstanceWorld? instance, CancellationToken ct = default) =>
        InstanceDoor.ThroughAsync(instance, PersonPresses.QuestDone, InstanceDoor.Said(("quest", ask.Quest), ("note", ask.Note)),
            () => RunAsync(ask, service, output, ct), output, ct);

    /// <summary>A quest's id, never a flag.</summary>
    private static bool Id(string word) => word.TrimStart('#').Length > 0 && !word.StartsWith('-');
}
