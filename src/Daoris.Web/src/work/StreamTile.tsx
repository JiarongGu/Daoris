import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import type { Quest, Session } from '../api';
import { elapsed } from '../format';
import { Button, Dot, Icon, SESSION_ACTIVE, SESSION_DOT } from '../ui';
import { sessionOrigin, sessionTitle } from './identity';

/**
 * One session's stream, headed by what it is (SURF8) — the monitor window's unit.
 *
 * @remarks
 * **A molecule: it imports no hook** (components plan §2). The stream itself is an organism and
 * arrives as `children`, which is what makes every state here reachable by passing props — a parked
 * session, a conversation with no quest, and the one nothing local can produce: a session running on
 * another machine.
 *
 * **Its anatomy is the rail row's, laid out wide**: the mark and its word, what the session is for,
 * which repository, and how long it has been going. A monitor is read at a glance from across a
 * desk, so the identity is one line and the stream is the rest.
 *
 * **A session somewhere else has no stream here, and says so.** A record fed from another machine is
 * keyed `origin/id` (D47 §6) and its console never left that machine (D47 §4). An empty well would
 * read as "this session is silent", which is the one thing a console must never imply.
 */
export function StreamTile({ session, quest, onDetach, children }: {
  session: Session;
  /** The quest it serves, where the caller has it — absent is a state, not a gap. */
  quest?: Quest | null;
  /**
   * Open this session in a window of its own. Absent where nothing can open one — a browser, or a
   * window that is already the detached one.
   */
  onDetach?: (id: string) => void;
  /** The stream. Rendered only where this machine is the one that holds it. */
  children?: ReactNode;
}) {
  const { t } = useTranslation();
  const origin = sessionOrigin(session);
  const running = SESSION_ACTIVE.has(session.state);

  return (
    // `flex-1`, because a tile is given its height by the grid and the stream fills what is left.
    // Without it the section sizes to its content and a console region reads as a text field.
    <section className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-card border border-line bg-raised">
      <header className="flex shrink-0 items-baseline gap-2 border-b border-line px-3 py-1.5">
        <Dot tone={SESSION_DOT[session.state]} label={t(`sessionState.${session.state}`)} />
        <span title={sessionTitle(session, quest)} className="truncate text-body">
          {sessionTitle(session, quest)}
        </span>
        <span className="shrink-0 font-mono text-meta text-ink-faint">{session.repository}</span>
        <span
          title={t('work.rail.elapsedTip')}
          className="ml-auto shrink-0 font-mono text-meta text-ink-faint"
        >
          {elapsed(session.created, running ? null : session.updated)}
        </span>
        {/* A `title`, not a `Tip`: a molecule that needed a tooltip provider would be a molecule
            no story and no props-only test could render (components plan §2). */}
        {onDetach && (
          <Button
            variant="ghost"
            aria-label={t('work.monitor.detach')}
            title={t('work.monitor.detach')}
            onClick={() => onDetach(session.id)}
            className="-my-0.5 h-6 w-6 shrink-0 justify-center px-0"
          >
            <Icon name="external" size={13} />
          </Button>
        )}
      </header>

      <div className="flex min-h-0 flex-1 flex-col px-3 py-2">
        {origin
          ? <p className="m-0 text-small text-ink-faint">{t('work.monitor.elsewhere', { origin })}</p>
          : children}
      </div>
    </section>
  );
}
