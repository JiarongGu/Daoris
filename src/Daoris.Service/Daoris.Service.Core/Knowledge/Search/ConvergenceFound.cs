namespace Daoris.Knowledge;

/// <summary>
/// What a convergence pass found, and why its semantic half did not run where it was configured and failed: an empty answer
/// from a down endpoint is not "nothing converges".
/// </summary>
/// <param name="Candidates">The groups, strongest method first.</param>
/// <param name="SemanticFailure">The embedder's failure, or null where the pass ran or none was configured.</param>
public sealed class ConvergenceFound(IReadOnlyList<ConvergenceCandidate> Candidates, string? SemanticFailure = null)
    : IReadOnlyList<ConvergenceCandidate>
{
    /// <summary>The embedder's failure, or null where the pass ran or none was configured.</summary>
    public string? SemanticFailure { get; } = SemanticFailure;

    public int Count => Candidates.Count;

    public ConvergenceCandidate this[int index] => Candidates[index];

    public IEnumerator<ConvergenceCandidate> GetEnumerator() => Candidates.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
