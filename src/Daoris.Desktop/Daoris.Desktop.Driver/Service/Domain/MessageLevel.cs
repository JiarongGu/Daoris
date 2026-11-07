namespace Daoris.Driver;

/// <summary>
/// A message's urgency: the sender's statement of when it should reach a working agent, never judged by
/// a model (<c>model-decoupling</c>). A request, not a guarantee: each door gives what it can, and the answer says what it gave
/// and why. No level stops a turn; only a stop does.
/// </summary>
public enum MessageLevel
{
    /// <summary>At a natural point: never inside a turn in flight, so held for its end, then the next prompt.</summary>
    Ordinary,

    /// <summary>
    /// At the agent's next step, where its door takes words then; held for the turn's end anywhere else, and said so. The
    /// person's default, and every message's that names none.
    /// </summary>
    Urgent,

    /// <summary>
    /// At once, cutting the step in flight, where the agent steers; what urgent gets anywhere else, said so. The person's
    /// own words only.
    /// </summary>
    Now,
}

/// <summary>A message's urgency in the words the wire, the record and the terminal carry.</summary>
public static class MessageLevels
{
    public const string Ordinary = "ordinary";

    public const string Urgent = "urgent";

    public const string Now = "now";

    /// <summary>The level in the record's and the wire's spelling.</summary>
    public static string Spelled(MessageLevel level) => level switch
    {
        MessageLevel.Ordinary => Ordinary,
        MessageLevel.Now => Now,
        _ => Urgent,
    };

    /// <summary>A level from its spelling; null for any other word, and for none.</summary>
    public static MessageLevel? Read(string? word) => word switch
    {
        Ordinary => MessageLevel.Ordinary,
        Urgent => MessageLevel.Urgent,
        Now => MessageLevel.Now,
        _ => null,
    };

    /// <summary>
    /// The level a press asked for: its <c>level</c>, else the person's default, urgent. A level word this
    /// build does not know is the caller's to refuse.
    /// </summary>
    public static MessageLevel Asked(string? level) => Read(level) ?? MessageLevel.Urgent;
}
