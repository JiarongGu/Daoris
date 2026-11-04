namespace Daoris.Driver;

/// <summary>
/// One prompt on the wire: the person's words, or null for the driver's own, and what the session has done with them. Its flags are
/// read and written under <see cref="OpenPrompts"/>' gate, never outside it.
/// </summary>
internal sealed class OpenPrompt(IAgentWord? words, IReadOnlyList<IAgentWord>? together = null)
{
    public IAgentWord? Words { get; } = words;

    /// <summary>The words a transient try carries after <see cref="Words"/> in the same prompt, in the order said.</summary>
    public IReadOnlyList<IAgentWord> Together { get; } = together ?? [];

    /// <summary>Every word this prompt carries, <see cref="Words"/> first.</summary>
    public IEnumerable<IAgentWord> All => Words is null ? Together : [Words, .. Together];

    /// <summary>Whether the session has taken its words: set once, by whichever take comes first.</summary>
    public bool Taken { get; set; }

    /// <summary>
    /// Whether an update of this prompt's turn came while it was the oldest open: what makes a word whose prompt then met an error taken,
    /// where none makes it unacknowledged. Read from the turn, never from <see cref="Taken"/> alone.
    /// </summary>
    public bool Heard { get; set; }

    /// <summary>Whether it is the run's first prompt: a resume's blocks, or a start's target.</summary>
    public bool Opening { get; init; }

    /// <summary>
    /// Whether its write returned: on the wire, so after an episode opened, a prompt whose stream is lost before any answer is uncertain,
    /// never unreached.
    /// </summary>
    public bool Written { get; set; }
}
