namespace Daoris.Knowledge;

/// <summary>
/// The <c>plugin</c> kind's writer (PLUG9): a plugin added from its folder or from the install's own offers by
/// id, switched on or off, or updated, with the proposal's <c>repository</c> and its own <c>folder</c> and <c>offer</c>.
/// </summary>
public sealed partial class HelpProposalBox
{
    /// <summary>
    /// Write one plugin proposal (PLUG9): <c>add</c> a plugin that has landed, from its folder in the
    /// checkout of the repository that holds it — <c>daoris plugin add</c>'s copy — or <c>enable</c> or
    /// <c>disable</c> one installed here, Settings → Plugins' switch. Whether the folder holds a sound
    /// manifest, and whether the id is installed, is the driver's, read with the catalogue's own reader.
    /// </summary>
    /// <param name="id">For enable, disable and update: the installed plugin's id.</param>
    /// <param name="folder">For add: the folder from the repository's checkout root, or a whole path the person gave.</param>
    /// <param name="repository">For add: the repository whose checkout holds it; null only for a whole path.</param>
    /// <param name="offer">For add, instead of a folder: one of the install's own plugins, by its id (PLUG9 d) — never a path.</param>
    /// <remarks>
    /// Since PLUG9 (c) and (d) (D103) an <c>add</c> may name an offer, and an <c>update</c> takes a newer copy of an
    /// installed plugin from where it came from. Whether the install offers it, and whether the plugin recorded
    /// where it came from, is the driver's, which reads both with its own code.
    /// </remarks>
    public (string? Id, string Message) ProposePlugin(
        string action, string? id, string? folder, string? repository, string why, string? session, DateTimeOffset at,
        string? offer = null)
    {
        var door = action.Trim().ToLowerInvariant();
        var named = Blank(id);
        var where = Blank(folder);
        var holder = Blank(repository);
        var offered = Blank(offer);
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        var refused = door is not ("add" or "enable" or "disable" or "update")
            ? "a plugin's change is `add`, `enable`, `disable` or `update`: add copies a plugin that has landed, or one of "
              + "Daoris's own, into Daoris's home; enable or disable switches one installed here; update takes a newer copy "
              + "from where an installed one came from."
            : door == "add" ? AddRefusal(named, where, holder, offered)
            : named is null ? $"`{door}` names the plugin by its id, as the room lists the plugins installed here."
            : where is not null || holder is not null || offered is not null
                ? $"`{door}` names only the plugin's id — a folder or an offer is for `add`."
            : Word(named, "an id");
        if (refused is not null) return (null, $"{Capital(refused)} Nothing was proposed.");

        return Write(writer =>
        {
            writer.WriteString("kind", "plugin");
            writer.WriteString("door", door);
            Nullable(writer, "target", door == "add" ? null : named);
            writer.WriteNull("workspace");
            writer.WriteNull("value");
            writer.WriteNull("sentence");
            Nullable(writer, "repository", door == "add" ? holder : null);
            Nullable(writer, "folder", door == "add" ? where : null);
            Nullable(writer, "offer", door == "add" ? offered : null);
        }, why, session, at);
    }

    /// <summary>Why an add is not an add's shape: an offer by its id, or a folder, no id, and a repository's checkout it stays inside.</summary>
    private static string? AddRefusal(string? id, string? folder, string? repository, string? offer)
    {
        if (offer is not null)
        {
            if (folder is not null || repository is not null)
            {
                return "`add` names an offer or a folder, never both: an offer is one of Daoris's own plugins by its id, and a folder one that has landed in a repository.";
            }

            return id is not null ? "`add` names no id — an offer is named in `offer`, and a folder's id is its manifest's." : Word(offer, "an offer");
        }

        if (folder is null) return "`add` names the plugin's folder: where it landed, in the checkout of the repository that holds it — or one of Daoris's own plugins by its id, in `offer`.";
        if (id is not null) return "`add` names no id — a plugin's id is its manifest's, read from its folder.";
        if (repository is null)
        {
            return Path.IsPathRooted(folder)
                ? null
                : "name the repository whose checkout holds it, with its folder there — a whole path only when the person gave one.";
        }

        if (Word(repository, "a repository") is { } word) return word;
        if (Path.IsPathRooted(folder)) return "a folder in a repository is written from its checkout's root, like `plugins/quiet-hours`, never as a whole path.";
        return folder.Split('/', '\\').Contains("..")
            ? "a folder in a repository stays inside its checkout — `..` leaves it."
            : null;
    }
}
