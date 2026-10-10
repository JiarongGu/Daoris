import { useTranslation } from 'react-i18next';
import type { Session } from '../api';
import {
  retryNotice, stopNotice, useArchiveSessions, useDeleteSession, useOpenSessionFolder, useOpenWindow, useResolveSession,
  useRetryQuest, useStopSession,
} from '../shell';
import { list, sentence } from '../format';
import { failure, type Notify } from '../ui';
import { type ActFacts, folderOf, type SessionActId, workTargetOf } from './acts';
import { forgetDraft } from './drafts';
import type { Answered } from './InlineConfirm';
import type { Translate, WorkTarget } from './pausing';
import { sessionWindowName } from './window';
import { useWorkActs } from './workActs';

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
  /** Attend it, then open its pause's ask under its page header (*Pause quest…*, *Pause ask…*, PAUSE1e, D132 §2.6). */
  pause?: (id: string, target: WorkTarget) => void;
};

/** The acts the frame carries out: a door that has no frame to hand them (the monitor) offers none of them. */
const FRAME_ACTS = new Set<SessionActId>(['answer', 'stop', 'review', 'terminal', 'delete', 'pauseQuest', 'pauseAsk']);

/** Which of the frame's doors an act goes through. */
const DOOR_OF: Partial<Record<SessionActId, keyof SessionDoors>> = { pauseQuest: 'pause', pauseAsk: 'pause' };

/**
 * The names a delete's `stayed` carries (SESSDEL1; the driver's `SessionHomeFiles`), each said by the catalogue's words for
 * it. A name a later driver adds is said as it is named, rather than as a key the catalogue does not hold.
 */
const KEPT_NAMES = new Set([
  'conversation', 'transcript', 'files', 'harness', 'marker', 'mark', 'choice', 'spawn', 'help', 'held', 'landing', 'archived',
]);

const keptSaid = (t: Translate, name: string) =>
  (KEPT_NAMES.has(name) ? t(`work.delete.kept.${name}`) : t('work.delete.kept.other', { name }));

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
 * - **Pause quest…** and **Pause ask…** are the frame's (PAUSE1e, D132 §2.6), which asks once under the header where the
 *   pause ends work in flight; **Resume** goes to the work's one owner (`workActs.ts`), the press the pages make too, and
 *   never asks, since it starts nothing itself.
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
  const work = useWorkActs({ notify });

  /** Whether this door can carry an act out: the frame's acts only where the frame handed its door. */
  const can = (act: SessionActId) => !FRAME_ACTS.has(act) || Boolean(doors[DOOR_OF[act] ?? (act as keyof SessionDoors)]);

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
      case 'pauseQuest':
      case 'pauseAsk': {
        const target = workTargetOf(act, facts);
        if (target) doors.pause?.(session.id, target);
        return;
      }
      case 'resumeQuest':
      case 'resumeAsk': {
        const target = workTargetOf(act, facts);
        if (target) work.resume(target);
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

  // A refusal is said in the ask that was answered (UXFIX2), whole; with no ask to say it in, in a toast.
  const refusedIn = (answered?: Answered) => (answered
    ? (error: unknown) => answered.refused(sentence(error))
    : failure(notify));

  /** The stop's second press (§3.3), told back to its ask: `done` once the driver has answered it, `refused` with why not. */
  const stopNow = (session: Session, answered?: Answered) => {
    if (session.state === 'awaiting-person') {
      resolve.mutate({ id: session.id, state: 'stopped' }, {
        onSuccess: () => {
          notify(t('work.awaiting.resolved', { id: session.id, state: t('sessionState.stopped') }));
          answered?.done();
        },
        onError: refusedIn(answered),
      });
      return;
    }
    stop.mutate(session.id, {
      onSuccess: (answer) => {
        notify(t(stopNotice(answer), { id: session.id }));
        answered?.done();
      },
      onError: refusedIn(answered),
    });
  };

  /**
   * *Delete…*'s second press (SESSUX1f, D126 §5.4), told back to its ask: `done` once the host deleted it, when the record
   * and what this machine kept of it go, and so does the draft this viewer kept for it (§5.1). A refusal is said in the
   * catalogue's words inside the ask (UXFIX2), and the ask stays, since nothing went.
   *
   * What the disk would not let go of (`stayed`, SESSDEL1) is said by name, in the error's tone, since not all of it went
   * (SESSDEL1b); the ask still closes, since the record did.
   */
  const deleteNow = (session: Session, answered?: Answered) => remove.mutate(session.id, {
    onSuccess: (answer) => {
      forgetDraft(session.id);
      const stayed = answer?.stayed ?? [];
      if (stayed.length === 0) notify(t('work.delete.done', { id: session.id }));
      else notify(t('work.delete.stayed', { id: session.id, what: list(stayed.map((name) => keptSaid(t, name))) }), 'error');
      answered?.done();
    },
    onError: refusedIn(answered),
  });

  /**
   * *Archive what ended…*'s second press (§5.3): what its first listed, each judged again by the host as it goes. What
   * changed since the list is kept and counted, never refused as a whole.
   */
  const archiveListed = (ids: readonly string[], answered?: Answered) => archive.mutate({ ids, archived: true }, {
    onSuccess: (answer) => {
      const kept = answer?.kept?.length ?? 0;
      notify(kept > 0
        ? t('work.archive.endedKept', { archived: ids.length - kept, count: ids.length })
        : t('work.archive.ended', { count: ids.length }));
      answered?.done();
    },
    onError: refusedIn(answered),
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
    /** The work's one owner, whose pause the frame's ask presses (PAUSE1e). */
    work,
    /** An act on its way that the header's buttons wait for. */
    busy: retry.isPending || archive.isPending || folder.isPending || work.busy,
    /** An archive on its way: *Archive what ended…*'s presses wait for it. */
    archiving: archive.isPending,
  };
}
