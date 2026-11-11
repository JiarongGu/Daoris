namespace Daoris.Desktop;

/// <summary>
/// A slice's judgement of a read that a kept list answers: the list is the same object while nothing in it moved, so it is judged once
/// and the same judgement handed back, which <see cref="StateSlice"/> compares no further. A slice ticks every period with no window
/// open, and judging every record of an unmoved list was its whole cost.
/// </summary>
public sealed class JudgedOnce<TList>(Func<TList, IReadOnlyList<SliceJudged>> judge) where TList : class
{
    private readonly Lock _gate = new();
    private (TList List, IReadOnlyList<SliceJudged> Judged)? _last;

    /// <summary>The judgement of <paramref name="list"/>: the one last made where it is the list last judged, else a new one.</summary>
    public IReadOnlyList<SliceJudged> Of(TList list)
    {
        lock (_gate)
        {
            if (_last is { } last && ReferenceEquals(last.List, list)) return last.Judged;
        }

        var judged = judge(list);
        lock (_gate) _last = (list, judged);
        return judged;
    }
}
