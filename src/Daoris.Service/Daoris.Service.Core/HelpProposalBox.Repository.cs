namespace Daoris.Knowledge;

/// <summary>
/// The <c>repository</c> kind's writer (ENTRY1d1, D161's ENTRY1d note): a registered repository moved to a workspace on this
/// machine, by the registry's name and the workspace's.
/// </summary>
public sealed partial class HelpProposalBox
{
    /// <summary>
    /// Write one proposal to move a repository to a workspace (ENTRY1d1): the repository as the registry names it, its
    /// <c>target</c>, and the workspace, its <c>value</c>, for the person to apply. It is the Manage drawer's *Move to
    /// workspace*, which edits one field of the registry's row (D48 §7). A move needs no path, so none is written; adding or
    /// importing a repository needs a folder, which stays the person's pick. Whether the repository is registered, and whether
    /// it is already in that workspace, is the driver's to judge against the registry.
    /// </summary>
    /// <param name="workspace">The workspace to move it to: one this machine has, or a new name, as the drawer's free text takes.</param>
    public (string? Id, string Message) ProposeRepository(string repository, string workspace, string why, string? session, DateTimeOffset at)
    {
        var named = Blank(repository);
        var circle = Blank(workspace);
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        var refused = named is null ? "a move names the repository, as Repositories lists it."
            : named.IndexOfAny(['/', '\\']) >= 0 || Path.IsPathRooted(named)
                ? "a repository is named as Repositories lists it, never by its folder: a move needs no path."
            : Word(named, "a repository")
              ?? (circle is null ? $"a move names the workspace to move `{named}` to." : null);
        if (refused is not null) return (null, $"{Capital(refused)} Nothing was proposed.");

        return Write(writer =>
        {
            writer.WriteString("kind", "repository");
            writer.WriteString("door", "wire");
            writer.WriteString("target", named);
            writer.WriteNull("workspace");
            writer.WriteString("value", circle);
            writer.WriteNull("sentence");
        }, why, session, at);
    }
}
