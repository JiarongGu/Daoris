using System.Text.Json;

namespace Daoris.Desktop;

// The payloads of WorkflowRoutes' reads and presses, each named for its route.

/// <summary>A saved workflow shown by its <c>Id</c>, at a <c>Version</c> or the newest.</summary>
public sealed record WorkflowShowRead(string? Id, long? Version);

/// <summary>A repository or a workspace, one of them; the driver refuses both or neither. A <c>Route</c> adds the graph that route follows.</summary>
public sealed record WorkflowCurrent(string? Repository, string? Workspace, string? Route);

/// <summary>A branch or a commit, not both; the driver refuses a blank repository and both.</summary>
public sealed record WorkflowVerify(string? Repository, string? Branch, string? Commit);

/// <summary>The blob read, or <c>Clear</c> to withdraw the acceptance; the driver refuses a blank repository and both.</summary>
public sealed record IntegrationGates(string? Repository, string? Gates, bool Clear = false);

/// <summary>A repository or a workspace, one of them. No workflow clears the route; <c>Gates</c> is the blob the person read.</summary>
public sealed record WorkflowLandedPress(string? Repository, string? Workspace, string? Workflow, string? Gates);

/// <summary>A repository or a workspace, one of them. No workflow clears the choice; <c>Kind</c> names a kind of task, <c>Version</c> the newest read, <c>Expected</c> a plan's token.</summary>
public sealed record WorkflowUsePress(
    string? Repository, string? Workspace, string? Workflow, string? Kind, long? Version, string? Expected, bool Plan = false);

/// <summary>A label declares the kind; <c>Drop</c> withdraws it, with no label and no paths.</summary>
public sealed record WorkflowKind(string? Workspace, string? Kind, string? Label, IReadOnlyList<string>? Paths, bool Drop = false);

/// <summary>Exactly one of the four: a session, a quest, an ask, or a repository whose line's verification runs are read.</summary>
public sealed record WorkflowRunRead(string? Session, string? Quest, string? Ask, string? Line = null);

/// <summary>A version saved under <c>Id</c>, from a preset or a saved workflow, or of the <c>Steps</c> or <c>Graph</c> sent, whose nodes the workflow's reader judges.</summary>
public sealed record WorkflowSavePress(
    string? Id, string? From, JsonElement? Steps, JsonElement? Graph, string? Name, long? Base, IReadOnlyList<string>? Authorize, string? Updates,
    bool WithOpinion = false, bool WithLook = false, bool Plan = false);

/// <summary>A word on a run's node; an <c>accept</c> names the review record drawn and the unchecked dones it passes, <c>changes</c> their part.</summary>
public sealed record WorkflowNodePress(
    string Run, string Node, string Act, string? Words, bool Said = false, long? Attempt = null, string? Revision = null, bool Unchecked = false,
    string? Part = null);
