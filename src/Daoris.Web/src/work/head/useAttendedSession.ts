import { useEffect, useRef } from 'react';
import { usePushedResyncing } from '../../bridge/state/slices';
import {
  useAccounts, useHarnesses, useSessionEntries, useSessionGroups, useSessionOpenings, useSessionWhere,
} from '../../bridge/shell';
import { SESSIONS_SLICE, useQuests, useRegistry, useSessions } from '../../data/queries';
import { type Notify, SESSION_ACTIVE, useErrorNotify } from '../../design/ui';
import { askOfEntry, entryOfSession, isIntakeEntry } from '../entries';
import { isHelper, isIntake, isOpinion, sessionOrigin } from '../identity';

/**
 * The sessions the Work frame reads and the one attended: its line of work, its quest, and what kind of session it
 * is here, each read once under the rail's own keys so the head, the rows and the box agree.
 */
export function useAttendedSession({ selected, onSelect, notify }: {
  selected: string | null;
  onSelect: (id: string | null) => void;
  notify: Notify;
}) {
  const sessions = useSessions(null, true);
  const sessionsResyncing = usePushedResyncing(SESSIONS_SLICE);
  // A conversation's name: the same answer the rail asks for, from the same list, so the head
  // and the row read one name and the bridge is asked once.
  const openings = useSessionOpenings(sessions.data);
  // Where the attended session is listed, and where its work is: the rail's own two answers, asked under the
  // same keys, so the page header and its row are offered one set of acts and the bridge is asked once.
  const groups = useSessionGroups();
  // The entries beside them: the attended session's parts, and what an archive reaches. The same answer.
  const entries = useSessionEntries().data;
  const where = useSessionWhere(sessions.data);
  const quests = useQuests(null, true);
  const registry = useRegistry();
  const harnesses = useHarnesses();
  // Each account's cool-off, which says when words a cooling account holds go on.
  const accounts = useAccounts();

  useErrorNotify(sessions.error, notify);

  const attended = (sessions.data ?? []).find((session) => session.id === selected) ?? null;
  // Its ask's channel: the attended line of work's panel, where an ask began it. The same read the
  // ask's page holds, under one key; a conversation, an intake and a line rooted in a quest alone have none.
  const attendedEntry = attended ? entryOfSession(entries, attended.id) : null;
  const channelAsk = attendedEntry && !isIntakeEntry(attendedEntry) ? askOfEntry(attendedEntry) : null;
  const quest = attended?.quest
    ? (quests.data ?? []).find((row) => row.id === attended.quest) ?? null
    : null;
  const live = attended ? SESSION_ACTIVE.has(attended.state) : false;

  // A conversation is the only thing there is anything to say to. A driven session also holds a
  // tree, but it is given its whole target at once and has no channel to speak into — an input
  // box nothing is listening to is worse than none.
  const conversation = attended?.kind === 'chat';
  // A teammate's record came down with the sync and its process is on THEIR machine: it is
  // read here, and nothing this window sends could reach it — so it gets no moves and no composer.
  const here = attended ? sessionOrigin(attended) === null : false;
  // An intake takes no messages at all: it is one turn, framed as one prompt.
  // Parked, there is no process left to hear one, and its answer is on the ask. Running, the pipe
  // door gives it no stdin and on the protocol door its stdin is the driver's own frames — so a box
  // there would send a person's words into nothing, or into the middle of the JSON-RPC stream. The driver
  // refuses such a line too; the frame offers no box, and the page header carries the stop.
  const intake = attended ? isIntake(attended) : false;
  // So does a second opinion's reviewer, and a helper: one turn and no words, which its
  // record's host refuses besides.
  const oneTurn = intake || (attended ? isOpinion(attended) || isHelper(attended) : false);

  // Cleared only when the record it pointed at is gone entirely. A session that ENDED stays
  // attended, because the person is very likely reading exactly that. 🔴 And only a record this
  // frame has SEEN can be gone: a session just started is attended before the list has caught up
  // with it, and clearing on that first answer would leave "Nothing attended" beside a running chat.
  const seen = useRef(new Set<string>());
  useEffect(() => {
    if (!sessions.data) return;
    for (const session of sessions.data) seen.current.add(session.id);
    if (selected && seen.current.has(selected) && !sessions.data.some((session) => session.id === selected)) {
      onSelect(null);
    }
  }, [selected, sessions.data, onSelect]);

  return {
    sessions, sessionsResyncing, openings, groups, entries, where, quests, registry, harnesses, accounts, attended, attendedEntry,
    channelAsk, quest, live, conversation, here, intake, oneTurn,
  };
}
