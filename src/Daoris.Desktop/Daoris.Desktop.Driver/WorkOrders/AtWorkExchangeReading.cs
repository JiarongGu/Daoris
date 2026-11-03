namespace Daoris.Driver;

/// <summary>
/// The exchanges half of <i>At work</i> (COMMS1i, the comms design §7, §13 point 12): the attended line's requests of others, read by the
/// one read (<see cref="Exchanges.Of"/>) over the home's helper, piece and hand-over files and the quests and records the caller holds,
/// each file taken with its health. The window's read and the terminal's verb fill the composition's port with it.
/// </summary>
public static class AtWorkExchangeReading
{
    /// <summary>
    /// The sources it reads: what the line asked of others. A question and a go-ahead are asked of the person, whose waits the head and
    /// <i>What needs you</i> draw, and an opinion's reviewer is the landing gate's; none of them is read here, so none is said unread.
    /// </summary>
    public static readonly IReadOnlyList<string> Sources = ["helper", "piece", "hand-over", "quest"];

    public static AtWorkExchanging Reading(string home, IReadOnlyDictionary<string, PriorSession> records, IReadOnlyList<QuestView>? quests) =>
        attended => Of(Look(home, records, quests), attended);

    /// <summary>The records a line is walked in, as the read takes them, from the records a listing holds.</summary>
    public static IReadOnlyDictionary<string, PriorSession> Priors(IEnumerable<SessionRecord> records) =>
        records.DistinctBy(record => record.Id, StringComparer.Ordinal).ToDictionary(
            record => record.Id,
            record => new PriorSession(record.Id, record.Tree, record.State, null, record.Repository)
            {
                From = record.From, FromWhy = record.FromWhy, Quest = record.Quest, TookQuest = record.TookQuest,
            },
            StringComparer.Ordinal);

    public static ExchangeLook Look(string home, IReadOnlyDictionary<string, PriorSession> records, IReadOnlyList<QuestView>? quests)
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
        };
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
