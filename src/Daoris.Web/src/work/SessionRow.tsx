import { useTranslation } from 'react-i18next';
import type { Quest, Session } from '../api';
import { ago, elapsed, moment } from '../format';
import { Dot, Icon, Menu, SESSION_ACTIVE, SESSION_DOT, shownKey, StripMark } from '../ui';
import { cn } from '../lib/cn';
import { contextOffer } from '../menus/press';
import { actMenu, type SessionActId } from './acts';
import { type SessionGrouping, shownOf } from './groups';
import { isIntake, ownTree, sessionOrigin, sessionTitle } from './identity';
import { ListRowDoor } from './ListPane';
import { movedAt } from './rail';

/**
 * One session in the rail — the working surface's smallest unit of attention (design §3).
 *
 * @remarks
 * **A molecule: it imports no hook that reaches data** (components plan §2). The quest arrives as a
 * prop rather than being looked up, which is what makes "a session whose quest is closed", "a
 * session on another machine" and all nine states reachable in a story by passing them.
 *
 * **The anatomy is the reference console's, with D55's two facts added** (components plan §3a): the
 * mark and its word, what the session is for, how long it has been going, where it runs, which
 * tree it opened for itself, and when it last moved. `elapsed`, `sessionOrigin` and `ownTree`
 * each carry the reason they exist.
 *
 * **Selection is the frame's to hold** (see `SessionRail`): this row is told whether it is attended
 * and reports a click.
 */
/**
 * One session in the rail's 56px strip (FRAME6): its repository's initial and its mark, one press
 * away — a `StripMark` (D118 §5). The title, the repository and the state are its name and its tip,
 * since the strip has no room for the words and a mark is never hue alone (D41 §6).
 */
export function SessionStripRow({ session, quest, opening, taking, grouping, selected = false, onSelect }: {
  session: Session;
  /** Whether a turn is in flight, as the driver says: a live chat between turns reads idle (UX5 U17). */
  taking?: boolean;
  quest?: Quest | null;
  /** What the person first said in it, where this machine holds its record (RAIL1). */
  opening?: string | null;
  /** Where the list's reader placed it (SESSUX1c): a parked quest's last session wears *parked* here too. */
  grouping?: SessionGrouping | null;
  selected?: boolean;
  onSelect?: (id: string) => void;
}) {
  const { t } = useTranslation();
  const shown = shownOf(session, grouping, taking);
  const name = [sessionTitle(session, quest, opening), session.repository, t(shownKey(shown))].join(' · ');

  return (
    <StripMark
      label={name}
      initialOf={session.repository}
      tone={SESSION_DOT[shown]}
      current={selected}
      onPress={() => onSelect?.(session.id)}
    />
  );
}

/**
 * Where a session's work is now, as this machine's own files say (LOOK2b): whether the tree it opened is still here,
 * and its landing (D113), standing or gone since as the record holds it. The rail asks it once for its rows.
 */
export type SessionWhere = {
  treeGone: boolean;
  landed?: { repository: string; branch: string; state: 'standing' | 'gone' } | null;
};

