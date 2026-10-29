namespace Daoris.Driver;

/// <summary>A member of a candidate as the judgement names it: its branch (the word a hand-back and a record call it by) and the tip its request froze.</summary>
internal sealed record QueueMember(string Branch, string Tip = "");

/// <summary>A member handed back, with how (<c>pinned</c>, <c>pinned with</c> or <c>unresolved</c>), the failure it is handed back for, the members it needs and the ones it interacts with, and why where it is unresolved.</summary>
internal sealed record HandedBackMember(QueueMember Member, string How, string Failure, IReadOnlyList<string> With, IReadOnlyList<string> Interacting, string? Why = null);

/// <summary>One failure with how it was judged.</summary>
internal sealed record JudgedEntry(JudgedFailure Failure, Judged Judged);

/// <summary>A failure that did not fail again on its gate's confirm run, kept on its unit apart from the known red and the flakes: a sighting, never a pass.</summary>
internal sealed record Sighting(JudgedFailure Failure, ConfirmReading? Confirm);

/// <summary>What a candidate does next.</summary>
internal enum CandidateAction
{
    /// <summary>Every failure is inherited (or none): promoted as it stands.</summary>
    Green,

    /// <summary>The owner chose <c>land</c> for an unconfirmed failure: the confirm run's pass is the verdict of the tip alone, naming the gates it passed on.</summary>
    Land,

    /// <summary>An unconfirmed failure under <c>hold</c>: the candidate waits for a decide.</summary>
    Hold,

    /// <summary>The members one at a time.</summary>
    Split,

    /// <summary>Members handed back, the rest formed again.</summary>
    HandBack,

    /// <summary>The line's integration stops.</summary>
    Stop,

    /// <summary>The candidate was dropped.</summary>
    Dropped,
}

internal static class CandidateActionWords
{
    /// <summary>The action as the merge queue's records spell it.</summary>
    internal static string Word(this CandidateAction action) => action switch
    {
        CandidateAction.Green => "green",
        CandidateAction.Land => "land",
        CandidateAction.Hold => "hold",
        CandidateAction.Split => "split",
        CandidateAction.HandBack => "hand back",
        CandidateAction.Stop => "stop",
        _ => "dropped",
    };
}

/// <summary>What a candidate does with its failures' judgements.</summary>
internal sealed record CandidateOutcome(CandidateAction Action)
{
    /// <summary>The failure that stops the line (<see cref="CandidateAction.Stop"/>).</summary>
    public JudgedFailure? Failure { get; init; }

    public string? Why { get; init; }

    public string? Said { get; init; }

    /// <summary>What a held candidate waits for: <c>decide</c>.</summary>
    public string? On { get; init; }

    /// <summary>The gates whose only pass was the confirm run (<see cref="CandidateAction.Land"/>).</summary>
    public IReadOnlyList<string> PassedOnConfirm { get; init; } = [];

    public IReadOnlyList<JudgedFailure> Inherited { get; init; } = [];

    public IReadOnlyList<JudgedFailure> Flakes { get; init; } = [];

    public IReadOnlyList<Sighting> Sightings { get; init; } = [];

    public IReadOnlyList<JudgedFailure> Inconclusive { get; init; } = [];

    public IReadOnlyList<HandedBackMember> HandedBack { get; init; } = [];

    /// <summary>The members kept to be judged again, or run one at a time under <see cref="CandidateAction.Split"/>.</summary>
    public IReadOnlyList<QueueMember> Retain { get; init; } = [];
}

/// <summary>
/// What a red candidate does with its failures' judgements (INTEGRATE1f part f1; the integration design §4.5, §5.3): <c>stop</c> when any stops
/// the line; else the members handed back (pinned, pinned with, or, for a candidate of one, unresolved), the inherited failures to file, and
/// either the members kept to be judged again or, where an unresolved failure left a candidate of several, its members one at a time
/// (<c>split</c>). A pass on an unresolved failure's re-run never makes the candidate green.
/// </summary>
/// <remarks>
/// <para>The confirm run's readings, by the landing rule's <c>integrate.unconfirmed</c> (resolved by the caller and handed in as a word;
/// anything else is <c>split</c>, and Review reads no file for it):</para>
/// <list type="bullet">
/// <item><c>dropped</c>, before anything: the candidate's job ended, and nothing is handed back, filed or kept.</item>
/// <item><c>inconclusive</c> reads as an unresolved failure whatever the policy says, never <c>land</c>, and is listed apart.</item>
/// <item><c>unconfirmed</c> is a sighting, never a flake and never a pass, so no candidate it touches is <c>green</c>. Under <c>split</c> it is an
/// unresolved failure: several members run one at a time, one of one is handed back unresolved. Where nothing splits the candidate and a
/// member was handed back, the rest are formed again and judged on their own tree. Otherwise, under <c>hold</c>, the candidate waits for a
/// decide; under <c>land</c>, the confirm run's pass is the gate's verdict on that tip alone, naming the gates it passed on.</item>
/// </list>
/// <b>A twin</b> of <c>candidateOutcome</c> in <c>tools/merge-queue.mjs</c>, held to <c>fixtures/failure-judging.json</c>' outcomes
/// (<c>FailureJudgeTests</c>, <c>integration-judge.test.ts</c>).
/// </remarks>
internal static class CandidateJudgement
{
    /// <summary>The words <c>integrate.unconfirmed</c> may be; the first is the default and every unknown word's reading.</summary>
    internal static readonly IReadOnlyList<string> UnconfirmedPolicies = ["split", "hold", "land"];

