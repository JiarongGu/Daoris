using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>A driven session asks what it cannot decide itself before it ends its turn on it: the question, the answers it offers
/// with what it does on each, the one it recommends and by when, which the page draws at the conversation's end with each answer a
/// press; an owner-class mark keeps it from every supervisor. Not a go-ahead, which <see cref="GoAheadTools"/> holds on the ask.
/// </summary>
/// <param name="intake">Which session this connector speaks for, when the driver started it.</param>
/// <param name="ledger">The ledger, for the session's record the box judges.</param>
/// <param name="box">Where the question is written, under the home's sessions folder.</param>
[McpServerToolType]
public sealed class QuestionTools(IntakeScope? intake = null, SessionLedger? ledger = null, QuestionBox? box = null)
{
    [McpServerTool(Name = "question_ask")]
    [Description(
        "Ask what you cannot decide yourself (a choice the ticket leaves open, a conflict you cannot settle) before you end your "
        + "turn waiting on it with your quest still taken. It shows in this session's conversation, each answer you offer a press with "
        + "what you will do on it; a supervisor here may answer it unless you mark it `owner`. Offer answers you would act on and "
        + "recommend one where you have a reason. Not for a go-ahead on an act outside this repository: `go_ahead_ask`. One question "
        + "at a time: asking again replaces it. Then say in your last message that the work waits, commit what you have, and end your turn.")]
    public async Task<string> AskAsync(
        [Description("The decision you need, at most 1000 characters: the question, not your report.")]
        string question,
        [Description("At most six answers, each a label (the words on its press) and what you will do on it; none for an answer only in the person's words.")]
        QuestionChoiceInput[]? choices = null,
        [Description("The answer you recommend, by number from 1; give the reason as `why`.")]
        int? recommend = null,
        [Description("By when the work needs the answer, at most 200 characters.")]
        string? by = null,
        [Description("Why you recommend it, at most 300 characters. Only with `recommend`.")]
        string? why = null,
        [Description(
            "Only where it is the owner's alone, never a supervisor's to answer: `direction` (what to build, a target), `money` (spending, a "
            + "budget, a paid account beyond the work's runs), `sign-in` (a credential or account), `release` (one the owner marked "
            + "theirs), `history` (a rewrite), `data` (deleting the owner's data), `publish` (to people outside beyond what the "
            + "repository declares). Otherwise leave it out.")]
        string? owner = null,
        [Description(
            "Only where the work needs the person to sign in at a site you reach through Daoris's browser (an identity expired, a "
            + "sign-in page asks for a password): the page's address, at most 500 characters. Say in `question` which identity and "
            + "why; offer no answers. The person gets *Open the sign-in…* and *Signed in*, and a sign-in Daoris's browser sees there "
            + "answers it. Never type a password yourself.")]
        string? site = null,
        CancellationToken ct = default)
    {
        var questions = box ?? QuestionBox.FromEnvironment();
        var session = intake?.Session;
        var record = ledger is not null && session is not null ? await ledger.FindAsync(session, ct).ConfigureAwait(false) : null;
        return questions.Ask(
            record, session, question, choices?.Select(choice => (choice?.Label, choice?.Does)).ToList(), recommend, by, DateTimeOffset.UtcNow,
            why, owner, site).Message;
    }
}

/// <summary>One answer a question offers: each part nullable, so a part left out is refused by name.</summary>
public sealed record QuestionChoiceInput(
    [property: Description("The words on its press, as the person would say the answer, at most 80 characters.")]
    string? Label,
    [property: Description("What you will do on this answer, at most 300 characters.")]
    string? Does);
