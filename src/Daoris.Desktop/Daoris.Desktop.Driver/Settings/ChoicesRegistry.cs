namespace Daoris.Driver;

/// <summary>The checkouts the service's registry names, or why it could not be read: what a review's procedure is looked for in.</summary>
public sealed record ChoicesRegistry(IReadOnlyList<(string Repository, string? Workspace, string? Root)>? Checkouts, string? Unread);
