namespace Daoris.Driver;

/// <summary>What the look's workspace-plan pass reaches of the driver beyond <see cref="ILookWiring"/>: a test's stand-in for the plans' world, and whether the starts it plans may drive a repository that has not adopted Daoris.</summary>
internal interface ISetupWiring : ILookWiring
{
    /// <summary>A stand-in for the workspace plans' world; null reads the home's.</summary>
    Func<IWorkspaceSetupWorld>? SetupPlans { get; }

    /// <summary>Whether this machine's starts may drive a repository that has not adopted Daoris (<see cref="Adoption"/>).</summary>
    bool Unadopted();
}
