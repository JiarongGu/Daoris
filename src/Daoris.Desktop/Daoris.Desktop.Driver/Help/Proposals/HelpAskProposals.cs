using System.Text.Json;
using static Daoris.Driver.HelpProposals;

namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's <c>ask</c> proposal (HELP1c, D89): something to start — its sentence the words, its workspace
/// where it is asked — which the ordinary loop then answers as it answers any ask (D65). The helper never runs
/// a session's work, and never publishes a quest itself. Applied through the local host's ask door.
/// </summary>
/// <remarks>
/// <para><b>Since ENTRY1c (D161's ENTRY1c note)</b> it carries the review choice the person said for the ask's work and their
/// words with it, as the composer sends them and <c>daoris-driver ask --review rule|on|&lt;environment&gt;|off
/// [--review-words "…"]</c> takes them (REVIEWENV1j): judged here with <see cref="AskReviewCommand.Compose"/>, the
/// composer's own judgement, and a named environment the ask's workspace does not declare refused by the ask door at Apply,
/// as the terminal refuses it. An ask's kind and workflow are the person's (D157 point 10), so no proposal carries them.</para>
/// </remarks>
internal sealed class HelpAskProposals : IHelpProposalKind
{
    /// <summary>The words with a review choice, where no spelling holds in every shell.</summary>
    private const string WordsPlaceholder = "<words>";

    public string Kind => "ask";

    public string Tool => "ask_propose";

    public IReadOnlyList<string> Doors { get; } = ["ask"];

    public HelpProposal Read(HelpProposal proposal, JsonElement file) => proposal with
    {
        Review = Text(file, "review"),
        ReviewWords = Text(file, "reviewWords"),
    };

    public HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts)
    {
        var workspace = proposal.Workspace?.Trim() ?? "";
        var sentence = proposal.Sentence?.Trim() ?? "";
        var composed = AskReviewCommand.Compose(proposal.Review, proposal.ReviewWords, out var unfit);
        // ACCTQUOTE1b: the workspace spelled for any shell, as every command the driver names spells it.
        var terminal = $"daoris-driver ask --workspace {ShellWord.Of(workspace, ShellWord.Workspace)} "
            + ReviewFlags(proposal.Review, proposal.ReviewWords)
            + $"\"{sentence.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
        var describe = $"Ask at workspace `{workspace}`: “{sentence}”{Said(composed)}";
        if (sentence.Length == 0) return new HelpPlan("an ask needs its words.", describe, terminal, null);
        if (!facts.Workspaces.Contains(workspace, StringComparer.OrdinalIgnoreCase))
        {
            return new HelpPlan($"there is no workspace `{workspace}` on this machine — one of {Names(facts.Workspaces)}.", describe, terminal, null);
        }

        // ENTRY1c: a choice the composer would not send is refused in the composer's words, before anything is asked.
        if (composed is null) return new HelpPlan(unfit, describe, terminal, null);

        return new HelpPlan(null, describe, terminal, null);
    }

    public async Task<HelpApplied> ApplyAsync(HelpApplying applying, CancellationToken ct)
    {
        var proposal = applying.Proposal;
        // A refused plan never reaches Apply (HelpProposals.ApplyAsync), so the choice is one Compose took.
        var review = AskReviewCommand.Compose(proposal.Review, proposal.ReviewWords, out _)!;
        var answer = await applying.Doors.AskAsync(proposal.Workspace!.Trim(), proposal.Sentence!.Trim(), review.Choice, review.Words, ct)
            .ConfigureAwait(false);
        return applying.Settled(answer.Ok, $"{(answer.Ok ? "Applied" : "Not applied")}: `#{applying.Id}` (`{applying.Plan.Terminal}`) — {answer.Message}", answer.Message);
    }

    /// <summary>The terminal's <c>--review</c> and <c>--review-words</c>, as the proposal holds them: none for <c>rule</c> or nothing said.</summary>
    private static string ReviewFlags(string? review, string? words)
    {
        var flags = review is null or AskReviewCommand.Rule ? "" : $"--review {ShellWord.Of(review, ShellWord.Name)} ";
        return string.IsNullOrWhiteSpace(words) ? flags : $"{flags}--review-words {ShellWord.Of(words.Trim(), WordsPlaceholder)} ";
    }

    /// <summary>The choice in the composer's words (its <i>Review before it lands</i> options), and the person's words with it.</summary>
    private static string Said(AskReviewComposed? composed) => composed?.Choice switch
    {
        null => "",
        var choice => (choice switch
        {
            "on" => " — review it in the default environment before it lands",
            "off" => " — no review before it lands",
            _ => $" — review it in `{choice}` before it lands",
        }) + (composed.Words is { } words ? $": “{words}”" : ""),
    };
}

public sealed partial record HelpProposal
{
    /// <summary>
    /// ENTRY1c: the review choice the person said for an ask's work, as the composer sends it: <c>on</c>, an environment's name
    /// or <c>off</c>; null where none was said, and each repository's rule decides.
    /// </summary>
    public string? Review { get; init; }

    /// <summary>ENTRY1c: the person's words with an ask's review choice, sent only with one.</summary>
    public string? ReviewWords { get; init; }
}

public partial interface IHelpDoors
{
    /// <summary>
    /// An ask, through the local host's ask door, with the review choice and words the composer sends (ENTRY1c): null for none.
    /// A named environment the ask's workspace does not declare is the door's refusal, as the terminal's.
    /// </summary>
    Task<AskAnswer> AskAsync(string workspace, string sentence, string? review, string? reviewWords, CancellationToken ct);
}
