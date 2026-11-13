using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// An ask's own doors on the page, `bridge/asks.ts`: its
/// channel, where its sessions and the person talk, read, posted to and marked read. The screen twin of
/// <c>daoris ask channel</c>; the service's three doors are the same ones, through the driver library's
/// <see cref="ServiceClient"/>.
/// </summary>
/// <remarks>
/// <para><b>The person's own read</b>: no <c>entry</c> is the person's mark (the service's <c>person</c>), which is what the ask's
/// page and the rail's unread count read; a session's entry is the driver's to move, never the page's.</para>
///
/// <para><b>A refusal of the post is an answer</b> (<see cref="ChannelAnswers.Posted"/>), the service's code, which the page words
/// in the reader's language; a service that did not answer in a code is the driver's refusal. An ask the host holds no
/// channel for reads as one with nothing on it, so a host from before the channel shows an empty section.</para>
/// </remarks>
public sealed class AsksRoutes(DriverLoop loop)
{
    private readonly DriverLoop _loop = loop;

    /// <summary>
    /// An ask's channel as the person reads it, moving no mark: every item in time order, how many are past the mark, and the
    /// mark. <c>entry</c> reads as that entry would, and <c>since</c> only the items after a moment.
    /// </summary>
    [DriverRoute("CHANNEL_READ", PipeClass.Open)]
    private async Task<object?> ChannelReadAsync(ChannelReadRequest press, CancellationToken cancellationToken)
    {
        var ask = Asked(press.Ask);
        var service = _loop.Service ?? throw NotReady();
        var since = press.Since is { } moment ? Moments.Read(moment) : null;
        var view = await service.ChannelAsync(ask, press.Entry, cancellationToken, since).ConfigureAwait(false);
        return view is null ? ChannelAnswers.Empty(ask) : ChannelAnswers.Read(view);
    }

