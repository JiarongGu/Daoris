namespace Daoris.Driver;

/// <summary>The start of an agent-performed step's session on the door runner: taken once by its attempt's invocation.</summary>
internal sealed record StepAgentStart(string Invocation) : NodeDoorStart
{
    public override string Key => Invocation;
}
