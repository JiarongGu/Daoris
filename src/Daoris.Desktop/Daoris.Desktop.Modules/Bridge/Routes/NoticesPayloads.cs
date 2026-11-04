namespace Daoris.Desktop;

/// <summary>The notices of <c>Ids</c> read.</summary>
public sealed record NoticesReadPress(IReadOnlyList<string>? Ids);

/// <summary>The notice <c>Id</c> dismissed; with <c>All</c>, every notice of the <c>Workspace</c> and every machine-wide one.</summary>
public sealed record NoticeDismissPress(string? Id, string? Workspace, bool All = false);

/// <summary>What came to the person; <c>Json</c> asks for the machine's form.</summary>
public sealed record NotificationsRead(bool Json = false);
