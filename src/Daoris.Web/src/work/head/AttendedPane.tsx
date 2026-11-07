import type { ComponentProps } from 'react';
import type { TFunction } from 'i18next';
import type { useSessionAtWork } from '../../bridge/sessions/atWork';
import { buildChain } from '../../map/chain';
import { sentence } from '../../locales/format';
import type { Notify } from '../../design/ui';
import { sayDiscard } from '../../settings/Sweep';
import type { namer } from '../../settings/accounts/namer';
import { AttendedSession } from '../AttendedSession';
import { entryOfSession } from '../entries';
import type { useAttendedBox } from '../conversation/useAttendedBox';
import type { useSessionLanding } from '../review/useSessionLanding';
import type { useAttendedWaits } from '../waits/useAttendedWaits';
import type { Roster } from '../start/agentRoster';
import type { HandOverRoster } from '../start/HandOverAsk';
import type { useAttendedHead } from './useAttendedHead';
import type { useAttendedRecord } from './useAttendedRecord';
import type { useAttendedSession } from './useAttendedSession';
import { relationsOf } from './relations';
import { SessionTerminalHold } from './SessionTerminalHold';

type Page = ComponentProps<typeof AttendedSession>;

/**
 * The attended session's record (D76), the head the conversation sits under: what waits on the person, its line of work, its pieces,
 * its landing and its channel, each the page's own props from the hooks that read them.
 */
