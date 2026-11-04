namespace Daoris.Driver;

/// <summary>A git identity a <c>git-use</c> proposal chooses (GITAUTH1e): applied as <c>GIT_USE</c> applies it. A null <c>Identity</c> takes the choice back.</summary>
public sealed record HelpGitUse(string Scope, string? Repository, string? Workspace, string? Remote, string? Host, string? Identity);

/// <summary>A git identity as a proposal's judge knows it: its id, label, host and way. Never a key, a token or a user name.</summary>
public sealed record HelpGitIdentity(string Id, string Label, string Host, string Way);

public interface IHelpSettingDoors
{
    /// <summary>A repository's remote or a workspace's host pointed at an identity, or cleared, as <c>GIT_USE</c> does it (GITAUTH1e).</summary>
    /// <exception cref="DriverException">The pointer's own refusal.</exception>
    Task UseGitAsync(HelpGitUse use, CancellationToken ct);

    /// <summary>An edit to the driver's file, as the `SET_*` routes and `RETRY_QUEST` make it.</summary>
    void Change(Func<DriverConfig, DriverConfig> edit);
}
