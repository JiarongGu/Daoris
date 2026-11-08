namespace Daoris.Driver;

/// <summary>
/// The second opinion's part of the landing gate for the chain's work in one repository, and why it stands so.
/// </summary>
/// <param name="State">One of <see cref="OpinionGateStates"/>.</param>
public sealed record OpinionGateState(string State, string Repository)
{
    /// <summary>The rule standing for the repository; null where nothing is set anywhere.</summary>
    public ResolvedOpinion? Rule { get; init; }

    /// <summary>The working session the door lands, which the terminal's doors name.</summary>
    public string? Session { get; init; }

    /// <summary>The commit that would land; null where it is not read.</summary>
    public string? Tip { get; init; }

    /// <summary>The chat run whose gate this is, whose opinion the person's press asks; null for a quest's work.</summary>
    public string? Run { get; init; }

    /// <summary>Whether a press asked the chat run's opinion and the look has not started it yet.</summary>
    public bool Asked { get; init; }

    /// <summary>A later step of the chain here still to run.</summary>
    public string? Later { get; init; }

    /// <summary>Whether the quest's own hold comes first: nothing is asked until the person lifts it.</summary>
    public bool Held { get; init; }

    /// <summary>The first pass the state speaks of, the newest asked; null where none is.</summary>
    public OpinionView? Opinion { get; init; }

    /// <summary>Its recheck, where one is asked.</summary>
    public OpinionView? Recheck { get; init; }

    /// <summary>The working session's answers as the driver read them when its turn ended.</summary>
    public OpinionReading? Answers { get; init; }

    /// <summary>Why the findings go to the person instead of the working session, a code.</summary>
    public string? ToPerson { get; init; }

    /// <summary>When they went there, where it was kept: a <i>Reviewed</i> given before it cannot have seen them.</summary>
    public DateTimeOffset? ToPersonAt { get; init; }

    /// <summary>What waits for the person.</summary>
    public OpinionDisputes? Disputes { get; init; }

    /// <summary>Commits that would land that no other agent read; null where git could not say.</summary>
    public int? Since { get; init; }

    /// <summary>Why no opinion could be had, a code: the choice's, or a failed pass's (<c>ended</c>, <c>out-of-time</c>).</summary>
    public string? Code { get; init; }

    /// <summary>The cool-off's reset, where that is why none could be had.</summary>
    public DateTimeOffset? Until { get; init; }

    /// <summary>The person's answer that let it go, or that a press answered with.</summary>
    public OpinionPersonWord? Person { get; init; }

    /// <summary>What the person's answer settled: the state they answered, for the landing record.</summary>
    public string? Answered { get; init; }

    /// <summary>Why it could not be read, for <see cref="OpinionGateStates.Unread"/>.</summary>
    public string? Problem { get; init; }

    /// <summary>How many passes were asked for this work, the person's own included.</summary>
    public int Passes { get; init; }

    /// <summary>Whether the rule requires an opinion where none can be had.</summary>
    public bool Required => Rule?.Rule.Required == true;

    /// <summary>Whether the work may land as far as the second opinion is concerned.</summary>
    public bool LetsGo => State is OpinionGateStates.None or OpinionGateStates.Settled or OpinionGateStates.Anyway
        or OpinionGateStates.Myself or OpinionGateStates.Answered
        || (State == OpinionGateStates.Unavailable && !Required);

    /// <summary>Whether a press that shows what is unsettled answers it by being made: a dispute, or commits unread.</summary>
    public bool PressAnswers => State is OpinionGateStates.Disputed or OpinionGateStates.CommitsSince;

    /// <summary>What the gate says, in the driver's words: the terminal's line and every door's refusal.</summary>
    public string Says => OpinionGate.Says(this);

    /// <summary>
    /// What a press must echo to answer what it showed: the opinion, the commit and what is unsettled, so a press made
    /// over a page drawn before the gate moved answers nothing.
    /// </summary>
    public string Token => string.Join(':', Opinion?.Id ?? "none", Tip ?? "-", State, Disputes?.Count ?? 0, Since ?? 0);

    /// <summary>What the landing record keeps of it; null where the level asks none.</summary>
    public LandingOpinion? Landing => OpinionGate.Landing(this);
}
