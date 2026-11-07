using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The park as the question kind's keeping reads it (the checks the driver makes): its repository, whether it is parked here and holds a
/// quest, the question on file, whether a word of the owner's waits, whether its ask's go-aheads are open or cannot be read, whether its
/// line is at its answers-alone bound, and its line's counts.
/// </summary>
internal static class SupervisedParks
{
    /// <summary>The park of <paramref name="id"/>; null where no record of this machine has the id.</summary>
    public static async Task<SupervisedPark?> ReadAsync(DriverLoop loop, ServiceClient service, string id, CancellationToken ct)
    {
        if (await service.RecordAsync(id, ct).ConfigureAwait(false) is not { } record) return null;
        var filed = await Task.Run(() => new QuestionFiles(loop.Home).Read(id), ct).ConfigureAwait(false);
        var ownerWaits = record.Waiting.Any(word => word.Persons);
        var (open, unread) = (false, false);
        if (record.Quest is { } quest)
        {
            var asked = await service.FindQuestAsync(quest, ct).ConfigureAwait(false);
            if (AskWords.AskOf(asked?.From) is { } askId)
            {
                var ask = await service.FindAskAsync(askId, ct).ConfigureAwait(false);
                unread = ask?.GoAheads is null;
                open = ask?.GoAheads?.Any(each => each.Answer is null) == true;
            }
            else
            {
                unread = true;
            }
        }

        // The one count, the line's admission, as the look's hold reads it.
        var atBound = record.Parked && new AnswersAloneHold(loop.Events, new GoOnMarks(loop.Home), new ContinuationAdmissions(loop.Home), SupervisorRuleBook.Read(loop.Home))
            .AtBound(id, await AnswersAloneHold.RecordsAsync(service, ct).ConfigureAwait(false));
        // The line's counts as the print hands them, which a reason's `count:` source names and the decision keeps.
        var counts = (await LineCountsRead.FactsAsync(service, loop.Home, id, ct).ConfigureAwait(false)).Counts;
        return new SupervisedPark(record.Repository ?? "", record.Parked, record.Quest is not null, filed, ownerWaits, open, unread, atBound)
        {
            Counts = counts,
        };
    }
}
