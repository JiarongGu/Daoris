namespace Daoris.Driver;

/// <summary>
/// A run's words to its work's session (a failed check's, the person's changes): through the one door that reaches a session that is working,
/// its inbox, as a terminal's <c>session say</c> does, and through the record's say door for any other.
/// </summary>
/// <remarks>
/// <para><b>A running session</b> this loop runs is told as Daoris's urgent word under the telling's key: kept on its record first, then held by
/// its inbox, which sends it at the session's next step where its door takes words then, and holds it for the turn's end where it takes them only
/// then (<see cref="SayAnswer.Reaches"/> says which). Said again under the key it is held once.</para>
///
/// <para><b>Any other session</b> is said to at the record's door, which keeps the words for a session that does not run and refuses one that
/// does as <c>running</c>: a conversation or a session another loop runs. The refusal is the caller's to offer again once the record does not run
/// (<see cref="NodeTold.Running"/>); an inbox that closed as its session wound up falls to it, so words are never dropped.</para>
///
/// <para>Where the record went on in its line's head, the words are said there as every say is (<see cref="WordsHead"/>).</para>
/// </remarks>
/// <param name="inboxOf">A running session's inbox where this loop runs it and its door takes words, else null.</param>
/// <param name="doorOf">A running session's record as its one dispatcher keeps words on it.</param>
/// <param name="record">The record's say door: the words said to a session that is not running, under their key, as Daoris's.</param>
public sealed class WorkSay(
    Func<string, DrivenInbox?> inboxOf, Func<string, ISaidDoor?> doorOf, Func<string, string, string, CancellationToken, Task<SayAnswer>> record)
{
    /// <summary>Tell <paramref name="session"/>, or the head of its line, <paramref name="text"/> under <paramref name="key"/>: the last answer.</summary>
    public async Task<SayAnswer> SayAsync(string session, string text, string key, CancellationToken ct) =>
        (await WordsHead.FollowAsync(session, at => SayAtAsync(at, text, key, ct)).ConfigureAwait(false)).Said;

    private async Task<SayAnswer> SayAtAsync(string session, string text, string key, CancellationToken ct)
    {
        if (inboxOf(session) is not { } inbox) return await record(session, text, key, ct).ConfigureAwait(false);

        var word = new ChatMessage(text, []) { Level = MessageLevel.Urgent, Key = key, Daoris = new DaorisWord(key) };
        var kept = await inbox.KeepOrHoldAsync(doorOf(session), word, ct).ConfigureAwait(false);
        switch (kept)
        {
            // Winding up: the record's door answers for it, and a refusal is offered again.
            case { Outcome: InboxKeep.Closed }:
                return await record(session, text, key, ct).ConfigureAwait(false);
            case { Outcome: InboxKeep.Continued, Said: { } head }:
                return head;
            default:
                return new SayAnswer(true, "held for the session by its inbox")
                {
                    Word = kept.Said?.Word, Settled = kept.Said?.Settled,
                    Reaches = kept.Said?.Settled is not null ? null : kept.Reach == DrivenReach.NextStep ? SayAnswer.NextStep : SayAnswer.TurnEnd,
                };
        }
    }
}
