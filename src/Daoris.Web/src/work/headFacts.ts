import type { TFunction } from 'i18next';
import type { Quest, Session } from '../api';
import { ago, elapsed } from '../format';
import type { Consideration } from '../signals';
import type { SweepBranch } from '../settings/Sweep';
import { SESSION_ACTIVE } from '../ui';
import type { SessionGrouping } from './groups';
import { isIntake } from './identity';
import { placedFact } from './SessionRow';

// The facts line under a head's title (UX7c, D152 §7, the UX7 design §5.2 and §5.3): the two or three facts a record's
// state makes matter, said once, faint, beside the state's pill and the title on the line above. Pure, so a head is
// handed its line and every state is a story; the facts are the row's (`placedFact`, `SessionGroups.Read`) and the
// record's, so a head and its row cannot disagree.

/** What a session's facts line is read from: its record, its shown word, and what the list's reader placed it by. */
export type SessionFactsOf = {
  session: Session;
  /** Its shown word (D126 §2.2): the record's state, *idle*, or the reader's derived one. */
  shown: string;
  /** Where the list's reader placed it, with its line's facts (SESSUX1a); absent where none answered. */
  grouping?: SessionGrouping | null;
  /** Its agent has accounts, so a record naming none ran on the tool's own sign-in (D125 §3.7). */
  ownSignIn?: boolean;
  /** What its own tree left, as the clean-up judged it (SESS1 S10): an ended session's landing. */
  branch?: SweepBranch | null;
};

/** The account it ran as, as the head says it: the profile it names, or the tool's own sign-in where it names none. */
function account(t: TFunction, session: Session, ownSignIn: boolean): string | null {
  if (session.profile) return session.profile;
  return ownSignIn ? t('quests.session.ownSignIn') : null;
}

/**
 * A session's facts line by its shown state (the UX7 design §5.2's table): its repository first, then what its state
 * makes matter. A live session's account and how long it has run, and how many failed before it where its quest's
 * earlier sessions failed; what holds it, from the row's own line where the reader placed it; an ended one's landing and
 * how long it ran. An intake says its ask. Nothing it has nothing to say: an absent fact is left out, never a dash.
 */
export function sessionFacts(t: TFunction, { session, shown, grouping, ownSignIn = false, branch }: SessionFactsOf): string[] {
  if (isIntake(session)) return [t('work.scope.ask', { id: session.ask }), t('work.intake.kind')];

  const facts: (string | null)[] = [session.repository];
  // What the row's line says for its group (D126 §2.2): parked after its failures, what it awaits, what holds the words,
  // its stop's hold, its work to review. Said here in the row's own words.
  const placed = placedFact(grouping, shown);
  const running = SESSION_ACTIVE.has(session.state);

  if (placed) {
    facts.push(t(placed.line, placed.values));
    if (!running) facts.push(t('work.facts.ran', { time: elapsed(session.created, session.updated) }));
    return facts.filter((fact): fact is string => Boolean(fact));
  }

  if (session.state === 'awaiting-person') {
    // Waiting on the person: how long it has waited, from when its record last moved; the card below says what it asks.
    facts.push(t('work.facts.waiting', { time: elapsed(session.updated) }));
    return facts.filter((fact): fact is string => Boolean(fact));
  }

  if (running) {
    facts.push(account(t, session, ownSignIn));
    if (session.kind !== 'chat') facts.push(elapsed(session.created));
    // UX7c (D152): the fact that matters most on a retried quest, another try and how many failed before it.
    const failed = typeof grouping?.strikes === 'number' && grouping.strikes > 0 ? grouping.strikes : null;
    if (failed !== null && session.kind !== 'chat') facts.push(t('work.facts.attempt', { count: failed, attempt: failed + 1 }));
    return facts.filter((fact): fact is string => Boolean(fact));
  }

  // Ended: where its work went, where its own tree says, and how long it ran.
  if (branch?.kind === 'landed') {
    facts.push(branch.where ? t('work.head.landed.on', { where: branch.where }) : t('work.head.landed.somewhere'));
  }
  facts.push(t('work.facts.ran', { time: elapsed(session.created, session.updated) }));
  return facts.filter((fact): fact is string => Boolean(fact));
}

/** What a quest's facts line is read from: the quest, its driven session's freshest record, and why the driver leaves it. */
export type QuestFactsOf = {
  quest: Quest;
  /** Its driven session's freshest record on this machine, where there is one. */
  session?: Session | null;
  /** Why this machine's driver leaves it waiting (D46 §3), where it does. */
  sitting?: Consideration | null;
};

/**
 * A quest's facts line (the UX7 design §5.3): whom it asks, who asked it and when it was filed, then the one live fact
 * where there is one: a session starting or working on it here, its park after its failed sessions, or when it closed.
 * The id, the full times and the lanes are its *Details*.
 */
export function questFacts(t: TFunction, { quest, session, sitting }: QuestFactsOf): string[] {
  const facts = [
    t('quests.facts.to', { repository: quest.to }),
    t('quests.facts.from', { from: quest.from }),
    t('quests.facts.filed', { ago: ago(quest.filed) }),
  ];

  if (quest.status === 'Done') return [...facts, t('quests.facts.done', { ago: ago(quest.updated) })];
  if (quest.status === 'Declined') return [...facts, t('quests.facts.declined', { ago: ago(quest.updated) })];

  if (session && SESSION_ACTIVE.has(session.state)) {
    if (session.state === 'working') return [...facts, t('quests.facts.working', { time: elapsed(session.created) })];
    if (session.state === 'awaiting-person') return [...facts, t('quests.facts.waiting')];
    return [...facts, t('quests.facts.starting')];
  }

  if (sitting?.verdict === 'Exhausted') {
    return [...facts, typeof sitting.strikes === 'number'
      ? t('quests.facts.parked', { count: sitting.strikes })
      : t('quests.facts.parkedSome')];
  }

  return quest.status === 'Taken' ? [...facts, t('quests.facts.taken', { ago: ago(quest.updated) })] : facts;
}
