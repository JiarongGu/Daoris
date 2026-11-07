namespace Daoris.Desktop;

/// <summary>
/// One row of <c>helpGrants</c>: the grant, what is chosen, the operations the driver acts on (none for one
/// set elsewhere, the browser's), whether a running conversation has yet to take it, whether its plugin's update changed it since
/// approval, and the plugin whose tab sets it.
/// </summary>
internal sealed record HelpGrantRow(string Id, IReadOnlyList<string> Chosen, IReadOnlyList<string>? Built, bool Pending, bool Changed, string? Plugin);
