namespace Daoris.Driver;

/// <summary>
/// What a landing supervisor's word on a node asks of the record it waits on: no second word beside one, the person's taking its place,
/// and the pass dropping it where the attempt it names or the decision that released it no longer stands.
/// </summary>
internal static class SupervisedWords
{
    /// <summary>Why a word by <paramref name="by"/> is refused beside <paramref name="standing"/>: any word pending refuses a supervisor's, and a supervisor's alone gives way to the person's.</summary>
    public static string? Refused(NodeWord? standing, NodeWordBy? by) =>
        standing is null || (by is null && standing.By is not null) ? null : RunNodeWords.Worded;

    /// <summary>The decision the person's word takes the place of: the supervisor's pending one on that node.</summary>
    public static string? Replaced(NodeWord? standing, NodeWordBy? by) => by is null ? standing?.By?.Decision : null;

    /// <summary>
    /// Why the pass applies no <paramref name="word"/> at <paramref name="standing"/>: a supervisor's word on another attempt than the
    /// one it names, or whose decision is not live in this run (a restart empties the decisions, the record keeps the word).
    /// </summary>
    public static string? Dropped(NodeWord word, NodeStatus standing, ILandingWords? landings) =>
        word.By is not { } by ? null
        : standing.Attempt != word.Attempt || landings?.Live(by.Decision) != true ? RunNodeWords.Stale
        : null;

    /// <summary>Tell the decisions the pass settled: a supervisor's word applied or dropped, and a supervisor's word the person's took the place of.</summary>
    public static void Settle(ILandingWords? landings, IEnumerable<(NodeWord Word, string? Refused)> applied)
    {
        if (landings is null) return;
        foreach (var (word, refused) in applied)
        {
            if (word.Supersedes is { } replaced) landings.Settled(replaced, SupervisorState.Superseded, null);
            if (word.By is { } by) landings.Settled(by.Decision, refused is null ? SupervisorState.WentOn : SupervisorState.Stale, refused);
        }
    }
}

/// <summary>The landing supervisor's decisions as the pass asks them: whether one is live in this run, and what became of its word.</summary>
public interface ILandingWords
{
    /// <summary>Whether the decision is released and live in this run: its window past, its word kept, nothing since closing it.</summary>
    bool Live(string decision);

    /// <summary>The decision's word is applied (<c>went-on</c>), dropped (<c>stale</c>), or replaced by the person's (<c>superseded</c>).</summary>
    void Settled(string decision, SupervisorState to, string? why);
}
