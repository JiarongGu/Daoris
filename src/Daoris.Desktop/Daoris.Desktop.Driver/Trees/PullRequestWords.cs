using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// Why a row's pull request was not asked about at an occasion (PLUGHOOK1c, the plugin hooks design §2.1): no plugin to ask, the
/// plugin not ready, or the occasion's bound reached first. Nothing goes on any of them, and none is kept: the next look asks again.
/// </summary>
/// <param name="Code"><see cref="PullRequestCodes.NoPlugin"/>, <see cref="PullRequestCodes.Unready"/> or <see cref="PullRequestCodes.NotAsked"/>.</param>
/// <param name="Sentence">Daoris's sentence for it, naming the plugin where there is one.</param>
public sealed record PullRequestNotAsked(string Code, string Sentence);

/// <summary>
/// What <i>Ask again</i> came to (PLUGHOOK1c, design §2.1 occasion 4): the landing as kept after it, whether its plugin answered
/// this time, why nothing new is known where nothing is, and what the kept answer proves here.
/// </summary>
/// <param name="Entry">The landing entry, standing or a trace, as the record keeps it after the ask.</param>
public sealed record PullRequestAskedAgain(LandedBranch Entry)
{
    /// <summary>What the kept answer proves on this machine (design §2.3); null where nothing is kept.</summary>
    public PullRequestVerdict? Verdict { get; init; }

    /// <summary>Whether the plugin answered this time.</summary>
    public bool Answered { get; init; }

    /// <summary>Whether the kept answer is <c>completed</c>, which is final, so nothing was asked.</summary>
    public bool Final { get; init; }

    /// <summary>
    /// Why nothing new is known: <see cref="PullRequestCodes.NoPlugin"/>, <see cref="PullRequestCodes.Unready"/>,
    /// <see cref="PullRequestCodes.NotAsked"/> (nothing due to ask), or a failure's kind.
    /// </summary>
    public string? Code { get; init; }

    /// <summary>Daoris's sentence for <see cref="Code"/>.</summary>
    public string? Why { get; init; }
}

/// <summary>
/// The kept answer about a landed branch's pull request in the driver's words (PLUGHOOK1c, D148 point 6, design §2.5): one way for
/// every terminal door that reads it — the clean-up's rows and bringing up to date's, <c>trees land --plan</c>, <c>trees state</c>,
/// <c>git branches</c>, <c>trace</c> and Ask Daoris's room — so they never say it two ways. The page says the same in its own
/// catalogue (PLUGHOOK1d).
/// </summary>
public static class PullRequestWords
{
    /// <summary>Why nothing was asked where no plugin pushed the branch and the rule names none (design §2.1).</summary>
    public const string NoPlugin = "no plugin pushed it and its repository's landing rule names none, so its pull request was not asked about.";

    /// <summary>
    /// What a plugin last answered, after <i>its pull request:</i> — its state, how and into what it completed and when, and who
    /// answered when. What the record did not keep is not said, never guessed.
    /// </summary>
    public static string Kept(PullRequestState kept)
    {
        var said = kept.State switch
        {
            PullRequestStates.Completed => "completed" + (kept.How is { } how ? $" by {how}" : "") + (kept.Target is { } target ? $" into `{target}`" : "")
                + (kept.At is { } at ? $" on {When(at)}" : ""),
            PullRequestStates.Abandoned => "abandoned" + (kept.At is { } at ? $" on {When(at)}" : ""),
            PullRequestStates.Open => "open",
            _ => "unknown (no pull request from that branch, or a status Daoris has no word for)",
        };
        var by = kept.Plugin is { } plugin ? $"`{plugin}`" : "its plugin";
        return $"{said}, as {by} answered" + (kept.AskedAt == DateTimeOffset.MinValue ? "" : $" at {When(kept.AskedAt)}");
    }

    /// <summary>A failed ask (design §2.4), which never overwrote the answer kept: <paramref name="again"/> where one is kept.</summary>
    public static string Failed(PullRequestAskFailed failed, bool again) =>
        $"asking `{failed.Plugin}` {(again ? "again " : "")}at {When(failed.At)} failed (`{failed.Code}`)";

