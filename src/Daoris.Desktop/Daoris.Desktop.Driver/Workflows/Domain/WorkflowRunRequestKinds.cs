namespace Daoris.Driver;

/// <summary>What a request inside the work is (<see cref="WorkflowRunRequest.Kind"/>).</summary>
public static class WorkflowRunRequestKinds
{
    /// <summary>A quest a session of the work published, asked of the repository it names.</summary>
    public const string Quest = "quest";

    /// <summary>A helper a session of the work asked: a request to an agent.</summary>
    public const string Agent = "agent";
}
