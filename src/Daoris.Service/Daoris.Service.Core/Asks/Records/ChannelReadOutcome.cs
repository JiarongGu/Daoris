namespace Daoris.Knowledge;

/// <summary>What a session's read did: its refusal, or the ask it read and what it read, and the entry it read as (COWORK1d).</summary>
public sealed record ChannelReadOutcome(ChannelRefusal Refusal, Ask? Ask, ChannelRead? Read, string? Entry = null)
{
    /// <summary>The origin whose channel was read; a line on no ask has no <see cref="Ask"/>.</summary>
    public string? Origin { get; init; }
}
