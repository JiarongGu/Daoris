using System.Text.Json.Serialization;

namespace Daoris.Driver;

/// <summary>The ask a chain names (D133, D135), from the service's ask record.</summary>
public sealed record TraceAskLink(string Id)
{
    /// <summary>Its store: <see cref="TraceStores.Asks"/>.</summary>
    public string Source => TraceStores.Asks;

    /// <summary>Why it could not be read, <see cref="TraceLinkGaps.Unread"/> or <see cref="TraceLinkGaps.NotFound"/>; null where it was.</summary>
    public string? Missing { get; init; }

    [JsonIgnore]
    public string? Problem { get; init; }

    public TraceNamedBy? NamedBy { get; init; }

    public string? Workspace { get; init; }

    /// <summary>Open, Proposed, Published or Closed, as the record spells it.</summary>
    public string? State { get; init; }

    /// <summary>Which tier answered it (D24).</summary>
    public string? Tier { get; init; }

    public TraceIntake? Intake { get; init; }

    /// <summary>The quests it became.</summary>
    public IReadOnlyList<string> Quests { get; init; } = [];

    /// <summary>What its close said, as written.</summary>
    public string? Note { get; init; }

    /// <summary>Its sentence, where the service answers no words: the person's own, as written.</summary>
    public string? Sentence { get; init; }

    /// <summary>The person's words, oldest first, verbatim; null where the service answers none.</summary>
    public IReadOnlyList<AskWordView>? Words { get; init; }

    /// <summary>For an ask from before its words were kept: from when they are.</summary>
    public DateTimeOffset? WordsKeptFrom { get; init; }

    /// <summary>Its go-aheads, oldest first; null where the service answers none.</summary>
    public IReadOnlyList<TraceGoAhead>? GoAheads { get; init; }
}

/// <summary>The intake session an ask names, and its record where the service holds one.</summary>
public sealed record TraceIntake(string Session)
{
    /// <summary>Its record's state, or null where the service holds no record of it.</summary>
    public TraceState? State { get; init; }

    public TraceAgent? Agent { get; init; }
}

/// <summary>A go-ahead on an ask (KNOWUSE1a): its act, the person's answer, and who first asked it.</summary>
/// <param name="Kind">write, release, push, sign-in or run, as the service spells it.</param>
/// <param name="On">Where it lands, in the session's words.</param>
/// <param name="Act">What it touches, in the session's words.</param>
public sealed record TraceGoAhead(int Number, string Kind, string On, string Act)
{
    /// <summary>The act in one line, as the service names it: the terminal's.</summary>
    [JsonIgnore]
    public string Named => $"{Kind} on {On}: \"{Act}\"";

    /// <summary>The person's answer, the latest; null while it waits on them.</summary>
    public GoAheadAnswerView? Answer { get; init; }

    /// <summary>The session that first asked it, and when.</summary>
    public string? FirstAskedBy { get; init; }

    public DateTimeOffset? FirstAskedAt { get; init; }

    /// <summary>The go-ahead whose words it shared, when that is why it was asked again.</summary>
    public int? Near { get; init; }
}
