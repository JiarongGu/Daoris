namespace Daoris.Driver;

/// <summary>
/// A kind whose node reports a settled state another node may watch: a <c>monitor</c> wired straight after it reads the
/// state through <see cref="Read"/> and is reached with it as its outcome, so any step that reports a state can be monitored.
/// </summary>
public interface IWatchable
{
    /// <summary>The states it reports, in order: the outcomes a monitor watching it declares.</summary>
    IReadOnlyList<string> States { get; }

    /// <summary>
    /// Its state on the reading's unit, which the step and the monitor watching it share; the reading's node is the monitor. A state of
    /// <see cref="States"/> once it settled, none while it is open or unanswered.
    /// </summary>
    NodeWatched Read(NodeReading reading);
}
