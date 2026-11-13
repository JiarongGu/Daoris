namespace Daoris.Knowledge;

/// <summary>One thing only a person can settle about the delivery, asked once on the ask's queue under a key that stays the same however often it is found.</summary>
/// <param name="Key">The identity the queue counts it by.</param>
/// <param name="Kind">One of the constants of this record, or the lead's own word for a contract point.</param>
/// <param name="Text">What it is, in a sentence.</param>
public sealed record DeliveryQuestion(string Key, string Kind, string Text)
{
    /// <summary>The release orders run in a circle.</summary>
    public const string Cycle = "cycle";

    /// <summary>An edge's evidence changed after the lead read it.</summary>
    public const string Stale = "stale";

    /// <summary>A point of the contract or of the delivery the lead could not settle from evidence.</summary>
    public const string Open = "open";

    /// <summary>What yes does.</summary>
    public string Yes { get; init; } = "";

    /// <summary>What waits on the answer.</summary>
    public string Blocks { get; init; } = "";

    /// <summary>What is done if nobody answers.</summary>
    public string Default { get; init; } = "";
}
