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
        "Ask the role that can what you cannot decide or do yourself. With no `role` it asks the line above you here: offer answers "
        + "you would act on, each a press, and recommend one. For an act the person allows, name its `act` and `on`. The answer is your "
        + "next turn; asking again replaces it. Then say the work waits, commit, and end your turn.")]
    public async Task<string> AskAsync(
        [Description("What you need, at most 1000 characters; for an act, why and exactly what you would do.")]
        string question,
        [Description("At most six answers: each a label and what you will do on it.")]
        QuestionChoiceInput[]? choices = null,
        [Description("The answer you recommend, from 1; its reason as `why`.")]
        int? recommend = null,
        [Description("By when.")]
        string? by = null,
        [Description("Why you recommend it.")]
        string? why = null,
        [Description("Only where it is the owner's alone, never a supervisor's: `direction`, `money`, `sign-in`, `release`, `history`, `data` or `publish`.")]
        string? owner = null,
        [Description("For a sign-in in Daoris's browser: the page's address; say which identity, offer no answers, never type a password.")]
        string? site = null,
        [Description("The role that can, if not the line above: `credentials-keeper` (a sign-in), `helper`, `approver`, `owner` or a role's id; another repository's goes as a quest.")]
        string? role = null,
        [Description("For an act the person allows: its kind (write, release, push, sign-in, run) and what it touches, e.g. `write the dashboard configuration`.")]
        string? act = null,
        [Description("For an act: where it lands, e.g. production.")]
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
