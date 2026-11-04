namespace Daoris.Driver;

/// <summary>
/// What a chat run reads of its tree and its conversation (CHATFLOW1c; the chat-flow design §4.2), read once for the view: the tip its tree
/// stands at, the commits beyond the line as the review's offer counts them, and whether its newest record is between turns.
/// </summary>
/// <param name="Run">Its key, <c>chat-&lt;first&gt;</c>.</param>
public sealed record ChatRunFacts(string Run)
{
    /// <summary>The tree's <c>HEAD</c>; null where the tree does not stand or git could not say.</summary>
    public string? Tip { get; init; }

    /// <summary>The commits beyond the line; null where git could not say or the tree does not stand.</summary>
    public int? Beyond { get; init; }

    /// <summary>Whether its newest record's turn ended with nothing said since.</summary>
    public bool Between { get; init; }
}
