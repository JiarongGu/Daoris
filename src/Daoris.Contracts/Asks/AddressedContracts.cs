namespace Daoris.Contracts.Asks;

// The doors of the messages to one part, a local host's alone: what waits for one part a driver runs, and that part's acknowledgment.
// The driver's call is how it says it reads those messages; one that never asks is handed none, and they wait for the part's own read.

/// <summary>What waits for one part a driver runs: the messages to it and on its line's own channel, each with its line's <c>Ack</c>.</summary>
public sealed record AddressedResponse(IReadOnlyList<ChannelItemResponse> Messages);

/// <summary>Answered not found, with the channel's code, for a blank session or one with no channel here.</summary>
public sealed record ReadAddressed(string? Session = null) : IRoute<AddressedResponse>
{
    public static string Method => "GET";
    public static string Path => "/api/channel/addressed";
}

/// <summary>
/// A line's own channel as the person reads it: its <c>Origin</c> and its messages oldest first, each to one part with its <c>Part</c> and
/// <c>Receipt</c>. It moves no mark.
/// </summary>
public sealed record LineChannelResponse(string Origin, IReadOnlyList<ChannelItemResponse> Items);

/// <summary>
/// The person's read of the channel of the line <c>Session</c> is a part of, where that line is on no ask held here; answered not found,
/// with the channel's code, for a session with no such channel.
/// </summary>
public sealed record ReadLineChannel(string? Session = null) : IRoute<LineChannelResponse>
{
    public static string Method => "GET";
    public static string Path => "/api/channel/line";
}

/// <summary>
/// One part's acknowledgment of the messages to it by their ids, as an entry's of an ask's: <c>taking</c>, <c>released</c>, <c>sent</c>,
/// <c>taken</c>, <c>read</c> or <c>partly</c>. Answers the ids it changed.
/// </summary>
public sealed record AcknowledgeAddressed(string? Session, string? State, IReadOnlyList<string>? Messages) : IRoute<ChannelAckResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/channel/addressed/ack";
}
