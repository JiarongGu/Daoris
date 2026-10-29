namespace Daoris.Driver;

/// <param name="SessionId">The record's id, when one was opened — null when nothing started.</param>
/// <param name="Message">What to tell the person: the ledger's sentence, or why nothing began.</param>
public sealed record ChatStart(string? SessionId, string Message)
{
    /// <summary>
    /// That the start's open was asked to move the words and its host moved them (FOUNDATION1d-ii part 4, <see cref="DestinationOpen.Moves"/>):
    /// the delivery is then the new record's moved words. False for every start not asked to move, and against a host whose mark is below 2.
    /// </summary>
    public bool Moves { get; init; }

    /// <summary>
    /// Every word the start's open moved onto its record, in order (<see cref="DestinationOpen.Moved"/>): read whatever <see cref="Moves"/>
    /// says, since a host below the mark replaying a newer host's receipt answers what that host moved. Empty for every other start.
    /// </summary>
    public IReadOnlyList<MovedWord> Moved { get; init; } = [];
}
