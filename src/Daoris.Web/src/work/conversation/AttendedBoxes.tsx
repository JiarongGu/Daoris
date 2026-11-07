import type { TFunction } from 'i18next';
import { failure, type Notify } from '../../design/ui';
import { sentence } from '../../locales/format';
import { ownTree } from '../identity';
import type { useAttendedHead } from '../head/useAttendedHead';
import type { useAttendedSession } from '../head/useAttendedSession';
import type { useHeadAsks } from '../head/useHeadAsks';
import type { useAttendedWaits } from '../waits/useAttendedWaits';
import { openGoAheads } from '../waits/parkQuestion';
import type { Roster } from '../start/agentRoster';
import { Composer } from './Composer';
import { SessionBox } from './SessionBox';
import type { useAttendedBox } from './useAttendedBox';
import type { useBoxWords } from './useBoxWords';
import type { Refusal, useWords } from './useWords';

/** Whether a door reports context, from the roster's word on it — undefined until the roster says. */
const door = (structured?: boolean): 'structured' | 'text' | undefined =>
  structured === true ? 'structured' : structured === false ? 'text' : undefined;

/**
 * The box under the conversation (MSG1f, D137 §5.1): a chat's composer, or every other box (the running door, the park's answer, the
 * box where the same session goes on, and the line where nothing takes words), each keyed by its session so a draft's form starts fresh.
 */
