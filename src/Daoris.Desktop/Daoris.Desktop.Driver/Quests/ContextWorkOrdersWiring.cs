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
}
