namespace Daoris.Desktop;

// The payloads of WorkflowRoutes' reads and presses, each named for its route.

/// <summary>A repository or a workspace, one of them; the driver refuses both or neither. A <c>Route</c> adds the graph that route follows.</summary>
public sealed record WorkflowCurrent(string? Repository, string? Workspace, string? Route);

/// <summary>A branch or a commit, not both; the driver refuses a blank repository and both.</summary>
public sealed record WorkflowVerify(string? Repository, string? Branch, string? Commit);

/// <summary>The blob read, or <c>Clear</c> to withdraw the acceptance; the driver refuses a blank repository and both.</summary>
public sealed record IntegrationGates(string? Repository, string? Gates, bool Clear = false);
