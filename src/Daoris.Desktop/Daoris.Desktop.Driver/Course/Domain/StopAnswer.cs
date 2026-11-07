namespace Daoris.Driver;

/// <summary>What a stop did: ended a process here, ended an orphan's record, or found another Daoris process here running it.</summary>
/// <param name="Stopped">A process here is stopped, or a record nothing runs is ended.</param>
/// <param name="Orphan">What it ends is a record nothing on this machine runs: its ending is the person's, and says nothing runs it.</param>
/// <param name="Elsewhere">Another Daoris process on this machine runs it, which this one can neither stop nor call ended.</param>
public sealed record StopAnswer(bool Stopped, bool Orphan, bool Elsewhere);
