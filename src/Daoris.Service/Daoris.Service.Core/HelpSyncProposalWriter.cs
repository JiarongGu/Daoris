using static Daoris.Knowledge.HelpProposalFile;

namespace Daoris.Knowledge;

/// <summary>
/// The <c>sync</c> kind's writer (HELP10): bringing repositories up to date after a pull request merged (WSR6, D109) —
/// Repositories → the workspace's page → Branches → <i>Bring up to date</i>, and <c>daoris-driver trees sync</c> — for one
/// repository, or every one with a checkout on the machine.
/// </summary>
internal sealed class HelpSyncProposalWriter(HelpProposalFile file)
{
    /// <summary>
    /// Write one proposal to bring repositories up to date (HELP10). What the look lists, which fetches and so waits for
    /// the person's press, and what the press does, are the driver's; here only the shape is checked.
    /// </summary>
    /// <param name="repository">The one repository to bring up to date; null for every one with a checkout here.</param>
    /// <param name="workspace">BRSCOPE1b: the one workspace the look and the press are scoped to; null for the whole machine.</param>
    public (string? Id, string Message) Propose(
        string? repository, string why, string? session, DateTimeOffset at, string? workspace = null)
    {
        var named = Blank(repository);
        var circle = Blank(workspace);
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        if (named is not null && Word(named, "a repository") is { } refused) return (null, $"{Capital(refused)} Nothing was proposed.");
        if (circle is not null && Word(circle, "a workspace") is { } refusedWorkspace)
            return (null, $"{Capital(refusedWorkspace)} Nothing was proposed.");

        return file.Write(writer =>
        {
            writer.WriteString("kind", "sync");
            writer.WriteString("door", "sync");
            Nullable(writer, "target", named);
            Nullable(writer, "workspace", circle);
            writer.WriteNull("value");
            writer.WriteNull("sentence");
        }, why, session, at);
    }
}
