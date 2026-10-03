import type { Quest, Session } from '../api';
import { answeredPark, type IconName, type MenuAct, SESSION_ACTIVE } from '../ui';
import type { SessionGrouping } from './groups';
import { isHelp, isIntake, sessionOrigin } from './identity';
import { askOf, type WorkTarget } from './pausing';
import type { SessionWhere } from './SessionRow';

// The acts on a session, as its row's ⋯ and its page header offer them (SESSUX1d, D126 §3.1): which apply, in which
// order, how each is named, and what a stop says before it is meant. Pure, so both doors read one rule and a molecule may
// read it; `sessionActs.ts` is the one owner that carries each act out.

/** Every act a session's doors offer, by the name `sessionActs.ts` runs it by. Finish and Decline… stay its card's. */
export type SessionActId =
  | 'answer' | 'stop' | 'pauseQuest' | 'pauseAsk' | 'resumeQuest' | 'resumeAsk' | 'retry' | 'review' | 'openFolder'
  | 'terminal' | 'detach' | 'archive' | 'unarchive' | 'delete' | 'copy';

/** §3.1's order, the order a menu lists them in; a pause beside the stop, and a resume where *Try again* stands (D132 §7.1). */
export const ACT_ORDER: readonly SessionActId[] = [
  'answer', 'stop', 'pauseQuest', 'pauseAsk', 'resumeQuest', 'resumeAsk', 'retry', 'review', 'openFolder', 'terminal',
  'detach', 'archive', 'unarchive', 'delete', 'copy',
];

/**
 * How each act is named and drawn: its catalogue key and its glyph, the one name it has on every door (D116). `danger` is
 * the act that ends work in flight, which wears the outcome's hue (platform language §4).
 */
export const ACT_LOOK: Record<SessionActId, { label: string; icon: IconName; danger?: boolean }> = {
  answer: { label: 'work.act.answer', icon: 'answer' },
  stop: { label: 'work.act.stop', icon: 'stop', danger: true },
  // PAUSE1e: the player's pause and play, since a pause keeps everything and Resume carries it on where it stood.
  pauseQuest: { label: 'work.act.pauseQuest', icon: 'pause' },
  pauseAsk: { label: 'work.act.pauseAsk', icon: 'pause' },
  resumeQuest: { label: 'work.act.resumeQuest', icon: 'resume' },
  resumeAsk: { label: 'work.act.resumeAsk', icon: 'resume' },
  retry: { label: 'work.act.retry', icon: 'refresh' },
  review: { label: 'work.head.review', icon: 'diff' },
  openFolder: { label: 'work.act.openFolder', icon: 'folder' },
  terminal: { label: 'work.act.terminal', icon: 'terminal' },
  detach: { label: 'work.monitor.detach', icon: 'external' },
  archive: { label: 'work.act.archive', icon: 'archive' },
  unarchive: { label: 'work.act.unarchive', icon: 'unarchive' },
  // SESSUX1f: the bin, since nothing brings it back; an archive is the box.
  delete: { label: 'work.act.delete', icon: 'remove', danger: true },
  copy: { label: 'work.rail.menu.copy', icon: 'copy' },
};

/**
 * A session's acts as a menu's rows (CTX1, D138): each by its one name and glyph, its press reported. A row's ⋯ and its
 * right-click, and the page header's ⋯ and the page's right-click, each draw this one list, so they never disagree.
 */
export function actMenu(
  acts: readonly SessionActId[], t: (key: string) => string, press: (act: SessionActId) => void, disabled = false,
): MenuAct[] {
  return acts.map((act) => ({
    id: act, label: t(ACT_LOOK[act].label), icon: ACT_LOOK[act].icon, disabled, onSelect: () => press(act),
  }));
}

/** What decides a session's acts: its record, where the list's reader placed it, its repository's checkout, and where its work is. */
export type ActFacts = {
  session: Session;
  /** Its quest, where the page holds it: whether an ask asked it decides *Pause ask…* (PAUSE1e). */
  quest?: Quest | null;
  /** Where the reader placed it (SESSUX1a); absent where none answered, and then only the record speaks. */
  grouping?: SessionGrouping | null;
  /** Its repository's registered checkout on this machine, answered only to the machine that holds it (D48 §7). */
  root?: string | null;
  /** Where its work is now (LOOK2b): whether a tidy took its tree, and its landing. */
  where?: SessionWhere | null;
};

/**
 * The folder a session worked in, where this machine holds it (D126 §3.5): the tree its record names until a tidy took
 * it, which is its own or its repository's checkout, else that checkout. Null for a teammate's record, an intake (it
 * runs in Daoris's own room), Ask Daoris, and a record naming nothing with no checkout here. The terminal opens here;
 * the page never prints it (platform language §4).
 */
export function folderOf({ session, root, where }: ActFacts): string | null {
  if (sessionOrigin(session) !== null || isIntake(session) || isHelp(session)) return null;
  if (session.tree) return where?.treeGone ? null : session.tree;
  return root ?? null;
}

