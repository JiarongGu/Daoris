namespace Daoris.Desktop;

/// <summary>The tools list; <c>Ask</c> has it look at what the machine holds.</summary>
public sealed record ToolsListRead(bool Ask = false);

/// <summary>How a tool is run: <c>Action</c> is system, managed or file, and each reads the fields it names.</summary>
public sealed record ToolsUsePress(string Tool, string Action, string? File, string? Version);

/// <summary>A managed version of a tool downloaded; none named is the newest.</summary>
public sealed record ToolsDownloadPress(string Tool, string? Version);

/// <summary>The download or use of a tool stopped.</summary>
public sealed record ToolsStopPress(string Tool);

/// <summary>A downloaded version of a tool deleted.</summary>
public sealed record ToolsDeletePress(string Tool, string Version);

/// <summary>A resource location added or removed by its address.</summary>
public sealed record ToolsLocationPress(string Action, string Address);

/// <summary>A setting Daoris's git carries, set or (with no <c>Value</c>) cleared.</summary>
public sealed record ToolsGitSetPress(string Key, string? Value);

/// <summary>
/// One act on a git identity: <c>Action</c> is add-ssh, add-https, test, rename, commits-as, read-only, remove or default, and each
/// reads the fields it names.
/// </summary>
public sealed record GitIdentityPress(
    string Action, string? Identity, string? Host, string? Label, string? Name, string? Email, string? Import, string? User, string? Token,
    string? Repository, string? Trust, bool Rsa = false, bool Clear = false, bool On = false);

/// <summary>One pointer written into the choices of a repository's remote, a workspace's host or the machine's host.</summary>
public sealed record GitUsePress(string Scope, string? Repository, string? Workspace, string? Remote, string? Host, string? Value);

/// <summary>A git go-ahead answered, <c>allow</c> or <c>refuse</c>.</summary>
public sealed record GitAnswerPress(string? Ask, string? Answer);

/// <summary>Which git a tool is set to: <c>Way</c> is system, managed or file, and each reads the fields it names.</summary>
public sealed record ToolsGitPress(string Way, string? Version, string? File);

/// <summary>The file picker; <c>Title</c> and <c>Window</c> are what it is shown with.</summary>
public sealed record ToolsPickRead(string? Title, string? Window);
