namespace Daoris.Driver;

/// <summary>
/// A conversation move's fence before anything is handed to its new part: the open's answer with every word it moved, the line's head read,
/// then <c>delivering</c>. Asked once, by whichever reaches it first: a carried part's run before its spawn, else the move's hand.
/// </summary>
internal sealed class MoveFence(ConversationMove move)
{
    private Task<string?>? _asked;

    /// <summary>Why nothing may be handed, a write the store refused or a line gone on past its new part; null where the hand may go.</summary>
    public Task<string?> AskAsync(string to, IReadOnlyList<MovedWord> moved) => _asked ??= FenceAsync(to, moved);

    private async Task<string?> FenceAsync(string to, IReadOnlyList<MovedWord> moved)
    {
        var fenced = move.Opened(to, moved);
        if (fenced is null && await move.SupersededAsync().ConfigureAwait(false)) fenced = "the line went on past its new part";
        return fenced ?? move.Delivering();
    }
}
