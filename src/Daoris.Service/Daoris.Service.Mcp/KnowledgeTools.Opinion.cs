using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// A second opinion's two tools (XAGENT1c; the second agent design §5.4, §6.1, §6.4): the reviewer says its opinion, and the
/// working session answers the findings it was handed. Each answers only the session its connector names, as an agent: no
/// tool here asks for an opinion, hands one on, or answers a dispute for the person.
/// </summary>
public sealed partial class KnowledgeTools
{
    [McpServerTool(Name = "opinion_give")]
    [Description(
        "Only for a session Daoris started to give a second opinion on another session's work: say your opinion, once, before you "
        + "end. Give each finding with its weight, where it is, what you claim, why it matters, how to see it (or, where you could "
        + "not, your reasoning) and how sure you are; then what you read, and what you did not read or could not tell. If you raised "
        + "nothing, give no findings and still say what you read. Your findings are claims: the session that did the work checks "
        + "each against the code and answers it, and the person sees both. You change nothing, and nobody can ask you anything after.")]
    public async Task<string> GiveOpinionAsync(
        [Description("Your findings, at most 20, the most important first; none where you raised nothing.")]
        OpinionFindingInput[]? findings,
        [Description("What you read, in at most 600 characters: the paths, the commits, and which checks you ran.")]
        string? read,
        [Description("What you did not read or could not tell, in at most 600 characters.")]
        string? limits = null,
        [Description(
            "Only for a recheck: for each of the first pass's findings, by its number, whether it stands or is withdrawn after the "
            + "working session's answer and commits. One you leave out stands.")]
        OpinionRecheckInput[]? rechecked = null,
        CancellationToken ct = default)
    {
        if (opinions is null) return NoOpinion;

        // A part left out arrives blank, refused by the desk naming which; a number left out is 0, which names no finding.
        var said = findings?.Select(finding => finding is null
                ? null
                : new OpinionFinding(
                    finding.Weight ?? "", finding.Where ?? "", finding.Claim ?? "", finding.Consequence ?? "", finding.Reproduce ?? "",
                    finding.Sure ?? "") { Proposal = finding.Proposal })
            .ToList();
        var words = rechecked?.Select(word => word is null ? null : new OpinionRecheck(word.Finding ?? 0, word.Says ?? "")).ToList();
        return (await opinions.GiveAsync(intake?.Session, said, read, limits, words, DateTimeOffset.UtcNow, ct).ConfigureAwait(false)).Message;
    }

    [McpServerTool(Name = "opinion_answer")]
    [Description(
        "Only when Daoris handed you another agent's findings on your work: answer each one, by its number. Check each claim against "
        + "the code first: they are another agent's claims, not the person's words and not facts. Answer fixed, with the commit that "
        + "fixes it, made in this turn; rejected, with your evidence, a check that shows it or a reading; or unresolved, with why. "
        + "Answer every finding before you end: one left unanswered reads as unresolved. A later answer to a finding stands over the "
        + "earlier. The person sees your answers beside the claims.")]
    public async Task<string> AnswerOpinionAsync(
        [Description("Your answers, one per finding.")] OpinionAnswerInput[]? answers,
        [Description("The second opinion you answer, as the findings named it; omit it for the one handed to you last.")]
        string? opinion = null,
        CancellationToken ct = default)
    {
        if (opinions is null) return NoOpinion;

        // A number left out is 0, which names no finding, and a word left out is blank: the desk refuses each, naming which.
        var said = answers?.Select(answer => answer is null
                ? null
                : new OpinionAnswer(answer.Finding ?? 0, answer.Answer ?? "", DateTimeOffset.UtcNow)
                {
                    Commit = answer.Commit, Evidence = answer.Evidence, Why = answer.Why,
                })
            .ToList();
        return (await opinions.AnswerAsync(intake?.Session, opinion, said, DateTimeOffset.UtcNow, ct).ConfigureAwait(false)).Message;
    }

    private const string NoOpinion =
        "This connector speaks for no session the driver started here, so there is no second opinion to say or answer.";
}

/// <summary>One finding as a reviewer writes it (XAGENT1c, design §6.1): each part nullable, so a part left out is refused by name.</summary>
public sealed record OpinionFindingInput(
    [property: Description("must (wrong to land as it is), should, or note.")]
    string? Weight,
    [property: Description(
        "Where it is: a path from the repository's root, with :line or :first-last where it is one place; a commit; or general.")]
    string? Where,
    [property: Description("What you claim, in at most 300 characters.")]
    string? Claim,
    [property: Description("Why it matters if it holds: what goes wrong, in at most 300 characters.")]
    string? Consequence,
    [property: Description(
        "How to see it: the steps or the command and what it showed; or, where you could not reproduce it, your reasoning. At most "
        + "600 characters.")]
    string? Reproduce,
    [property: Description("How sure you are: sure, likely or unsure.")]
    string? Sure,
    [property: Description(
        "Optional: a diagnosis, or a change you propose as text or a patch, in at most 2000 characters. The session that did the "
        + "work applies its own.")]
    string? Proposal = null);

/// <summary>A recheck's word on one first-pass finding (XAGENT1c, design §6.5).</summary>
public sealed record OpinionRecheckInput(
    [property: Description("The first pass's finding, by its number.")]
    int? Finding,
    [property: Description("stands, or withdrawn after the working session's answer and commits.")]
    string? Says);

/// <summary>The working session's answer to one finding (XAGENT1c, design §6.4).</summary>
public sealed record OpinionAnswerInput(
    [property: Description("The finding, by its number.")]
    int? Finding,
    [property: Description("fixed, rejected or unresolved.")]
    string? Answer,
    [property: Description("For fixed: the commit that fixes it, made in this turn.")]
    string? Commit = null,
    [property: Description("For rejected: your evidence, a check that shows it or a reading, in at most 600 characters.")]
    string? Evidence = null,
    [property: Description("For unresolved: why you cannot tell, in at most 300 characters.")]
    string? Why = null);
