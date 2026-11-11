namespace Daoris.Driver;

/// <summary>
/// The port an agent-performed plugin step's attempt is sent to: the shape of a check's port (<see cref="ICheckPort"/>), its attempts the
/// plugin's own, answered from the attempt's files.
/// </summary>
public interface IStepAgents : ICheckPort
{
    /// <summary>Whether the attempt is this port's: its ask is kept.</summary>
    bool Holds(string invocation);
}
