using System.Text;

namespace Daoris.Ports.Repositories;

/// <summary>What one ref act does to its ref.</summary>
public enum GitRefActKind
{
    /// <summary>Makes the ref, only where it is absent.</summary>
    Create,

    /// <summary>Sets the ref, only where it stands at its old value.</summary>
    Update,

    /// <summary>Deletes the ref, only where it stands at its old value.</summary>
    Delete,
}

/// <summary>One act on one ref, with what it expects to find there.</summary>
/// <param name="Kind">What the act does.</param>
/// <param name="Ref">The ref's full name.</param>
/// <param name="Old">The commit the ref must stand at (an update and a delete); null for a create.</param>
/// <param name="New">The commit the ref is left at (a create and an update); null for a delete.</param>
public sealed record GitRefAct(GitRefActKind Kind, string Ref, string? Old, string? New);

/// <summary>A ref a transaction only reads: it must stand at <paramref name="Value"/>, or be absent where that is null.</summary>
/// <param name="Ref">The ref's full name.</param>
/// <param name="Value">The commit it must stand at, or null for absent.</param>
public sealed record GitRefCheck(string Ref, string? Value);

/// <summary>
/// Acts on refs that all hold or none is made: each act with its expectation, and each check of a ref that is only read. Every ref is
/// named once, by a full name, and every commit by its full id; the caller refuses what contradicts itself before it asks a repository.
/// </summary>
/// <param name="Acts">The acts.</param>
/// <param name="Checks">The refs only read.</param>
public sealed record GitRefTransaction(IReadOnlyList<GitRefAct> Acts, IReadOnlyList<GitRefCheck> Checks)
{
    /// <summary>The transaction as <c>git update-ref --stdin</c> reads it: the checks first, then each act.</summary>
    public string Input()
    {
        var text = new StringBuilder();
        foreach (var check in Checks) text.Append($"verify {check.Ref} {check.Value}".TrimEnd()).Append('\n');
        foreach (var act in Acts)
        {
            text.Append(act.Kind switch
            {
                GitRefActKind.Create => $"create {act.Ref} {act.New}",
                GitRefActKind.Update => $"update {act.Ref} {act.New} {act.Old}",
                _ => $"delete {act.Ref} {act.Old}",
            }).Append('\n');
        }

        return text.ToString();
    }
}

/// <summary>What a ref transaction came to.</summary>
/// <param name="Done">Whether every act took effect.</param>
/// <param name="Said">Why nothing moved where git refused; where git did not answer, the refs as they stand after a read of each.</param>
/// <param name="Unanswered">Whether git never started or ended without an exit, so only the refs themselves say whether anything moved.</param>
public sealed record GitRefRun(bool Done, string Said, bool Unanswered = false);
