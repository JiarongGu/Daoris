using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Knowledge;

/// <summary>One change a session intends in its system: a resource given a whole new value, or removed.</summary>
public sealed record SystemPlanChange(string? Resource, string? Value, bool Remove = false);

/// <summary>
/// The connector's half of <c>system_plan</c>: each call is a keyed request in the home's session requests for the session's own
/// record, as a checkout read's is (<see cref="CheckoutBox"/>), which the driver's loop running that session takes, hands to the
/// system's adapter and answers beside with the plan it kept. Nothing here reaches the system.
/// </summary>
/// <remarks>
/// A twin, the file the contract: the request's fields are the driver's <c>SessionRequests</c> (<c>session</c>, <c>move</c>,
/// <c>at</c>, <c>by</c>, <c>key</c>, <c>text</c> holding the changes as a JSON list), and the answer's its <c>SystemPlanAnswer</c>
/// (<c>operation</c>, <c>done</c>, <c>code</c>, <c>said</c>).
/// </remarks>
/// <param name="home">The home the requests go under, or null when there is none: every call is then refused.</param>
public sealed partial class SystemPlanBox(string? home)
{
    /// <summary>The request's move in the session requests.</summary>
    public const string Move = "plan";

    /// <summary>Who asked, as the requests name a door: a session's own connector.</summary>
    public const string By = "connector";

    /// <summary>The most changes one plan carries.</summary>
    public const int Most = 500;

    private const string Nothing = " Nothing was planned.";

    public string? Home { get; } = home;

    /// <summary>How long the request waits for a loop to take it before it is withdrawn.</summary>
    public TimeSpan TakeWithin { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>How long a taken request waits for its answer: the adapter's start and its read of the system included.</summary>
    public TimeSpan AnswerWithin { get; init; } = TimeSpan.FromSeconds(45);

    /// <summary>How often the folder is looked at while waiting.</summary>
    public TimeSpan Poll { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>The home the driver names on the connector it offers, else the account's.</summary>
    public static SystemPlanBox FromEnvironment() => new(RuleProposalBox.FromEnvironment().Home);

    /// <summary>Ask the loop running the session to plan <paramref name="changes"/> against its system, and say what it kept.</summary>
    /// <param name="asker">The record of the session the connector names, or null when it names none or none is held.</param>
    /// <param name="named">The session the connector names, said back where no record is held for it.</param>
    public async Task<string> PlanAsync(Session? asker, string? named, IReadOnlyList<SystemPlanChange>? changes, DateTimeOffset at, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(named) || asker is null || !SessionId().IsMatch(asker.Id))
        {
            return "This connector speaks for no session Daoris started here, so there is no system department to plan for." + Nothing;
        }

        if (Unfit(changes) is { } unfit) return unfit + Nothing;
        if (Home is null) return char.ToUpperInvariant(DaorisHome.Sentence[0]) + DaorisHome.Sentence[1..] + Nothing;

        var (ending, text) = await HelpRequestExchange.ExchangeAsync(
            Home, asker.Id, key => HelpRequestExchange.Request(asker.Id, Move, By, key, at, writer => writer.WriteString("text", Listed(changes!))),
            TakeWithin, AnswerWithin, Poll, ct).ConfigureAwait(false);
        return ending switch
        {
            HelpRequestExchange.Ending.Untaken =>
                $"No Daoris on this machine took it within {Seconds(TakeWithin)} seconds: the loop that runs this session did not answer." + Nothing,
            HelpRequestExchange.Ending.Unanswered =>
                $"Daoris took it and did not answer within {Seconds(AnswerWithin)} seconds: the system's adapter may still be reading. Ask again once.",
            _ => HelpRequestExchange.Said(text!) ?? "Daoris answered in a shape this connector does not read, so the plan cannot be said here.",
        };
    }

    /// <summary>What the call does not name that a plan needs, or null where it names it.</summary>
    private static string? Unfit(IReadOnlyList<SystemPlanChange>? changes)
    {
        if (changes is not { Count: > 0 }) return "It names no change: a plan is one or more resources, each given a value or removed.";
        if (changes.Count > Most) return $"It names {changes.Count} changes, and one plan carries at most {Most}: plan them in parts.";
        var named = new HashSet<string>(StringComparer.Ordinal);
        foreach (var change in changes)
        {
            if (string.IsNullOrWhiteSpace(change.Resource) || change.Resource.Any(char.IsControl)) return "A change names no resource, or one holding a control character.";
            if (change.Remove == (change.Value is not null)) return $"`{change.Resource}` is given neither a value nor a removal, or both: say one.";
            if (!named.Add(change.Resource.Trim())) return $"`{change.Resource.Trim()}` is named twice: say each resource once, with the value it should end holding.";
        }

        return null;
    }

    /// <summary>The changes as the request's text: a JSON list in their order, each a value or a removal.</summary>
    private static string Listed(IReadOnlyList<SystemPlanChange> changes)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var change in changes)
            {
                writer.WriteStartObject();
                writer.WriteString("resource", change.Resource!.Trim());
                if (change.Remove) writer.WriteBoolean("remove", true);
                else writer.WriteString("value", change.Value);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static string Seconds(TimeSpan span) => Math.Round(span.TotalSeconds).ToString(CultureInfo.InvariantCulture);

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9-]{0,63}$")]
    private static partial Regex SessionId();
}
