namespace Daoris.Driver;

/// <summary>
/// The second opinion's part of the landing gate for the chain's work in one repository (XAGENT1f, D155 points 9 and 10; the
/// second-agent design §7–§8), and why it stands so.
/// </summary>
/// <param name="State">One of <see cref="OpinionGateStates"/>.</param>
public sealed record OpinionGateState(string State, string Repository)
{
    /// <summary>The rule standing for the repository; null where nothing is set anywhere.</summary>
    public ResolvedOpinion? Rule { get; init; }

    /// <summary>The working session the door lands, which the terminal's doors name.</summary>
    public string? Session { get; init; }

    /// <summary>The commit that would land; null where it was not read.</summary>
    public string? Tip { get; init; }

    /// <summary>The chat run whose gate this is (CHATFLOW1d), whose opinion the person's press asks; null for a quest's work.</summary>
    public string? Run { get; init; }

    /// <summary>Whether a press asked the chat run's opinion and the look has not started it yet (CHATFLOW1d, §5.2 point 4).</summary>
    public bool Asked { get; init; }

    /// <summary>A later step of the chain here still to run.</summary>
    public string? Later { get; init; }

    /// <summary>Whether the quest's own hold comes first (§7 item 1): nothing is asked until the person lifts it.</summary>
    public bool Held { get; init; }

    /// <summary>The first pass the state speaks of, the newest asked; null where none was.</summary>
    public OpinionView? Opinion { get; init; }

    /// <summary>Its recheck, where one was asked.</summary>
    public OpinionView? Recheck { get; init; }

    /// <summary>The working session's answers as the driver read them when its turn ended.</summary>
    public OpinionReading? Answers { get; init; }

    /// <summary>Why the findings went to the person instead of the working session (§6.7), a code.</summary>
    public string? ToPerson { get; init; }

    /// <summary>What waits for the person (§6.6).</summary>
    public OpinionDisputes? Disputes { get; init; }

    /// <summary>Commits that would land that no other agent read; null where git could not say.</summary>
    public int? Since { get; init; }

    /// <summary>Why no opinion could be had, a code: the choice's (§3.3), or a failed pass's (<c>ended</c>, <c>out-of-time</c>).</summary>
    public string? Code { get; init; }

    /// <summary>The cool-off's reset, where that is why none could be had.</summary>
    public DateTimeOffset? Until { get; init; }

    /// <summary>The person's answer that let it go, or that a press answered with.</summary>
    public OpinionPersonWord? Person { get; init; }

    /// <summary>What the person's answer settled: the state they answered, for the landing record.</summary>
    public string? Answered { get; init; }

    /// <summary>Why it could not be read, for <see cref="OpinionGateStates.Unread"/>.</summary>
    public string? Problem { get; init; }

    /// <summary>How many passes were asked for this work, the person's own included (§8.3, §8.6).</summary>
    public int Passes { get; init; }

    /// <summary>Whether the rule requires an opinion where none can be had (§8.4).</summary>
    public bool Required => Rule?.Rule.Required == true;

    /// <summary>Whether the work may land as far as the second opinion is concerned.</summary>
    public bool LetsGo => State is OpinionGateStates.None or OpinionGateStates.Settled or OpinionGateStates.Anyway
        or OpinionGateStates.Myself or OpinionGateStates.Answered
        || (State == OpinionGateStates.Unavailable && !Required);

    /// <summary>Whether a press that shows what is unsettled answers it by being made (§8.2–§8.3): a dispute, or commits unread.</summary>
    public bool PressAnswers => State is OpinionGateStates.Disputed or OpinionGateStates.CommitsSince;

    /// <summary>What the gate says, in the driver's words: the terminal's line and every door's refusal.</summary>
    public string Says => OpinionGate.Says(this);

    /// <summary>
    /// What a press must echo to answer what it showed (§8.2): the opinion, the commit and what was unsettled, so a press made
    /// over a page drawn before the gate moved answers nothing.
    /// </summary>
    public string Token => string.Join(':', Opinion?.Id ?? "none", Tip ?? "-", State, Disputes?.Count ?? 0, Since ?? 0);

    /// <summary>What the landing record keeps of it (§8.6); null where the level asks none.</summary>
    public LandingOpinion? Landing => OpinionGate.Landing(this);
}
