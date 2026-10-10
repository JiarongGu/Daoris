namespace Daoris.Knowledge;

/// <summary>The <c>go</c> kind's writer (HELP6): a screen to open, with its own <c>domain</c> and <c>part</c>.</summary>
public sealed partial class HelpProposalBox
{
    /// <summary>
    /// Write one proposal to take the person to a screen (HELP6): a view, a domain of Settings, and a part
    /// of it or a setup step. It changes nothing; which places exist is the driver's to judge.
    /// </summary>
    public (string? Id, string Message) ProposeGo(string view, string? domain, string? part, string why, string? session, DateTimeOffset at)
    {
        var where = Blank(view)?.ToLowerInvariant();
        var within = Blank(domain)?.ToLowerInvariant();
        var piece = Blank(part)?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        var refused = where is null ? "a screen names the view it is on — overview, sessions, quests, projects, map, knowledge, agents, plugins or settings."
            : within is not null && where != "settings" ? "a domain is a part of Settings — name `settings` as the view."
            : Word(where, "a view") ?? (within is null ? null : Word(within, "a domain")) ?? (piece is null ? null : Word(piece, "a part"));
        if (refused is not null) return (null, $"{Capital(refused)} Nothing was proposed.");

        return Write(writer =>
        {
            writer.WriteString("kind", "go");
            writer.WriteString("door", "go");
            writer.WriteString("target", where);
            writer.WriteNull("workspace");
            writer.WriteNull("value");
            writer.WriteNull("sentence");
            Nullable(writer, "domain", within);
            Nullable(writer, "part", piece);
        }, why, session, at);
    }
}