    /// <summary>A candidate's outcome from its failures' judgements, by the policy for an unconfirmed failure.</summary>
    public static CandidateOutcome Of(IReadOnlyList<JudgedEntry> judgements, IReadOnlyList<QueueMember> members, string? unconfirmed = null)
    {
        var policy = unconfirmed is not null && UnconfirmedPolicies.Contains(unconfirmed) ? unconfirmed : "split";
        if (judgements.Any(each => each.Judged.Outcome == JudgeOutcome.Dropped)) return new CandidateOutcome(CandidateAction.Dropped);
        if (judgements.FirstOrDefault(each => each.Judged.Outcome == JudgeOutcome.Stop) is { } stop)
        {
            return new CandidateOutcome(CandidateAction.Stop) { Failure = stop.Failure, Why = stop.Judged.Why, Said = stop.Judged.Said };
        }

        var inherited = judgements.Where(each => each.Judged.Outcome == JudgeOutcome.Inherited).Select(each => each.Failure).ToList();
        var sightings = judgements.Where(each => each.Judged.Outcome == JudgeOutcome.Unconfirmed).Select(each => new Sighting(each.Failure, each.Judged.Confirm)).ToList();
        var inconclusive = judgements.Where(each => each.Judged.Outcome == JudgeOutcome.Inconclusive).ToList();
        var back = new List<HandedBackMember>();
        string? Branch(int number) => number >= 1 && number <= members.Count ? members[number - 1].Branch : null;
        foreach (var each in judgements.Where(each => each.Judged.Outcome == JudgeOutcome.Pinned))
        {
            var number = each.Judged.Member ?? 0;
            if (number < 1 || number > members.Count || back.Any(known => known.Member.Branch == members[number - 1].Branch)) continue;
            back.Add(new HandedBackMember(
                members[number - 1], each.Judged.With.Count > 0 ? "pinned with" : "pinned", each.Failure.Label,
                [.. each.Judged.With.Select(Branch).OfType<string>()], [.. each.Judged.Interacting.Select(Branch).OfType<string>()]));
        }

        var unresolved = judgements.Where(each => each.Judged.Outcome == JudgeOutcome.Unresolved).ToList();
        var flakes = unresolved.Select(each => each.Failure).ToList();
        var reads = sightings;
        var readInconclusive = inconclusive.Select(each => each.Failure).ToList();
        // Read as unresolved: the prefixes' own, an inconclusive confirm run under any policy, and a sighting under `split`.
        var splitting = new List<JudgedEntry>([.. unresolved, .. inconclusive, .. policy == "split" ? judgements.Where(each => each.Judged.Outcome == JudgeOutcome.Unconfirmed) : []]);
        if (splitting.Count > 0 && members.Count == 1 && back.Count == 0)
        {
            return new CandidateOutcome(CandidateAction.HandBack)
            {
                Inherited = inherited, Flakes = flakes, Sightings = reads, Inconclusive = readInconclusive, Retain = [],
                HandedBack =
                [
                    new HandedBackMember(
                        members[0], "unresolved", string.Join(", ", splitting.Select(each => each.Failure.Label)), [], [],
                        string.Join("; ", splitting.Select(each => each.Judged.Why ?? ""))),
                ],
            };
        }

        var kept = members.Where(member => !back.Any(known => known.Member.Branch == member.Branch)).ToList();
        if (splitting.Count > 0)
        {
            return new CandidateOutcome(CandidateAction.Split) { Inherited = inherited, Flakes = flakes, Sightings = reads, Inconclusive = readInconclusive, HandedBack = back, Retain = kept };
        }

        if (sightings.Count > 0 && back.Count == 0)
        {
            if (policy == "hold")
            {
                return new CandidateOutcome(CandidateAction.Hold)
                {
                    On = "decide",
                    Why = $"an unconfirmed failure holds the candidate for a decide: {string.Join(", ", sightings.Select(each => each.Failure.Label))}",
                    Inherited = inherited, Flakes = flakes, Sightings = reads, Inconclusive = readInconclusive, Retain = [.. members],
                };
            }

            return new CandidateOutcome(CandidateAction.Land)
            {
                PassedOnConfirm = [.. sightings.Select(each => each.Failure.Gate).Distinct(StringComparer.Ordinal)],
                Inherited = inherited, Flakes = flakes, Sightings = reads, Inconclusive = readInconclusive, Retain = [.. members],
            };
        }

        return new CandidateOutcome(back.Count > 0 ? CandidateAction.HandBack : CandidateAction.Green)
        {
            Inherited = inherited, Sightings = reads, Inconclusive = readInconclusive, HandedBack = back, Retain = back.Count > 0 ? kept : [.. members],
        };
    }
}
