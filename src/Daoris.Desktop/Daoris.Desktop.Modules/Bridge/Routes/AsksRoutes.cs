using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// An ask's own doors on the page, `bridge/asks.ts` (SESSONE2c, D167 points 6 to 8; the co-working design §2.4, D50): its
/// channel, where its sessions and the person talk, read, posted to and marked read. The screen twin of
/// <c>daoris-driver ask channel</c>; the service's three doors (SESSONE2a) are the same ones, through the driver library's
/// <see cref="ServiceClient"/>.
/// </summary>
/// <remarks>
/// <para><b>The person's own read</b>: no <c>entry</c> is the person's mark (the service's <c>person</c>), which is what the ask's
/// page and the rail's unread count read; a session's entry is the driver's to move (SESSONE2b), never the page's.</para>
///
/// <para><b>A refusal of the post is an answer</b> (<see cref="ChannelAnswers.Posted"/>), the service's code, which the page words
/// in the reader's language (D142); a service that did not answer in a code is the driver's refusal. An ask the host holds no
/// channel for reads as one with nothing on it, so a host from before the channel shows an empty section.</para>
/// </remarks>
public sealed class AsksRoutes(DriverLoop loop)
{
    private readonly DriverLoop _loop = loop;

    /// <summary>
    /// An ask's channel as the person reads it, moving no mark: every item in time order, how many are past the mark, and the
    /// mark. <c>entry</c> reads as that entry would, and <c>since</c> only the items after a moment.
    /// </summary>
    [DriverRoute("CHANNEL_READ")]
    private async Task<object?> ChannelReadAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var ask = Asked(request);
        var service = _loop.Service ?? throw NotReady();
        var since = Optional(request, "since") is { } moment ? Moments.Read(moment) : null;
        var view = await service.ChannelAsync(ask, Optional(request, "entry"), cancellationToken, since).ConfigureAwait(false);
        return view is null ? ChannelAnswers.Empty(ask) : ChannelAnswers.Read(view);
    }

    /// <summary>
    /// The person's post: their words, to everyone or one of the ask's repositories (<c>to</c>), answering a message (<c>re</c>)
    /// or not. Their words, so every session on the ask reads them (D167 point 7). Answers <c>posted</c> with the message kept, or
    /// the service's code where it refused.
    /// </summary>
    [DriverRoute("CHANNEL_POST")]
    private async Task<object?> ChannelPostAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var ask = Asked(request);
        var service = _loop.Service ?? throw NotReady();
        var posted = await service.ChannelPostAsync(
            ask, Optional(request, "text") ?? "", Optional(request, "to"), Optional(request, "re"), cancellationToken).ConfigureAwait(false);
        return ChannelAnswers.Posted(posted);
    }

    /// <summary>
    /// The person's mark moved through a moment (<c>through</c>), or the newest item: what the page has shown them. A mark never
    /// moves back. Answers the channel as it then reads.
    /// </summary>
    [DriverRoute("CHANNEL_MARK")]
    private async Task<object?> ChannelMarkAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var ask = Asked(request);
        var service = _loop.Service ?? throw NotReady();
        var through = Optional(request, "through") is { } moment ? Moments.Read(moment) : null;
        var view = await service.ChannelMarkAsync(ask, through, cancellationToken).ConfigureAwait(false);
        return view is null ? ChannelAnswers.Empty(ask) : ChannelAnswers.Read(view);
    }

    /// <summary>
    /// The asks the mirrored channel messages name that this machine does not hold (SESSONE2h): the Asks list's <i>Teammates' asks</i>.
    /// Each is read at <c>CHANNEL_READ</c> by its id; the service holds no record of it, so it has no title.
    /// </summary>
    [DriverRoute("ASKS_ELSEWHERE")]
    private async Task<object?> AsksElsewhereAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var service = _loop.Service ?? throw NotReady();
        return new ElsewhereAnswer(await service.ElsewhereAsksAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>The ask a route is about, as the service names it.</summary>
    /// <exception cref="DriverException">None named: the driver's sentence, said verbatim.</exception>
    private static string Asked(IpcRequest request) =>
        Optional(request, "ask")?.Trim().TrimStart('#') is { Length: > 0 } ask ? ask : throw new DriverException("a channel is an ask's: name the ask.");
}