export function AttendedPane({
  t, notify, session, record, work, boxState, waitsState, headState, roster, nameOf, agentRoster, following, atWorkHeld, heavy, partRows,
  onShowPart, openAtWork, attend, onOpenQuest, onAnswerAsk, onOpenBranch,
}: {
  t: TFunction;
  notify: Notify;
  session: ReturnType<typeof useAttendedSession>;
  record: ReturnType<typeof useAttendedRecord>;
  work: ReturnType<typeof useSessionLanding>;
  boxState: ReturnType<typeof useAttendedBox>;
  waitsState: ReturnType<typeof useAttendedWaits>;
  headState: ReturnType<typeof useAttendedHead>;
  roster: Roster;
  nameOf: ReturnType<typeof namer>;
  agentRoster: HandOverRoster;
  following: { id: string } | null;
  atWorkHeld: ReturnType<typeof useSessionAtWork>['data'];
  heavy: Page['heavy'];
  partRows: NonNullable<Page['parts']>['rows'];
  onShowPart: (id: string) => void;
  /** Opens *At work* at the request named (COMMS1i). */
  openAtWork: (exchange: string) => void;
  attend: (id: string) => void;
  onOpenQuest?: (quest: string) => void;
  onAnswerAsk?: (ask: string) => void;
  onOpenBranch?: (repository: string, branch: string) => void;
}) {
  const { attended, entries, quest, openings, attendedEntry, channelAsk, sessions, quests, here, live } = session;
  const {
    related, relatedOwn, together, bringTogether, togetherAct, memoryRows, decideMemory, sessionRecord, channel, channelOpened,
    postChannel, forwardChannel, branch, discardBranch, told, tracing, setTracing, trace, milestone, supervision,
  } = record;
  const { ownBranch, landEntry, entryOffered, entryAccept, workAskAt } = work;
  const { waits, pieceSection, toEvent, ruleAskAt, seekAt } = waitsState;
  const { continues, helps } = headState;
  const { taking, lastTurns } = boxState;
  if (!attended || !waits) return null;
  const attendedParts = entryOfSession(entries, attended.id);

  return (
    <AttendedSession
      // What waits on the person, and every press on it: the strip at the foot is handed the same (D168 point 3).
      {...waits}
      // Its pieces (COAGENT1b): what other agents write for it, and the ask of one; no wait of the person's.
      pieces={pieceSection}
      // COMMS1i: what its line waits on of others, At work's line from At work's read, a door to At work at the request it names.
      waitingOn={following?.id === attended.id && atWorkHeld && 'answer' in atWorkHeld ? {
        answer: atWorkHeld.answer,
        nameOf: (agent: string) => agentRoster.names?.[agent] ?? agent,
        onOpen: openAtWork,
      } : null}
      // RPC1g1: a terminal holding this conversation, said on its head with *Let go of the terminal*.
      terminalHold={<SessionTerminalHold session={attended.id} />}
      workAsk={workAskAt('head')}
      ruleAsk={ruleAskAt('head')}
      toEvent={toEvent}
      seek={seekAt('head')}
      quest={quest}
      opening={openings[attended.id]}
      taking={taking[attended.id]}
      lastTurn={lastTurns[attended.id]}
      // The page header above carries its state, its id and its stop (D126 §3.2): said once.
      headed
      // An agent on the roster has accounts, so a record naming none ran on the tool's own sign-in (D125 §3.7).
      ownSignIn={roster.some((row) => row.door === attended.adapter)}
      nameOf={nameOf}
      // Its entry's parts (SESSONE1c, D167 point 5): every act on this page stays the part shown's, and the strip picks it.
      parts={attendedParts ? { entry: attendedParts, attended: attended.id, rows: partRows, onShow: onShowPart } : undefined}
      entryAccept={entryAccept}
      // Its ask's channel, kept to this line of work (SESSONE2c): a post is the person's words, and a refusal is the service's code.
      channel={attendedEntry && channelAsk && channel.available && (channel.loading || channel.channel) ? {
        channel: channel.channel,
        only: { entry: attendedEntry.entry, repository: attendedEntry.repository },
        onPost: (post, answered) => postChannel.mutate({ ask: channelAsk, post }, {
          onSuccess: (posted) => (posted.posted ? answered.done() : answered.refused(posted.code ?? 'other')),
          onError: (error) => answered.refused(sentence(error)),
        }),
        // COWORK1d: *Send to its session*, the person's own press, as on the ask's page.
        onForward: (message, repository, answered) => forwardChannel.mutate({ ask: channelAsk, message, repository }, {
          onSuccess: (forwarded) => (forwarded.sent ? answered.done() : answered.refused(forwarded.code ?? 'other')),
          onError: (error) => answered.refused(sentence(error)),
        }),
        onOpenQuest,
        onOpenAsk: onAnswerAsk ? () => onAnswerAsk(channelAsk) : undefined,
        opened: channelOpened,
      } : undefined}
      chain={quest ? buildChain(quest.id, quests.data ?? [], sessions.data ?? []) : []}
      relations={relationsOf(attended, quest, quests.data ?? [], sessions.data ?? [])}
      related={related}
      relatedOwn={relatedOwn}
      onRelated={attend}
      together={{
        candidates: related.filter((row) => row.read !== 'not-read').map((row) => row.id),
        operation: together,
        busy: bringTogether.isPending || togetherAct.isPending || landEntry.isPending,
        said: bringTogether.data?.refused ?? togetherAct.data?.refused
          ?? (landEntry.variables?.entry === attendedEntry?.entry ? landEntry.data?.message ?? null : null),
        onBring: here ? (members) => bringTogether.mutate({ session: attended.id, members }) : undefined,
        onLetGo: () => togetherAct.mutate({ session: attended.id, act: 'let-go' }),
        onAskAgain: () => togetherAct.mutate({ session: attended.id, act: 'ask-again' }),
        onAccept: attendedEntry
          ? () => landEntry.mutate({ entry: attendedEntry.entry, parts: Math.max(1, entryOffered), leaveOut: [] })
          : undefined,
        onSession: attend,
      }}
      memory={{
        rows: memoryRows,
        ended: !live,
        busy: decideMemory.isPending,
        said: decideMemory.variables?.session === attended.id ? decideMemory.data?.message ?? null : null,
        onDecide: (op, approve) => decideMemory.mutate({ session: attended.id, op, approve }),
        onKeep: (keep) => decideMemory.mutate({ session: attended.id, keep }),
      }}
      continues={continues}
      helps={helps}
      heavy={heavy}
      process={told.data?.process ?? null}
      supervision={supervision}
      onSession={(row) => attend(row.id)}
      onQuest={onOpenQuest ? (row) => onOpenQuest(row.id) : undefined}
      branch={branch}
      ownBranch={ownBranch}
      // Only this machine's own record: the clean-up's list is this machine's, and so is the branch it names.
      // Told back to its ask (UXFIX2): a branch kept, or a refusal, is said inside it.
      onDiscardBranch={branch && here
        ? (answered) => discardBranch.mutate(branch, {
          onSuccess: sayDiscard(notify, t, answered),
          onError: (error) => answered.refused(sentence(error)),
        })
        : undefined}
      discardingBranch={discardBranch.isPending}
      milestone={milestone}
      record={sessionRecord}
      onOpenBranch={onOpenBranch}
      trace={trace.available ? {
        open: tracing === attended.id,
        onToggle: () => setTracing(tracing === attended.id ? null : attended.id),
        answer: tracing === attended.id ? trace.query.data : undefined,
        reading: tracing === attended.id && trace.query.isPending,
        refusal: tracing === attended.id && trace.query.error ? sentence(trace.query.error) : null,
        onSession: attend,
        onQuest: onOpenQuest,
        onAsk: onAnswerAsk,
      } : undefined}
    />
  );
}
