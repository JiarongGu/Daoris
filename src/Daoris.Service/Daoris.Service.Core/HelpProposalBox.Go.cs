namespace Daoris.Knowledge;

/// <summary>
/// The <c>go</c> kind's writer (HELP6): a screen to open, with its own <c>domain</c>, <c>part</c> and <c>item</c>, and the
/// common <c>workspace</c> for Repositories' Add and Import (ENTRY1d2a).
/// </summary>
public sealed partial class HelpProposalBox
{
    /// <summary>
    /// Write one proposal to take the person to a screen (HELP6): a view, a domain of Settings, and a part
    /// of it or a setup step — or, since ENTRY1f1 and ENTRY1f2, one session on Sessions or one quest or ask on Quests, its
    /// item. It changes nothing; which places and which items exist is the driver's to judge.
    /// </summary>
    /// <param name="workspace">
    /// ENTRY1d2a (D161's ENTRY1d note): for Repositories' Add repository or Import a folder, the workspace the drawer opens
    /// with, trimmed and not held to one word, since the drawer's free text takes any name. Never a folder: the person picks
    /// that in the drawer. Where it may be named is the driver's to judge.
    /// </param>
    public (string? Id, string Message) ProposeGo(
        string view, string? domain, string? part, string? item, string why, string? session, DateTimeOffset at, string? workspace = null)
    {
        var where = Blank(view)?.ToLowerInvariant();
        var within = Blank(domain)?.ToLowerInvariant();
        var piece = Blank(part)?.ToLowerInvariant();
        // ENTRY1f1: an item is an id, kept as spelled; the driver matches it against the machine's records.
        var one = Blank(item);
        var circle = Blank(workspace);
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        var refused = where is null ? "a screen names the view it is on — overview, sessions, quests, projects, map, knowledge, agents, plugins or settings."
            : within is not null && where != "settings" ? "a domain is a part of Settings — name `settings` as the view."
            : Word(where, "a view") ?? (within is null ? null : Word(within, "a domain")) ?? (piece is null ? null : Word(piece, "a part"))
                ?? (one is null ? null : Word(one, "an item"))
                // ENTRY1d2a: a path the conversation was not given never enters it (D48 §3/§7), so none is written.
                ?? (circle is not null && Folder(circle)
                    ? "a workspace is named as Repositories lists it, never by a folder: the person picks the folder in the drawer."
                    : null);
        if (refused is not null) return (null, $"{Capital(refused)} Nothing was proposed.");

        return Write(writer =>
        {
            writer.WriteString("kind", "go");
            writer.WriteString("door", "go");
            writer.WriteString("target", where);
            Nullable(writer, "workspace", circle);
            writer.WriteNull("value");
            writer.WriteNull("sentence");
            Nullable(writer, "domain", within);
            Nullable(writer, "part", piece);
            Nullable(writer, "item", one);
        }, why, session, at);
    }

    // ENTRY1d2a: a separator, a rooted path, or a drive (`D:`, which is rooted on Windows alone) is a folder, never a name.
    private static bool Folder(string name) =>
        name.IndexOfAny(['/', '\\']) >= 0 || Path.IsPathRooted(name) || (name.Length >= 2 && name[1] == ':' && char.IsAsciiLetter(name[0]));
}
