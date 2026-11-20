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
import { Composer } from '../conversation/Composer';
import { SessionBox } from '../conversation/SessionBox';
import type { useAttendedBox } from '../conversation/useAttendedBox';
import type { useBoxWords } from '../conversation/useBoxWords';
import type { Refusal, useWords } from '../conversation/useWords';

/** Whether a door reports context, from the roster's word on it — undefined until the roster says. */
const door = (structured?: boolean): 'structured' | 'text' | undefined =>
  structured === true ? 'structured' : structured === false ? 'text' : undefined;

/**
 * **The attended session's box**, in the panel's composer: a chat's `Composer` (files, `@` from its tree, what waits with its edit,
 * removal and level, *Stop turn* and the keeping stop, *Finish*, the context ring and the model's options), or every other box
 * (`SessionBox`: the running door, the park's answer, a parked intake's answer posted on its channel, the box where the session
 * goes on, and the line where nothing takes words), each keyed by its session.
 */
export function AttendedComposer({
  t, notify, session, boxState, words, boxWords, asks, waitsState, headState, refusal, roster, rootOf, attend, onAskWord, askSending,
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
  /** A parked intake's words, posted on its ask's channel. */
  onAskWord: (text: string) => void;
  askSending: boolean;
}) {
  const { attended, live, sessions } = session;
  const {
    talking, box, draft, setDraft, turns, levels, editQueued, keepingStop, treeFiles, reading, offered, setOption, setMentioning, held,
    chatTurns, goes, answering, question, goAheads, parkAsk,
  } = boxState;
  const { send, end, cancelTurn, onStopTurn, onKeepingStop, onSend, sending, say, onMoveWord, onDraftToHead } = words;
  const { parkGoAhead, parkPicked, onParkPick, ending, boxAccepts, onSay, onBoxGoAhead } = boxWords;
  const { seekAt, toEvent } = waitsState;
  if (!attended) return null;
  const refused = refusal?.session === attended.id ? refusal.text : null;
  const focus = asks.answerFocus?.session === attended.id ? asks.answerFocus.at : 0;
  const agent = roster.find((row) => row.door === attended.door);

  if (talking && box.kind === 'chat') {
    const parked = attended.state === 'awaiting-person';
    return (
      <Composer
        // Per session: the files attached in one conversation never follow the person to another.
        key={`composer:${attended.id}`}
        live={live}
        sending={send.isPending}
        refusal={refused}
        // While it is parked its card in the dock holds finish and decline, and this keeps *Send* alone.
        endings={!parked}
        focus={focus}
        draft={draft}
        onDraft={setDraft}
        queued={turns.queued}
        // A parked conversation's waiting words are its record's, which it goes on with: no turn, no door.
        queuedLabel={parked ? t('work.say.waiting') : undefined}
        fold={parked}
        going={sending.going}
        taking={turns.taking}
        opening={turns.opening}
        // Only a door that can see a turn end can stop one; the roster says which.
        stoppable={agent?.structured === true}
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
        context={{ usage: reading?.session === attended.id ? reading.usage : undefined, door: door(agent?.structured) }}
        offered={offered}
        optionsBusy={setOption.isPending}
        onOption={(option, value) => setOption.mutate({ id: attended.id, option, value }, { onError: failure(notify) })}
        onSend={onSend}
        onFinish={() => end.mutate(attended.id, {
          onSuccess: () => notify(t('work.composer.ending', { id: attended.id })),
          onError: failure(notify),
        })}
      />
    );
  }
  if (box.kind === 'chat' || box.kind === 'none') return null;
  return (
    <SessionBox
      // Keyed by session and box, so a draft's form starts fresh.
      key={`${box.kind === 'say' ? box.mode : box.kind}:${attended.id}`}
      box={box}
      quest={attended.quest}
      draft={draft}
      onDraft={setDraft}
      sending={box.kind === 'ask' ? askSending : say.isPending}
      refusal={refused}
      held={held ? { queued: held.queued, taking: held.taking } : undefined}
      ending={ending?.session === attended.id ? ending.words : []}
      waiting={box.kind === 'say' ? chatTurns[attended.id]?.queued ?? [] : []}
      going={sending.going}
      stopping={cancelTurn.isPending}
      focus={focus}
      accepts={boxAccepts}
      onSend={box.kind === 'ask' ? onAskWord : onSay}
      levels={box.kind === 'steer' ? levels : undefined}
      onLevel={editQueued.level}
      onKeepingStop={box.kind === 'steer' && keepingStop ? onKeepingStop : undefined}
      onRemove={editQueued.remove}
      onEdit={editQueued.reword}
      goes={goes}
      onMove={onMoveWord}
      toHead={onDraftToHead}
      continued={box.kind === 'line' && box.why === 'continued' && box.to
        ? headState.headLink((sessions.data ?? []).find((row) => row.id === box.to))
        : null}
      onOpenSession={(row) => attend(row.id)}
      // What the park asks, in its box with every answer a press the box's words go with. Only where the box answers a park.
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
  );
}
