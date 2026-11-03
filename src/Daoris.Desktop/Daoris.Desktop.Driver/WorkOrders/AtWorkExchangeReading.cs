namespace Daoris.Driver;

/// <summary>
/// The exchanges half of <i>At work</i> (COMMS1i, the comms design §7, §13 point 12): the attended line's requests of others, read by the
/// one read (<see cref="Exchanges.Of"/>) over the home's helper, piece and hand-over files, the quests and records the caller holds, and
/// the reviewers' records, each file taken with its health. The window's read and the terminal's verb fill the composition's port with it.
/// </summary>
public static class AtWorkExchangeReading
{
    /// <summary>
    /// The sources it reads: what the line asked of others, its reviewers among them. A question and a go-ahead are asked of the person,
    /// whose waits the head and <i>What needs you</i> draw; neither is read here, so neither is said unread.
    /// </summary>
    public static readonly IReadOnlyList<string> Sources = ["helper", "piece", "hand-over", "quest", "opinion"];

    /// <param name="reviewers">The reviewers' records as opinions on the lines they serve (<see cref="Reviewer"/>); null where none were read.</param>
    public static AtWorkExchanging Reading(
        string home, IReadOnlyDictionary<string, PriorSession> records, IReadOnlyList<QuestView>? quests, IReadOnlyList<ExchangeOpinion>? reviewers) =>
        attended => Of(Look(home, records, quests, reviewers), attended);

    /// <summary>
    /// A reviewer's record as an opinion asked on the line it serves (COMMS1i, D203's <c>serves</c>): reading while its session runs, failed
    /// where it failed, else ended. Null for a record that reads no opinion, or one that names no line it serves (an opinion from before the
    /// mark), which is on no line.
    /// </summary>
    public static ExchangeOpinion? Reviewer(string session, string? opinion, string? serves, string? adapter, string? state) =>
        opinion is { Length: > 0 } && serves is { Length: > 0 }
            ? new ExchangeOpinion(opinion, serves, session, adapter ?? "reviewer", adapter, ReviewerState(state), HandedTo: null)
            : null;

    /// <summary>The reviewers among <paramref name="records"/>, as <see cref="Reviewer"/> reads each.</summary>
    public static IReadOnlyList<ExchangeOpinion> Reviewers(IEnumerable<SessionRecord> records) =>
        [.. records.Select(record => Reviewer(record.Id, record.Opinion, record.Serves, record.Adapter, record.State)).OfType<ExchangeOpinion>()];

    private static string ReviewerState(string? state) => state?.ToLowerInvariant() switch
    {
        "queued" or "starting" or "working" => "reading",
        "failed" => "failed",
        _ => "ended",
    };

    /// <summary>The records a line is walked in, as the read takes them, from the records a listing holds.</summary>
    public static IReadOnlyDictionary<string, PriorSession> Priors(IEnumerable<SessionRecord> records) =>
        records.DistinctBy(record => record.Id, StringComparer.Ordinal).ToDictionary(
            record => record.Id,
            record => new PriorSession(record.Id, record.Tree, record.State, null, record.Repository)
            {
                From = record.From, FromWhy = record.FromWhy, Quest = record.Quest, TookQuest = record.TookQuest,
            },
            StringComparer.Ordinal);

    public static ExchangeLook Look(
        string home, IReadOnlyDictionary<string, PriorSession> records, IReadOnlyList<QuestView>? quests, IReadOnlyList<ExchangeOpinion>? reviewers)
    {
        var helpers = new HelperFiles(home);
        var pieces = new PartnerFiles(home);
        var handOvers = new HandOverAskFiles(home);
        return new ExchangeLook
        {
            Records = records,
            Helpers = Try(helpers.Asks),
            HelperState = helpers.ReadState,
            Pieces = Try(pieces.Asks),
            PieceState = pieces.ReadState,
            HandOvers = Try(handOvers.Asks),
            HandOverState = handOvers.ReadState,
            Quests = quests,
            Opinions = reviewers,
        };
    }

    /// <summary>
    /// What each line on this machine waits on, for Ask Daoris's room (COMMS1i, the comms design §7): each line's head among
    /// <paramref name="records"/> (a record no other continues, and no helper's, partner's or reviewer's) with the requests of its line it
    /// still waits on, in the verb's words, by the same read <i>At work</i> draws; and the sources not read.
    /// </summary>
    public static HelpWaits Waits(string home, IReadOnlyList<SessionRecord> records, IReadOnlyList<QuestView>? quests)
    {
        var look = Look(home, Priors(records), quests, Reviewers(records));
        var continued = records.Select(record => record.From).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var waits = new List<HelpWait>();
        var unread = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var head in records.DistinctBy(record => record.Id, StringComparer.Ordinal)
                     .Where(record => !continued.Contains(record.Id) && record.Serves is null && record.Opinion is null)
                     .OrderBy(record => record.Id, StringComparer.Ordinal))
        {
            var read = Of(look, head.Id);
            unread.UnionWith(read.Unread);
            waits.AddRange(read.Rows.Where(row => row.Holds)
                .Select(row => new HelpWait(head.Id, head.Repository, head.Workspace, AtWorkVerb.Exchange(row, head.Id))));
        }

        return new HelpWaits(waits, [.. unread]);
    }

    /// <summary>The line <paramref name="attended"/> ends: its requests of others, and the sources among <see cref="Sources"/> not read.</summary>
    public static AtWorkExchanges Of(ExchangeLook look, string attended)
    {
        IReadOnlyList<Exchange> all;
        try
        {
            all = [.. Exchanges.Of(look, attended).Where(each => Sources.Contains(SourceOf(each.Id)))];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return new AtWorkExchanges([], [.. Sources]);
        }

        return new AtWorkExchanges(
            [.. all.Where(each => each.Source != Exchanges.Unread).Select(Row)],
            [.. all.Where(each => each.Source == Exchanges.Unread).Select(each => SourceOf(each.Id)).Distinct(StringComparer.Ordinal)]);
    }

    private static AtWorkExchange Row(Exchange each) =>
        new(each.Id, each.Part, each.Expects, each.To, each.Delivery, each.Outcome, each.Holds)
        {
            At = each.At == DateTimeOffset.MinValue ? null : each.At,
            Session = each.By?.Session,
            Agent = each.By?.Agent,
            Answered = each.Answer?.At,
            Stale = each.Source == Exchanges.Stale ? true : null,
        };

    private static string SourceOf(string id) => id[..Math.Max(0, id.IndexOf(':'))];

    private static IReadOnlyList<T>? Try<T>(Func<IReadOnlyList<T>> read)
    {
        try
        {
            return read();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return null;
        }
    }
}
