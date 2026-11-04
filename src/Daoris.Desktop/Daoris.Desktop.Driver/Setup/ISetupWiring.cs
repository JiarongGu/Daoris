namespace Daoris.Driver;

/// <summary>What the look's workspace-plan pass reaches of the driver beyond <see cref="ILookWiring"/>: a test's stand-in for the plans' world, and the door the starts ride.</summary>
internal interface ISetupWiring : ILookWiring
{
    /// <summary>A stand-in for the workspace plans' world; null reads the home's.</summary>
    Func<IWorkspaceSetupWorld>? SetupPlans { get; }

    /// <summary>The door this machine's starts ride.</summary>
    SessionWire Door();
}
