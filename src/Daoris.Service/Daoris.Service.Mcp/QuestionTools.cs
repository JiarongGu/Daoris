using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// A driven session records what only the person can decide before it ends its turn on it (PARKVIEW1, the park's ask design §2):
/// the question, the answers it offers with what it does on each, the one it recommends and by when, which the page draws at the
/// conversation's end with each answer a press. Not a go-ahead, which <see cref="GoAheadTools"/> holds on the ask.
/// </summary>
/// <param name="intake">Which session this connector speaks for, when the driver started it.</param>
/// <param name="ledger">The ledger, for the session's record the box judges.</param>
/// <param name="box">Where the question is written, under the home's sessions folder.</param>
[McpServerToolType]
public sealed class QuestionTools(IntakeScope? intake = null, SessionLedger? ledger = null, QuestionBox? box = null)
{
    [McpServerTool(Name = "question_ask")]
    [Description(
        "Ask the person what only they can decide, before you end your turn waiting on them with your quest still taken: a choice "
        + "the ticket leaves open, a preference nothing records, a value nobody wrote down. The person sees the question at the end of "
        + "this session's conversation, each answer you offer a press, with what you will do on it, so offer the answers you would "
        + "act on and recommend one where you have a reason. Not for a go-ahead on an act outside this repository, which "
        + "`go_ahead_ask` holds once for every session. One question at a time: asking again replaces it. Then say in your last "
        + "message that the work waits on it, commit what you have, and end your turn.")]
    public async Task<string> AskAsync(
        [Description("The decision you need, in a sentence or two, at most 1000 characters: the question, not your report.")]
        string question,
        [Description("The answers you offer, at most six, each its label (the words on its press) and what you will do on it; none for an answer only in the person's words.")]
        QuestionChoiceInput[]? choices = null,
        [Description("The answer you recommend, by its number from 1, where you have a reason; give the reason as `why`.")]
        int? recommend = null,
        [Description("By when the work needs the answer, where you know it (a release window, a meeting): at most 200 characters.")]
        string? by = null,
        [Description("Why you recommend that answer, in a sentence, at most 300 characters: shown under it. Only with `recommend`.")]
        string? why = null,
        [Description(
            "Set only where you know the question is the owner's alone, never the supervisor's to answer for them: `sign-in` (a sign-in, "
            + "a credential or an account), `access` (widening what an agent may do: a permission, a rule, a trust, writing across, full "
            + "access), `spending` (money, or an account's use beyond the work's own runs), `delete` (deleting or losing the owner's data, "
            + "or rewriting history), `repository-asks` (a repository document says to ask the person), `preference` (the owner's own "
            + "taste where nothing records it). Leave it out otherwise: an unmarked question may still be left to the owner.")]
        string? owner = null,
        CancellationToken ct = default)
    {
        var questions = box ?? QuestionBox.FromEnvironment();
        var session = intake?.Session;
        var record = ledger is not null && session is not null ? await ledger.FindAsync(session, ct).ConfigureAwait(false) : null;
        return questions.Ask(
            record, session, question, choices?.Select(choice => (choice?.Label, choice?.Does)).ToList(), recommend, by, DateTimeOffset.UtcNow,
            why, owner).Message;
    }
}

/// <summary>One answer a question offers (PARKVIEW1): each part nullable, so a part left out is refused by name.</summary>
public sealed record QuestionChoiceInput(
    [property: Description("The words on its press, as the person would say the answer, at most 80 characters.")]
    string? Label,
    [property: Description("What you will do on this answer, at most 300 characters.")]
    string? Does);
