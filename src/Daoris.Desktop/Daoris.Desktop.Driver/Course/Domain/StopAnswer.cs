namespace Daoris.Driver;

/// <summary>What a stop did (2026-09-25, REV3): ended a process here, ended an orphan's record, or found another Daoris process here running it.</summary>
/// <param name="Stopped">A process here was stopped, or a record nothing ran was ended.</param>
/// <param name="Orphan">What it ended was a record nothing on this machine ran: its ending is the person's, and says nothing ran it.</param>
/// <param name="Elsewhere">Another Daoris process on this machine runs it, which this one can neither stop nor call ended.</param>
public sealed record StopAnswer(bool Stopped, bool Orphan, bool Elsewhere);
