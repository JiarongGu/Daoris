namespace Daoris.Knowledge;

/// <summary>
/// The <c>browser</c> kind's writer (HELP10): Daoris's browser's settings, each a <c>daoris browser</c> verb, and a
/// favorite with its address (the proposal's target) and its own <c>title</c>.
/// </summary>
public sealed partial class HelpProposalBox
{
    /// <summary>
    /// Write one proposal to change Daoris's browser's settings (HELP10): Settings → Browser, and <c>daoris browser</c>.
    /// Whether an address is a web page, a favorite removed is one kept, or the files can be read is the driver's.
    /// </summary>
    /// <param name="door">`use`, `links`, `extensions` or `favorite`, as the terminal spells them.</param>
    /// <param name="value">`daoris`|`edge`, `system`|`daoris`, `offer`|`refuse`, or for a favorite `add`|`remove`.</param>
    /// <param name="address">A favorite's address; null for every other door.</param>
    /// <param name="title">A favorite added's title; null for its page's host.</param>
    public (string? Id, string Message) ProposeBrowser(
        string door, string? value, string? address, string? title, string why, string? session, DateTimeOffset at)
    {
        var setting = door.Trim().ToLowerInvariant();
        var to = Blank(value)?.ToLowerInvariant();
        var page = Blank(address);
        var named = Blank(title);
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        var refused = setting switch
        {
            "use" or "links" or "extensions" when page is not null || named is not null =>
                "only a favorite names an address or a title.",
            "use" => to is "daoris" or "edge" ? null : "`use` is `daoris` or `edge`.",
            "links" => to is "system" or "daoris" ? null : "`links` is `system` or `daoris`.",
            "extensions" => to is "offer" or "refuse" ? null : "`extensions` is `offer` or `refuse`.",
            "favorite" => to is not ("add" or "remove") ? "a favorite is `add` or `remove`."
                : page is null ? "a favorite names its address: the page to keep, or the one to stop keeping."
                : to == "remove" && named is not null ? "only a favorite added takes a title."
                : Word(page, "an address"),
            _ => $"`{setting}` is not a setting of Daoris's browser — `use`, `links`, `extensions` or `favorite`.",
        };
        if (refused is not null) return (null, $"{Capital(refused)} Nothing was proposed.");

        return Write(writer =>
        {
            writer.WriteString("kind", "browser");
            writer.WriteString("door", setting);
            Nullable(writer, "target", page);
            writer.WriteNull("workspace");
            writer.WriteString("value", to);
            writer.WriteNull("sentence");
            Nullable(writer, "title", named);
        }, why, session, at);
    }
}
