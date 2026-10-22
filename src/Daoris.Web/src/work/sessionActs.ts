import { useTranslation } from 'react-i18next';
import type { Session } from '../data/api';
import {
  retryNotice, stopNotice, useApproveHelper, useArchiveSessions, useAskHelper, useAskPartner, useCancelTurn, useDeclineHelper, useDeleteSession,
  useHandOver, useOpenSessionFolder, useOpenWindow, useResolveSession, useRetryQuest, useStopSession,
} from '../bridge/shell';
import { list, sentence } from '../locales/format';
import { failure, type Notify, SESSION_ACTIVE } from '../design/ui';
import { handOverSaid } from './say';
import { type ActFacts, folderOf, type SessionActId, workTargetOf } from './acts';
import { forgetDraft } from './conversation/drafts';
import { partIds, type SessionEntry } from './entries';
import type { Answered } from './InlineConfirm';
import type { Translate, WorkTarget } from './pausing/pausing';
import { sessionWindowName } from './window';
import { useWorkActs } from './pausing/workActs';

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
  /** Attend it, then open its hand-over's ask under its page header (*Go on with another agent…*, MIXAGENT1b-i, D163). */
  handOver?: (id: string) => void;
  /** Attend the conversation a hand-over opened (not an act: the ask's answer, MIXAGENT1b-i). */
  attend?: (id: string) => void;
  /** Attend it, then open its helper's ask under its page header (*Ask another agent…*, MIXAGENT2b, D163). */
  askHelper?: (id: string) => void;
};

/** What a hand-over's ask tells back: how it ended, and that it was kept for the conversation's turn to end (MIXAGENT1b-i). */
export type HandOverTold = Answered & { kept: (sentence: string) => void };

/** The acts the frame carries out: a door that has no frame to hand them (the monitor) offers none of them. */
const FRAME_ACTS = new Set<SessionActId>([
  'answer', 'stop', 'handOver', 'askHelper', 'review', 'terminal', 'delete', 'pauseQuest', 'pauseAsk',
]);

/**
 * The codes a helper's ask and the person's yes or no to one answer by (MIXAGENT2b, HELPERNO1; the bridge's `HelperAskWhy`), each
 * said in the catalogue's words. A code a later driver adds is said in the driver's own sentence.
 */
const HELPER_REFUSALS = new Set([
  'blank', 'too-long', 'agent', 'not-held', 'helper', 'reviewer', 'intake', 'gone', 'person', 'waits', 'past', 'approved', 'unread',
]);

/** The codes a piece's ask answers by (COAGENT1b; the bridge's `PartnerAskWhy`), each said in the catalogue's words. */
const PIECE_REFUSALS = new Set(['blank', 'too-long', 'agent', 'path', 'not-held', 'unfit', 'no-commit']);

/** A piece's refusal in the catalogue's words, by its code; one a later driver adds is said in the driver's own sentence. */
export const pieceSaid = (t: Translate, why: string | null, message: string) =>
  (why && PIECE_REFUSALS.has(why) ? t(`work.pieces.refused.${why}`) : message || why || '');

/** A helper's refusal in the catalogue's words, by its code: *What needs you*'s yes and no say them too (MIXAGENT2c). */
export const helperSaid = (t: Translate, why: string | null, message: string) =>
  (why && HELPER_REFUSALS.has(why) ? t(`work.helper.refused.${why}`) : message || why || '');

/** Which of the frame's doors an act goes through. */
const DOOR_OF: Partial<Record<SessionActId, keyof SessionDoors>> = { pauseQuest: 'pause', pauseAsk: 'pause' };

/**
 * The names a delete's `stayed` carries (SESSDEL1; the driver's `SessionHomeFiles`), each said by the catalogue's words for
 * it. A name a later driver adds is said as it is named, rather than as a key the catalogue does not hold.
 */