    /// <summary>An ask's plan as the person reads it, each part with its quest; moves nothing.</summary>
    [DriverRoute("ASK_PLAN", PipeClass.Open)]
    [ByEvent("ASK_PLAN", Untold = "the service's plan rows, which `team_plan` writes and no scope or slice tells")]
    private async Task<object?> AskPlanAsync(AskPlanRead press, CancellationToken cancellationToken)
    {
        var service = _loop.Service ?? throw NotReady();
        return await service.AskPlanAsync(Asked(press.Ask), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The person's post: their words, to everyone or one of the ask's repositories (<c>to</c>), answering a message (<c>re</c>)
    /// or not. Their words, so every session on the ask reads them. Answers <c>posted</c> with the message kept, or
    /// the service's code where it refused. The channel sits under the asks' key, which it declares.
    /// </summary>
    [DriverRoute("CHANNEL_POST", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("CHANNEL_POST", Ask = "ask")]
    [GroundAsk("CHANNEL_POST", "ask")]
    [Says("CHANNEL_POST", "text:words")]
    [Publishes("CHANNEL_POST", StateScopes.Asks)]
    private async Task<object?> ChannelPostAsync(ChannelPostPress press, CancellationToken cancellationToken)
    {
        var ask = Asked(press.Ask);
        var service = _loop.Service ?? throw NotReady();
        // `ask channel <id> "…"` sent over the pipe asks for the verb's own words, its level as the verb sends it.
        if (press.Said)
        {
            var asked = new ChannelAsk(ask, press.To, press.Re, press.Text ?? "", press.Ordinary);
            return await TerminalSaid.RunAsync(output => ChannelCommand.RunAsync(asked, service, output, cancellationToken)).ConfigureAwait(false);
        }

        var posted = await service.ChannelPostAsync(
            ask, press.Text ?? "", press.To, press.Re, cancellationToken).ConfigureAwait(false);
        return ChannelAnswers.Posted(posted);
    }

    /// <summary>
    /// The person's mark moved through a moment (<c>through</c>), or the newest item: what the page has shown them. A mark never
    /// moves back. Answers the channel as it then reads, and declares the asks: every window's unread count
    /// falls with it.
    /// </summary>
    [DriverRoute("CHANNEL_MARK", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("CHANNEL_MARK", None = OwnerNone.ActsOnNothing)]
    [Timeless("CHANNEL_MARK", "a read mark, which never moves back")]
    [Publishes("CHANNEL_MARK", StateScopes.Asks)]
    private async Task<object?> ChannelMarkAsync(ChannelMarkPress press, CancellationToken cancellationToken)
    {
        var ask = Asked(press.Ask);
        var service = _loop.Service ?? throw NotReady();
        var through = press.Through is { } moment ? Moments.Read(moment) : null;
        var view = await service.ChannelMarkAsync(ask, through, cancellationToken).ConfigureAwait(false);
        return view is null ? ChannelAnswers.Empty(ask) : ChannelAnswers.Read(view);
    }

    /// <summary>
    /// The person's <i>Send to its session</i>: a message of the ask's channel
    /// (<c>message</c>) sent on to the newest part here of a line of work it addresses (<c>repository</c>), which takes it as the agent's
    /// claim passed on by the person, never their words, and which it may reopen, since the person pressed. Answers <c>sent</c> and the
    /// session, or the code that says why not (<c>no-part</c>, <c>gone</c>, <c>held</c>, or the say door's own). It moves the asks' key,
    /// whose channel's receipts change with it.
    /// </summary>
    [DriverRoute("CHANNEL_FORWARD", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("CHANNEL_FORWARD", Ask = "ask")]
    [GroundAsk("CHANNEL_FORWARD", "ask")]
    [GroundRepository("CHANNEL_FORWARD", "repository")]
    [Says("CHANNEL_FORWARD", "repository:repository")]
    [Publishes("CHANNEL_FORWARD", StateScopes.Asks)]
    private async Task<object?> ChannelForwardAsync(ChannelForwardPress press, CancellationToken cancellationToken)
    {
        var ask = Asked(press.Ask);
        var service = _loop.Service ?? throw NotReady();
        var message = press.Message?.Trim() is { Length: > 0 } id ? id : throw new DriverException("name the message to send on.");
        var repository = press.Repository?.Trim() is { Length: > 0 } named
            ? named
            : throw new DriverException("name the repository whose session the message goes to.");
        var records = await service.RecordsAsync(cancellationToken).ConfigureAwait(false);
        return ChannelAnswers.Forwarded(await ChannelReach.ForwardAsync(
            new ServiceChannelDoors(service), records, _loop.Processes, ask, repository, message, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// The asks the mirrored channel messages name that this machine does not hold: the Asks list's <i>Teammates' asks</i>.
    /// Each is read at <c>CHANNEL_READ</c> by its id; the service holds no record of it, so it has no title.
    /// </summary>
    [DriverRoute("ASKS_ELSEWHERE", PipeClass.Open)]
    private async Task<object?> AsksElsewhereAsync(NoPayload _, CancellationToken cancellationToken)
    {
        var service = _loop.Service ?? throw NotReady();
        return new ElsewhereAnswer(await service.ElsewhereAsksAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// The channel of the line a session is a part of, where that line is on no ask held here (a chat's, a quest session's, a
    /// teammate's ask's): every message on it oldest first, each to one part with what it reached in that part's line. Moves nothing.
    /// No origin where the line has none, or the host has no such door: the page then draws no channel for it.
    /// </summary>
    [DriverRoute("CHANNEL_LINE", PipeClass.Open)]
    [ByEvent("CHANNEL_LINE", Untold = "the service's messages on a line's own channel, which a session's post moves and no scope or slice tells")]
    private async Task<object?> ChannelLineAsync(LineChannelRead press, CancellationToken cancellationToken)
    {
        var session = ActField.Needs(press.Session, "session");
        var service = _loop.Service ?? throw NotReady();
        return ChannelAnswers.Line(await service.Addressed.LineAsync(session, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// The person's post on a line's own channel: their words to everyone on it or one of its repositories (<c>to</c>), which a
    /// part of the line takes at its look as it takes their words. Answers <c>posted</c> with the message kept, or the service's code
    /// where it refused. It moves the asks' key, which holds each line's channel.
    /// </summary>
    [DriverRoute("CHANNEL_LINE_POST", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("CHANNEL_LINE_POST", Session = "session")]
    [GroundSession("CHANNEL_LINE_POST", "session")]
    [Says("CHANNEL_LINE_POST", "session:session", "text:words")]
    [Publishes("CHANNEL_LINE_POST", StateScopes.Asks)]
    private async Task<object?> ChannelLinePostAsync(LineChannelPostPress press, CancellationToken cancellationToken)
    {
        var session = ActField.Needs(press.Session, "session");
        var service = _loop.Service ?? throw NotReady();
        return ChannelAnswers.Posted(await service.Addressed.PostLineAsync(session, press.Text ?? "", press.To, ct: cancellationToken).ConfigureAwait(false));
    }

    /// <summary>The ask a route is about, as the service names it.</summary>
    /// <exception cref="DriverException">None named: the driver's sentence, said verbatim.</exception>
    private static string Asked(string? named) =>
        named?.Trim().TrimStart('#') is { Length: > 0 } ask ? ask : throw new DriverException("a channel is an ask's: name the ask.");
}
