using System.Globalization;
using System.Text.RegularExpressions;

namespace Daoris.Knowledge;

/// <summary>
/// Ask Daoris's reading of the checkouts, the connector's half (HELPMODE1c4, D183's note; the Ask Daoris modes design §4.1): each call
/// of <c>checkout_read</c> is a keyed request in the home's session requests for the conversation's own session, as a chosen setting's
/// is (<see cref="SettingsBox"/>), which the driver's loop running that conversation takes once, judges and answers beside it with
/// what was read. One tool for every agent, in place of the permission rules only Claude took.
/// </summary>
/// <remarks>
/// <para><b>This judges what only the connector can say</b>: that it speaks for a session, that the session's record is an Ask
/// Daoris conversation of this machine's (a driven session is handed the same connector), and that the call names what its read
/// needs. Whether the checkout is granted and was handed at this start, the conversation's workspace, the path and what git ignores
/// are the driver's, at every call, whatever the agent.</para>
///
/// <para><b>A twin, the file the contract</b> (<c>.claude/knowledge/twins.md</c>): the request's fields are the driver's
/// <c>SessionRequests</c> (<c>session</c>, <c>move</c>, <c>at</c>, <c>by</c>, <c>key</c>, <c>operation</c>, <c>repository</c>,
/// <c>path</c>), and the answer's its <c>CheckoutAnswer</c> (<c>operation</c>, <c>done</c>, <c>code</c>, <c>said</c>, <c>text</c>),
/// each side's test holding the same names.</para>
/// </remarks>
/// <param name="home">The home the requests go under, or null when there is none: every call is then refused.</param>
public sealed partial class CheckoutBox(string? home)
{
    /// <summary>A file's text, or a folder's entries: the path says which.</summary>
    public const string Read = "read";

    /// <summary><c>git status</c> of the whole checkout.</summary>
    public const string Status = "status";

    /// <summary><c>git branch --list</c>.</summary>
    public const string Branches = "branches";

    /// <summary>The reads the tool asks, and never another: never <c>git log</c>, <c>diff</c> or <c>show</c>.</summary>
    public static readonly IReadOnlyList<string> Operations = [Read, Status, Branches];

    /// <summary>The request's move in the session requests, beside the settings' <c>set</c>.</summary>
    public const string Move = "read";

    /// <summary>Who asked, as the requests name a door: Ask Daoris's tool.</summary>
    public const string By = "help";

    /// <summary>The most of what was read this side says: the driver's own bound, held here again so no answer runs past it.</summary>
    public const int Bound = 200 * 1024;

    private const string NoSession =
        "This connector speaks for no session the driver started here, so there is no Ask Daoris conversation to read a checkout for. "
        + "Nothing was read.";

    public string? Home { get; } = home;

    /// <summary>How long the request waits for a loop to take it before it is withdrawn: the settings' ten seconds.</summary>
    public TimeSpan TakeWithin { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>How long a taken request waits for its answer: under an agent's own timeout for a tool call, as the settings'.</summary>
    public TimeSpan AnswerWithin { get; init; } = TimeSpan.FromSeconds(45);

    /// <summary>How often the folder is looked at while waiting.</summary>
    public TimeSpan Poll { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>The home the driver names on the connector it offers, else the account's (D63): where the session requests live.</summary>
    public static CheckoutBox FromEnvironment() => new(RuleProposalBox.FromEnvironment().Home);

    /// <summary>
    /// Ask the loop running the conversation to read one checkout, and answer what the agent reads: the loop's words, then what was
    /// read, or why there is none.
    /// </summary>
    /// <param name="asker">The record of the session the connector names, or null when it names none or none is held.</param>
    /// <param name="named">The session the connector names, said back where no record is held for it.</param>
    /// <param name="operation">One of <see cref="Operations"/>.</param>
    /// <param name="repository">The checkout's repository, by its registered name.</param>
    /// <param name="path">For a read, a path relative to the checkout; none for its top, and for the other two.</param>
    public async Task<string> ReadAsync(
        Session? asker, string? named, string operation, string? repository, string? path, DateTimeOffset at, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(named)) return NoSession;
        if (asker is not { Origin: null } || !SessionLedger.IsHelp(asker) || !SessionId().IsMatch(asker.Id))
        {
            return $"Reading the checkouts through this tool is Ask Daoris's alone, and session `{named.Trim()}` is no Ask Daoris conversation "
                   + "of this machine's. Nothing was read.";
        }

        var target = repository?.Trim() ?? "";
        var where = string.IsNullOrWhiteSpace(path) ? null : path.Trim();
        if (Unfit(operation, target, where) is { } unfit) return $"{unfit} Nothing was read.";
        if (Home is null) return $"{Capital(DaorisHome.Sentence)} Nothing was read.";

        var (ending, text) = await HelpRequestExchange.ExchangeAsync(
            Home, asker.Id, key => HelpRequestExchange.Request(asker.Id, Move, By, key, at, writer =>
            {
                writer.WriteString("operation", operation);
                writer.WriteString("repository", target);
                if (where is not null) writer.WriteString("path", where);
            }),
            TakeWithin, AnswerWithin, Poll, ct).ConfigureAwait(false);
        return ending switch
        {
            HelpRequestExchange.Ending.Untaken =>
                $"No Daoris on this machine took it within {Seconds(TakeWithin)} seconds: the loop that runs this conversation did not "
                + "answer. Nothing was read.",
            HelpRequestExchange.Ending.Unanswered =>
                $"Daoris took it and did not answer within {Seconds(AnswerWithin)} seconds. Ask again once, or say what you could not read.",
            _ => Said(text!),
        };
    }

    /// <summary>The loop's words, then what was read, bounded; or why the answer does not read.</summary>
    private static string Said(string answer)
    {
        if (HelpRequestExchange.Said(answer) is not { } said)
        {
            return "Daoris answered in a shape this connector does not read, so what was read cannot be said here.";
        }

        if (HelpRequestExchange.Text(answer) is not { } read) return said;
        return read.Length <= Bound
            ? $"{said}\n\n{read}"
            : $"{said}\n\n{read[..Bound]}\n\n(cut here: past 200 KB, the rest is not said)";
    }

    /// <summary>What the read needs and the call does not name, or null where it names it.</summary>
    private static string? Unfit(string operation, string repository, string? path)
    {
        if (!Operations.Contains(operation)) return $"`{operation}` is no read of a checkout: it is `read`, `status` or `branches`.";
        if (repository.Length == 0) return "It names no repository: a checkout is read by its repository's registered name.";
        if (repository.Any(c => char.IsWhiteSpace(c) || char.IsControl(c))) return $"`{repository}` is no repository's name: it is one word.";
        if (path is not null && path.Any(char.IsControl)) return "The path holds a control character, so it is no path.";
        return operation != Read && path is not null
            ? $"`{operation}` answers for the whole checkout and names no path; `read` takes one."
            : null;
    }

    private static string Seconds(TimeSpan span) => Math.Round(span.TotalSeconds).ToString(CultureInfo.InvariantCulture);

    private static string Capital(string sentence) =>
        sentence.Length == 0 ? sentence : char.ToUpperInvariant(sentence[0]) + sentence[1..];

    // A session id of this machine's names a file under the home; anything else names none (the driver's IsLocalId judges again).
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9-]{0,63}$")]
    private static partial Regex SessionId();
}