    /// <summary>
    /// Why a completed answer does not clear the branch, beyond its state (design §2.3), or null where the state says it:
    /// <c>open</c>, <c>abandoned</c> and <c>unknown</c> are their own codes.
    /// </summary>
    public static string? Code(string? code, PullRequestState? kept) => code switch
    {
        PullRequestCodes.OtherTarget => $"it completed into `{kept?.Target}`, not the line",
        PullRequestCodes.MergeNotHere => "its merge commit is not on the line on this machine (bringing the repository up to date fetches it)",
        PullRequestCodes.Beyond => "the branch holds commits its pull request did not carry",
        PullRequestCodes.SourceNotHere => "it merged a commit this machine never had",
        _ => null,
    };

    /// <summary>
    /// What a kept row adds of its pull request, each part after <c>; </c>: the answer and why it does not clear the branch, a
    /// failed ask beside it, and why nothing was asked. Empty where nothing is kept and nothing was said.
    /// </summary>
    public static string Row(PullRequestState? kept, string? code, PullRequestAskFailed? failed, PullRequestNotAsked? notAsked)
    {
        var parts = new List<string>();
        if (kept is not null) parts.Add($"its pull request: {Kept(kept)}" + (Code(code, kept) is { } why ? $", but {why}" : ""));
        if (failed is not null) parts.Add(Failed(failed, again: kept is not null));
        if (notAsked is not null) parts.Add(notAsked.Sentence.TrimEnd('.'));
        return parts.Count == 0 ? "" : "; " + string.Join("; ", parts);
    }

    /// <summary>
    /// <c>trees state</c>'s lines (design §2.1 occasion 4): what came of the ask, what is kept with its address, and what the kept
    /// answer proves here. The plugin's own sentence is said under its name as it answers, not here.
    /// </summary>
    public static IReadOnlyList<string> AskedAgain(PullRequestAskedAgain asked)
    {
        var entry = asked.Entry;
        var kept = entry.PullRequestState;
        var head = $"`{entry.Branch}` in `{entry.Repository}` (session {entry.Session}): ";
        var lines = new List<string>
        {
            head + (asked.Final ? "its pull request completed, which is final, so it was not asked again."
                : asked.Answered ? $"asked {(kept?.Plugin is { } plugin ? $"`{plugin}`" : "its plugin")} again."
                : asked.Code is PullRequestCodes.NoPlugin or PullRequestCodes.Unready or PullRequestCodes.NotAsked or null ? asked.Why ?? NoPlugin
                : $"asking again failed (`{asked.Code}`): {asked.Why?.TrimEnd('.')}. Nothing is removed on its word until it answers, and what was kept stands."),
        };

        if (kept is null)
        {
            lines.Add("nothing is kept of its pull request.");
            return lines;
        }

        lines.Add($"its pull request{(kept.PullRequest is { } address ? $" {address}" : "")}: {Kept(kept)}.");
        if (asked.Verdict is { } verdict) lines.Add(Proves(verdict, kept));
        return lines;
    }

    /// <summary>What a kept answer proves here (design §2.3), as <c>trees state</c> says it.</summary>
    private static string Proves(PullRequestVerdict verdict, PullRequestState kept) => verdict switch
    {
        { Code: null, Clears: true } => $"its merge commit is on `{verdict.Form}` here and the branch stands at or under the commit it merged, so it "
            + "may go at the clean-up and at bringing the repository up to date, each of which lists it first.",
        { Code: null, Carries: true } => $"its merge commit is on `{verdict.Form}` here; the session branches at or under the commit it merged go at the clean-up.",
        { Code: PullRequestCodes.Beyond } => $"its merge commit is on `{verdict.Form}` here, but the branch holds commits its pull request did not "
            + "carry, so it stays; the session branches under the commit it merged go at the clean-up.",
        { Code: PullRequestStates.Open } => "nothing goes on its word: its pull request has not completed.",
        { Code: PullRequestStates.Abandoned } => "nothing goes on its word: its pull request was abandoned, not completed.",
        { Code: PullRequestStates.Unknown } => "nothing goes on its word: it named no state Daoris can act on.",
        _ => $"nothing goes on its word here: {Code(verdict.Code, kept) ?? verdict.Code}.",
    };

    private static string When(DateTimeOffset at) =>
        at.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";
}
