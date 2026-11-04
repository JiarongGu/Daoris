namespace Daoris.Knowledge;

/// <summary>What a post did.</summary>
public sealed record ChannelOutcome(ChannelRefusal Refusal, ChannelMessage? Message);
