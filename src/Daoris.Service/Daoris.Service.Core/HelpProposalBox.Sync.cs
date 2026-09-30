namespace Daoris.Knowledge;

/// <summary>
/// The <c>sync</c> kind's writer (HELP10): bringing repositories up to date after a pull request merged (WSR6, D109) —
/// Settings → Workspace → Session branches → <i>Bring up to date</i>, and <c>daoris-driver trees sync</c> — for one
/// repository, or every one with a checkout on the machine.
/// </summary>
public sealed partial class HelpProposalBox
{
    /// <summary>
    /// Write one proposal to bring repositories up to date (HELP10). What the look lists, which fetches and so waits for
    /// the person's press, and what the press does, are the driver's; here only the shape is checked.
    /// </summary>
    /// <param name="repository">The one repository to bring up to date; null for every one with a checkout here.</param>
    public (string? Id, string Message) ProposeSync(string? repository, string why, string? session, DateTimeOffset at)
    {
        var named = Blank(repository);
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        if (named is not null && Word(named, "a repository") is { } refused) return (null, $"{Capital(refused)} Nothing was proposed.");

        return Write(writer =>
        {
            writer.WriteString("kind", "sync");
            writer.WriteString("door", "sync");
            Nullable(writer, "target", named);
            writer.WriteNull("workspace");
            writer.WriteNull("value");
            writer.WriteNull("sentence");
        }, why, session, at);
    }
}
