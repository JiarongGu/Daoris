using Daoris.Contracts.Asks;

namespace Daoris.Driver;

/// <summary>
/// The local host's doors of the messages to one part, as the driver asks them: what waits for one part it runs, alone and on its line's
/// own channel, and that part's acknowledgment. The read is how the driver says it reads those messages: a host from before the doors
/// answers not found, so nothing is handed and they wait for the part's own read.
/// </summary>
public sealed class AddressedDoors
{
    private readonly ServiceClient _service;

    internal AddressedDoors(ServiceClient service) => _service = service;

    /// <summary>The messages waiting for <paramref name="session"/>, each with its line's acknowledgment; null where the host holds no such session or door.</summary>
    /// <exception cref="DriverException">The host answered something other than the list.</exception>
    public async Task<IReadOnlyList<ChannelItemView>?> WaitingAsync(string session, CancellationToken ct = default)
    {
        var sent = await _service.SendAsync(new ReadAddressed(session), ct).ConfigureAwait(false);
        if (sent.Reply.Status == (int)System.Net.HttpStatusCode.NotFound) return null;
        if (!sent.Reply.Ok) throw new DriverException($"the service would not read what waits for session `{session}`: {sent.Reply.Payload}");

        return sent.Answer?.Messages is { } listed
            ? [.. listed.Select(ChannelView.ItemOf).OfType<ChannelItemView>()]
            : throw new DriverException($"the service answered something other than what waits for session `{session}`: {sent.Reply.Payload}");
    }

    /// <summary>
    /// The channel of the line <paramref name="session"/> is a part of, as the person reads it, where that line is on no ask the host holds;
    /// null where it has none, or the host has no such door.
    /// </summary>
    /// <exception cref="DriverException">The host answered something other than the channel.</exception>
    public async Task<LineChannelView?> LineAsync(string session, CancellationToken ct = default)
    {
        var sent = await _service.SendAsync(new ReadLineChannel(session), ct).ConfigureAwait(false);
        if (sent.Reply.Status == (int)System.Net.HttpStatusCode.NotFound) return null;
        if (!sent.Reply.Ok) throw new DriverException($"the service would not read the channel of session `{session}`'s line: {sent.Reply.Payload}");

        return sent.Answer is { Origin: { } origin, Items: { } listed }
            ? new LineChannelView(origin, [.. listed.Select(ChannelView.ItemOf).OfType<ChannelItemView>()])
            : throw new DriverException($"the service answered something other than the channel of session `{session}`'s line: {sent.Reply.Payload}");
    }

    /// <summary>
    /// The person's post on the channel of the line <paramref name="session"/> is a part of, to everyone on it or one of its repositories;
    /// the message kept, or the service's code where it refused (<c>empty</c>, <c>too-long</c>, <c>bad-to</c>, <c>no-session</c>).
    /// </summary>
    /// <exception cref="DriverException">The host answered neither the message nor a code.</exception>
    public async Task<ChannelPosted> PostLineAsync(string session, string text, string? to, bool? urgent = null, CancellationToken ct = default)
    {
        var sent = await _service.SendAsync(new PostLineChannelMessage(session, text, to, urgent), ct).ConfigureAwait(false);
        if (sent.Answer is { } posted)
        {
            return ChannelView.ItemOf(posted.Message) is { } item
                ? new ChannelPosted(true, item, null)
                : throw new DriverException($"the service answered something other than a posted message on session `{session}`'s line: {sent.Reply.Payload}");
        }

        return AskDoorViews.CodeOf(sent.Reply.Payload) is { Length: > 0 } code
            ? new ChannelPosted(false, null, code)
            : throw new DriverException($"the service would not take the post on session `{session}`'s line's channel: {sent.Reply.Payload}");
    }

    /// <summary>One part's acknowledgment of the messages to it by their ids; the ids it changed, none from a host without the door.</summary>
    /// <exception cref="DriverException">The host refused the acknowledgment.</exception>
    public async Task<IReadOnlyList<string>> AcknowledgeAsync(string session, string state, IReadOnlyList<string> messages, CancellationToken ct = default)
    {
        var sent = await _service.SendAsync(new AcknowledgeAddressed(session, state, messages), ct).ConfigureAwait(false);
        if (sent.Reply.Status == (int)System.Net.HttpStatusCode.NotFound) return [];
        if (!sent.Reply.Ok) throw new DriverException($"the service would not keep session `{session}`'s {state} of the messages to it: {sent.Reply.Payload}");

        return sent.Answer?.Acknowledged ?? [];
    }
}

/// <summary>A line's own channel as the person reads it: its origin and its messages, oldest first.</summary>
public sealed record LineChannelView(string Origin, IReadOnlyList<ChannelItemView> Items);

/// <summary>The client's doors of the messages to one part, as the work orders' own member of it, holding no state of its own.</summary>
public static class AddressedDoorsOfClient
{
    extension(ServiceClient client)
    {
        public AddressedDoors Addressed => new(client);
    }
}
