using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// One delete the journal holds: the act, what it deleted and at which commit, who pressed it, the recovery ref that keeps the
/// commit where one was kept, and whether its Undo is open. <see cref="Closed"/> is a code the page words, null where the Undo is open.
/// </summary>
public sealed record DeletedRef(string Act, string Kind, string Name, string Commit, DateTimeOffset At, string Press, string? Recovery, string? Closed);

/// <summary>The deletes of one repository, newest first.</summary>
public sealed record DeletedRefs(IReadOnlyList<DeletedRef> Rows)
{
    /// <summary>The recovery refs standing here that no journal record names (<see cref="RecoveryCensus"/>), counted where the deletes' read lists refs.</summary>
    public int Unrecorded { get; init; }
}

/// <summary>
/// **A repository's deletes, read from the journal alone**: each done <c>ref.delete</c> act is a row, whoever pressed
/// it (the person's <c>GIT_REF_DELETE</c> or a door of Daoris's through <see cref="JournaledDeletes"/>), with the Undo the act carries. A row
/// reads the plan the act wrote: which delete it was, at which commit, which recovery ref kept the commit. git is asked only whether the Undo
/// can still run (the name free, the commit still known), in a listing that also counts the recovery refs no journal record names.
/// </summary>
public static class DeletedBranches
{
    /// <summary>How many deletes a read lists.</summary>
    public const int Listed = 50;

    /// <summary>The Undo closed because the act was undone already.</summary>
    public const string Undone = "undone";

    /// <summary>The Undo closed because the name stands again.</summary>
    public const string NameTaken = "name-taken";

    /// <summary>The Undo closed because the commit is no longer in the repository.</summary>
    public const string CommitGone = "commit-gone";

    public static async Task<GitAnswer<DeletedRefs>> ReadAsync(string home, string root, string repository, CancellationToken ct)
    {
        var journal = new ActJournal(home);
        var deletes = journal.OfKind(GitRefActs.DeleteKind, repository, Listed).Where(each => each.Outcome == ActOutcomes.Done).ToList();
        if (deletes.Count == 0) return new(new DeletedRefs([]), GitReadRefusal.None, null);

        var undone = journal.OfKind(GitRefActs.UndoKind, repository, Listed * 4).Where(each => each.Outcome == ActOutcomes.Done)
            .Select(each => (string?)JsonNode.Parse(each.Plan)?["undo"]).Where(each => each is not null).ToHashSet(StringComparer.Ordinal);

        // The same listing names the recovery refs, so the census of those no record names costs no further git start.
        var (listed, standing, _) = await WorkingTree.GitAsync(
            root, ["for-each-ref", "--format=%(refname)", "refs/heads/", "refs/tags/", ContentHolds.DiscardedPrefix], ct).ConfigureAwait(false);
        if (listed != 0) return new(null, GitReadRefusal.NotAnswered, "git could not list the repository's refs, so the deletes' Undo is not known.");
        var names = standing.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
        var recorded = RecoveryKeeps.Recorded(home, repository);
        var unrecorded = names.Count(each => each.StartsWith(ContentHolds.DiscardedPrefix, StringComparison.Ordinal) && !recorded.ContainsKey(each));

        var rows = new List<DeletedRef>();
        foreach (var act in deletes)
        {
            if (JsonNode.Parse(act.Plan) is not JsonObject plan || plan["ref"]?.GetValue<string>() is not { } full || plan["at"]?.GetValue<string>() is not { } commit) continue;
            var (known, _, _) = await WorkingTree.GitAsync(root, ["cat-file", "-e", commit + "^{commit}"], ct).ConfigureAwait(false);
            var closed = undone.Contains(act.Id) ? Undone : names.Contains(full) ? NameTaken : known != 0 ? CommitGone : null;
            var tag = full.StartsWith("refs/tags/", StringComparison.Ordinal);
            rows.Add(new(act.Id, tag ? "tag" : "branch", full[(tag ? "refs/tags/".Length : "refs/heads/".Length)..], commit, act.At, act.Press,
                plan["recovery"]?.GetValue<string>(), closed));
        }

        return new(new DeletedRefs(rows) { Unrecorded = unrecorded }, GitReadRefusal.None, null) { Commands = ["git reflog show --all", "git for-each-ref refs/daoris/discarded/"] };
    }
}
