import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Session } from './api';
import { SessionConsole } from './SessionConsole';
import { sessionTool } from './format';
import { useEndChat, useSendMessage, useStopSession } from './shell';
import { Button, Drawer, type Notify, Pill } from './ui';

/**
 * A conversation with an agent in one repository (D49 §3).
 *
 * @remarks
 * **The harness is the chat.** Daoris pipes the person's lines in and streams the session's out;
 * which model answers is that repository's own harness configuration, and no model is named anywhere
 * here (`model-decoupling`). A Daoris-owned chat loop was rejected in the design: it would duplicate
 * what every harness already is, and produce sessions with no doctrine path into them.
 *
 * **It is a session, not a second kind of thing** — the same record, the same observed lifecycle, and
 * the same one-session-per-repository lock, because two agents in one working tree corrupt it
 * regardless of who is typing.
 *
 * **Two ways to end it, and they mean different things.** Finishing closes the input so the harness
 * winds up on its own; stopping cuts it off, and the record says the person did.
 */
export function ChatDrawer({ session, onClose, notify }: {
  session: Session;
  onClose: () => void;
  notify: Notify;
}) {
  const { t } = useTranslation();
  const [draft, setDraft] = useState('');
  const send = useSendMessage();
  const end = useEndChat();
  const stop = useStopSession();

  const live = session.state === 'starting' || session.state === 'working'
    || session.state === 'queued' || session.state === 'awaiting-person';

  const say = () => {
    const text = draft.trim();
    if (!text) return;
    send.mutate({ id: session.id, text }, {
      onSuccess: (result) => {
        // False is an answer: the session ended while they were typing. Said plainly, because the
        // alternative is a person retyping into a conversation that is over.
        if (!result.sent) notify(t('chat.notListening'), 'error');
        else setDraft('');
      },
      onError: (error: unknown) => notify((error as Error).message, 'error'),
    });
  };

  return (
    <Drawer
      title={t('chat.title', { repository: session.repository })}
      onClose={onClose}
      meta={
        <span className="flex flex-wrap items-center gap-2">
          <Pill tone={live ? 'taken' : 'neutral'}>{t(`sessionState.${session.state}`)}</Pill>
          <span className="font-mono text-[0.72rem] text-ink-faint">
            {session.id} · {sessionTool(session)}
          </span>
        </span>
      }
      footer={
        <div className="flex flex-wrap items-center gap-2">
          {live && (
            <>
              <Button
                onClick={() => end.mutate(session.id, {
                  onSuccess: () => notify(t('chat.ending')),
                  onError: (error: unknown) => notify((error as Error).message, 'error'),
                })}
              >
                {t('chat.finish')}
              </Button>
              <Button
                variant="danger"
                onClick={() => stop.mutate(session.id, {
                  onSuccess: () => notify(t('quests.session.stopped', { id: session.id })),
                  onError: (error: unknown) => notify((error as Error).message, 'error'),
                })}
              >
                {t('chat.stop')}
              </Button>
            </>
          )}
          <Button variant="ghost" className="ml-auto" onClick={onClose}>{t('common.close')}</Button>
        </div>
      }
    >
      <p className="text-[0.85rem] text-ink-soft">{t('chat.hint')}</p>

      <SessionConsole id={session.id} tall />

      {live && (
        <form
          className="mt-3 flex items-end gap-2"
          onSubmit={(event) => { event.preventDefault(); say(); }}
        >
          <label className="grid flex-1 gap-1 text-[0.78rem] text-ink-faint">
            {t('chat.say')}
            <textarea
              value={draft}
              onChange={(event) => setDraft(event.target.value)}
              // Enter sends, because this is a conversation; a newline needs the modifier, because a
              // paste of several lines is one message and should stay one.
              onKeyDown={(event) => {
                if (event.key === 'Enter' && !event.shiftKey) { event.preventDefault(); say(); }
              }}
              placeholder={t('chat.placeholder')}
              className="min-h-16 resize-y rounded-control border border-line bg-raised px-2.5 py-1.5 text-[0.9rem] text-ink"
            />
          </label>
          <Button variant="primary" disabled={!draft.trim() || send.isPending} onClick={say}>
            {t('chat.send')}
          </Button>
        </form>
      )}

      {!live && <p className="mt-3 text-[0.85rem] text-ink-soft">{t('chat.over')}</p>}
    </Drawer>
  );
}
