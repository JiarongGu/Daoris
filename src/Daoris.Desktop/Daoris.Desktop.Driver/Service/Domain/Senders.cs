namespace Daoris.Driver;

/// <summary>
/// Who said a word (COMMS1a, the comms design §3.3), read once from the marks it carries and never stored: the person, Daoris, the
/// supervisor, a channel post, a helper, a partner, or a second opinion. The driver's twin of the service's <c>Senders</c>; both are held
/// with the page's <c>sender.ts</c> to <c>fixtures/senders.json</c> among the service's tests, row for row.
/// </summary>
/// <remarks>
/// An event carries no key (<see cref="SessionEvent"/>), so a partner is told from a helper by the work of the record its
/// <see cref="SessionEvent.Helper"/> names (B11), which the caller reads and hands in; a kept word tells it by its key.
/// </remarks>
public static class Senders
{
    public const string Person = "person";
    public const string Daoris = "daoris";
    public const string Supervisor = "supervisor";
    public const string Channel = "channel";
    public const string Helper = "helper";
    public const string Partner = "partner";
    public const string Opinion = "opinion";
    public const string Unattributed = "unattributed";

    /// <summary>An event's sender where only the composed prompt carries no mark of one: the target Daoris composed.</summary>
    public const string Composed = "composed";

    private const string Target = "target";

    // The partner's work, spelled as ParticipantWork.Partner, which this lower subdomain may not name.
    private const string PartnerWork = "partner";

    /// <summary>The sender of a word as the service keeps it.</summary>
    public static string Of(SaidWordView word) => Read(
        word.From is null && word.By is null && word.Helper is null ? null : Target, word.From, word.By, word.Helper,
        word.Key?.StartsWith("partner:", StringComparison.Ordinal) == true ? PartnerWork : null, null);

    /// <summary>The sender of a <c>user</c> event; <paramref name="helperWork"/> is the work of the record <see cref="SessionEvent.Helper"/> names, or null where it is not read.</summary>
    public static string Of(SessionEvent sent, string? helperWork) =>
        Read(sent.Origin, sent.From, sent.Opinion, sent.Helper, helperWork, sent.Channel);

    /// <summary>The sender of a message, as <see cref="ChatMessage.Words"/> records it.</summary>
    internal static string Of(ChatMessage message) => Read(
        message.Daoris is not null || message.Supervised is not null || message.Helped is not null || message.Channel is { Person: false } ? Target : null,
        message.Daoris is not null ? DaorisWords.From : message.Supervised is not null ? SupervisorHanded.From : null,
        null, message.Helped?.Helper, null, message.Channel is { Person: false } ? "channel" : null);

    private static string Read(string? origin, string? from, string? opinion, string? helper, string? helperWork, string? channel)
    {
        var target = origin == Target;
        if (from is not null)
        {
            return from switch
            {
                SaidFrom.Supervisor => Supervisor,
                SaidFrom.Daoris => target ? Daoris : Person,
                ChannelFrom.Agent => Channel,
                _ => Unattributed,
            };
        }

        if (opinion is not null) return Opinion;
        if (helper is not null) return helperWork == PartnerWork ? Partner : Helper;
        if (target) return string.IsNullOrEmpty(channel) ? Composed : Channel;
        return Person;
    }
}
