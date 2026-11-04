namespace Daoris.Driver;

/// <summary>Why an agent itself cannot run (XAGENT1b): what <see cref="HarnessSelection.Absent"/> says.</summary>
public enum AgentAbsence
{
    /// <summary>Nothing of it is installed on this machine: no binary to spawn.</summary>
    NotInstalled,

    /// <summary>Pinned to a version nothing is installed at (D57): refused, never run from <c>PATH</c> instead.</summary>
    PinNotInstalled,
}
