namespace Daoris.Driver;

/// <summary>What <c>daoris-driver quest reopen</c> or <c>quest go-on</c> was asked: the quest, which, and the person's words.</summary>
public sealed record QuestReopenAsk(string Quest, bool GoOn, string? Note);

/// <summary>
/// The terminal's doors to the person's reopen of a sealed submission and their go-on with the step it held (FABRIC-CLOSE1c3, §E5;
/// D173's CLOSE1c note; D50): <c>daoris-driver quest reopen &lt;id&gt; [--note "…"]</c> and <c>quest go-on &lt;id&gt;</c>, through the
/// service's own person doors, printing the service's sentence. The quest page's <i>Reopen…</i> and <i>Go on with it</i> are the
/// other doors; abandoning a held step is <c>quest abandon</c>, its decline.
/// </summary>
/// <remarks>
/// <para><b>The person's, never an agent's</b> (D156): both are person-only doors, which a host given its key refuses a call without
/// it, and which a terminal is asked to confirm in the window once that confirmation is built (RPC1c).</para>
///
/// <para><b>The submission reopened is the quest's current one</b>, read just before the reopen is sent: the door refuses it once
/// another is current, so a <c>ready</c> made between the two is never reopened in its place.</para>
///
/// <para><b>Exit codes</b>: 0 moved, 1 refused (no submission to reopen, no such quest, a host older than the door), 2 the usage.</para>
/// </remarks>
public static class QuestReopenCommand
{
    public const string Usage =
        "usage: daoris-driver quest reopen <id> [--note \"…\"]  ·  quest go-on <id>\n"
        + "       reopen what a quest submitted, once the step it published stands on it: its agent makes it ready again, and the\n"
        + "       step waits. go-on carries a step its source's reopen held on with the new work; `quest abandon` abandons it.";

    /// <summary>Whether a <c>quest</c> line asks for a reopen or a go-on.</summary>
    public static bool Asks(IReadOnlyList<string> args) => args is ["reopen" or "go-on", ..];

    /// <summary>The line read, or null with why not.</summary>
    public static QuestReopenAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        switch (args)
        {
            case ["reopen", var id] when Id(id):
                return new QuestReopenAsk(id.TrimStart('#'), false, null);
            case ["reopen", var id, "--note", var note] when Id(id) && note.Trim().Length > 0:
                return new QuestReopenAsk(id.TrimStart('#'), false, note.Trim());
            case ["go-on", var id] when Id(id):
                return new QuestReopenAsk(id.TrimStart('#'), true, null);
            default:
                problem = args is ["go-on", ..]
                    ? "`quest go-on` takes one quest's id: the step its source's reopen held."
                    : "`quest reopen` takes one quest's id, and `--note` your words its reopen keeps.";
                return null;
        }
    }

    /// <summary>Post the reopen or the go-on and say the service's sentence: 0 moved, 1 refused.</summary>
    public static async Task<int> RunAsync(QuestReopenAsk ask, ServiceClient service, TextWriter output, CancellationToken ct = default)
    {
        if (ask.GoOn) return Say(await service.ContinueQuestAsync(ask.Quest, ct).ConfigureAwait(false), output);

        // The submission the quest answers now: the door refuses another, so a `ready` made since is never reopened in its place.
        var quest = await service.FindQuestAsync(ask.Quest, ct).ConfigureAwait(false);
        if (quest?.Submission is not { } submission)
        {
            output.WriteLine(quest is null
                ? $"daoris-driver: no quest `#{ask.Quest}` here. Ids come from Quests or `daoris-driver sessions`."
                : $"daoris-driver: quest `#{ask.Quest}` is {quest.Status} with no submission to reopen: only what an agent submitted in a "
                    + "workflow that closes its quests itself is reopened. Nothing moved.");
            return 1;
        }

        return Say(await service.ReopenQuestAsync(
            ask.Quest, new QuestSubmissionRef(submission.Machine, submission.Sequence), ask.Note, ct).ConfigureAwait(false), output);
    }

    /// <summary>The service's sentence, said; a refusal is an answer, not a tool error.</summary>
    private static int Say((bool Ok, string Message) answer, TextWriter output)
    {
        output.WriteLine($"daoris-driver: {answer.Message}");
        return answer.Ok ? 0 : 1;
    }

    /// <summary>A quest's id, never a flag.</summary>
    private static bool Id(string word) => word.TrimStart('#').Length > 0 && !word.StartsWith('-');
}
