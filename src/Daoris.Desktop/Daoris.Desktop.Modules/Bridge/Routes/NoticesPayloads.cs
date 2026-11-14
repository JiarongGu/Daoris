namespace Daoris.Desktop;

/// <summary>The notices of <c>Ids</c> read.</summary>
public sealed record NoticesReadPress(IReadOnlyList<string>? Ids);

/// <summary>
/// The notice <c>Id</c> dismissed; with <c>All</c>, every notice of the <c>Workspace</c> and every machine-wide one; with <c>RowKind</c> and <c>RowId</c> (and <c>RowSince</c>), that row
/// of <i>What needs you</i> set aside with no act (kept in the history under the <c>Workspace</c>).
/// </summary>
public sealed record NoticeDismissPress(string? Id, string? Workspace, bool All = false, string? RowKind = null, string? RowId = null, string? RowSince = null);

/// <summary>What came to the person; <c>Json</c> asks for the machine's form.</summary>
public sealed record NotificationsRead(bool Json = false);
