using System.Collections.Concurrent;

namespace Daoris.Knowledge;

/// <summary>
/// A read's answers, each kept with the <see cref="StoreMark"/> read before it was made: asked again while the mark holds, the kept
/// answer is the answer, and asked at a mark that moved, it is made again. Readers that ask at once at one mark share one making.
/// </summary>
/// <remarks>
/// The mark is read before the rows, so a kept answer is never older than the mark it is kept under. Only what the store holds may
/// go into an answer kept here: a clock, a file or a caller's standing is part of the key or not part of the answer.
/// </remarks>
public sealed class MarkedAnswers<TKey, T>(Func<CancellationToken, Task<StoreMark>> mark) where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, (StoreMark Mark, Task<T> Answer)> _kept = new();

    public async Task<T> AnswerAsync(TKey key, Func<Task<T>> make, CancellationToken ct = default)
    {
        var at = await mark(ct).ConfigureAwait(false);
        while (true)
        {
            if (_kept.TryGetValue(key, out var held) && held.Mark == at && !held.Answer.IsFaulted && !held.Answer.IsCanceled)
            {
                return await held.Answer.WaitAsync(ct).ConfigureAwait(false);
            }

            var making = (at, make());
            var stood = _kept.TryGetValue(key, out var before);
            if (stood ? _kept.TryUpdate(key, making, before) : _kept.TryAdd(key, making))
            {
                return await making.Item2.WaitAsync(ct).ConfigureAwait(false);
            }
        }
    }
}
