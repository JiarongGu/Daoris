using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>
/// What the workplace asks of the review, installed when the driver loads: the second opinion a landing the gate holds is owed
/// (<see cref="IWorkplaceReview"/>). It does exactly what the auto-lander did before it was a port.
/// </summary>
internal sealed class WorkplaceReviewOperations : IWorkplaceReview
{
    [ModuleInitializer]
    internal static void Install() => WorkplaceReview.Current = new WorkplaceReviewOperations();

    public void OweLanding(
        string home, string session, string quest, string repository, string workspace, string tree, Func<DateTimeOffset> at, DateTimeOffset? since)
    {
        try
        {
            var dues = new OpinionDues(home);
            var owed = dues.All()
                .Where(due => string.Equals(due.Session, session, StringComparison.OrdinalIgnoreCase) && due.Occasion == OpinionRules.Landing)
                .ToList();
            if (owed.Any(due => due.Closed is null)) return;
            if (since is { } done && owed.Any(due => due.Why == OpinionDueClosed.Landed && due.Closed >= done)) return;

            dues.Due(new OpinionDue(session, quest, repository, workspace, tree, OpinionRules.Landing, at()));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Owed at the next look instead.
        }
    }
}
