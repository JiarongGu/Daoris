using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>What Context asks of the work orders, installed when the driver loads: the bounds the instruction's sections are quoted by (<see cref="IContextWorkOrders"/>).</summary>
internal sealed class ContextWorkOrdersOperations : IContextWorkOrders
{
    [ModuleInitializer]
    internal static void Install() => ContextWorkOrders.Current = new ContextWorkOrdersOperations();

    public int WordsLimit => AskWordsText.WordsLimit;

    public int WordLimit => AskWordsText.WordLimit;

    public (IReadOnlyList<AskWordView> Shown, IReadOnlyList<AskWordView> Left) AskBounded(IReadOnlyList<AskWordView> said) => AskWordsText.Bounded(said);

    public int GoAheadsLimit => GoAheadsText.Limit;

    public int GoAheadActLimit => GoAheadsText.ActLimit;

    public int GoAheadsShown(IReadOnlyList<GoAheadView> held) => GoAheadsText.Shown(held);

    public (IReadOnlyList<ChannelItemView> Shown, IReadOnlyList<ChannelItemView> Left) ChannelBounded(IReadOnlyList<ChannelItemView> messages) =>
        ChannelText.Bounded(messages);

    public async Task<IReadOnlyList<BriefWait>> WaitsAsync(
        ServiceClient service, string home, IReadOnlyDictionary<string, PriorSession> records, string session, CancellationToken ct)
    {
        try
        {
            var parts = LineKeys.Upto(session, records).Select(part => part.Session).ToList();
            if (!parts.Contains(session, StringComparer.Ordinal)) parts.Add(session);
            var look = await LookAsync(service, home, records, parts, session, ct).ConfigureAwait(false);
            var asked = (look.Helpers ?? []).ToDictionary(ask => $"helper:{ask.Id}", ask => ask.Question, StringComparer.Ordinal);
            return
            [
                .. Exchanges.Of(look, session)
                    .Where(exchange => exchange.Source != Exchanges.Unread && exchange.Holds)
                    .Select(exchange => new BriefWait(exchange.Id, exchange.Part, exchange.To, exchange.Delivery, exchange.Outcome, asked.GetValueOrDefault(exchange.Id))),
            ];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return [];
        }
    }

    /// <summary>The look of a line's exchanges that the brief can have: each source it cannot read is left null, said unread and told nowhere.</summary>
    private static async Task<ExchangeLook> LookAsync(
        ServiceClient service, string home, IReadOnlyDictionary<string, PriorSession> records, IReadOnlyList<string> parts, string session, CancellationToken ct)
    {
        var helpers = new HelperFiles(home);
        var pieces = new PartnerFiles(home);
        var handOvers = new HandOverAskFiles(home);
        var questions = new QuestionFiles(home);
        var (ask, goAheads) = await GoAheadsAsync(service, records, session, ct).ConfigureAwait(false);
        return new ExchangeLook
        {
            Records = records,
            Helpers = Try(helpers.Asks),
            HelperState = helpers.ReadState,
            Pieces = Try(pieces.Asks),
            PieceState = pieces.ReadState,
            Questions = Try(() => (IReadOnlyList<ParkQuestion>)[.. parts.Select(part => questions.Read(part)).OfType<ParkQuestion>()]),
            HandOvers = Try(handOvers.Asks),
            HandOverState = handOvers.ReadState,
            Quests = await QuestsAsync(service, ct).ConfigureAwait(false),
            Ask = ask,
            GoAheads = goAheads,
        };
    }

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

    private static async Task<IReadOnlyList<QuestView>?> QuestsAsync(ServiceClient service, CancellationToken ct)
    {
        try
        {
            return [.. (await service.EveryQuestAsync(ct).ConfigureAwait(false)).Where(quest => !string.IsNullOrEmpty(quest.Id)).DistinctBy(quest => quest.Id, StringComparer.OrdinalIgnoreCase)];
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or System.Text.Json.JsonException or InvalidOperationException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            return null;
        }
    }

    private static async Task<(string? Ask, IReadOnlyList<GoAheadView>? GoAheads)> GoAheadsAsync(
        ServiceClient service, IReadOnlyDictionary<string, PriorSession> records, string session, CancellationToken ct)
    {
        try
        {
            if (!records.TryGetValue(session, out var record) || AskWords.AskOfEntry(record.Entry) is not { } id) return (null, []);
            return (id, await service.FindAskAsync(id, ct).ConfigureAwait(false) is { } ask ? AskWords.Of(ask).GoAheads ?? [] : []);
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or System.Text.Json.JsonException or InvalidOperationException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            return (null, null);
        }
    }
}
