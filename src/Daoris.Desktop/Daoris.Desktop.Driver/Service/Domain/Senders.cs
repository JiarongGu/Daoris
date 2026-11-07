namespace Daoris.Driver;

/// <summary>
/// Who said a word, read once from the marks it carries and never stored: the person, Daoris, the
/// supervisor, a channel post, a helper, a partner, or a second opinion. The driver's twin of the service's <c>Senders</c>; both are held
/// with the page's <c>sender.ts</c> to <c>fixtures/senders.json</c> among the service's tests, row for row.
/// </summary>
/// <remarks>
/// An event carries no key, so a partner is told from a helper by the work of the record the event's <c>Helper</c> names, which
/// the caller reads and hands in; a kept word tells it by its key. A reader of an event or a message hands in the marks it carries.
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

    /// <summary>The origin a <c>user</c> event names for every word that is not the person's.</summary>
    public const string Target = "target";

    /// <summary>The partner's work, spelled as <c>ParticipantWork.Partner</c>, which this lower subdomain may not name.</summary>
    public const string PartnerWork = "partner";

    /// <summary>The sender of a word as the service keeps it.</summary>
    public static string Of(SaidWordView word) => Of(
        word.From is null && word.By is null && word.Helper is null ? null : Target, word.From, word.By, word.Helper,
        word.Key?.StartsWith("partner:", StringComparison.Ordinal) == true ? PartnerWork : null, null);

    /// <summary>
    /// The sender of a <c>user</c> event or a message from its marks: its <paramref name="origin"/>, <paramref name="from"/>, the
    /// second <paramref name="opinion"/> it carries, the session <paramref name="helper"/> whose answer it is with that record's
    /// <paramref name="helperWork"/> (null where it is not read), and its <paramref name="channel"/> message.
    /// </summary>
    public static string Of(string? origin, string? from, string? opinion, string? helper, string? helperWork, string? channel)
    {
        var target = origin == Target;
        if (from is not null)
        {
            return from switch
            {
                SaidFrom.Supervisor => Supervisor,
                SaidFrom.Daoris => target ? Daoris : Person,
                SaidFrom.Agent => Channel,
                _ => Unattributed,
            };
        }

        if (opinion is not null) return Opinion;
        if (helper is not null) return helperWork == PartnerWork ? Partner : Helper;
        if (target) return string.IsNullOrEmpty(channel) ? Composed : Channel;
        return Person;
    }
}