export function SessionRow({
  session, quest, opening, root, where, taking, lastTurn, grouping, place, archived = false, selected = false, onSelect,
  acts = [], onAct,
}: {
  session: Session;
  /**
   * Whether a turn is in flight, as the driver says. A live chat between turns reads idle, a quiet
   * mark and the word (UX5 U17); absent, the record's word stands.
   */
  taking?: boolean;
  /** When its last turn ended here, as the driver says (RAIL2): *moved* reads the later of it and the record. */
  lastTurn?: string | null;
  /** The quest it serves, where the caller has it — absent is a state, not a gap (D49 §3). */
  quest?: Quest | null;
  /** What the person first said in it, where this machine holds its record — a conversation's name (RAIL1). */
  opening?: string | null;
  /**
   * The repository's registered checkout, so the row can tell a session in a tree of its OWN from
   * one in the root. Absent where the path is not answered, and then nothing is claimed.
   */
  root?: string | null;
  /**
   * Where its work is now (LOOK2b): its tree, or where its landing put the work once it read as landed. Absent where
   * the machine has not answered, and then the tree the record names stands, as before.
   */
  where?: SessionWhere | null;
  /**
   * Where the list's reader placed it (SESSUX1c, D126 §2.2): its derived word (*parked*, *awaiting reply*) and the
   * facts its line says for its group. Absent where no reader answered (a browser), and then the record speaks alone.
   */
  grouping?: SessionGrouping | null;
  /**
   * Its repository as its line says it, first, where no group header names it: the list by state (D126 §4.2). Absent,
   * the group it sits in says it.
   */
  place?: string | null;
  /**
   * Whether its line says it is archived (SESSUX1e, D126 §4.5): where the reader's mark stands and no heading above it
   * says so, as in a search and by repository. Under the Archived heading the heading says it.
   */
  archived?: boolean;
  selected?: boolean;
  onSelect?: (id: string) => void;
  /**
   * The row's menu (RAIL1; SESSUX1d, D126 §3.1): the session's acts where its row is, as the one rule offers them
   * (`offeredActs`), in that order. Handed in, since which apply is the rule's and not the row's. Finish and Decline…
   * stay its card's (D56).
   */
  acts?: readonly SessionActId[];
  /** An act pressed, with the session it was pressed for, for the one owner to carry out (`sessionActs.ts`). Absent, no menu. */
  onAct?: (act: SessionActId, id: string) => void;
}) {
  const { t } = useTranslation();
  const title = sessionTitle(session, quest, opening);
  const origin = sessionOrigin(session);
  // Where its work is (LOOK2b): where it landed while that reads as landed, as its review does (D113) — the landed
  // branch still stands, or the tree is gone — else the tree it opened, unless that is gone too.
  const landed = where?.landed && (where.treeGone || where.landed.state === 'standing') ? where.landed : null;
  const tree = landed || where?.treeGone ? null : ownTree(session, root);
  const running = SESSION_ACTIVE.has(session.state);
  const shown = shownOf(session, grouping, taking);
  const placed = placedFact(grouping, shown);

  // One line of secondary facts, each absent when it has nothing to say — `MetaLine`'s rule, and
  // what keeps a rail of a dozen rows readable at 18rem. What the two absences MEAN is on the
  // helpers that produce them. Its repository leads where no group header says it, then what its
  // group is about (SESSUX1c), since a cut line keeps its start.
  const meta = [
    place ?? null,
    archived ? t('work.rail.archived') : null,
    placed ? t(placed.line, placed.values) : null,
    // An intake is a chat only by the way it was opened (INT4b); it says what it is (INT4g).
    t(isIntake(session) ? 'work.intake.kind' : session.kind === 'chat' ? 'work.kind.chat' : 'work.kind.driven'),
    landed ? t(landed.state === 'gone' ? 'work.rail.landedGone' : 'work.rail.landedOn', { branch: landed.branch }) : null,
    tree ? t('work.rail.inTree', { tree }) : null,
    origin ? t('work.rail.on', { origin }) : null,
    t('work.rail.moved', { ago: ago(movedAt(session, lastTurn)) }),
  ].filter(Boolean).join(' · ');

  // Only the unusual facts explain themselves: a tip that appeared on every row would be one
  // people stop reading.
  const tip = [
    archived ? t('work.rail.archivedTip') : null,
    placed ? t(placed.tip) : null,
    landed ? t(landed.state === 'gone' ? 'work.rail.landedGoneTip' : 'work.rail.landedTip') : null,
    tree ? t('work.rail.treeTip') : null,
    origin ? t('work.rail.onTip') : null,
  ].filter(Boolean).join(' ');

  // What the rule offered, each absent where it does not apply and never disabled (D119 §3.2); nothing at all where there
  // is nowhere to report a press. One list for its ⋯ and its right-click (CTX1).
  const actions = actMenu(onAct ? acts : [], t, (act) => onAct?.(act, session.id));

  return (
    // A group, so the menu's trigger shows on the row's hover and focus and stays out of the way else;
    // and a row of its list, so the list's arrows move to it (D118 §3e). A right-click offers its ⋯'s acts (CTX1).
    <li data-list-row="" className="group relative" {...contextOffer({ label: title, acts: actions })}>
      {/* Every list's row door (FRAME1d): `aria-current` rather than `aria-selected`, since the row is a
          button, not a listbox option, and the accent stripe beside it is not something every reader has. */}
      <ListRowDoor chosen={selected} onPress={() => onSelect?.(session.id)}>
        <span className="flex items-baseline justify-between gap-2">
          <Dot tone={SESSION_DOT[shown]} label={t(shownKey(shown))} />
          {/* A span, so a finished session reads as a lifetime and a live one as an age. */}
          <span
            title={t('work.rail.elapsedTip')}
            className="shrink-0 font-mono text-meta text-ink-faint"
          >
            {elapsed(session.created, running ? null : session.updated)}
          </span>
        </span>
        <span
          title={title}
          className={cn('mt-0.5 block truncate text-body', running ? 'text-ink' : 'text-ink-soft')}
        >
          {title}
        </span>
        <span
          title={tip || undefined}
          className="block truncate font-mono text-meta text-ink-faint"
        >
          {meta}
        </span>
      </ListRowDoor>

      {/* Beside the row, never inside it: a button inside a button is not a thing a page may hold. */}
      {actions.length > 0 && (
        <Menu.Root>
          <Menu.Trigger asChild>
            <button
              type="button"
              aria-label={t('work.rail.menu.label', { title })}
              className={cn(
                'absolute bottom-1 right-1.5 flex h-5 w-5 items-center justify-center rounded-control text-ink-faint',
                'bg-raised opacity-0 transition-opacity duration-(--speed) hover:text-ink',
                'focus-visible:opacity-100 group-hover:opacity-100 data-[state=open]:opacity-100',
              )}
            >
              <Icon name="more" size={13} />
            </button>
          </Menu.Trigger>
          <Menu.Content side="bottom" align="end" highlight="accent" className="min-w-44">
            <Menu.Acts acts={actions} />
          </Menu.Content>
        </Menu.Root>
      )}
    </li>
  );
}

