using System.Globalization;
using Daoris.Contracts.Review;
using static Daoris.Driver.Json;

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
    string Id, string Occasion, string Pass, string Working, string Session, string Repository, string Base, string Tip, string Door,
    string Label, string State)
{
    /// <summary>For a recheck, the first pass it reads the commits since; null for a first pass.</summary>
    public string? Rechecks { get; init; }

    /// <summary>The reviewer's declared product; null where none is declared.</summary>
    public string? Product { get; init; }

    /// <summary>The reviewer's declared maker; null where none is declared, which its label says.</summary>
    public string? Maker { get; init; }

    /// <summary>A failed pass's code: <c>ended</c>, <c>out-of-time</c> or <c>limited</c> (LIMITRESUME1h). Null otherwise.</summary>
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
    public string Who => (Product ?? Door) + (Maker is { } maker ? $" ({maker})" : "");

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

    /// <summary>
    /// A failed pass's code where an account's limit ended its session (LIMITRESUME1h, D206 design §3.8): the host's <c>Opinions.Limited</c>,
    /// a twin by its spelling.
    /// </summary>
    public const string Limited = "limited";

    /// <summary>A failed pass's code where its provider kept failing through the retry's bound (AGENTERRORS1d): the host's <c>Opinions.Transient</c>, which the gate reads as its own unavailable code.</summary>
    public const string Transient = "transient";

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
    public static OpinionView? Read(OpinionResponse? root)
    {
        if (root is not { Id.Length: > 0, Working: not null, Session: not null, Candidate: { Repository: { } repository, Base: { } @base, Tip: { } tip }, Reviewer.Door: { } door })
        {
            return null;
        }

        var given = root.Given;
        return new OpinionView(
            root.Id, root.Occasion ?? "", root.Pass ?? FirstPass, root.Working, root.Session, repository, @base, tip, door,
            root.Reviewer.Label ?? "", root.State ?? "")
        {
            Rechecks = root.Rechecks,
            Product = root.Reviewer.Product,
            Maker = root.Reviewer.Maker,
            Why = root.Why,
            Minutes = root.Minutes,
            // Empty strings are kept here, as the copy this replaced kept them.
            Commits = [.. (root.Candidate.Commits ?? []).OfType<string>()],
            Findings = given is null ? null : [.. (given.Findings ?? []).Select(Finding).OfType<OpinionFindingView>()],
            Read = given?.Read,
            Limits = given?.Limits,
            Rechecked = given is null
                ? new Dictionary<int, string>()
                : (given.Rechecked ?? [])
                    .Where(each => each.Finding > 0 && each.Says is { Length: > 0 })
                    .GroupBy(each => each.Finding)
                    .ToDictionary(each => each.Key, each => each.Last().Says),
            HandedTo = root.Handed?.Session,
            HandedWord = root.Handed?.Word,
            Answers = [.. (root.Answers ?? []).Select(Answer).OfType<OpinionAnswerView>()],
        };
    }

    private static OpinionFindingView? Finding(OpinionFindingResponse each) =>
        each is { Number: > 0, Weight: not null, Where: not null, Claim: not null }
            ? new OpinionFindingView(each.Number, each.Weight, each.Where, each.Claim, each.Consequence ?? "", each.Reproduce ?? "", each.Sure ?? "")
            {
                Proposal = each.Proposal,
            }
            : null;

    private static OpinionAnswerView? Answer(OpinionAnswerResponse each) =>
        each is { Finding: > 0, Said: { Length: > 0 } }
            ? new OpinionAnswerView(each.Finding, each.Said) { Commit = each.Commit, Evidence = each.Evidence, Why = each.Why }
            : null;

    /// <summary>A moment as the store writes it: ISO 8601, round trip.</summary>
    internal static string Moment(DateTimeOffset at) => Moments.Write(at);
}
