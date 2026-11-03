namespace Daoris.Knowledge;

/// <summary>
/// Who said a word kept on a session (COMMS1a, the comms design §3.3), read once from the marks it carries and never stored: the
/// person, Daoris, the supervisor, a channel post, a helper, a partner, or a second opinion. Authorship comes from the door that kept
/// the word; a supplied <c>from</c>, a role's name or a quoted instruction grants nothing.
/// </summary>
/// <remarks>
/// A twin (<c>.claude/knowledge/twins.md</c>): the driver's <c>Senders</c> and the page's <c>sender.ts</c> read an event's marks by the
/// same rule, held to <c>fixtures/senders.json</c> among the service's tests, row for row.
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

    /// <summary>Every sender a kept word has, the closed set.</summary>
    public static IReadOnlyList<string> Codes { get; } = [Person, Daoris, Supervisor, Channel, Helper, Partner, Opinion, Unattributed];

    /// <summary>The sender of <paramref name="word"/>: the first mark that holds, and the person where it carries none.</summary>
    public static string Of(SaidWord word)
    {
        if (word.From is { } from)
        {
            return from switch
            {
                SaidFrom.Supervisor => Supervisor,
                SaidFrom.Daoris => Daoris,
                SaidFrom.Agent => Channel,
                _ => Unattributed,
            };
        }

        if (word.By is not null) return Opinion;
        if (word.Helper is not null) return word.Key?.StartsWith("partner:", StringComparison.Ordinal) == true ? Partner : Helper;
        return Person;
    }
}
