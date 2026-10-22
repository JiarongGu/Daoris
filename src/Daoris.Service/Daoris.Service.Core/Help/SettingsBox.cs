using System.Globalization;
using System.Text.RegularExpressions;

namespace Daoris.Knowledge;

/// <summary>
/// Ask Daoris's chosen settings, the connector's half (HELPMODE1c3, D183's note; the Ask Daoris modes design §4.1): each tool's call
/// is a keyed request in the home's session requests for the conversation's own session, as a housekeeping call is
/// (<see cref="HousekeepingBox"/>), which the driver's loop running that conversation takes once, judges, applies through the
/// setting's own checks and writer, and answers beside it.
/// </summary>
/// <remarks>
/// <para><b>This judges what only the connector can say</b>: that it speaks for a session, that the session's record is an Ask
/// Daoris conversation of this machine's (a driven session is handed the same connector), and that the call names what its
/// setting needs. Whether the person granted the setting, whether it was handed at this start, the conversation's workspace and
/// the value itself are the driver's, at every call, whatever the agent.</para>
///
/// <para><b>A twin, the file the contract</b> (<c>.claude/knowledge/twins.md</c>): the request's fields are the driver's
/// <c>SessionRequests</c> (<c>session</c>, <c>move</c>, <c>at</c>, <c>by</c>, <c>key</c>, <c>operation</c>, <c>value</c>,
/// <c>repository</c>, <c>workspace</c>), and the answer's those of housekeeping's (<c>operation</c>, <c>done</c>, <c>code</c>,
/// <c>said</c>), each side's test holding the same names.</para>
/// </remarks>
/// <param name="home">The home the requests go under, or null when there is none: every call is then refused.</param>
public sealed partial class SettingsBox(string? home)
{
    /// <summary>Whether to say so when a session parks, or ends without being asked: <c>on</c> or <c>off</c>.</summary>
    public const string Notify = "notify";

    /// <summary>The language a repository's or a workspace's sessions write to the person in: <c>en</c>, <c>zh</c>, or <c>clear</c>.</summary>
    public const string Language = "language";

    /// <summary>The retired setting of how many minutes one session may run (SESSWATCH1a, D200): no request of it is written.</summary>
    public const string Timeout = "timeout";

    /// <summary>
    /// What the retired <c>timeout</c> door and <c>settings_timeout</c> answer (SESSWATCH1a, D200, the session-watch design §3): the twin of
    /// the driver's <c>DriverChoicesCommand.TimeoutRetired</c>, word for word.
    /// </summary>
    public const string TimeoutRetired =
        "sessions are no longer limited by time: a working session runs as long as it works, and Daoris looks in on one that has gone "
        + "quiet and brings it to you; nothing was changed.";

    /// <summary>How many sessions run at once on this machine.</summary>
    public const string Cap = "cap";

    /// <summary>How many minutes an account whose limit names no time cools before it is offered again.</summary>
    public const string CoolOff = "cooloff";

    /// <summary>The settings these tools change, in the grant's order: the five ordinary ones, and never another.</summary>
    public static readonly IReadOnlyList<string> Settings = [Notify, Language, Cap, CoolOff];

    /// <summary>The request's move in the session requests, beside housekeeping's.</summary>
    public const string Move = "set";

    /// <summary>Who asked, as the requests name a door: Ask Daoris's tool.</summary>
    public const string By = "help";

    /// <summary>A language's value that takes the repository's or the workspace's own back.</summary>
    public const string Clear = "clear";

    private const string NoSession =
        "This connector speaks for no session the driver started here, so there is no Ask Daoris conversation to change a setting "
        + "for. Nothing was changed.";

    public string? Home { get; } = home;

