namespace Daoris.Driver;

/// <summary>A registration as the service answered it. The root is present only from a local service.</summary>
/// <param name="Workspace">
/// The circle this repository is wired into on THIS machine (D48 §2) — the registry row, which is the
/// only honest source for it. The toolchain reads it to pick a workspace's credential profile
/// (D49 §4): a work account for the work circle, a personal one at home.
/// </param>
public sealed record RepoView(
    string Repository, bool Adopted, string? Root, string Workspace = RemoteTarget.DefaultWorkspace);

/// <summary>One lane of a repository, as the registry answered it (D115 §2.2): what a quest addresses as `repository:id`.</summary>
/// <param name="Steward">The one lane that keeps the repository's records.</param>
public sealed record LaneView(string Id, string Title, string Summary, bool Steward);
