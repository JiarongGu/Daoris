import { useTranslation } from 'react-i18next';
import type { Quest, Session } from '../api';
import { ago, elapsed } from '../format';
import { Dot, SESSION_ACTIVE, SESSION_DOT } from '../ui';
import { cn } from '../lib/cn';
import { isIntake, ownTree, sessionOrigin, sessionTitle } from './identity';

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
export function SessionRow({ session, quest, root, selected = false, onSelect }: {
  session: Session;
  /** The quest it serves, where the caller has it — absent is a state, not a gap (D49 §3). */
  quest?: Quest | null;
  /**
   * The repository's registered checkout, so the row can tell a session in a tree of its OWN from
   * one in the root. Absent where the path is not answered, and then nothing is claimed.
   */
  root?: string | null;
  selected?: boolean;
  onSelect?: (id: string) => void;
}) {
  const { t } = useTranslation();
  const title = sessionTitle(session, quest);
  const origin = sessionOrigin(session);
  const tree = ownTree(session, root);
  const running = SESSION_ACTIVE.has(session.state);

  // One line of secondary facts, each absent when it has nothing to say — `MetaLine`'s rule, and
  // what keeps a rail of a dozen rows readable at 18rem. What the two absences MEAN is on the
  // helpers that produce them.
  const meta = [
    // An intake is a chat only by the way it was opened (INT4b); it says what it is (INT4g).
    t(isIntake(session) ? 'work.intake.kind' : session.kind === 'chat' ? 'work.kind.chat' : 'work.kind.driven'),
    tree ? t('work.rail.inTree', { tree }) : null,
    origin ? t('work.rail.on', { origin }) : null,
    t('work.rail.moved', { ago: ago(session.updated) }),
  ].filter(Boolean).join(' · ');

  // Only the two unusual facts explain themselves: a tip that appeared on every row would be one
  // people stop reading.
  const tip = [
    tree ? t('work.rail.treeTip') : null,
    origin ? t('work.rail.onTip') : null,
  ].filter(Boolean).join(' ');

  return (
    <li>
      <button
        type="button"
        // `aria-current` rather than `aria-selected`: the row is a button, not a listbox option,
        // and the accent stripe beside it is not something every reader has.
        aria-current={selected || undefined}
        onClick={() => onSelect?.(session.id)}
        className={cn(
          'block w-full border-l-[3px] px-2.5 py-1.5 text-left transition-colors duration-(--speed)',
          'hover:bg-accent-soft/50',
          selected ? 'border-l-accent bg-accent-soft' : 'border-l-transparent',
        )}
      >
        <span className="flex items-baseline justify-between gap-2">
          <Dot tone={SESSION_DOT[session.state]} label={t(`sessionState.${session.state}`)} />
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
      </button>
    </li>
  );
}
