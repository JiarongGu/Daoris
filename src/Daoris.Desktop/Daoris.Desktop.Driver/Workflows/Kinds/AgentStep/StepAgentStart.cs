namespace Daoris.Driver;

/// <summary>An agent-performed step's session the step agents start: one per attempt's invocation.</summary>
public sealed record StepAgentStart(string Invocation) : NodeDoorStart
{
    public override string Key => Invocation;
}
