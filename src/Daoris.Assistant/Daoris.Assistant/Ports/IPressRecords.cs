namespace Daoris.Assistant.Ports;

/// <summary>
/// The presses a conversation made in the window: each route, what it named, its outcome, the card it was asked on and its receipt.
/// The window keeps them; the assistant reads them by conversation.
/// </summary>
public interface IPressRecords
{
    /// <summary>The presses <paramref name="conversation"/> made, in the order made; none for one that made none.</summary>
    IReadOnlyList<PressMade> Of(string conversation);
}

/// <summary>One press: its id, conversation and route, and its outcome in the window's words (<c>running</c>, <c>done</c>, <c>failed</c>, or a card's state).</summary>
public sealed record PressMade(string Id, string Conversation, string Route, string Outcome)
{
    /// <summary>The place it named, by field (a workspace, a repository); empty where it named none.</summary>
    public IReadOnlyDictionary<string, string> Place { get; init; } = new Dictionary<string, string>();

    /// <summary>The card it was asked on in restricted mode, by name; null where it ran with no card.</summary>
    public string? Card { get; init; }

    /// <summary>What it says it made, once its answer is known.</summary>
    public string? Receipt { get; init; }
}