/**
 * Which acts a session is offered on a door (D126 §3.1), in §3.1's order: each where it applies and absent where it does
 * not, never disabled (D119 §3.2).
 *
 * @remarks
 * - **Answer…** for a session waiting on you, on its row: its header's card keeps the answer. Not for a park the person
 *   answered, which goes on at the driver's next look and has no box to answer in (ANSWER1c).
 * - **Stop…** for a live session this machine runs. **Try again** for a parked quest's last session, and a stopped one
 *   whose stop holds its quest. **Review** where there is work to read: its tree, its landing, or a place in To review.
 * - **Open folder** and **Open a terminal here** where its folder is on this machine (`folderOf`).
 * - **Archive** where the reader placed it in Ended, **Unarchive** wherever its mark stands, **Copy session ID** always.
 * - **Delete…** where the reader says the delete would be taken (SESSUX1f, D126 §5.4): a conversation that served no
 *   quest, its record one the ledger would delete, its tree and landing no longer here. D95's way: offered only there.
 * - **A teammate's record** (SYNC4) is offered only what reaches no process: the archive marks, which are this machine's
 *   (§5.2), and its id.
 * - **Pause quest…** for a live driven session, and **Pause ask…** where its quest is an ask's, unless a pause holds it
 *   already; **Resume ask** or **Resume quest** where one does, in *Try again*'s place, which a pause comes before (PAUSE1e,
 *   D132 §6.1, §7.1). A parked session's quest gets no pause verdict, so a pause of its work offers its pause again, which
 *   answers that it was paused already.
 */
export function offeredActs(facts: ActFacts, door: 'row' | 'header'): SessionActId[] {
  const { session, grouping, where } = facts;
  const here = sessionOrigin(session) === null;
  const intake = isIntake(session);
  const live = SESSION_ACTIVE.has(session.state);
  const pausedBy = grouping?.pausedBy ?? null;
  const offered = new Set<SessionActId>();

  if (here) {
    if (door === 'row' && session.state === 'awaiting-person' && !answeredPark(session) && !intake) offered.add('answer');
    if (live) offered.add('stop');
    const driven = live && Boolean(session.quest) && session.kind !== 'chat' && !intake && !isHelp(session);
    if (driven && !pausedBy) {
      offered.add('pauseQuest');
      if (askOf(facts.quest)) offered.add('pauseAsk');
    }
    if (pausedBy) offered.add(pausedBy.scope === 'ask' ? 'resumeAsk' : 'resumeQuest');
    else if (grouping?.shown === 'parked' || (session.state === 'stopped' && grouping?.holdsQuest)) offered.add('retry');
    if (!intake && !isHelp(session)
      && (grouping?.group === 'review' || Boolean(where?.landed) || (Boolean(session.tree) && !where?.treeGone))) {
      offered.add('review');
    }
    if (folderOf(facts) !== null) {
      offered.add('openFolder');
      offered.add('terminal');
    }
    offered.add('detach');
    if (grouping?.deletable) offered.add('delete');
  }
  if (grouping?.group === 'ended' && !grouping.archived) offered.add('archive');
  if (grouping?.archived) offered.add('unarchive');
  offered.add('copy');

  return ACT_ORDER.filter((act) => offered.has(act));
}

/**
 * The header's loud act (§3.2): *Resume* while a pause holds its quest (D132 §6.1), *Try again* while parked or held,
 * *Review* in To review, else none. The one primary control a page has is its next step (platform language §4).
 */
export function primaryAct(acts: readonly SessionActId[], grouping?: SessionGrouping | null): SessionActId | null {
  if (acts.includes('resumeAsk')) return 'resumeAsk';
  if (acts.includes('resumeQuest')) return 'resumeQuest';
  if (acts.includes('retry')) return 'retry';
  if (acts.includes('review') && grouping?.group === 'review') return 'review';
  return null;
}

/**
 * The page header's acts as it draws them (§3.2): its loud act, *Stop…* while it is offered, then the rest behind its ⋯;
 * *Answer…* stays the card's under the header. `all` is that order whole, which the page's right-click offers (CTX1).
 */
export function headerActs(
  acts: readonly SessionActId[], primary: SessionActId | null,
): { lead: SessionActId | null; stop: boolean; rest: SessionActId[]; all: SessionActId[] } {
  const lead = primary && acts.includes(primary) ? primary : null;
  const stop = acts.includes('stop');
  const rest = acts.filter((act) => act !== lead && act !== 'stop' && act !== 'answer');
  return { lead, stop, rest, all: [...(lead ? [lead] : []), ...(stop ? ['stop' as const] : []), ...rest] };
}

/**
 * The work a pause or a resume on a session names (PAUSE1e, D132 §7.1): *Pause quest…* its quest, *Pause ask…* the ask that
 * asked it, and *Resume* the pause that holds it, as the reader named it. Null for any other act, or where the fact is missing.
 */
export function workTargetOf(act: SessionActId, facts: Pick<ActFacts, 'session' | 'quest' | 'grouping'>): WorkTarget | null {
  switch (act) {
    case 'pauseQuest': return facts.session.quest ? { scope: 'quest', id: facts.session.quest } : null;
    case 'pauseAsk': {
      const ask = askOf(facts.quest);
      return ask ? { scope: 'ask', id: ask } : null;
    }
    case 'resumeAsk':
    case 'resumeQuest': {
      const pause = facts.grouping?.pausedBy;
      return pause ? { scope: pause.scope, id: pause.id } : null;
    }
    default: return null;
  }
}

/**
 * What a stop says under the header before its second press (§3.3), by what the session is: a driven session holding
 * its quest, one that has not taken it yet, one waiting on you, a chat, an intake. A driven session whose quest is still
 * open has not taken it; any other has, or is about to say so. A park the person answered is no longer stopped
 * unanswered: it says what a driven session's stop says (ANSWER1c).
 */
export function stopAsk(session: Session, quest?: Quest | null): { key: string; values: Record<string, string> } {
  if (isIntake(session)) return { key: 'work.intake.stopMeans', values: {} };
  if (session.kind === 'chat') return { key: 'work.stop.chat', values: {} };
  if (session.state === 'awaiting-person' && !answeredPark(session)) return { key: 'work.stop.parked', values: {} };
  if (quest?.status === 'Open') return { key: 'work.stop.drivenOpen', values: { quest: quest.id } };
  return { key: 'work.stop.drivenHeld', values: {} };
}
