namespace Daoris.Desktop;

/// <summary>The notices of <c>Ids</c> read.</summary>
public sealed record NoticesReadPress(IReadOnlyList<string>? Ids);

/// <summary>A notice, all of a window's, or one row of <i>What needs you</i> (<c>RowKind</c>, <c>RowId</c>, <c>RowSince</c>) set aside.</summary>
public sealed record NoticeDismissPress(string? Id, string? Workspace, bool All = false, string? RowKind = null, string? RowId = null, string? RowSince = null);

/// <summary>What came to the person; <c>Json</c> asks for the machine's form.</summary>
public sealed record NotificationsRead(bool Json = false);
