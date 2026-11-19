using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>A driven session asks another role through the one door (<see cref="RoleRequests"/>): what it cannot decide or do itself before
/// it ends its turn on it, naming the role that can and, for an act, the act. Its answer is the asker's next turn.</summary>
/// <param name="intake">Which session this connector speaks for, when the driver started it.</param>
/// <param name="ledger">The ledger, for the session's record the box judges.</param>
/// <param name="box">Where the question is written, under the home's sessions folder.</param>
/// <param name="helpers">Where the helper role's request is written, under the home.</param>
[McpServerToolType]
public sealed class QuestionTools(IntakeScope? intake = null, SessionLedger? ledger = null, QuestionBox? box = null, HelperBox? helpers = null)
{
    [McpServerTool(Name = "question_ask")]
    [Description(
        "Ask the role that can what you cannot decide or do yourself (a choice the ticket leaves open, a sign-in, an approval, an act "
        + "outside this repository) before you end your turn waiting on it with your quest still taken. With no `role` it asks the line "
        + "above you: it shows in this session's conversation, each answer you offer a press with what you will do on it; a supervisor here "
        + "may answer it unless you mark it `owner`. Offer answers you would act on and recommend one where you have a reason. Name `role` "
        + "where another can: `credentials-keeper` for a sign-in (with its `site`), `helper` for another agent's reading of your work, "
        + "`approver` or `owner` for an act; and for an act the person allows, the act (`act`, `on`). The answer is your next turn. One "
        + "question at a time: asking again replaces it. Then say in your last message that the work waits, commit what you have, and end your turn.")]
    public async Task<string> AskAsync(
        [Description("What you need, at most 1000 characters: the question, not your report; for an act, why the work needs it and exactly what you would do.")]
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
        [Description(
            "The role that can, where it is not the line above you: `credentials-keeper` (a sign-in), `helper` (another agent reads your "
            + "work once and answers), `approver`, `owner`, or another role's id. A role of another repository travels as a quest.")]
        string? role = null,
        [Description(
            "For an act the person allows, its kind then what it touches, in a few words: `write the dashboard configuration`, "
            + "`release stage 403 of the reporting app`; the kind is write, release, push, sign-in or run, and the same words join an act already asked.")]
        string? act = null,
        [Description("For an act: where it lands, the environment or system, such as production.")]
        string? on = null,
        CancellationToken ct = default)
    {
        var (kind, touches) = ActOf(act);
        var request = new RoleRequest(intake?.Session, question)
        {
            Role = role, Kind = kind, Touches = touches, On = on, Choices = choices?.Select(choice => (choice?.Label, choice?.Does)).ToList(),
            Recommend = recommend, By = by, Reason = why, Owner = owner, Site = site,
        };
        return await Requests(intake, ledger, box, helpers).AskAsync(request, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
    }

    /// <summary>The one door, on the boxes this host keeps.</summary>
    internal static RoleRequests Requests(IntakeScope? intake, SessionLedger? ledger, QuestionBox? box, HelperBox? helpers) =>
        new(box ?? QuestionBox.FromEnvironment(), helpers ?? HelperBox.FromEnvironment(), ledger);

    /// <summary>An act as the door takes it: its first word the kind, the rest what it touches.</summary>
    private static (string? Kind, string? Touches) ActOf(string? act)
    {
        if (act?.Trim() is not { Length: > 0 } words) return (null, null);
        var space = words.IndexOf(' ', StringComparison.Ordinal);
        return space < 0 ? (words, null) : (words[..space], words[(space + 1)..].Trim());
    }
}

/// <summary>One answer a question offers: each part nullable, so a part left out is refused by name.</summary>
public sealed record QuestionChoiceInput(
    [property: Description("The words on its press, as the person would say the answer, at most 80 characters.")]
    string? Label,
    [property: Description("What you will do on this answer, at most 300 characters.")]
    string? Does);
