import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Quest, Session } from '../api';
import { useQuests, useSessions } from '../queries';
import { useChatTurns, useDriver, useHarnesses, useOpenWindow, useSessionOpenings } from '../shell';
import { frameShortcut } from '../shortcuts';
import { EmptyState, type Notify, SESSION_ACTIVE, SkeletonRows, useErrorNotify } from '../ui';
import { SessionConsole } from '../SessionConsole';
import { type ListMode, listToggled } from './layout';
import { useListPanes } from './listPanes';
import { SessionConversation } from './SessionConversation';
import { SessionRail } from './SessionRail';
import { StreamTile } from './StreamTile';
import { type ListSpec, ViewFrame } from './ViewFrame';
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
 * **A second reader on the console pump.** A tile on a text door holds its own `SessionConsole`,
 * and so does the main window's output panel. That works because the driver's buffer keeps no
 * cursor — a reader says what it has seen and is told the rest (`SessionOutput`, SES1) — which is
 * asserted in the driver's own tests rather than assumed here. A tile on a door that keeps a
 * conversation shows the conversation instead (UX5 U2), read from the same record the main window
 * reads.
 *
 * **Its rail is a list pane** (D118 §4, FRAME1h), as every view's list is: resized by its edge, closed
 * to its strip, and a strip by itself where the tiles would fall below their floor. It is never hidden,
 * as it was below 1024 px with no way back (audit MO2): the strip keeps each running session's mark, and
 * its open lays the rail over the tiles. What it remembers is the monitor's own, apart from the main
 * window's rail, and Ctrl+B, the key of the one region this window has, toggles it.
 */
