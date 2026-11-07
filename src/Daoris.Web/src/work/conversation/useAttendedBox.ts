import { useCallback, useState } from 'react';
import type { TFunction } from 'i18next';
import {
  NO_TURNS, useChatTurns, useEditQueued, useSessionGroups, useSessionOptions, useSessionQuestion, useSessionReach,
  useSetSessionOption, useTreeFiles,
} from '../../bridge/shell';
import type { Quest, Session } from '../../data/api';
import { useAsks } from '../../data/queries';
import { answeredPark, SESSION_ACTIVE } from '../../design/ui';
import { type entryOfSession, placeOf } from '../entries';
import { askOf } from '../pausing/pausing';
import { boxOf, goesLine, levelsOf } from '../say';
import { useNow } from '../useNow';
import { goAheadsAsked } from '../waits/parkGoAheads';
import type { RunningCall, Usage } from './conversation';
import { useDraft } from './drafts';

/**
 * What the attended session's box takes and what it answers: the draft kept per session, each live
 * conversation's turns, what a word said now would reach, a park's question and go-aheads, the conversation's options and
 * mentions, and its context and waiting as the conversation tells them.
 */
export function useAttendedBox({ t, attended, here, intake, oneTurn, conversation, live, quest, sessions, attendedEntry, groups }: {
  t: TFunction;
  attended: Session | null;
  here: boolean;
  intake: boolean;
  oneTurn: boolean;
  conversation: boolean;
  live: boolean;
  quest: Quest | null;
  sessions: Session[] | undefined;
  attendedEntry: ReturnType<typeof entryOfSession>;
  groups: ReturnType<typeof useSessionGroups>['data'];
}) {
  const editQueued = useEditQueued(attended?.id ?? null);
  // The go-aheads a park asked on its quest's ask, read only for a driven park whose quest an ask
  // asked: shown in its head, where answering the last one open sends it on. The asks are asked for nowhere
  // else here.
  const parkAsk = attended?.state === 'awaiting-person' && !intake ? askOf(quest) : null;
  const asks = useAsks(false, parkAsk !== null);
  const goAheads = attended && parkAsk ? goAheadsAsked(asks.data, parkAsk, attended.id) : [];
  // A park's question as its agent asked it and the driver kept it for this park, read only for a
  // driven park of this machine's still waiting on the person. Drawn in its box with its answers as presses, and in its head's card.
  const parkWaits = Boolean(attended && here && !intake && attended.kind !== 'chat' && attended.state === 'awaiting-person'
    && !answeredPark(attended));
  const parkQuestion = useSessionQuestion(parkWaits ? attended!.id : null);
  const question = parkWaits ? parkQuestion.data ?? null : null;
  const talking = Boolean(attended && conversation && here && !oneTurn);
  // A driven session still working may be told something: its words are held and are its next prompt. Offered only
  // where the driver says it listens, which is the protocol door; the pipe door has nothing to hear it.
  const steerable = Boolean(attended && here && !oneTurn && !conversation && attended.quest
    && SESSION_ACTIVE.has(attended.state) && attended.state !== 'awaiting-person');

  // What the person is typing to this session, kept per session and across a reload: one draft for whichever
  // box its page offers, so words typed as it ends are still there in the box that goes on with them.
  const [draft, setDraft] = useDraft(attended && here && !oneTurn ? attended.id : null);
  // Where each live conversation's turns stand, as the driver holds them: the attended one's stop and
  // queue follow it, not the record, which learns a turn began only when its first event lands;
  // and the rail and the head read a chat between turns as idle.
  const liveChats = (sessions ?? [])
    .filter((session) => session.kind === 'chat' && SESSION_ACTIVE.has(session.state))
    .map((session) => session.id);
  // A driven session of this machine's that parked or ended, whose record the person's words may wait on: the
  // driver lists them as its queue, each by its id, for the box's *Edit* and *Remove*.
  const resting = Boolean(attended && here && !oneTurn && !conversation
    && (attended.state === 'awaiting-person' || !SESSION_ACTIVE.has(attended.state)));
  const chatTurns = useChatTurns(talking || steerable || resting ? [...liveChats, attended!.id] : liveChats);
  const turns = (talking && chatTurns[attended!.id]) || NO_TURNS;
  // What the driver holds for the attended driven session, and whether it hears anything at all.
  const held = steerable ? chatTurns[attended!.id] : undefined;
  const steering = held?.listening === true;
  // The box at the foot: on every session that takes words, by what a word said now would do, and the
  // line saying why on the rest. 🔴 A driven session parked to ask the person is answered from it, where a chat's is: a
  // button at the top of a record of 1,800 events is far from the question, which is read at its foot. The card above keeps
  // the endings and says the box carries it on: one owner for the answer. Answered, a second word joins the first,
  // so the box stays.
  const reach = useSessionReach(attended && here && !oneTurn ? attended : null, steering);
  const box = boxOf({ session: attended, here, intake, listening: steering, reach });
  // Where a word goes in a line with a head, and the press moving a word held beside the head to it.
  const stands = attended ? placeOf(attendedEntry, attended.id) : null;
  const goes = stands && (box.kind === 'steer' || box.kind === 'say') ? goesLine(t, {
    ...stands, reaches: reach?.reaches ?? null,
    holdsWhy: (groups ?? []).find((row) => row.session === attended?.id)?.holds?.why,
  }) : undefined;
  // What the attended session's door does with a word while a turn runs: the levels its box offers, and the
  // keeping stop where its door can stop a turn keeping the session. Nothing known, none: the box queues.
  const levels = levelsOf(reach);
  const keepingStop = reach?.keepingStop === true;
  const answering = box.kind === 'say' && box.mode === 'answer';
  const taking = Object.fromEntries(Object.entries(chatTurns).map(([id, held]) => [id, held.taking]));
  // When each live chat's last turn ended here: its *moved*, which its record never says.
  const lastTurns = Object.fromEntries(Object.entries(chatTurns).flatMap(([id, held]) => (held.lastTurn ? [[id, held.lastTurn]] : [])));
  // The conversation's model and effort as its agent offered them — asked of a live
  // conversation only, since only a live process has options to change.
  const offered = useSessionOptions(talking && live ? attended!.id : null);
  const setOption = useSetSessionOption();
  // The tree's files for `@`, asked for only while the person is writing a mention.
  const [mentioning, setMentioning] = useState(false);
  const treeFiles = useTreeFiles(talking ? attended!.id : null, mentioning);
  // The conversation's context reading, as its record says — told by the conversation, and
  // kept with the session it came from, so switching never shows one session's ring under another.
  const [reading, setReading] = useState<{ session: string; usage?: Usage } | null>(null);
  const onUsage = useCallback((session: string, usage?: Usage) => setReading({ session, usage }), []);
  // When the attended session's agent has been asked and has answered nothing, likewise told by the conversation,
  // and when a transient episode tries again, while one of its waits runs.
  const [waiting, setWaiting] = useState<{ session: string; since?: string; calling?: RunningCall; retrying?: string } | null>(null);
  const onWaiting = useCallback(
    (session: string, since?: string, calling?: RunningCall, retrying?: string) => setWaiting({ session, since, calling, retrying }), []);
  // The head's *running … for …* steps with the clock, only while a call is named; and *Trying again at* leaves once past.
  useNow(waiting?.calling || waiting?.retrying ? 15_000 : null);

  return {
    editQueued, parkAsk, goAheads, question, talking, draft, setDraft, chatTurns, turns, held, box, stands, goes, levels,
    keepingStop, answering, taking, lastTurns, offered, setOption, setMentioning, treeFiles, reading, onUsage, waiting, onWaiting,
  };
}