const KEPT_NAMES = new Set([
  'conversation', 'transcript', 'files', 'harness', 'marker', 'mark', 'choice', 'hand-over', 'spawn', 'help', 'held', 'landing', 'archived',
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
 * - **The hand-over's second press** (MIXAGENT1b-i, D163) through `SESSION_HAND_OVER` with the reason `press`, then, for a
 *   conversation that works, `CANCEL_TURN`, so the turn's end takes the kept ask; *Go on with another agent…* itself is the
 *   frame's, which asks once under the header.
 * - **The delete's second press** (SESSUX1f, §5.4) through `SESSION_DELETE`; *Delete…* itself is the frame's, which asks
 *   once under the header.
 * - **A helper's ask's second press** (MIXAGENT2b, D163) through `SESSION_HELPER_ASK`, and the person's yes to an agent's ask
 *   through `SESSION_HELPER_APPROVE` and their no through `SESSION_HELPER_DECLINE` (HELPERNO1); *Ask another agent…* itself is
 *   the frame's, which asks once under the header.
 * - **Pause quest…** and **Pause ask…** are the frame's (PAUSE1e, D132 §2.6), which asks once under the header where the
 *   pause ends work in flight; **Resume** goes to the work's one owner (`workActs.ts`), the press the pages make too, and
 *   never asks, since it starts nothing itself.
 */
export function useSessionActs({ notify, doors = {}, entries }: {
  notify: Notify;
  doors?: SessionDoors;
  /** The reader's entries (SESSONE1c): an archive reaches every part of the entry it is pressed for. Absent, a record is its own. */
  entries?: readonly SessionEntry[];
}) {
  const { t } = useTranslation();
  const archive = useArchiveSessions();
  const retry = useRetryQuest();
  const folder = useOpenSessionFolder();
  const openWindow = useOpenWindow();
  const stop = useStopSession();
  const resolve = useResolveSession();
  const remove = useDeleteSession();
  const handOver = useHandOver();
  const askHelper = useAskHelper();
  const askPartner = useAskPartner();
  const approveHelper = useApproveHelper();
  const declineHelper = useDeclineHelper();
  const cancelTurn = useCancelTurn();
  const work = useWorkActs({ notify });

  /** Whether this door can carry an act out: the frame's acts only where the frame handed its door. */
  const can = (act: SessionActId) => !FRAME_ACTS.has(act) || Boolean(doors[DOOR_OF[act] ?? (act as keyof SessionDoors)]);

  // Asked of one session, a refusal is the host's answer, said in the catalogue's words with the group that kept it; one
  // that was not archived is information (D48 §6).
  // An archive is of the entry (D167 point 5, D126 §5.2): every part's id goes, so no part is left as a row of its own.
  const archiveOne = (id: string, mark: boolean) => {
    const ids = partIds(entries, id);
    archive.mutate({ ids, archived: mark }, {
      onSuccess: (answer) => {
        const kept = answer?.kept?.length ?? 0;
        notify(t(mark
          ? kept > 0 ? 'work.archive.endedKept' : 'work.archive.done'
          : ids.every((part) => answer?.notArchived?.includes(part)) ? 'work.archive.wasNot' : 'work.archive.back',
        { archived: ids.length - kept, count: ids.length }));
      },
      onError: failure(notify),
    });
  };

  const run = (act: SessionActId, facts: ActFacts) => {
    const { session } = facts;
    switch (act) {
      case 'answer':
      case 'stop':
      case 'handOver':
      case 'askHelper':
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
        // Its window holds the session's own workspace where it names one, else follows this window's (MAINWINDOWS3).
        openWindow.mutate({ name: sessionWindowName(session.id), workspace: session.workspace });
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
   * *Go on with another agent…*'s second press (MIXAGENT1b-i, D163), told back to its ask. A conversation that is not talking
   * goes on at once: the ask closes and the new conversation is attended (`attend`; an unbriefed one is said as an answer, since
   * it opened all the same). One that is talking keeps the ask for its turn's end, and then, *Stop and hand over…*, the turn is
   * stopped here, so its end takes it; the ask says where it goes on. A refusal is said inside the ask by its code and keeps the
   * choice (UX5 U68); a refused hand-over stops nothing. Daoris's offer (MIXAGENT1b-ii) passes its reason: `near` or `limit`.
   */
  const handOverNow = (
    session: Session, choice: { agent: string; account: string; name: string }, told: HandOverTold, why?: 'near' | 'limit',
  ) => handOver.mutate(
    { session: session.id, agent: choice.agent, account: choice.account || null, why },
    {
      onSuccess: (answer) => {
        const said = answer.why ? handOverSaid(t, answer.why, { quest: session.quest, message: answer.message }) : null;
        if (answer.sessionId) {
          if (answer.why === 'unbriefed' && said) notify(said);
          told.done();
          doors.attend?.(answer.sessionId);
          return;
        }
        if (!answer.kept) {
          told.refused(said ?? answer.message);
          return;
        }
        // Kept for the turn's end: while it works, stop the turn, so that its end takes the ask.
        const keptSaid = t('work.handOver.kept', { agent: choice.name });
        if (!SESSION_ACTIVE.has(session.state)) {
          told.kept(keptSaid);
          return;
        }
        cancelTurn.mutate(session.id, { onSuccess: () => told.kept(keptSaid), onError: refusedIn(told) });
      },
      onError: refusedIn(told),
    },
  );

  /**
   * *Ask another agent…*'s second press (MIXAGENT2b, D163 point 2), told back to its ask: `done` once the ask is on file for the
   * driver's next look, said where its answer comes back; a refusal said inside the ask by its code, which keeps the question.
   */
  const askHelperNow = (session: Session, asked: { question: string; agent: string | null }, answered: Answered) => askHelper.mutate(
    { session: session.id, question: asked.question, agent: asked.agent },
    {
      onSuccess: (answer) => {
        if (!answer.asked) {
          answered.refused(helperSaid(t, answer.why, answer.message));
          return;
        }
        notify(t('work.helper.asked'));
        answered.done();
      },
      onError: refusedIn(answered),
    },
  );

  /**
   * *Ask another agent to work on a piece…*'s second press (COAGENT1b, D199 point 2), told back to its ask: `done` once the driver's
   * file holds it, a refusal by its code said inside the ask, keeping the words.
   */
  const askPartnerNow = (session: Session, asked: { piece: string; agent: string; paths: readonly string[] }, answered: Answered) =>
    askPartner.mutate(
      { session: session.id, piece: asked.piece, agent: asked.agent, paths: asked.paths },
      {
        onSuccess: (answer) => {
          if (!answer.asked) {
            answered.refused(pieceSaid(t, answer.why, answer.message));
            return;
          }
          notify(t('work.pieces.asked'));
          answered.done();
        },
        onError: refusedIn(answered),
      },
    );

  /**
   * A working piece's *Stop…* (COAGENT1b): its partner's record's stop, the stop's one press, told back to its ask. The driver reads it
   * as the person's and keeps what the partner wrote.
   */
  const stopPartnerNow = (partner: string, answered: Answered) => stop.mutate(partner, {
    onSuccess: (answer) => {
      notify(t(stopNotice(answer), { id: partner }));
      answered.done();
    },
    onError: refusedIn(answered),
  });

  /**
   * The person's yes to an agent's ask that waits on it (MIXAGENT2b, D163 point 3), told back to its press: `done` once the
   * driver's file holds it; a refusal by its code, said beside the press.
   */
  const approveHelperNow = (session: string, ask: string, answered: Answered) => approveHelper.mutate({ session, ask }, {
    onSuccess: (answer) => {
      if (!answer.approved) {
        answered.refused(helperSaid(t, answer.why, answer.message));
        return;
      }
      notify(t('work.helper.approved'));
      answered.done();
    },
    onError: refusedIn(answered),
  });

  /**
   * The person's no to an agent's ask that waits on their word (HELPERNO1, D163 point 3), told back to its press: `done` once the
   * driver refused it and noted so on the session's record; a refusal by its code, said beside the press.
   */
  const declineHelperNow = (session: string, ask: string, answered: Answered) => declineHelper.mutate({ session, ask }, {
    onSuccess: (answer) => {
      if (!answer.declined) {
        answered.refused(helperSaid(t, answer.why, answer.message));
        return;
      }
      notify(t('work.helper.declined'));
      answered.done();
    },
    onError: refusedIn(answered),
  });

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
  const archiveListed = (ids: readonly string[], answered?: Answered) => archive.mutate({ ids: ids.flatMap((id) => partIds(entries, id)), archived: true }, {
    onSuccess: (answer) => {
      // What the list counts are entries: one kept part keeps its entry in the list.
      const keptParts = new Set((answer?.kept ?? []).map((row) => row.session));
      const kept = ids.filter((id) => partIds(entries, id).some((part) => keptParts.has(part))).length;
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
    handOverNow,
    askHelperNow,
    askPartnerNow,
    stopPartnerNow,
    approveHelperNow,
    declineHelperNow,
    deleteNow,
    archiveListed,
    /** A helper's ask on its way: its ask's presses wait for it (MIXAGENT2b). */
    askingHelper: askHelper.isPending,
    /** A piece's ask on its way: its ask's presses wait for it (COAGENT1b). */
    askingPartner: askPartner.isPending,
    /** A yes or a no to an agent's ask on its way: the rows' presses wait for it. */
    approvingHelper: approveHelper.isPending || declineHelper.isPending,
    /** A stop on its way. */
    stopping: stop.isPending || resolve.isPending,
    /** A hand-over on its way (with the turn's stop it asks after): its ask's presses wait for it. */
    handingOver: handOver.isPending || cancelTurn.isPending,
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
