import { useTranslation } from 'react-i18next';
import type { Quest, Session } from '../api';
import { useQuests, useSessions } from '../queries';
import { useDriver, useOpenWindow } from '../shell';
import { EmptyState, type Notify, SESSION_ACTIVE, SkeletonRows, useErrorNotify } from '../ui';
import { SessionConsole } from '../SessionConsole';
import { SessionRail } from './SessionRail';
import { StreamTile } from './StreamTile';
import { sessionOrigin } from './identity';
import { sessionWindowName } from './window';
import { waitingFirst } from './rail';

/** The element id a tile is scrolled to by. Derived, so the rail and the tile cannot disagree. */
const tileId = (session: string) => `stream-${session}`;

/**
 * The monitor window (D55 §b, SURF8): everything running, live, on a second screen.
 *
 * @remarks
 * **It is a route into the same bundle**, not a second frontend — so it is the components the Work
 * frame already has, arranged for a screen nobody is typing into. A change to a tile lands in both
 * windows because there is one of it.
 *
 * **Read-only, deliberately.** D56's rule is one owner for the verbs at a time, and two windows
 * offering the same three moves on one parked session is exactly the arrangement it exists to
 * prevent. The main window is that owner; this window's only act is to hand one session a window of
 * its own, which is a window command rather than a move on a session.
 *
 * **What it answers is "what is each session doing"** — which is why a tray icon was rejected for
 * it (D55 §b): a tray icon answers whether anything is running, and that is a different question.
 *
 * **A second reader on the console pump.** Every tile holds its own `SessionConsole`, and so does
 * the main window's output panel. That works because the driver's buffer keeps no cursor — a reader
 * says what it has seen and is told the rest (`SessionOutput`, SES1) — which is asserted in the
 * driver's own tests rather than assumed here.
 */
export function MonitorWindow({ notify }: { notify: Notify }) {
  const { t } = useTranslation();
  const sessions = useSessions(null, false);
  const quests = useQuests(null, true);
  const driver = useDriver();
  const openWindow = useOpenWindow();

  useErrorNotify(sessions.error, notify);

  const questFor = new Map((quests.data ?? []).map((quest): [string, Quest] => [quest.id, quest]));

  // What is still running, and nothing else: a monitor is for the present tense. A finished session
  // is read in the main window, where its diff and its record are.
  const live = (sessions.data ?? []).filter((session) => SESSION_ACTIVE.has(session.state));

  // What needs a person first — the rail's rule (components plan §3a), which matters more here
  // because this window is read from across a desk — then the longest-running.
  const shown = [...live].sort((a, b) => waitingFirst(a, b) || a.created.localeCompare(b.created));

  return (
    <div className="flex h-screen flex-col overflow-hidden">
      <header className="flex shrink-0 items-baseline gap-3 border-b border-line px-4 py-2">
        <h1 className="m-0 text-h3">{t('work.monitor.title')}</h1>
        <p className="m-0 text-small text-ink-faint">{t('work.monitor.subtitle')}</p>
        {/* The one piece of ambient truth this window cannot do without: with the driver stopped,
            "nothing is running" means something entirely different and the window would be lying
            by omission. */}
        {driver.isError && (
          <p className="m-0 ml-auto text-small text-st-declined">{t('work.monitor.driverStopped')}</p>
        )}
      </header>

      <div className="flex min-h-0 flex-1">
        {/* The rail (D55 §b: "the rail plus live streams"). It is the same organism the Work frame
            uses, which is the point of a secondary window being a route rather than an app: it
            groups by repository and says which are drivable or held, and none of that had to be
            written twice. Selecting scrolls to that session's stream — navigation within this
            window, which is not a move on a session and so does not break read-only. */}
        <aside className="flex w-56 shrink-0 flex-col border-r border-line max-lg:hidden">
          <header className="flex h-7 shrink-0 items-center border-b border-line px-3">
            <span className="text-meta uppercase tracking-[0.06em] text-ink-faint">
              {t('work.rail.label')}
            </span>
          </header>
          <div className="min-h-0 flex-1 overflow-y-auto">
            <SessionRail
              notify={notify}
              onSelect={(id) => document.getElementById(tileId(id))
                ?.scrollIntoView({ behavior: 'smooth', block: 'start' })}
            />
          </div>
        </aside>

        <div className="min-h-0 flex-1 overflow-y-auto p-3">
          {sessions.isPending
          ? <SkeletonRows rows={3} />
          : shown.length === 0
            ? (
              <EmptyState
                icon="inbox"
                headline={t('work.monitor.empty.headline')}
                body={t('work.monitor.empty.body')}
              />
            )
            : (
              // `auto-fit`, not `auto-fill`: empty tracks collapse, so one session fills the
              // window instead of sitting in a sixth of it on a wide screen, and six share it
              // evenly. Measured on the real window at 2560px, where `auto-fill` left a single
              // tile looking like a rendering failure.
              //
              // Rows are `minmax(15rem, 1fr)` for the same reason in the other direction: few
              // sessions fill the height they are given, many fall back to a readable floor and
              // the window scrolls.
              <div
                className={[
                  'grid min-h-full gap-3',
                  '[grid-template-columns:repeat(auto-fit,minmax(26rem,1fr))]',
                  '[grid-auto-rows:minmax(15rem,1fr)]',
                ].join(' ')}
              >
                {shown.map((session) => (
                  <Tile
                    key={session.id}
                    session={session}
                    quest={session.quest ? questFor.get(session.quest) : null}
                    onDetach={(id) => openWindow.mutate(sessionWindowName(id))}
                  />
                ))}
              </div>
            )}
        </div>
      </div>
    </div>
  );
}

/**
 * One tile, with its stream inside it.
 *
 * @remarks
 * Separated so the console's hook is mounted per session by the tree rather than by a loop in the
 * parent — a hook cannot be called inside `map`, and a component can.
 */
function Tile({ session, quest, onDetach }: {
  session: Session;
  quest?: Quest | null;
  onDetach: (id: string) => void;
}) {
  const { t } = useTranslation();
  // Somebody else's machine holds this one, so there is no stream to ask for and the tile says so.
  const here = sessionOrigin(session) === null;

  return (
    // The id is what the rail scrolls to — a session is one thing with one anchor in this window.
    <div id={tileId(session.id)} className="flex min-h-0 scroll-mt-3 flex-col">
      <StreamTile session={session} quest={quest} onDetach={here ? onDetach : undefined}>
        {here && <SessionConsole id={session.id} fill quiet={t('work.monitor.silent')} />}
      </StreamTile>
    </div>
  );
}