export function AttendedBoxes({
  t, notify, session, boxState, words, boxWords, asks, waitsState, headState, refusal, roster, rootOf, attend,
}: {
  t: TFunction;
  notify: Notify;
  session: ReturnType<typeof useAttendedSession>;
  boxState: ReturnType<typeof useAttendedBox>;
  words: ReturnType<typeof useWords>;
  boxWords: ReturnType<typeof useBoxWords>;
  asks: ReturnType<typeof useHeadAsks>;
  waitsState: ReturnType<typeof useAttendedWaits>;
  headState: ReturnType<typeof useAttendedHead>;
  refusal: Refusal | null;
  roster: Roster;
  rootOf: (repository: string) => string | null | undefined;
  attend: (id: string) => void;
}) {
  const { attended, live, sessions } = session;
  const {
    talking, box, draft, setDraft, turns, levels, editQueued, keepingStop, treeFiles, reading, offered, setOption, setMentioning, held,
    chatTurns, goes, answering, question, goAheads, parkAsk,
  } = boxState;
  const { send, end, cancelTurn, onStopTurn, onKeepingStop, onSend, sending, say, onMoveWord, onDraftToHead } = words;
  const { parkGoAhead, parkPicked, onParkPick, ending, boxAccepts, onSay, onBoxGoAhead } = boxWords;
  const { answerFocus } = asks;
  const { seekAt, toEvent } = waitsState;
  if (!attended) return null;
  const refused = refusal?.session === attended.id ? refusal.text : null;
  const focus = answerFocus?.session === attended.id ? answerFocus.at : 0;

  return (
    <>
      {talking && box.kind === 'chat' && (
        <Composer
          // Per session: the files attached in one conversation never follow the person to another.
          key={`composer:${attended.id}`}
          live={live}
          sending={send.isPending}
          refusal={refused}
          // One owner for the moves at a time (D56): while the session is parked the card above holds
          // finish and decline, and this form keeps `send` alone. Its session stop is the page
          // header's at every state (D126 §3.3); this form keeps *Finish* and *Stop turn*.
          endings={attended.state !== 'awaiting-person'}
          // *Answer…* from its row puts the focus here, where a parked chat is answered.
          focus={focus}
          draft={draft}
          onDraft={setDraft}
          queued={turns.queued}
          // A parked conversation's waiting words are its record's, which it goes on with (SAIDBACK1b): no turn, no door.
          queuedLabel={attended.state === 'awaiting-person' ? t('work.say.waiting') : undefined}
          fold={attended.state === 'awaiting-person'}
          going={sending.going}
          taking={turns.taking}
          opening={turns.opening}
          // Only a door that can see a turn end can stop one; the roster says which (CONV3b).
          stoppable={roster.find((row) => row.door === attended.door)?.structured === true}
          stopping={cancelTurn.isPending}
          onStopTurn={onStopTurn}
          levels={levels}
          onLevel={editQueued.level}
          onKeepingStop={keepingStop ? onKeepingStop : undefined}
          onRemove={editQueued.remove}
          onEdit={editQueued.reword}
          mentions={{
            files: treeFiles.data?.files ?? null,
            unlisted: treeFiles.data?.unlisted ?? 0,
            refusal: treeFiles.error ? sentence(treeFiles.error) : null,
          }}
          onMentioning={setMentioning}
          context={{
            usage: reading?.session === attended.id ? reading.usage : undefined,
            door: door(roster.find((row) => row.door === attended.door)?.structured),
          }}
          offered={offered}
          optionsBusy={setOption.isPending}
          // The driver's refusal, or the agent's, reaches the person in its own words.
          onOption={(option, value) => setOption.mutate({ id: attended.id, option, value }, { onError: failure(notify) })}
          onSend={onSend}
          onFinish={() => end.mutate(attended.id, {
            onSuccess: () => notify(t('work.composer.ending', { id: attended.id })),
            onError: failure(notify),
          })}
        />
      )}

      {/* Every other box (MSG1f, D137 §5.1): the running door, the park's answer, the box where the same session goes
          on, and the line where nothing takes words. Keyed by session and box, so a draft's form starts fresh. */}
      {box.kind !== 'chat' && box.kind !== 'none' && (
        <SessionBox
          key={`${box.kind === 'say' ? box.mode : box.kind}:${attended.id}`}
          box={box}
          quest={attended.quest}
          draft={draft}
          onDraft={setDraft}
          sending={say.isPending}
          refusal={refused}
          held={held ? { queued: held.queued, taking: held.taking } : undefined}
          ending={ending?.session === attended.id ? ending.words : []}
          // What waits on a park's or an ended session's record (SAIDBACK1b), as the driver lists it.
          waiting={box.kind === 'say' ? chatTurns[attended.id]?.queued ?? [] : []}
          going={sending.going}
          stopping={cancelTurn.isPending}
          // *Answer…* from its row, and *Send back…* from its review, put the focus here (D126 §3.1, D137 §5.1).
          focus={focus}
          accepts={boxAccepts}
          onSend={onSay}
          levels={box.kind === 'steer' ? levels : undefined}
          onLevel={editQueued.level}
          onKeepingStop={box.kind === 'steer' && keepingStop ? onKeepingStop : undefined}
          onRemove={editQueued.remove}
          onEdit={editQueued.reword}
          goes={goes}
          onMove={onMoveWord}
          toHead={onDraftToHead}
          // The conversation it went on in, by its record's title (HANDWORDS1-ii); one the frame does not hold is said by its id.
          continued={box.kind === 'line' && box.why === 'continued' && box.to
            ? headState.headLink((sessions.data ?? []).find((row) => row.id === box.to))
            : null}
          onOpenSession={(row) => attend(row.id)}
          // What the park asks, in its box with every answer a press the box's words go with (PARKVIEW1, the design §3-§4): its
          // question as its agent asked it, its go-aheads still open, by when and what it holds. Only where the box answers a park.
          park={answering ? {
            quest: attended.quest,
            tree: ownTree(attended, rootOf(attended.repository)),
            since: attended.updated,
            question,
            goAheads: openGoAheads(goAheads),
            busy: say.isPending || parkGoAhead.isPending,
            selected: parkPicked,
            onSelect: question ? onParkPick : undefined,
            onGoAhead: parkAsk ? onBoxGoAhead : undefined,
            seek: seekAt('box'),
            toEvent,
          } : undefined}
        />
      )}
    </>
  );
}
