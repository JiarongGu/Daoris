namespace Daoris.Driver;

/// <summary>
/// The end a session branch's record keeps (<see cref="GrownBranch.Held"/>) as a proof for the content proof. The end judgement wrote where
/// the line held the branch's tip at its landing; a landing that replayed the session's commits leaves them under ids of their own,
/// and the line goes on changing the files they changed, so a later reading of the files finds them different. The fact still holds while
/// the branch stands at that tip, the cycle names that line, the repository is the one judged, and the form of the line descends from where
/// it held (<see cref="HeldFact.HoldsAt"/>).
/// </summary>
/// <param name="Fact">The kept end.</param>
/// <param name="Identity">The repository's identity as the checkout reads it now (<see cref="EndKeeping.RepositoryIdentity"/>).</param>
internal sealed record KeptEnd(HeldFact Fact, string Identity)
{
    /// <summary>The kept end of <paramref name="grown"/> in the repository at <paramref name="root"/>; null where it keeps none or the repository cannot be identified.</summary>
    internal static KeptEnd? Of(string root, GrownBranch? grown) =>
        grown?.Held is { } held && EndKeeping.RepositoryIdentity(root) is { } identity ? new KeptEnd(held, identity) : null;

    /// <summary>Whether the kept end holds <paramref name="tip"/> on the form of the line named <paramref name="form"/>, now at <paramref name="formId"/>.</summary>
    internal async Task<bool> HoldsOnAsync(string cwd, string form, string formId, string tip, string? line, CancellationToken ct)
    {
        if (!string.Equals(Fact.On, form, StringComparison.Ordinal)) return false;
        var (code, _, _) = await WorkingTree.GitAsync(cwd, ["merge-base", "--is-ancestor", Fact.OnAt, formId], ct).ConfigureAwait(false);
        return Fact.HoldsAt(tip, line, Identity, code switch { 0 => true, 1 => false, _ => null });
    }
}
