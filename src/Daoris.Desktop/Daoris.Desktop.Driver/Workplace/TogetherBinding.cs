namespace Daoris.Driver;

/// <summary>How an operation bringing sessions together holds a work, one of these words (<see cref="TogetherBinding"/>).</summary>
public static class TogetherHolds
{
    /// <summary>A member of an open operation: its head's delivery brings it in, so nothing of its own starts.</summary>
    public const string Member = "member";

    /// <summary>The head of an operation not yet checked at its tip: <c>asked</c>, <c>replaying</c>, <c>checking</c>, or <c>ready</c> at another tip.</summary>
    public const string HeadBefore = "head-before";

    /// <summary>The head of an operation <c>ready</c> at the work's tip: its step may start.</summary>
    public const string HeadReady = "head-ready";

    /// <summary>The head of an operation <c>landing</c> or <c>uncertain</c>: its attempt is settled before anything new starts.</summary>
    public const string HeadLanding = "head-landing";

    /// <summary>Whether a work held so waits: everything but a head ready at its tip.</summary>
    public static bool Waits(string how) => how != HeadReady;
}

/// <summary>An operation's hold on a work: how, the operation, and its words.</summary>
public sealed record TogetherHold(string How, TogetherOperation? Operation, string Said);

/// <summary>
/// The one read of an operation's hold on a work (§14 of the process steps' design): a member of an open operation; the head of one before
/// <c>ready</c>; the head of one <c>ready</c> at the work's tip; the head of one <c>landing</c> or <c>uncertain</c>; or none. A work is its
/// repository, branch and tree; the head is matched by its tree, a member by its tree or its branch. And the inclusion record's reading: the
/// tip a landed operation bound a member at, every commit up to which its head brought in.
/// </summary>
public static class TogetherBinding
{
    /// <summary>The hold the operations in <paramref name="home"/> have on the work; an unread record holds it as a member, said.</summary>
    public static TogetherHold? Of(string home, string repository, string branch, string tree, string tip)
    {
        try
        {
            return Of(new TogetherOperations(home).All(), repository, branch, tree, tip);
        }
        catch (IOException error)
        {
            return new(TogetherHolds.Member, null,
                $"whether `{branch}` is being brought together with other sessions could not be read ({error.Message.TrimEnd().TrimEnd('.')})");
        }
    }

    /// <summary>The hold <paramref name="operations"/> have on the work, the newest open operation's; null where none holds it.</summary>
    public static TogetherHold? Of(IReadOnlyList<TogetherOperation> operations, string repository, string branch, string tree, string tip)
    {
        foreach (var operation in operations.Where(each => each.IsOpen && Same(each.Repository, repository)).OrderByDescending(each => each.Since))
        {
            if (operation.Members.Any(member => IsMember(member, branch, tree)))
            {
                var head = operation.Head.Record;
                return new(TogetherHolds.Member, operation, $"Waits for `{head}`'s Accept, which brings this work in with it");
            }

            if (tree.Length == 0 || !TogetherOperation.SameTree(operation.Head.Tree, tree)) continue;
            var members = string.Join(" and ", operation.Members.Select(member => $"`{member.Session}`"));
            return operation.Phase switch
            {
                TogetherPhase.Ready when string.Equals(operation.Checked, tip, StringComparison.OrdinalIgnoreCase) =>
                    new(TogetherHolds.HeadReady, operation, $"{members} are brought in, checked at its tip"),
                TogetherPhase.Ready =>
                    new(TogetherHolds.HeadBefore, operation, $"Waits for {members} to be brought in: its tip moved since they were checked, so they are checked again"),
                TogetherPhase.Landing or TogetherPhase.Uncertain =>
                    new(TogetherHolds.HeadLanding, operation, $"Waits for the landing that brings {members} in to settle"),
                _ => new(TogetherHolds.HeadBefore, operation, $"Waits for {members} to be brought in: {operation.Phase}"),
            };
        }

        return null;
    }

    /// <summary>
    /// The tip the newest landed operation bound the work at as its member: every commit up to it its head brought in, whatever new ids its
    /// replay gave them. Null where no landed operation brought it in.
    /// </summary>
    public static string? BroughtIn(IReadOnlyList<TogetherOperation> operations, string repository, string branch, string tree) =>
        operations.Where(each => each.Phase == TogetherPhase.Landed && Same(each.Repository, repository))
            .OrderByDescending(each => each.Since)
            .SelectMany(each => each.Members.Where(member => IsMember(member, branch, tree)))
            .Select(member => member.Tip)
            .FirstOrDefault();

    private static bool IsMember(TogetherMember member, string branch, string tree) =>
        string.Equals(member.Branch, branch, StringComparison.Ordinal) || (tree.Length > 0 && TogetherOperation.SameTree(member.Tree, tree));

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
