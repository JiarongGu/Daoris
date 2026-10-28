namespace Daoris.Driver;

/// <summary>
/// What Context asks of the work orders: the bounds the instruction's ask, go-ahead and channel sections are quoted by, so the account
/// of an instruction (<see cref="InstructionAccounting"/>) counts by the very rules the instruction was composed by. Work orders
/// installs it when the driver loads and answers with the same members its sections are written from.
/// </summary>
internal interface IContextWorkOrders
{
    /// <summary>The most characters of the person's words after the ask's own sentence an instruction carries.</summary>
    int WordsLimit { get; }

    /// <summary>The most characters of one word after the ask's sentence.</summary>
    int WordLimit { get; }

    /// <summary>The words an instruction shows by the bound, and those it leaves out: the ask's own sentence always, then the newest that fit.</summary>
    (IReadOnlyList<AskWordView> Shown, IReadOnlyList<AskWordView> Left) AskBounded(IReadOnlyList<AskWordView> said);

    /// <summary>The most characters of the go-aheads an instruction carries.</summary>
    int GoAheadsLimit { get; }

    /// <summary>The most characters of what an act touches.</summary>
    int GoAheadActLimit { get; }

    /// <summary>How many go-aheads an instruction lists by <see cref="GoAheadsLimit"/>.</summary>
    int GoAheadsShown(IReadOnlyList<GoAheadView> held);

    /// <summary>The channel's messages shown by the bound, and those left out: the newest that fit, oldest first.</summary>
    (IReadOnlyList<ChannelItemView> Shown, IReadOnlyList<ChannelItemView> Left) ChannelBounded(IReadOnlyList<ChannelItemView> messages);
}

/// <summary>The required work-order operations, resolved only where an instruction's account counts the sections they bound.</summary>
internal static class ContextWorkOrders
{
    private static IContextWorkOrders? _current;

    internal static IContextWorkOrders Current
    {
        get => _current ?? throw new InvalidOperationException("no context work-order operations are installed: Work orders installs them when the driver loads");
        set => _current = value;
    }
}
