using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>One claim a reviewer made (design §6.1), as the local host answers it: a claim, never a fact (§6.6).</summary>
/// <param name="Number">Its number in the pass, from 1: what an answer names it by.</param>
public sealed record OpinionFindingView(int Number, string Weight, string Where, string Claim, string Consequence, string Reproduce, string Sure)
{
    /// <summary>A diagnosis or a change proposed as text; null where none was given.</summary>
    public string? Proposal { get; init; }
}

/// <summary>
/// The working session's answer to one finding as the host keeps it (design §6.4): <c>fixed</c> with the commit it named,
/// <c>rejected</c> with its evidence, or <c>unresolved</c> with why. The session's claim: the driver checks a fix's commit.
/// </summary>
public sealed record OpinionAnswerView(int Finding, string Said)
{
    public string? Commit { get; init; }

    public string? Evidence { get; init; }

    public string? Why { get; init; }
}

/// <summary>
/// A second opinion as this machine's local host answers it (XAGENT1c's <c>GET /api/opinions/{id}</c>; design §6.2), read for
/// what the driver delivers: where its pass stands, the candidate, the reviewer, what it said, where its findings went and the
/// working session's answers. Read without trusting the shape: a part this build does not know is passed over.
/// </summary>
/// <param name="Pass"><c>first</c>, or <c>recheck</c>.</param>
/// <param name="Working">The session whose work it read.</param>
/// <param name="Session">The reviewer's own session record.</param>
/// <param name="State"><c>reading</c>, <c>given</c> or <c>failed</c>, derived by the host when read.</param>
public sealed record OpinionView(
    string Id, string Occasion, string Pass, string Working, string Session, string Repository, string Base, string Tip, string Adapter,
    string Label, string State)
{
    /// <summary>For a recheck, the first pass it reads the commits since; null for a first pass.</summary>
    public string? Rechecks { get; init; }

    /// <summary>The reviewer's declared product; null where none is declared.</summary>
    public string? Product { get; init; }

    /// <summary>The reviewer's declared maker; null where none is declared, which its label says.</summary>
    public string? Maker { get; init; }

    /// <summary>A failed pass's code: <c>ended</c> or <c>out-of-time</c>. Null otherwise.</summary>
    public string? Why { get; init; }

    /// <summary>The rule's bound of the pass, in minutes; null where the host said none.</summary>
    public int? Minutes { get; init; }

    /// <summary>The commits the candidate holds, oldest first.</summary>
    public IReadOnlyList<string> Commits { get; init; } = [];

    /// <summary>What it said, its findings in their order; null until it said its opinion.</summary>
    public IReadOnlyList<OpinionFindingView>? Findings { get; init; }

    /// <summary>What it read, said with its opinion.</summary>
    public string? Read { get; init; }

    /// <summary>What it did not read or could not tell.</summary>
    public string? Limits { get; init; }

    /// <summary>A recheck's word on each first-pass finding it named, <c>stands</c> or <c>withdrawn</c>, by its number.</summary>
    public IReadOnlyDictionary<int, string> Rechecked { get; init; } = new Dictionary<int, string>();

    /// <summary>The working session its findings went to, and the word they wait in on its record; null until handed.</summary>
    public string? HandedTo { get; init; }

    public string? HandedWord { get; init; }

    /// <summary>The working session's answers, the latest standing for each finding.</summary>
    public IReadOnlyList<OpinionAnswerView> Answers { get; init; } = [];

    /// <summary>Whether it is the first pass, the one whose findings go to the working session.</summary>
    public bool First => Pass == OpinionViews.FirstPass;

    /// <summary>Whether its reviewer said its opinion.</summary>
    public bool Given => State == OpinionViews.GivenState && Findings is not null;

    /// <summary>Who read it, as the person reads it: its product, else its adapter, and its maker where declared.</summary>
    public string Who => (Product ?? Adapter) + (Maker is { } maker ? $" ({maker})" : "");

    /// <summary>The answer standing for <paramref name="finding"/>: the latest kept, or null where none was.</summary>
    public OpinionAnswerView? AnswerTo(int finding) => Answers.LastOrDefault(answer => answer.Finding == finding);
}

/// <summary>The host's words for an opinion, and its reading (XAGENT1e).</summary>
public static class OpinionViews
{
    /// <summary>The pass that reads the candidate first.</summary>
    public const string FirstPass = "first";

    /// <summary>The one pass that reads the commits made in answer (design §6.5).</summary>
    public const string RecheckPass = "recheck";

    /// <summary>A pass still being read.</summary>
    public const string Reading = "reading";

    /// <summary>A pass whose reviewer said its opinion.</summary>
    public const string GivenState = "given";

    /// <summary>A pass that ended without an opinion, never an empty one (design §6.1).</summary>
    public const string Failed = "failed";

    /// <summary>Wrong to land as it is.</summary>
    public const string Must = "must";

    public const string Fixed = "fixed";

    public const string Rejected = "rejected";

    public const string Unresolved = "unresolved";

    /// <summary>A first-pass finding the recheck withdraws.</summary>
    public const string Withdrawn = "withdrawn";