export function MonitorWindow({ notify }: { notify: Notify }) {
  const { t } = useTranslation();
  const sessions = useSessions(null, false);
  const quests = useQuests(null, true);
  const driver = useDriver();
  const openWindow = useOpenWindow();
  // A conversation's name (RAIL1), as the main window's rail reads it.
  const openings = useSessionOpenings(sessions.data);

  // The rail's closing and width (`daoris.list.monitor.*`); laid over the tiles is never kept (D118 §3f).
  const lists = useListPanes();
  const [over, setOver] = useState(false);
  const [listMode, setListMode] = useState<ListMode | null>(null);

  // Ctrl+B wherever focus is, as in the main window, by what the room made of the rail (D118 §3a): open,
  // it closes; laid over, it goes; a strip, it opens. The latest, for a listener added once.
  const latest = useRef({ lists, listMode });
  latest.current = { lists, listMode };
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      const { lists: held, listMode: mode } = latest.current;
      if (frameShortcut(event) !== 'view.list' || !mode) return;
      event.preventDefault();
      const next = listToggled({ mode });
      if (next.closed !== undefined) held.setClosed('monitor', next.closed);
      setOver(next.over);
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  useErrorNotify(sessions.error, notify);

  const questFor = new Map((quests.data ?? []).map((quest): [string, Quest] => [quest.id, quest]));

  // What is still running, and nothing else: a monitor is for the present tense. A finished session
  // is read in the main window, where its diff and its record are.
  const live = (sessions.data ?? []).filter((session) => SESSION_ACTIVE.has(session.state));
  // Whether each live conversation has a turn in flight, as the driver says: between turns a chat
  // reads idle, on its tile and in the rail, as in the main window (UX5 U17).
  const chatTurns = useChatTurns(live.filter((session) => session.kind === 'chat').map((session) => session.id));
  const taking = Object.fromEntries(Object.entries(chatTurns).map(([id, held]) => [id, held.taking]));
  // …and when each one's last turn ended here, for the rail's *moved* (RAIL2).
  const lastTurns = Object.fromEntries(Object.entries(chatTurns).flatMap(([id, held]) => (held.lastTurn ? [[id, held.lastTurn]] : [])));

  // What needs a person first — the rail's rule (components plan §3a), which matters more here
  // because this window is read from across a desk — then the longest-running.
  const shown = [...live].sort((a, b) => waitingFirst(a, b) || a.created.localeCompare(b.created));

  // A press on a session scrolls to its tile: navigation within this window, which is not a move on a
  // session and so does not break read-only. A rail laid over the tiles closes on it, as on any choice.
  const toTile = (id: string) => {
    setOver(false);
    document.getElementById(tileId(id))?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };

  // The rail (D55 §b: "the rail plus live streams"). It is the same organism the Work frame uses, which is
  // the point of a secondary window being a route rather than an app: it groups by repository and says
  // which are drivable or held, and none of that had to be written twice. Its name and its doors are the
  // session list's, since that is what it is.
  const rail: ListSpec = {
    view: 'monitor',
    name: t('work.rail.label'),
    labels: { open: t('work.rail.open'), close: t('work.rail.close'), resize: t('work.rail.resize') },
    loading: sessions.isPending,
    // Each running session's initial and mark, in the open rail's order, as Sessions' strip.
    strip: <SessionRail notify={notify} taking={taking} live compact onSelect={toTile} />,
    body: <SessionRail notify={notify} taking={taking} lastTurns={lastTurns} live onSelect={toTile} />,
  };

  return (
    <div className="flex h-screen flex-col overflow-hidden">
      <header className="flex shrink-0 items-baseline gap-3 border-b border-line px-4 py-2">
        {/* The one heading step a view has (audit MO11): its class named a step no token declares, so the
            title was drawn at the body's size. */}
        <h1 className="m-0 text-view font-[650] tracking-[-0.01em]">{t('work.monitor.title')}</h1>
        <p className="m-0 text-small text-ink-faint">{t('work.monitor.subtitle')}</p>
        {/* The one piece of ambient truth this window cannot do without: with the driver stopped,
            "nothing is running" means something entirely different and the window would be lying
            by omission. */}
        {driver.isError && (
          <p className="m-0 ml-auto text-small text-ink-danger">{t('work.monitor.driverStopped')}</p>
        )}
      </header>

      {/* No side bar beside the tiles (D55 §b), so the rail gives way only for the tiles' own floor. */}
      <ViewFrame
        layout={{
          list: rail,
          main: (
            <main data-region="main" className="min-h-0 flex-1 overflow-y-auto p-3">
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
                  // tile looking like a rendering failure. A tile is never wider than the tiles' own
                  // width (FRAME1h): beside the rail at its floor, a 26rem tile overflowed it.
                  //
                  // Rows are `minmax(15rem, 1fr)` for the same reason in the other direction: few
                  // sessions fill the height they are given, many fall back to a readable floor and
                  // the window scrolls.
                  <div
                    className={[
                      'grid min-h-full gap-3',
                      '[grid-template-columns:repeat(auto-fit,minmax(min(26rem,100%),1fr))]',
                      '[grid-auto-rows:minmax(15rem,1fr)]',
                    ].join(' ')}
                  >
                    {shown.map((session) => (
                      <Tile
                        key={session.id}
                        session={session}
                        quest={session.quest ? questFor.get(session.quest) : null}
                        opening={openings[session.id]}
                        taking={taking[session.id]}
                        onDetach={(id) => openWindow.mutate(sessionWindowName(id))}
                      />
                    ))}
                  </div>
                )}
            </main>
          ),
        }}
        lists={lists}
        over={over}
        onOver={setOver}
        onListMode={setListMode}
      />
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
function Tile({ session, quest, opening, taking, onDetach }: {
  session: Session;
  quest?: Quest | null;
  opening?: string | null;
  taking?: boolean;
  onDetach: (id: string) => void;
}) {
  const { t } = useTranslation();
  // Somebody else's machine holds this one, so there is no stream to ask for and the tile says so.
  const here = sessionOrigin(session) === null;
  // 🔴 The conversation where the door keeps one (UX5 U2, D76 §3): the console is its raw view, and
  // a tile of console alone said "Nothing said yet" over a kept conversation. A text door, or a
  // roster that has not answered, keeps the console, which claims least.
  const harnesses = useHarnesses();
  const structured = harnesses.data?.harnesses?.find((row) => row.harness === session.adapter)?.structured === true;
  const body = useRef<HTMLDivElement>(null);

  return (
    // The id is what the rail scrolls to — a session is one thing with one anchor in this window.
    <div id={tileId(session.id)} className="flex min-h-0 scroll-mt-3 flex-col">
      <StreamTile session={session} quest={quest} opening={opening} taking={taking} onDetach={here ? onDetach : undefined}>
        {here && (structured
          ? (
            <div ref={body} className="min-h-0 flex-1 overflow-y-auto">
              <SessionConversation
                session={session.id}
                adapter={session.adapter}
                chat={session.kind === 'chat'}
                tree={session.tree}
                live
                scroller={body}
              />
            </div>
          )
          : <SessionConsole id={session.id} fill quiet={t('work.monitor.silent')} />)}
      </StreamTile>
    </div>
  );
}
