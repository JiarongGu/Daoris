namespace Daoris.Driver;

/// <summary>Which signal of its door said a child's start or end (<see cref="WireReading.ChildSeen"/>, <see cref="WireReading.ChildEnded"/>).</summary>
public enum ChildSignal
{
    /// <summary>The native door's stream: its <c>task_started</c> and <c>task_notification</c> lines.</summary>
    Stream,

    /// <summary>The protocol door's announcement and state update: its <c>subagent_spawned</c> and <c>subagent_state_update</c>.</summary>
    Announced,
}