    /// <summary>
    /// An opinion as the host answers it, or null where the answer is not one: no id, no candidate or no reviewer, which says
    /// nothing anyone can act on.
    /// </summary>
    public static OpinionView? Read(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return Read(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <inheritdoc cref="Read(string)"/>
    public static OpinionView? Read(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || Text(root, "id") is not { Length: > 0 } id || Text(root, "working") is not { } working || Text(root, "session") is not { } session
            || !root.TryGetProperty("candidate", out var candidate) || candidate.ValueKind != JsonValueKind.Object
            || Text(candidate, "repository") is not { } repository || Text(candidate, "base") is not { } @base || Text(candidate, "tip") is not { } tip
            || !root.TryGetProperty("reviewer", out var reviewer) || reviewer.ValueKind != JsonValueKind.Object
            || Text(reviewer, "adapter") is not { } adapter)
        {
            return null;
        }

        var given = root.TryGetProperty("given", out var said) && said.ValueKind == JsonValueKind.Object ? said : (JsonElement?)null;
        var handed = root.TryGetProperty("handed", out var went) && went.ValueKind == JsonValueKind.Object ? went : (JsonElement?)null;
        return new OpinionView(
            id, Text(root, "occasion") ?? "", Text(root, "pass") ?? FirstPass, working, session, repository, @base, tip, adapter,
            Text(reviewer, "label") ?? "", Text(root, "state") ?? "")
        {
            Rechecks = Text(root, "rechecks"),
            Product = Text(reviewer, "product"),
            Maker = Text(reviewer, "maker"),
            Why = Text(root, "why"),
            Minutes = Number(root, "minutes"),
            Commits = Strings(candidate, "commits"),
            Findings = given is { } opinion ? [.. Items(opinion, "findings").Select(Finding).OfType<OpinionFindingView>()] : null,
            Read = given is { } reading ? Text(reading, "read") : null,
            Limits = given is { } limits ? Text(limits, "limits") : null,
            Rechecked = given is { } rechecked
                ? Items(rechecked, "rechecked")
                    .Where(each => Number(each, "finding") is > 0 && Text(each, "says") is { Length: > 0 })
                    .GroupBy(each => Number(each, "finding")!.Value)
                    .ToDictionary(each => each.Key, each => Text(each.Last(), "says")!)
                : new Dictionary<int, string>(),
            HandedTo = handed is { } to ? Text(to, "session") : null,
            HandedWord = handed is { } word ? Text(word, "word") : null,
            Answers = [.. Items(root, "answers").Select(Answer).OfType<OpinionAnswerView>()],
        };
    }

    private static OpinionFindingView? Finding(JsonElement each) =>
        Number(each, "number") is { } number and > 0
        && Text(each, "weight") is { } weight && Text(each, "where") is { } where && Text(each, "claim") is { } claim
            ? new OpinionFindingView(
                number, weight, where, claim, Text(each, "consequence") ?? "", Text(each, "reproduce") ?? "", Text(each, "sure") ?? "")
            {
                Proposal = Text(each, "proposal"),
            }
            : null;

    private static OpinionAnswerView? Answer(JsonElement each) =>
        Number(each, "finding") is { } finding and > 0 && Text(each, "said") is { Length: > 0 } said
            ? new OpinionAnswerView(finding, said) { Commit = Text(each, "commit"), Evidence = Text(each, "evidence"), Why = Text(each, "why") }
            : null;

    private static IEnumerable<JsonElement> Items(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var items) && items.ValueKind == JsonValueKind.Array
            ? items.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object)
            : [];

    private static IReadOnlyList<string> Strings(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var items) && items.ValueKind == JsonValueKind.Array
            ? [.. items.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!)]
            : [];

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? Number(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out var number)
            ? number
            : null;

    /// <summary>A moment as the store writes it: ISO 8601, round trip.</summary>
    internal static string Moment(DateTimeOffset at) => at.ToString("O", CultureInfo.InvariantCulture);
}

/// <summary>The local host's opinion doors the driver reads and hands through (XAGENT1e).</summary>
public sealed partial class ServiceClient
{
    /// <summary>
    /// The opinion under <paramref name="id"/> as the local host answers it (XAGENT1c), with where its pass stands; null where
    /// there is none, or the host is older than the door.
    /// </summary>
    /// <exception cref="DriverException">The host refused for another reason.</exception>
    public async Task<OpinionView?> ReadOpinionAsync(string id, CancellationToken ct = default)
    {
        var url = $"{_base}/api/opinions/{Uri.EscapeDataString(id)}";
        using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode) throw new DriverException($"{url} answered {(int)response.StatusCode}: {DriverHttp.ErrorOf(payload)}");
        return OpinionViews.Read(payload);
    }

    /// <summary>
    /// Hand a first pass's findings to its working session (XAGENT1c's <c>POST /api/opinions/{id}/hand</c>; design §6.3): the
    /// host keeps them on its record as a word whose <c>by</c> names the opinion, in its fixed words, once. A refusal is an
    /// answer, the host's sentence, not an exception, and so is a host older than the door.
    /// </summary>
    public async Task<(bool Handed, string Message, OpinionView? Opinion)> HandOpinionAsync(string id, CancellationToken ct = default)
    {
        var door = $"/api/opinions/{Uri.EscapeDataString(id)}/hand";
        var (ok, status, payload, root) = await PostJsonAsync(door, "{}", ct).ConfigureAwait(false);
        if (root is not { } answer) return (false, $"the service at {_base} has no `{door}` door ({status}) — is it older than this driver?", null);
        if (!ok) return (false, Text(answer, "error") ?? payload, null);
        var opinion = answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("opinion", out var kept) ? OpinionViews.Read(kept) : null;
        return (true, Text(answer, "message") ?? "", opinion);
    }
}