    /// <summary>How long the request waits for a loop to take it before it is withdrawn: housekeeping's ten seconds.</summary>
    public TimeSpan TakeWithin { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>How long a taken request waits for its answer: under an agent's own timeout for a tool call, as housekeeping's.</summary>
    public TimeSpan AnswerWithin { get; init; } = TimeSpan.FromSeconds(45);

    /// <summary>How often the folder is looked at while waiting.</summary>
    public TimeSpan Poll { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>The home the driver names on the connector it offers, else the account's (D63): where the session requests live.</summary>
    public static SettingsBox FromEnvironment() => new(RuleProposalBox.FromEnvironment().Home);

    /// <summary>
    /// Ask the loop running the conversation to change one setting, and answer the sentence the agent reads: the loop's answer, or
    /// why there is none.
    /// </summary>
    /// <param name="asker">The record of the session the connector names, or null when it names none or none is held.</param>
    /// <param name="named">The session the connector names, said back where no record is held for it.</param>
    /// <param name="setting">One of <see cref="Settings"/>.</param>
    /// <param name="value">What it is set to, as the driver's door spells it.</param>
    /// <param name="repository">A language's repository, by its registered name; null for every other setting.</param>
    /// <param name="workspace">A language's workspace; null for every other setting.</param>
    public async Task<string> SetAsync(
        Session? asker, string? named, string setting, string? value, string? repository, string? workspace, DateTimeOffset at,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(named)) return NoSession;
        if (asker is not { Origin: null } || !SessionLedger.IsHelp(asker) || !SessionId().IsMatch(asker.Id))
        {
            return $"Its settings are Ask Daoris's alone, and session `{named.Trim()}` is no Ask Daoris conversation of this machine's. "
                   + "Nothing was changed.";
        }

        var set = value?.Trim() ?? "";
        var target = string.IsNullOrWhiteSpace(repository) ? null : repository.Trim();
        var circle = string.IsNullOrWhiteSpace(workspace) ? null : workspace.Trim();
        if (Unfit(setting, set, target, circle) is { } unfit) return $"{unfit} Nothing was changed.";
        if (Home is null) return $"{Capital(DaorisHome.Sentence)} Nothing was changed.";

        var (ending, text) = await HelpRequestExchange.ExchangeAsync(
            Home, asker.Id, key => HelpRequestExchange.Request(asker.Id, Move, By, key, at, writer =>
            {
                writer.WriteString("operation", setting);
                writer.WriteString("value", set);
                if (target is not null) writer.WriteString("repository", target);
                if (circle is not null) writer.WriteString("workspace", circle);
            }),
            TakeWithin, AnswerWithin, Poll, ct).ConfigureAwait(false);
        return ending switch
        {
            HelpRequestExchange.Ending.Untaken =>
                $"No Daoris on this machine took it within {Seconds(TakeWithin)} seconds: the loop that runs this conversation did not "
                + "answer. Nothing was changed.",
            HelpRequestExchange.Ending.Unanswered =>
                $"Daoris took it and did not answer within {Seconds(AnswerWithin)} seconds, so it may still be under way. Ask the person "
                + "to look in Settings before you change it again.",
            _ => HelpRequestExchange.Said(text!)
                 ?? "Daoris answered in a shape this connector does not read, so what was changed cannot be said here. Ask the person to "
                 + "look in Settings.",
        };
    }

    /// <summary>What the setting needs and the call does not name, or null where it names it.</summary>
    private static string? Unfit(string setting, string value, string? repository, string? workspace)
    {
        if (!Settings.Contains(setting)) return $"`{setting}` is no setting these tools change.";
        if (value.Length == 0) return $"A change of `{setting}` names what it is set to.";
        if (value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c))) return $"`{value}` is no value of `{setting}`: it is one word.";
        if (setting == Language)
        {
            return (repository is null) == (workspace is null)
                ? "A session's language is set for a repository or a workspace: name exactly one."
                : null;
        }

        return repository is null && workspace is null ? null : $"`{setting}` is this machine's, for every repository: it names none.";
    }

    private static string Seconds(TimeSpan span) => Math.Round(span.TotalSeconds).ToString(CultureInfo.InvariantCulture);

    private static string Capital(string sentence) =>
        sentence.Length == 0 ? sentence : char.ToUpperInvariant(sentence[0]) + sentence[1..];

    // A session id of this machine's names a file under the home; anything else names none (the driver's IsLocalId judges again).
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9-]{0,63}$")]
    private static partial Regex SessionId();
}
