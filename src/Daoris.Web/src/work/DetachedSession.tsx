import { useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { useQuests, useSessions } from '../queries';
import { EmptyState, type Notify, SESSION_ACTIVE, SkeletonRows, useErrorNotify } from '../ui';
import { SessionConsole } from '../SessionConsole';
import { AttendedSession } from './AttendedSession';
import { sessionOrigin } from './identity';
import { SessionConversation } from './SessionConversation';

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
 */
export function DetachedSession({ id, notify }: { id: string; notify: Notify }) {
  const { t } = useTranslation();
  const sessions = useSessions(null, true);
  const quests = useQuests(null, true);
  const scroller = useRef<HTMLDivElement>(null);

  useErrorNotify(sessions.error, notify);

  const session = (sessions.data ?? []).find((row) => row.id === id) ?? null;
  const quest = session?.quest
    ? (quests.data ?? []).find((row) => row.id === session.quest) ?? null
    : null;

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

  // The console is this machine's to give only for a session this machine ran (D47 §4).
  const here = sessionOrigin(session) === null;

  return (
    <div className="flex h-screen flex-col overflow-hidden">
      {/* The record and the conversation scroll together, as in the main window (D76); the
          conversation follows its tail there too. */}
      <div ref={scroller} className="flex min-h-0 flex-1 flex-col overflow-y-auto px-4 py-3">
        {/* No dock in this window, so the timeline stays in the column at every width (REV3). */}
        <AttendedSession session={session} quest={quest} timeline="always" />
        {here && (
          <div className="mt-4 min-w-0">
            <SessionConversation session={session.id} adapter={session.adapter} chat={session.kind === 'chat'} tree={session.tree} live={SESSION_ACTIVE.has(session.state)} scroller={scroller} />
          </div>
        )}
      </div>

      {here && (
        <div className="flex h-56 shrink-0 flex-col border-t border-line px-4 py-2">
          {/* The raw view, beside the conversation it is the text of. A sentence rather than an
              empty well, for the reason the monitor's tiles have one: a bordered empty box in a
              read-only window reads as a field to type in. */}
          <SessionConsole id={session.id} fill quiet={t('work.panel.silent')} />
        </div>
      )}

      {!here && (
        <p className="border-t border-line px-4 py-3 text-small text-ink-faint">
          {t('work.monitor.elsewhere', { origin: sessionOrigin(session) })}
        </p>
      )}
    </div>
  );
}