/**
 * What a row's line says for the group the reader placed it in (D126 §2.2), with the tip that explains it: how many
 * sessions failed before its quest parked, which question its quest waits on and who it was asked of, whose pause holds
 * its quest (PAUSE1e), what holds the words the person wrote to it after it ended (MSG1f2), that its stop holds its quest
 * here, or what its own tree holds to review. Null for every other row, whose group needs no sentence.
 */
function placedFact(
  grouping: SessionGrouping | null | undefined, shown: string,
): { line: string; values?: Record<string, unknown>; tip: string } | null {
  if (!grouping) return null;
  // A pause that holds its quest (PAUSE1e, D132 §6.1) is the reason before every other, a stop's hold included.
  if (grouping.pausedBy) {
    return {
      line: grouping.pausedBy.scope === 'ask' ? 'work.rail.pausedAsk' : 'work.rail.pausedQuest',
      values: { id: grouping.pausedBy.id },
      tip: 'work.rail.pausedTip',
    };
  }
  // What holds the person's words on a record that resumes later (MSG1f2, D137 §3.2): said from the facts where the page
  // has words for them, and in the planner's own sentence for anything else.
  if (grouping.holds) {
    const { why, reason, repository, until } = grouping.holds;
    const tip = 'work.rail.wordsHeldTip';
    if (why === 'hold' && repository) return { line: 'work.rail.wordsHeld.hold', values: { repository }, tip };
    if (why === 'cap') return { line: 'work.rail.wordsHeld.cap', tip };
    if (why === 'cooling' && until) return { line: 'work.rail.wordsHeld.cooling', values: { time: moment(until) }, tip };
    return { line: 'work.rail.wordsHeld.waits', values: { why: reason.replace(/\.$/, '') }, tip };
  }
  if (shown === 'parked') {
    return typeof grouping.strikes === 'number'
      ? { line: 'work.rail.parkedAfter', values: { count: grouping.strikes }, tip: 'work.rail.parkedTip' }
      : { line: 'work.rail.parkedAfterSome', tip: 'work.rail.parkedTip' };
  }
  if (shown === 'awaiting-reply' && grouping.awaits) {
    return grouping.awaitsOf
      ? { line: 'work.rail.awaitsOf', values: { quest: grouping.awaits, repository: grouping.awaitsOf }, tip: 'work.rail.awaitsTip' }
      : { line: 'work.rail.awaits', values: { quest: grouping.awaits }, tip: 'work.rail.awaitsTip' };
  }
  // A person's stop that holds its quest here (SESSUX1b, D126 §2.2): its quest moves again only on *Try again*.
  if (shown === 'stopped' && grouping.holdsQuest) return { line: 'work.rail.heldHere', tip: 'work.rail.heldTip' };
  if (grouping.group === 'review' && grouping.work) {
    const { commits, uncommitted } = grouping.work;
    // A count git could not give is still work (D88's proof keeps it): said as work, never as nothing.
    if (typeof commits === 'number' && commits > 0) return { line: 'work.rail.toReview', values: { count: commits }, tip: 'work.rail.reviewTip' };
    if (typeof uncommitted === 'number' && uncommitted > 0 && commits === 0) return { line: 'work.rail.uncommitted', tip: 'work.rail.reviewTip' };
    return { line: 'work.rail.workToReview', tip: 'work.rail.reviewTip' };
  }
  return null;
}
