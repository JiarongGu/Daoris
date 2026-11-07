namespace Daoris.Driver;

/// <summary>The words a landing says, and the names an entry's branch is refused or taken by.</summary>
internal static class LandingSayings
{
    /// <summary>What a landing says of the tree it leaves — dropped when a tidy removed it; the squash's advance says it too.</summary>
    internal const string TreeStays =" The tree is still there; discard it when you are done with it.";

    /// <summary>How much of a refusing hook's output the sentence quotes.</summary>
    private const int HookSaidLimit = 600;

    /// <summary>
    /// What a merge its repository's hook refused says, told from the merge the integration tree made for the press: that it was the hook and no conflict, the hook's own output, and
    /// that Daoris does not skip a repository's hooks (this merge never passes <c>--no-verify</c>, as the git writes design's §4.1
    /// proposes for the person's own acts), so the person changes what the hook refuses or merges by hand. git runs a hook with its output on stderr; its own closing line, where it is in English, is left out.
    /// </summary>
    internal static string HookRefused(string branch, string canonical, string stderr, string stdout)
    {
        var said = string.Join(" ", (stderr.Length > 0 ? stderr : stdout).Split('\n').Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith("Not committing merge", StringComparison.Ordinal)));
        var quoted = said.Length > 0 ? $" It said: {TextBounds.Clip(said, HookSaidLimit)}" : " It said nothing.";
        return $"the repository's own git hook (`pre-merge-commit` or `commit-msg`) refused the merge commit, so `{branch}` did not merge "
            + $"into `{canonical}`; nothing conflicted.{quoted} Daoris does not skip a repository's hooks: change what the hook "
            + "refuses there, or merge the branch yourself. The merge was aborted and the checkout is as it was.";
    }

    /// <summary>
    /// A pinned landing whose branch moved past the commit its door judged: nothing lands, and
    /// it is judged again at its new tip.
    /// </summary>
    internal static TreeLanding Moved(string branch, string stands, string pinned) =>
        new(false, $"`{branch}` moved after its landing was judged: it stands at {TextBounds.ShortCommit(stands, 8)}, "
            + $"not {TextBounds.ShortCommit(pinned, 8)}. Nothing was landed; it is judged again at its new tip.")
        {
            Refusal = LandingCode.Refused,
        };

    /// <summary>What a landing says that waited for another of its tree past <see cref="LandingHolds.Wait"/>.</summary>
    internal static string StillLanding =>
        $"another landing of this session's work was still under way after {(int)LandingHolds.Wait.TotalMinutes} minutes, so nothing was "
        + "landed: accept it again once that one ends.";

    /// <summary>Words said before the sentence that the tree stays, which a tidy takes out; at the end where it is not there.</summary>
    internal static string BeforeTreeStays(string message, string words) => message.EndsWith(TreeStays, StringComparison.Ordinal)
        ? message[..^TreeStays.Length] + words + TreeStays
        : message + words;

    /// <summary>What a pattern is expanded from: the quest, or the session where there is none; the quest's name's words.</summary>
    internal static string EnteredInvalid(string name) =>
        $"the branch name you entered, `{name}`, is not a branch name git would take. Enter another, or leave it blank for the rule's.";

    /// <summary>What a branch name git would not take is refused with: the name the person entered, or the one the rule makes.</summary>
    internal static string Untaken(string name, bool entered) => entered ? EnteredInvalid(name)
        : $"the landing rule names `{name}` for this session, which is not a branch name git would take. Change the pattern with `daoris place landing`.";

    /// <summary>An entered name that is already a branch Daoris did not make: the press's refusal, which the plan says first.</summary>
    internal static string EnteredStands(string name, string repository) =>
        $"the branch name you entered, `{name}`, is already a branch in `{repository}`, and Daoris does "
        + "not move a branch it did not make. Enter another name, or leave it blank for the rule's.";

    /// <summary>A name entered where the tree's own branch lands as it stands: refused, not ignored, as a merge's is.</summary>
    internal static string EnteredOwn(string own) =>
        $"this session's tree is on its own branch, `{own}`, which lands as it stands and is not renamed, so there is no branch name "
        + "to enter. Leave it blank.";

    internal const string EnteredMerge = "the rule for this repository merges the work into its line and makes no branch, so there is no "
        + "branch name to enter. Leave it blank, or change the rule with `daoris place landing`.";

    /// <summary>
    /// What names the branch: the person's entered name where there is one, else the bound <c>land</c>'s pattern expanded, its
    /// <c>{ticket}</c> found by its key. The plan and the press both ask it, so they name one branch.
    /// </summary>
    internal static string BranchNamed(LandingSubject subject, Landing landing, string repository) =>
        subject.EnteredName ?? subject.EntryBranch ?? BranchPatterns.Branch(subject, landing, repository);
}
