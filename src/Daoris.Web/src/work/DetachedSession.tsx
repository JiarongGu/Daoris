import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useQuests, useSessions } from '../queries';
import { useChatTurns, useSessionOpenings, useSessionStreams } from '../shell';
import { frameShortcut } from '../shortcuts';
import { EmptyState, type Notify, SESSION_ACTIVE, SkeletonRows, useErrorNotify } from '../ui';
import { SessionConsole } from '../SessionConsole';
import { AttendedSession } from './AttendedSession';
import { useDetachedPanel } from './closings';
import { OutputPanel, PANEL_MIN } from './frame';
import { sessionOrigin } from './identity';
import { SessionConversation } from './SessionConversation';
import { panelTabs } from './streams';

/**
 * One session in a window of its own (D55 §b, SURF8).
 *
 * @remarks
 * **The same components as the attended column**, because it is the same thing: a record, what was
 * observed about it, and the stream. A second screen holding one conversation while the main window
 * is in Manage is what this is for.
 *
 * **Read-only, like the monitor** — `onResolve` is not passed, and `SessionHead` already has that
 * state and its reason: nothing here can act, so the analysis is shown and the moves are not. Half a
 * control is worse than none. The moves stay with the main window, which is D56's one-owner rule
 * holding across windows rather than only within one.
 *
 * **A record it cannot find is a state, not a failure.** This window outlives the ledger's memory of
 * a session, and a workspace scope can change under it (WSP5) — so it says so plainly instead of
 * rendering an empty frame.
 *
 * **Its console carries the session's streams** (CONSOLE3b), a tab each as in the main window, so a
 * subagent or a dev server is not out of sight here. With no stop: nothing in this window acts.
 *
 * **Its console is the main window's output panel** (D118 §4, FRAME1h): grown for a long console, hidden
 * for a long conversation, on its handle, its *Hide*, and Ctrl+J, the key of the one region this window
 * has. What a person makes of it is kept for every detached window, apart from the main window's panel
 * (`useDetachedPanel`). It offers no views to move: there is no side bar here to move one to.
 *
 * **The same head as the other windows'**: a chat named by what was asked of it, and idle between
 * turns, as the driver says. Without them this window headed one session `conversation · working`
 * beside a main window heading it `start the dev server · idle`.
 */
export function DetachedSession({ id, notify }: { id: string; notify: Notify }) {
  const { t } = useTranslation();
  const sessions = useSessions(null, true);
  const quests = useQuests(null, true);
  const scroller = useRef<HTMLDivElement>(null);
  // What the session runs beside itself (CONSOLE3b), and the one picked; one it no longer lists falls
  // back to the session's own console, as the main window's panel does.
  const streams = useSessionStreams(id);
  const [picked, setPicked] = useState<string | null>(null);
  const shown = picked && streams.some((row) => row.key === picked) ? picked : id;
  const tabs = panelTabs(id, streams);
  const panel = useDetachedPanel();

  useErrorNotify(sessions.error, notify);

  const session = (sessions.data ?? []).find((row) => row.id === id) ?? null;
  const openings = useSessionOpenings(session ? [session] : undefined);
  const turns = useChatTurns(session?.kind === 'chat' ? [session.id] : []);
  const quest = session?.quest
    ? (quests.data ?? []).find((row) => row.id === session.quest) ?? null
    : null;
  // The console is this machine's to give only for a session this machine ran (D47 §4).
  const here = session ? sessionOrigin(session) === null : false;

  // Ctrl+J, wherever focus is, as in the main window: the panel is the one region this window has, so
  // Ctrl+B and the side bar's key are left alone. The latest panel, for a listener added once.
  const latest = useRef({ here, panel });
  latest.current = { here, panel };
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      const now = latest.current;
      if (frameShortcut(event) !== 'view.panel' || !now.here) return;
      event.preventDefault();
      now.panel.setClosed(!now.panel.closed);
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  if (sessions.isPending) {
    return <div className="p-4"><SkeletonRows rows={4} /></div>;
  }

  if (!session) {
    return (
      <div className="p-4">
        <EmptyState
          icon="inbox"
          headline={t('work.detached.gone.headline')}
          body={t('work.detached.gone.body')}
        />
      </div>
    );
  }

  return (
    <div className="flex h-screen flex-col overflow-hidden">
      {/* The record and the conversation scroll together, as in the main window (D76); the
          conversation follows its tail there too. */}
      <div ref={scroller} className="flex min-h-0 flex-1 flex-col overflow-y-auto px-4 py-3">
        {/* No dock in this window, so the timeline stays in the column at every width (REV3). */}
        <AttendedSession
          session={session}
          quest={quest}
          opening={openings[session.id]}
          taking={turns[session.id]?.taking}
          lastTurn={turns[session.id]?.lastTurn}
          timeline="always"
        />
        {here && (
          <div className="mt-4 min-w-0">
            <SessionConversation session={session.id} adapter={session.adapter} chat={session.kind === 'chat'} tree={session.tree} live={SESSION_ACTIVE.has(session.state)} scroller={scroller} />
          </div>
        )}
      </div>

      {here && (
        <OutputPanel
          // The raw view, beside the conversation it is the text of. A sentence rather than an empty
          // well, for the reason the monitor's tiles have one: a bordered empty box in a read-only window
          // reads as a field to type in.
          console={<SessionConsole id={shown} fill quiet={t(shown === id ? 'work.panel.silent' : 'work.panel.streamSilent')} />}
          height={Math.max(PANEL_MIN, panel.height)}
          collapsed={panel.closed}
          onResize={panel.setHeight}
          onToggle={() => panel.setClosed(!panel.closed)}
          tabs={tabs}
          selected={shown}
          onSelect={(key) => {
            setPicked(key === id ? null : key);
            // Picking what to read is asking to read it: a hidden console opens, as the main window's does.
            if (panel.closed) panel.setClosed(false);
          }}
        />
      )}

      {!here && (
        <p className="border-t border-line px-4 py-3 text-small text-ink-faint">
          {t('work.monitor.elsewhere', { origin: sessionOrigin(session) })}
        </p>
      )}
    </div>
  );
}
