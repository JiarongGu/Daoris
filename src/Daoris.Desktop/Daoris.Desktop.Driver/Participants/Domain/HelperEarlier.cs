namespace Daoris.Driver;

/// <summary>
/// The ask a follow-up asks again after, as Daoris's record keeps it (COWORKUX1d, the co-worker design §7): its question and its answer, the
/// answer cut at <see cref="Bound"/> characters. A follow-up's helper is handed it quoted above the person's words; nothing is resumed.
/// </summary>
/// <param name="Id">The earlier ask.</param>
/// <param name="LastWords">The answer is the earlier helper's last words, since it never used its tool.</param>
/// <param name="Cut">The answer was longer than <see cref="Bound"/> and is cut there.</param>
public sealed record HelperEarlier(string Id, string Question, string Answer, bool LastWords, bool Cut)
{
    /// <summary>The longest earlier answer quoted, in characters: as long as a question may be.</summary>
    public const int Bound = 4000;

    /// <summary>The earlier ask's record, the answer bounded.</summary>
    public static HelperEarlier Of(HelperAsk ask, string answer, bool lastWords) =>
        answer.Length > Bound
            ? new(ask.Id, ask.Question, answer[..Bound], lastWords, Cut: true)
            : new(ask.Id, ask.Question, answer, lastWords, Cut: false);
}
