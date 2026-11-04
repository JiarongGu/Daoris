namespace Daoris.Driver;

/// <summary>
/// A prompt's way to the wire: entered before its words are marked on their record, admitted under the write lock as it is written and
/// gone from the door then, or turned away once the run's door has shut, its words handed back. Read and changed under
/// <see cref="OpenPrompts"/>' gate, never outside it.
/// </summary>
internal sealed class PromptPassage(IReadOnlyList<IAgentWord> words)
{
    /// <summary>The person's words it carries, which go back to the driver where it is turned away.</summary>
    public IReadOnlyList<IAgentWord> Words { get; set; } = words;

    public bool TurnedAway { get; set; }
}
