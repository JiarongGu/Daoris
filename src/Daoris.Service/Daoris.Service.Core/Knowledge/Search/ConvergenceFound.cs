namespace Daoris.Knowledge;

/// <summary>
/// What a convergence pass found, and the failure of its semantic half where one ran and failed: an empty answer from a
/// down endpoint is not "nothing converges".
/// </summary>
/// <param name="Candidates">The groups, strongest method first.</param>
/// <param name="SemanticFailure">The embedder's failure, or null.</param>
public sealed class ConvergenceFound(IReadOnlyList<ConvergenceCandidate> Candidates, string? SemanticFailure = null)
    : IReadOnlyList<ConvergenceCandidate>
{
    public string? SemanticFailure { get; } = SemanticFailure;

    public int Count => Candidates.Count;

    public ConvergenceCandidate this[int index] => Candidates[index];

    public IEnumerator<ConvergenceCandidate> GetEnumerator() => Candidates.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
