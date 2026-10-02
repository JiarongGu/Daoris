import { useTranslation } from 'react-i18next';
import type { Session } from '../api';
import {
  retryNotice, stopNotice, useArchiveSessions, useDeleteSession, useOpenSessionFolder, useOpenWindow, useResolveSession,
  useRetryQuest, useStopSession,
} from '../shell';
import { failure, type Notify } from '../ui';
import { type ActFacts, folderOf, type SessionActId } from './acts';
import { forgetDraft } from './drafts';
import { sessionWindowName } from './window';

/**
 * What only the frame can do with a session, because the frame holds the attended session and its regions (D118 §5):
 * each attends the session first, as a press on its row would, then does its part there.
 */
export type SessionDoors = {
  /** Attend it, then open the box at its foot with the focus (*Answer…*, D126 §3.1). */
  answer?: (id: string) => void;
  /** Attend it, then open its stop's ask under its page header (*Stop…*, §3.3). */
  stop?: (id: string) => void;
  /** Attend it, with the side bar open on its review (*Review*). */
  review?: (id: string) => void;
  /** Open the panel's terminal in this folder, and show the panel (*Open a terminal here*, §3.5). */
  terminal?: (folder: string) => void;
  /** Attend it, then open its delete's ask under its page header (*Delete…*, SESSUX1f, §5.4). */
  delete?: (id: string) => void;
};

/** The acts the frame carries out: a door that has no frame to hand them (the monitor) offers none of them. */
const FRAME_ACTS = new Set<SessionActId>(['answer', 'stop', 'review', 'terminal', 'delete']);

/**
 * **The one owner of each act on a session** (SESSUX1d, D126 §3.1): the row's ⋯ and the page header each call this,
 * so an act has one implementation whichever door pressed it. D56's one owner holds: the module is the owner, and each
 * door calls it.
 *
 * @remarks
 * **An organism**: it holds the bridge's hooks, so the row, the list and the header hold none. Which acts a session is
 * offered is `acts.ts`'s rule; this carries out the one pressed, and says how it went in the catalogue's words.
 *
 * - **Try again** sends the session's quest to `RETRY_QUEST`, which marks a parked quest or releases a held one as the
 *   driver's verdict says (SESSUX1b), and says which: the sentence the quest's page says for the same press.
 * - **Open folder** names the session alone (`SESSION_OPEN_FOLDER`): the module names the folder from its record.
 * - **Archive** and **Unarchive** of one session (SESSUX1e, moved here from the rail), and *Archive what ended…*'s second
 *   press, which archives exactly what its first listed.
 * - **The stop's second press** (§3.3): a session waiting on you through `RESOLVE_SESSION`'s `stopped`, as its card's stop
 *   was, since that route lets a parked process go before the record ends; every other through `STOP_SESSION`, whose
 *   three answers are said as `stopNotice` says them.
 * - **The delete's second press** (SESSUX1f, §5.4) through `SESSION_DELETE`; *Delete…* itself is the frame's, which asks
 *   once under the header.
 */
export function useSessionActs({ notify, doors = {} }: { notify: Notify; doors?: SessionDoors }) {
  const { t } = useTranslation();
  const archive = useArchiveSessions();
  const retry = useRetryQuest();
  const folder = useOpenSessionFolder();
  const openWindow = useOpenWindow();
  const stop = useStopSession();
  const resolve = useResolveSession();
  const remove = useDeleteSession();

  /** Whether this door can carry an act out: the frame's acts only where the frame handed its door. */
  const can = (act: SessionActId) => !FRAME_ACTS.has(act) || Boolean(doors[act as keyof SessionDoors]);

  // Asked of one session, a refusal is the host's answer, said in the catalogue's words with the group that kept it; one
  // that was not archived is information (D48 §6).
  const archiveOne = (id: string, mark: boolean) => archive.mutate({ ids: [id], archived: mark }, {
    onSuccess: (answer) => notify(t(mark ? 'work.archive.done'
      : answer?.notArchived?.includes(id) ? 'work.archive.wasNot' : 'work.archive.back')),
    onError: failure(notify),
  });

  const run = (act: SessionActId, facts: ActFacts) => {
    const { session } = facts;
    switch (act) {
      case 'answer':
      case 'stop':
      case 'review':
      case 'delete':
        doors[act]?.(session.id);
        return;
      case 'terminal': {
        const at = folderOf(facts);
        if (at) doors.terminal?.(at);
        return;
      }
      case 'retry': {
        const quest = session.quest;
        if (!quest) return;
        retry.mutate({ quest }, {
          onSuccess: (state) => notify(t(...retryNotice(quest, state))),
          onError: failure(notify),
        });
        return;
      }
      case 'openFolder':
        folder.mutate(session.id, { onError: failure(notify) });
        return;
      case 'detach':
        openWindow.mutate(sessionWindowName(session.id));
        return;
      case 'archive':
      case 'unarchive':
        archiveOne(session.id, act === 'archive');
        return;
      case 'copy':
        void navigator.clipboard?.writeText(session.id).then(() => notify(t('work.rail.menu.copied', { id: session.id })), () => {});
        return;
    }
  };

  /** The stop's second press (§3.3), and `done` once the driver has answered it. */
  const stopNow = (session: Session, done?: () => void) => {
    if (session.state === 'awaiting-person') {
      resolve.mutate({ id: session.id, state: 'stopped' }, {
        onSuccess: () => {
          notify(t('work.awaiting.resolved', { id: session.id, state: t('sessionState.stopped') }));
          done?.();
        },
        onError: failure(notify),
      });
      return;
    }
    stop.mutate(session.id, {
      onSuccess: (answer) => {
        notify(t(stopNotice(answer), { id: session.id }));
        done?.();
      },
      onError: failure(notify),
    });
  };

  /**
   * *Delete…*'s second press (SESSUX1f, D126 §5.4), and `done` once the host deleted it: the record and what this machine
   * kept of it go, and so does the draft this viewer kept for it (§5.1). A refusal is said in the catalogue's words, and
   * the ask stays, since nothing went.
   */
  const deleteNow = (session: Session, done?: () => void) => remove.mutate(session.id, {
    onSuccess: () => {
      forgetDraft(session.id);
      notify(t('work.delete.done', { id: session.id }));
      done?.();
    },
    onError: failure(notify),
  });

  /**
   * *Archive what ended…*'s second press (§5.3): what its first listed, each judged again by the host as it goes. What
   * changed since the list is kept and counted, never refused as a whole.
   */
  const archiveListed = (ids: readonly string[], done?: () => void) => archive.mutate({ ids, archived: true }, {
    onSuccess: (answer) => {
      const kept = answer?.kept?.length ?? 0;
      notify(kept > 0
        ? t('work.archive.endedKept', { archived: ids.length - kept, count: ids.length })
        : t('work.archive.ended', { count: ids.length }));
      done?.();
    },
    onError: failure(notify),
  });

  return {
    can,
    run,
    stopNow,
    deleteNow,
    archiveListed,
    /** A stop on its way. */
    stopping: stop.isPending || resolve.isPending,
    /** A delete on its way: its ask's presses wait for it. */
    deleting: remove.isPending,
    /** An act on its way that the header's buttons wait for. */
    busy: retry.isPending || archive.isPending || folder.isPending,
    /** An archive on its way: *Archive what ended…*'s presses wait for it. */
    archiving: archive.isPending,
  };
}
