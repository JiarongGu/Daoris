using static Daoris.Knowledge.HelpProposalFile;

namespace Daoris.Knowledge;

/// <summary>
/// The <c>setup</c> kind's writer (LAYOUT8, D170 point 1, D117 §6.1): a registered repository set up for every agent, by the
/// registry's name, its one door <c>setup</c>.
/// </summary>
/// <remarks>
/// The file keeps its <c>door</c> and its common <c>workspace</c>, written null, so the doors WSSETUP7 adds to this kind
/// (<c>register</c>, and a workspace's plan with its pause, resume and stop, D124 §4.5) are a door and a field of the same file.
/// </remarks>
internal sealed class HelpSetupProposalWriter(HelpProposalFile file)
{
    /// <summary>
    /// Write one proposal to set a repository up (LAYOUT8): the repository as the registry names it, its <c>target</c>, for the
    /// person to apply — what <c>daoris-driver setup &lt;repository&gt;</c> and the repository page's *Set up for agents* do: one
    /// quest to its own session, and the rule its session needs to run the doctrine tool. A set-up needs no path, so none is
    /// written. Whether the press would publish (registered here, driven, a tree of its own, the doctrine tool found, not set up
    /// already, none open) is the driver's to judge, by the press's own refusals.
    /// </summary>
    public (string? Id, string Message) Propose(string repository, string why, string? session, DateTimeOffset at)
    {
        var named = Blank(repository);
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        var refused = named is null ? "a set-up names the repository to set up, as Repositories lists it."
            : named.IndexOfAny(['/', '\\']) >= 0 || Path.IsPathRooted(named)
                ? "a repository is named as Repositories lists it, never by its folder: a set-up needs no path."
            : Word(named, "a repository");
        if (refused is not null) return (null, $"{Capital(refused)} Nothing was proposed.");

        return file.Write(writer =>
        {
            writer.WriteString("kind", "setup");
            writer.WriteString("door", "setup");
            writer.WriteString("target", named);
            writer.WriteNull("workspace");
            writer.WriteNull("value");
            writer.WriteNull("sentence");
        }, why, session, at);
    }
}
