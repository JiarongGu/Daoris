import type { TFunction } from 'i18next';
import type { Quest, Session } from '../data/api';
import { ago, elapsed, moment } from '../locales/format';
import type { Consideration } from '../data/signals';
import type { SweepBranch } from '../settings/Sweep';
import type { AccountNamer } from '../agents/tools';
import { SESSION_ACTIVE } from '../design/ui';
import type { SessionGrouping } from './groups';
import { isIntake } from './identity';
import { HANDED_REASONS } from './noteLines';
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
  /** What a person calls an account (ACCTNAME1), from the roster; absent, the record's id is said. */
  nameOf?: AccountNamer;
  /**
   * The agent of the session it continues (`from`, MIXAGENT3a-i), by the roster's name for it, where the frame holds that
   * session. Absent, the line says it continues another session.
   */
  continuesAgent?: string | null;
  /** The title of the session a helper helps (`serves`, MIXAGENT3a-i), where the frame holds it; absent, another session. */
  helping?: string | null;
  /** When the turn its agent is on was asked, while it has answered nothing (`firstAnswerWait`, WAITSHOWN1); absent otherwise. */
  waitingSince?: string | null;
  /** The call its agent is inside, and since when (`runningCall`, WAITSAY1); absent where none is open. */
  calling?: { title: string; since: string } | null;
};

/**
 * What the record's own links say (MIXAGENT3a-i, the mixed-agents design §4): the agent it continues, then why, in the hand-over's
 * words (`HANDED_REASONS`, the timeline's, `curve` among them since SWITCH1c-i), and whom a helper helps. A why the page has no
 * words for says nothing, never a guess.
 */
function linkFacts(t: TFunction, session: Session, { continuesAgent, helping }: SessionFactsOf): string[] {
  const facts: string[] = [];
  if (session.from) {
    facts.push(continuesAgent ? t('work.facts.continues', { agent: continuesAgent }) : t('work.facts.continuesAnother'));
    const why = session.fromWhy && Object.hasOwn(HANDED_REASONS, session.fromWhy) ? HANDED_REASONS[session.fromWhy] : null;
    if (why) facts.push(t(why.key));
  }
  if (session.serves) facts.push(helping ? t('work.facts.helping', { title: helping }) : t('work.facts.helpingAnother'));
  return facts;
}

/**
 * A session's facts line by its shown state (the UX7 design §5.2's table, as HEAD2c amends it): its repository first, then
 * its age and the one fact its state makes matter. A live session's age, and how many failed before it where its quest's
 * earlier sessions failed; what holds it, from the row's own line where the reader placed it (which names a cooling
 * account); an ended one's landing and how long it ran. An intake says its ask. The account is *Details*' (ACCTNAME1 names
 * it there). Nothing it has nothing to say: an absent fact is left out, never a dash. Whom it continues or helps follows the
 * repository in every state (MIXAGENT3a-i), since it says what the session is.
 */
export function sessionFacts(t: TFunction, of: SessionFactsOf): string[] {
  const { session, shown, grouping, branch } = of;
  if (isIntake(session)) return [t('work.scope.ask', { id: session.ask }), t('work.intake.kind')];

  const facts: (string | null)[] = [session.repository, ...linkFacts(t, session, of)];
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
    // HEAD2c (D162): the account is *Details*'. Where it explains a hold, the row's own line above already says it.
    facts.push(session.kind === 'chat' ? t('work.facts.started', { ago: ago(session.created) }) : elapsed(session.created));
    // UX7c (D152): the fact that matters most on a retried quest, another try and how many failed before it.
    const failed = typeof grouping?.strikes === 'number' && grouping.strikes > 0 ? grouping.strikes : null;
    if (failed !== null && session.kind !== 'chat') facts.push(t('work.facts.attempt', { count: failed, attempt: failed + 1 }));
    // WAITSHOWN1: its agent has said nothing since it was asked; a resumed long conversation can wait many minutes.
    if (of.waitingSince && session.state === 'working') facts.push(t('work.facts.firstAnswer', { time: moment(of.waitingSince) }));
    // WAITSAY1: a call that runs for minutes is what the session waits on; said with how long, and only while it is working.
    if (of.calling && session.state === 'working') facts.push(t('work.facts.calling', { title: of.calling.title, elapsed: elapsed(of.calling.since) }));
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
 * A quest's facts line (HEAD2b, D162; the entry-and-heads design §2, amending the UX7 design §5.3): a hold the person lifts
 * first where there is one (*awaits your yes*, *awaits review*: their sentences stay in their cards), then where it goes
 * and its age, then the one live fact where there is one: a session starting or working on it here, its park after its
 * failed sessions, or when it closed. The requester, the id, the full times and the lanes are its *Details*.
 */
export function questFacts(t: TFunction, { quest, session, sitting }: QuestFactsOf): string[] {
  const unreviewed = quest.held === true && quest.hold === 'unreviewed';
  // FABRIC-CLOSE1c3: a step its source's reopen holds waits on the person's go-on or abandon, which no yes lifts.
  const reopened = quest.held === true && quest.hold === 'source-reopened';
  const facts = [
    ...(unreviewed ? [t('review.pill')] : reopened ? [t('quests.card.reopened')] : quest.held === true ? [t('quests.card.held')] : []),
    t('quests.facts.to', { repository: quest.to }),
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

  // FABRIC-CLOSE1d (§E5): a taken quest at its submission says when it was submitted; it is done only at its graph's close.
  if (quest.status === 'Taken' && quest.submission) return [...facts, t('quests.facts.submitted', { ago: ago(quest.submission.at) })];
  return quest.status === 'Taken' ? [...facts, t('quests.facts.taken', { ago: ago(quest.updated) })] : facts;
}
