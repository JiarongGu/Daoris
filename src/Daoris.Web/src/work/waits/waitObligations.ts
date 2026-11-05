import type { Wait, WaitFacts } from './waits';

/** What a wait's key is read against: its session, the ask its go-aheads are on, and the set-up step its review gate waits on. */
type ObligationOn = { session: string; ask?: string | null; quest?: string | null };

/**
 * The obligation a wait of a session is (COMMS1j, the comms design §4.1 clause 3, §7): the key *What needs you* lists its row by
 * (`obligationOf`), so the foot, the bell and *Notifications* count one wait once. Each is the key .NET's row for the same wait
 * carries (`NeedsAPerson`): a go-ahead by its exchange's id, the work's acceptance as its *To review* row, the rest by kind and id.
 */
export function waitObligation(wait: Wait, on: ObligationOn): string {
  switch (wait.kind) {
    case 'question': return `parked:${on.session}`;
    case 'intake': return `intake:${wait.ask}`;
    case 'goAhead': return `go-ahead:${on.ask ?? ''}/${wait.number}`;
    case 'permission': return `permission:${on.session}#${wait.request}`;
    case 'gitAsk': return `git-ask:${wait.id}`;
    case 'answerAlone': return `answer:${on.session}`;
    case 'rule': return `rule:${wait.id}`;
    case 'helper': return `helper:${wait.id}`;
    case 'handOver': return `hand-over:${wait.id}`;
    // *Accept…* and the rebase words in its place are one decision on the work, the row Sessions places *To review*.
    case 'accept': case 'rebase': return `review:${on.session}`;
    case 'opinion': return `opinion:${on.session}`;
    case 'review': return `set-up:${on.quest ?? on.session}`;
    case 'discard': return `work:${on.session}`;
  }
}

/**
 * The obligations `waits` are, each once, in the order they are drawn. A park on a go-ahead its session asked, or on its agent's ask to
 * hand it over, waits on that one decision, so its question is that obligation and never a second (`NeedsAPerson.OwnAsks`). `all` is
 * every wait of the session, drawn or not, which says what the park waits on where its box draws the decision.
 */
export function waitObligations(facts: WaitFacts, waits: readonly Wait[], all: readonly Wait[] = waits): string[] {
  const on: ObligationOn = { session: facts.session.id, ask: facts.goAheadsOn ?? null, quest: facts.landing?.review?.quest ?? null };
  const decision = all.find((wait) => wait.kind === 'goAhead' || wait.kind === 'handOver');
  const parkIs = decision ? waitObligation(decision, on) : null;
  return [...new Set(waits.map((wait) => (wait.kind === 'question' && parkIs ? parkIs : waitObligation(wait, on))))];
}
