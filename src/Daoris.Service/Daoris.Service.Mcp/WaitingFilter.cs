using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// What waits for the connector's session on its ask's channel, said as one line (COWORK1d; the message-urgency design §4.2, D188's
/// note of the owner's direction): how many messages its entry has neither had sent, taken nor read, and how many of them are urgent.
/// Said once per new message: the line is not delivery, and the agent reads when it chooses, with <c>channel_read</c>.
/// </summary>
/// <param name="channel">The channel's judgement; none where the connector has no channel.</param>
/// <param name="intake">Which session this connector speaks for, when the driver started it.</param>
public sealed class WaitingLine(ChannelDesk? channel, IntakeScope? intake)
{
    private readonly object _gate = new();

    // The messages a line already named, this connector's own: one process speaks for one session.
    private readonly HashSet<string> _told = new(StringComparer.Ordinal);

    /// <summary>The line, where a message waits that no line named yet; else null.</summary>
    public async Task<string?> NextAsync(CancellationToken ct)
    {
        if (channel is null || intake?.Session is not { Length: > 0 } session) return null;

        var (ask, waiting) = await channel.WaitingForAsync(session, ct).ConfigureAwait(false);
        if (ask is null || waiting.Count == 0) return null;
        lock (_gate)
        {
            var told = 0;
            foreach (var message in waiting) told += _told.Add(message.Id) ? 1 : 0;
            if (told == 0) return null;
        }

        var urgent = waiting.Count(message => message.Urgent == true);
        var count = waiting.Count == 1 ? "1 message waits" : $"{waiting.Count} messages wait";
        return $"Daoris: {count} for you on ask `#{ask.Id}`'s channel"
               + (urgent > 0 ? $", {urgent} of them urgent" : "")
               + ". channel_read reads them, at a point you choose between steps.";
    }
}

/// <summary>
/// Every tool's answer ends with what waits for the caller (COWORK1d, D188's note of the owner's direction): the pull an agent meets at
/// its next call to Daoris, whatever its door, with no interrupt. <c>channel_read</c>'s answer never does, since it is the read.
/// </summary>
/// <remarks>
/// It changes nothing a tool answered: the line is a block of its own after the tool's, and a line that could not be read costs the
/// line, never the answer.
/// </remarks>
public static class WaitingFilter
{
    /// <summary>The tool whose answer is the read itself.</summary>
    public const string Read = "channel_read";

    /// <summary>Each <c>tools/call</c>'s answer, then <paramref name="waiting"/>'s line after it where it says one.</summary>
    public static IMcpServerBuilder TellWhatWaits(this IMcpServerBuilder builder, Func<CancellationToken, Task<string?>> waiting) =>
        builder.WithRequestFilters(filters => filters.AddCallToolFilter(next => async (request, cancellationToken) =>
        {
            var result = await next(request, cancellationToken).ConfigureAwait(false);
            if (request.Params?.Name == Read) return result;

            string? line;
            try
            {
                line = await waiting(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                return result;
            }

            if (line is not null) result.Content.Add(new TextContentBlock { Text = line });
            return result;
        }));
}
