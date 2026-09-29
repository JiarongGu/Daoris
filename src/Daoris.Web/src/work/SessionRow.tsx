import { useTranslation } from 'react-i18next';
import * as Menu from '@radix-ui/react-dropdown-menu';
import type { Quest, Session } from '../api';
import { ago, elapsed } from '../format';
import { Dot, DotMark, Icon, type IconName, SESSION_ACTIVE, SESSION_DOT, shownState, Tip } from '../ui';
import { cn } from '../lib/cn';
import { isIntake, ownTree, sessionOrigin, sessionTitle } from './identity';
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
 * away. The title, the repository and the state are its name and its tip, since the strip has no room
 * for the words and a mark is never hue alone (D41 §6).
 */
export function SessionStripRow({ session, quest, opening, taking, selected = false, onSelect }: {
  session: Session;
  /** Whether a turn is in flight, as the driver says: a live chat between turns reads idle (UX5 U17). */
  taking?: boolean;
  quest?: Quest | null;
  /** What the person first said in it, where this machine holds its record (RAIL1). */
  opening?: string | null;
  selected?: boolean;
  onSelect?: (id: string) => void;
}) {
  const { t } = useTranslation();
  const shown = shownState(session, taking);
  const name = [sessionTitle(session, quest, opening), session.repository, t(`sessionState.${shown}`)].join(' · ');

  return (
    <li>
      <Tip content={name} side="right">
        <button
          type="button"
          aria-label={name}
          aria-current={selected || undefined}
          onClick={() => onSelect?.(session.id)}
          className={cn(
            'relative flex h-8 w-10 items-center justify-center rounded-control transition-colors duration-(--speed)',
            selected ? 'bg-accent-soft text-ink' : 'text-ink-soft hover:bg-accent-soft/50',
          )}
        >
          {/* The same 2px accent rail the activity bar gives its current place. */}
          {selected && <span aria-hidden className="absolute inset-y-1 left-0 w-0.5 rounded-full bg-accent" />}
          <span aria-hidden className="text-small font-semibold uppercase">{Array.from(session.repository)[0] ?? '?'}</span>
          <DotMark tone={SESSION_DOT[shown]} className="absolute right-1 top-1" />
        </button>
      </Tip>
    </li>
  );
}

export function SessionRow({
  session, quest, opening, root, taking, lastTurn, selected = false, onSelect, onDetach, onReview, onCopy,
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
  selected?: boolean;
  onSelect?: (id: string) => void;
  /**
   * The row's menu (RAIL1): what has no other home — its own window, its review, its id. Absent, no
   * menu. The session's verbs are never here: finish and stop have one owner each (D56).
   */
  onDetach?: (id: string) => void;
  onReview?: (id: string) => void;
  onCopy?: (id: string) => void;
}) {
  const { t } = useTranslation();
  const title = sessionTitle(session, quest, opening);
  const origin = sessionOrigin(session);
  const tree = ownTree(session, root);
  const running = SESSION_ACTIVE.has(session.state);
  const shown = shownState(session, taking);

  // One line of secondary facts, each absent when it has nothing to say — `MetaLine`'s rule, and
  // what keeps a rail of a dozen rows readable at 18rem. What the two absences MEAN is on the
  // helpers that produce them.
  const meta = [
    // An intake is a chat only by the way it was opened (INT4b); it says what it is (INT4g).
    t(isIntake(session) ? 'work.intake.kind' : session.kind === 'chat' ? 'work.kind.chat' : 'work.kind.driven'),
    tree ? t('work.rail.inTree', { tree }) : null,
    origin ? t('work.rail.on', { origin }) : null,
    t('work.rail.moved', { ago: ago(movedAt(session, lastTurn)) }),
  ].filter(Boolean).join(' · ');

  // Only the two unusual facts explain themselves: a tip that appeared on every row would be one
  // people stop reading.
  const tip = [
    tree ? t('work.rail.treeTip') : null,
    origin ? t('work.rail.onTip') : null,
  ].filter(Boolean).join(' ');

  const actions = [
    onDetach && { label: t('work.monitor.detach'), icon: 'external' as const, act: onDetach },
    onReview && { label: t('work.rail.menu.review'), icon: 'diff' as const, act: onReview },
    onCopy && { label: t('work.rail.menu.copy'), icon: 'copy' as const, act: onCopy },
  ].filter((action) => Boolean(action)) as Array<{ label: string; icon: IconName; act: (id: string) => void }>;

  return (
    // A group, so the menu's trigger shows on the row's hover and focus and stays out of the way else.
    <li className="group relative">
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
          <Dot tone={SESSION_DOT[shown]} label={t(`sessionState.${shown}`)} />
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

      {/* Beside the row, never inside it: a button inside a button is not a thing a page may hold. */}
      {actions.length > 0 && (
        <Menu.Root modal={false}>
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
          <Menu.Portal>
            <Menu.Content
              side="bottom"
              align="end"
              sideOffset={4}
              collisionPadding={8}
              className="z-30 min-w-44 rounded-control border border-line bg-overlay p-1 text-small shadow-lg"
            >
              {actions.map(({ label, icon, act }) => (
                <Menu.Item
                  key={label}
                  onSelect={() => act(session.id)}
                  className="flex cursor-default items-center gap-2 rounded-control px-2 py-1.5 text-ink outline-none data-[highlighted]:bg-accent-soft"
                >
                  <Icon name={icon} size={12} className="shrink-0 text-ink-faint" />
                  {label}
                </Menu.Item>
              ))}
            </Menu.Content>
          </Menu.Portal>
        </Menu.Root>
      )}
    </li>
  );
}
