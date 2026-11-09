namespace Daoris.Driver;

/// <summary>How a resolver's ask ended.</summary>
public static class ResolverEnds
{
    /// <summary>Its turn ended: the tree is read.</summary>
    public const string Ended = "ended";

    /// <summary>The ask's time ran out first.</summary>
    public const string TimedOut = "timed-out";

    /// <summary>The step lapsed while it waited: the run stopped or superseded, its work moved, or the person stopped the step.</summary>
    public const string Lapsed = "lapsed";

    /// <summary>The resolver may not be asked: the person stopped or declined its session, or it stood down.</summary>
    public const string Declined = "declined";

    /// <summary>The ask could not be told.</summary>
    public const string Untold = "untold";
}

/// <summary>
/// Who a hand-off asks to settle a stop, and how it stops them: the session that is the work's newest executor, its conversation going on.
/// The door asks, waits for the resolver's turn to end, and stops it before it puts the tree back.
/// </summary>
public interface IResolvers
{
    /// <summary>The session to ask about <paramref name="row"/>'s tree; null where none may be asked, said in <c>Why</c>.</summary>
    Task<(string? Session, string? Why)> SessionAsync(TreeHandOff row, CancellationToken ct);

    /// <summary>
    /// <paramref name="words"/> told to <paramref name="session"/> under the row's ask key; completes once its turn has ended, at the row's
    /// <see cref="TreeHandOff.Until"/>, or once <paramref name="lapsed"/> says the step lapsed: one of <see cref="ResolverEnds"/>.
    /// </summary>
    Task<string> AskAsync(TreeHandOff row, string session, string words, Func<bool> lapsed, CancellationToken ct);

    /// <summary>The resolver stopped (its turn through the session's stop door) and awaited until nothing runs in the tree.</summary>
    Task StopAsync(TreeHandOff row, CancellationToken ct);
}
