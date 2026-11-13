using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// A run's review, opened by its run (<see cref="RunReviews"/>): read from the run's own records, so it opens after its trees and sessions are
/// gone, and from any session of the run's chain, an earlier part's included, by the run its quest is (<see cref="QuestRuns"/>). A file is drawn
/// at the record's revision (<see cref="ReviewContent"/>). A session no run governs reads its own changes through <c>SESSION_DIFF</c>.
/// </summary>
public sealed class RunReviewRoutes(DriverLoop loop)
{
    private readonly DriverLoop _loop = loop;

    /// <summary>The review of <c>run</c>, or of the run <c>session</c>'s quest is; <c>not-run</c> where none is bound here with a review.</summary>
    [DriverRoute("RUN_REVIEW", PipeClass.Open)]
    [ByEvent("RUN_REVIEW", Untold = "a run's review records and node record, which the runtime's pass writes, the supervisor's decisions in memory, and the service's quests a session's run is read by")]
    private async Task<object?> RunReviewAsync(RunReviewRead press, CancellationToken cancellationToken)
    {
        var run = string.IsNullOrWhiteSpace(press.Run) ? null : press.Run.Trim().TrimStart('#');
        if (run is null && !string.IsNullOrWhiteSpace(press.Session)) run = await RunOfAsync(press.Session.Trim(), cancellationToken).ConfigureAwait(false);
        if (run is null && string.IsNullOrWhiteSpace(press.Session)) throw new DriverException("a run's review is read for a `run` or a `session` — name one of them.");
        var review = run is null ? null : await Task.Run(() => RunReviews.Read(_loop.Home, run, press.Attempt > 0 ? press.Attempt : null), cancellationToken).ConfigureAwait(false);
        if (review is null) return new { Run = run, Refused = RunNodeWords.NotRun };
        var decided = _loop.Supervisor.LandingOn(review.Run, review.Node, review.Record?.Attempt ?? review.Standing);
        if (review.Record is not { Kind: ReviewKinds.Branch or ReviewKinds.Checkout } record || await CheckoutOfAsync(record.Place, cancellationToken).ConfigureAwait(false) is not { } root)
        {
            return review with { Supervisor = RunReviewSupervisor.Of(decided, review.Record) };
        }

        var files = await ReviewCommitFiles.ListAsync(root, record, cancellationToken).ConfigureAwait(false);
        var commits = await ReviewBranchCommits.ListAsync(root, record, cancellationToken).ConfigureAwait(false);
        var before = await ReviewCommits.CommitAsync(root, record, ReviewContent.Before, cancellationToken).ConfigureAwait(false);
        var after = await ReviewCommits.CommitAsync(root, record, ReviewContent.After, cancellationToken).ConfigureAwait(false);
        var diff = before is null || after is null ? null : await TreeDiffs.DiffBetweenAsync(root, before, after, cancellationToken).ConfigureAwait(false);
        return review with { Supervisor = RunReviewSupervisor.Of(decided, review.Record), Files = files, Commits = commits, Diff = diff };
    }

    /// <summary>The text of <c>path</c> on <c>side</c> of the run's record at <c>attempt</c>, or why it is not drawn (<see cref="ReviewContent"/>).</summary>
    [DriverRoute("RUN_REVIEW_FILE", PipeClass.Open)]
    [ByEvent("RUN_REVIEW_FILE", Untold = "a review record's file, from the place's content store or the place itself while it hashes as the record says")]
    private async Task<object?> RunReviewFileAsync(RunReviewFileRead press, CancellationToken cancellationToken)
    {
        var run = press.Run.Trim().TrimStart('#');
        var review = await Task.Run(() => RunReviews.Read(_loop.Home, run, press.Attempt), cancellationToken).ConfigureAwait(false);
        if (review is not { Record: { } record } || record.Attempt != press.Attempt) return new { Run = run, press.Path, Refused = RunNodeWords.NotRun };

        var (text, refused) = record.Kind is ReviewKinds.Branch or ReviewKinds.Checkout
            ? await CheckoutOfAsync(record.Place, cancellationToken).ConfigureAwait(false) is { } root
                ? await ReviewCommits.ReadAsync(root, record, press.Path, press.Side, cancellationToken).ConfigureAwait(false)
                : (null, ReviewContent.NotKept)
            : await Task.Run(() => ReviewContent.Read(_loop.Home, record, press.Path, press.Side), cancellationToken).ConfigureAwait(false);
        return new { Run = run, review.Node, record.Attempt, record.Revision, press.Path, press.Side, Text = text, Refused = refused };
    }

    /// <summary>The first checkout of <paramref name="repository"/> on this disk, as the registry names it; null where none stands here.</summary>
    private async Task<string?> CheckoutOfAsync(string repository, CancellationToken ct)
    {
        if (_loop.Service is not { } service) return null;
        var registry = await service.RegistryAsync(ct).ConfigureAwait(false);
        return LinesRoutes.Checkouts(registry, repository, null).Select(row => row.Root).FirstOrDefault(Directory.Exists);
    }

    /// <summary>The run the quest of <paramref name="session"/> is, its chain read up its parents; null where it serves none.</summary>
    private async Task<string?> RunOfAsync(string session, CancellationToken ct)
    {
        var service = _loop.Service ?? throw DriverArguments.NotReady();
        if (await service.RecordAsync(session, ct).ConfigureAwait(false) is not { Quest: { Length: > 0 } quest }) return null;
        var quests = (await service.EveryQuestAsync(ct).ConfigureAwait(false))
            .GroupBy(each => each.Id, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        return quests.TryGetValue(quest.TrimStart('#'), out var view) ? QuestRuns.Of(view, id => quests.GetValueOrDefault(id.TrimStart('#'))) : quest;
    }
}
