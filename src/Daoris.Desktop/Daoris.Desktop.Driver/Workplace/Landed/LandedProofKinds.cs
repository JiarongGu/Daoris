namespace Daoris.Driver;

/// <summary>
/// The landed proofs' wiring: each proof by the kinds it answers for, chosen by the repository's declared landing kind. Merge-tree is the
/// only proof today, for a local landing; a kind no proof answers for is unknown, never landed. Only a proof's answer marks a save
/// landed (<see cref="TreeSaveRecords.Landed"/> takes nothing else), so no agent's word can. The branch clean-up's own proofs by content
/// (<see cref="LandedProofs"/>) answer a removal's other questions and are not this port's.
/// </summary>
public static class LandedProofKinds
{
    private static readonly IReadOnlyList<ILandedProof> All = [new MergeTreeLandedProof()];

    /// <summary>
    /// The landing kind the repository at <paramref name="root"/> declares: a local landing for every repository until its declaration is
    /// read, which replaces this line alone.
    /// </summary>
    public static LandingKind DeclaredKind(string root) => LandingKind.Local;

    /// <summary>The proof <paramref name="kind"/> is judged by; null where none answers for it.</summary>
    public static ILandedProof? For(LandingKind kind) => All.FirstOrDefault(proof => proof.Kinds.Contains(kind));

    /// <summary>The answer of the proof the repository's declared kind chooses; unknown where none answers for it.</summary>
    public static Task<LandedAnswer> JudgeAsync(LandedQuestion question, CancellationToken ct = default)
    {
        var kind = DeclaredKind(question.Root);
        return For(kind) is { } proof
            ? proof.ProveAsync(question, ct)
            : Task.FromResult(LandedAnswer.Unknown($"no landed proof answers for a {kind} landing yet"));
    }
}
