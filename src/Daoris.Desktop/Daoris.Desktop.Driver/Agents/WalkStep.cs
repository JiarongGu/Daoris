namespace Daoris.Driver;

/// <summary>
/// The step of the walk that chose a start's account (TOOL6b, D130 §13 as §16 amends it): the log's <c>why</c>. <c>Near</c>
/// and <c>Pace</c> read the agent's own word about its windows (TOOL6c).
/// </summary>
public enum WalkStep { Cooling, Refused, SignedOut, Locked, Kept, Near, Room, Fewest, Lapsing, Pace, LeastRecent, List }
