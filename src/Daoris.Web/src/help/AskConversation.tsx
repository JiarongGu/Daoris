import { type ReactNode, type RefObject, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { HELP_REPOSITORY } from '../data/api';
import { type Carry, NO_CARRY } from '../compose/carry';
import { sentence } from '../locales/format';
import { useHelpSessions, useWorkspaces } from '../data/queries';
import { useScope } from '../data/scope';
import { DEFAULT_WORKSPACE, workspaceOf } from '../data/workspaces';
import {
  type HelpSettled, NO_TURNS, helpModeOf, useCancelTurn, useChatTurns, useDeleteSession, useDriver, useEditQueued, useEndChat,
  useHarnesses, useHelpConversations, useHelpProposals, useHelpReadied, useHelpStartFrom, useHelpWired, usePinHelp, usePlugins,
  usePollUnlessTold, useRenameHelp, useSendMessage, useSessionEvents, useSetHelpMode, useSettleHelp, useStartHelp, useStopSession,
  stoppedWaiting,
} from '../bridge/shell';
import { useHeldHarnessRun } from '../agents/harnessRuns';
import { Button, Prose, SESSION_ACTIVE } from '../design/ui';
import { FullAccessConfirm, HelpModeChip, useModeChoice } from './access/HelpModePicker';
import { fullAdds } from './access/modes';
import { WhatItHas } from './access/WhatItHas';
import type { ChatMessage } from '../work/conversation/conversation';
import { Composer } from '../work/conversation/Composer';
import { BackToBottom, SessionConversation, type TailState } from '../work/conversation/SessionConversation';
import { AskHistory } from './AskHistory';
import { searchable } from '../lib/searchable';
import { type HelpConversationRow, sameWorkspace } from './history';
import type { AskConversationSlot } from './AskPanel';
import { placeDoor } from './places';
import { ProposalCard } from './ProposalCard';
import type { StarterDoor } from './starters';
import { type HelpWhere, prefaceOf } from './where';
import { logEvent } from '../bridge/shell';

/** How often the list is asked again while words to an ended conversation wait for it to go on (ASKHIST1). */
const GOING_ON_POLL = 1500;

/**
 * How far before its send a record's words may be stamped and still be the words sent (SENDLOST1): the record's time is the
 * driver's, the send's the page's, on one machine.
 */
const SAID_SKEW = 5000;

/** The words that open a conversation (HELP4), with their files, and the conversation that takes them once known. */
type FirstWords = {
  words: ChatMessage;
  files: File[];
  session?: string;
  /**
   * Where the page stopped waiting on the start (SENDLOST1): the conversations listed as the words were said, when it stopped,
   * and its sentence, said if nothing took them.
   */
  late?: { before: string[]; at: number; why: string };
  /** The workspace the start is in (ASKSCOPE1d), read at its press: only a conversation of that workspace takes them. */
  circle?: string;
};

/** Words on their way to one conversation (SENDLOST1), shown the moment they are sent until it holds them. */
type Sent = {
  /** Which send, so its answer settles its own words. */
  n: number;
  words: ChatMessage;
  files: File[];
  session: string;
  /** When they were sent, on the page's clock. */
  at: number;
  /** The page stopped waiting on the send (the bridge's TIMEOUT): the machine may still take them. */
  late: boolean;
};

/** What the person is writing to one conversation: its words and its files (ASKHIST1c). */
type Draft = { text: string; carry: Carry };
const NO_DRAFT: Draft = { text: '', carry: NO_CARRY };
/** The draft of a new conversation, kept apart from every conversation's own. */
const NEW_DRAFT = 'new';

/**
 * Words handed to Ask Daoris from another door, once per `id`: a question, sent as a typed one is (the palette's, the
 * setup guide's), or a draft, put in the box for the person to finish (CTX1's *Ask Daoris about it*).
 */
export type AskOpening = { text: string; id: number; draft?: boolean };

/**
 * Ask Daoris's conversation (HELP1a, D89): the organism that holds the newest help session's record
 * and its composer, so the panel holds neither.
 *
 * @remarks
 * **One conversation per workspace, carried on across opens** (design §2, D158's ASKSCOPE1 note): the panel shows the
 * newest help session of the window's workspace, running or ended. **With none running, one opens as the panel shows**
 * (HELP5) — the driver writes the room from the machine as it stands, spawns the agent and opens its session — so that is done
 * before the person types, and there is no start button to press before asking. Nobody has spoken in it
 * yet, so the panel goes on showing what it showed until the first message goes to it. One that could not
 * open says nothing: **a message with none to take it starts one**, and says why when it cannot.
 * *New conversation* finishes the one running and clears the panel; the next opens once it has gone.
 *
 * **Where the person is goes ahead of their words** (HELP1b): the view, the workspace in scope, the
 * session they attend and what a parked one asks — said when it changed since the last message to that
 * conversation, so the agent is not handed the same line every turn.
 *
 * Its words reach nothing but the harness, as every conversation's do (D24): the driver makes no model
 * call. Desktop-only for the console's reason (D47 §4): the record arrives over the bridge.
 *
 * **A question can arrive already asked** (DOCK1d): the palette's *Ask Daoris: "…"* row hands its words
 * in as `opening`, sent once per `id` exactly as a typed message is, so Quick Ask opens on the question
 * rather than on a box the person must type it into again.
 *
 * **Its conversations are kept** (ASKHIST1): the history lists them, kept on this machine only, and the one the person
 * picks is shown in the newest's place. One that ended goes on in itself with the person's next words where its own
 * conversation was kept (D137 §2.2), so its knowledge carries; one that cannot is offered a new conversation from its words.
 * The newest that ended still starts a new one by default, as it did; *Go on in it* chooses it. A draft handed in
 * (CTX1, the composer's *Make it a task with Daoris…*) shows that new conversation: the panel clears first, ending nothing (NEW1g).
 */
export function useAskConversation(
  scroller: RefObject<HTMLElement | null>,
  where?: HelpWhere,
  opening?: AskOpening | null,
  /** The agent Ask Daoris runs on, or null: with none named, nothing is opened ahead of the person. */
  helper: string | null = null,
  /**
   * Told once an opening has been sent (SETUP1b), so its holder lets it go: this organism is drawn again
   * with its tab or its box, and a question still held would be asked again by the next drawing.
   */
  onOpened?: () => void,
  /** Where a go the person applied takes them (HELP6): the starters' own door, so nothing else changes. */
  onGo?: (door: StarterDoor) => void,
) {
  const { t } = useTranslation();
  // The workspace the panel's conversations are in (ASKSCOPE1d, D158's ASKSCOPE1 note, points 1 and 6): the window's; where it
  // shows every workspace, the one there is, or `default` where none is registered. Null where it shows several, so a start
  // asks which; undefined while this machine's workspaces are still being read. One unreadable is none registered.
  const { workspace: scoped, setWorkspace } = useScope();
  const circles = useWorkspaces();
  const registered = circles.data ?? (circles.isError ? [] : undefined);
  const here: string | null | undefined = scoped
    ?? (registered === undefined ? undefined : registered.length > 1 ? null : registered[0] ?? DEFAULT_WORKSPACE);
  const inHere = (workspace?: string) => typeof here === 'string' && sameWorkspace(workspace ?? DEFAULT_WORKSPACE, here);
  // What a start names, read at its press: the workspace it is in, or none where the window chose none and none is registered,
  // which is `default`'s, as every start before ASKSCOPE1d was. Never `default` read for a window that shows several.
  const startIn = (workspace: string) => (scoped === null && registered?.length === 0 ? undefined : workspace);
  // The window's workspace now, for an answer that arrives after it moved.
  const hereNow = useRef(here);
  useEffect(() => {
    hereNow.current = here;
  }, [here]);
  // The conversation the person chose from the history, or went on in (ASKHIST1): shown in the newest's place.
  const [chosen, setChosen] = useState<string | null>(null);
  // Words said to an ended conversation, waiting for its record to go on: the list is asked again until it has.
  const [goingOn, setGoingOn] = useState<string | null>(null);
  // A desktop window is told `sessions` as the driver takes the words up (FABRIC-EVENT5a), so only a window with no bus reads on a clock.
  const goingOnPoll = usePollUnlessTold(GOING_ON_POLL);
  const sessions = useHelpSessions(goingOn ? goingOnPoll : false);
  // The session the person started again from: shown no longer, whatever the list still says.
  const [cleared, setCleared] = useState<string | null>(null);
  // The conversation opened as the panel showed (HELP5), which nobody has spoken in: not shown, so the
  // starters, or the conversation that ended, stay in front of the person until the first words go to it.
  const help = useHelpReadied();
  // The one opened ahead in this window's workspace (ASKSCOPE1d): one opened in another never takes this one's words.
  const readied = typeof here === 'string' ? help.readiedIn(here) : null;
  const helpSessions = (sessions.data ?? []).filter((session) => session.repository === HELP_REPOSITORY);
  // The window's workspace's conversations (ASKSCOPE1d): its newest is shown, and its running one carried on. A window on every
  // workspace shows the newest of any, named; one whose workspaces are still being read shows none yet.
  const mine = here === null ? helpSessions : helpSessions.filter((session) => inHere(session.workspace));
  const newest = mine
    .filter((session) => !help.unspoken.includes(session.id))
    .sort((a, b) => b.created.localeCompare(a.created))[0] ?? null;
  const picked = chosen ? helpSessions.find((session) => session.id === chosen) ?? null : null;
  const shown = picked ?? (newest && newest.id !== cleared ? newest : null);
  const live = shown ? SESSION_ACTIVE.has(shown.state) : false;
  // The workspace the conversation shown started in; another workspace's is read here, named, and gone on in only once the
  // window has switched to it (ASKSCOPE1d, point 1): words are never taken up in another workspace from this one.
  const shownIn = shown ? workspaceOf(shown) : null;
  const elsewhere = shown !== null && typeof here === 'string' && !inHere(shown.workspace);
  useEffect(() => {
    if (goingOn && shown?.id === goingOn && live) setGoingOn(null);
  }, [goingOn, shown?.id, live]);

  // The history (ASKHIST1): open or not, its search, and each conversation's row, which says whether one that ended goes on
  // in itself. The panel's own rows are asked whenever an agent is named, so the conversation shown knows its own.
  const [historyOpen, setHistoryOpen] = useState(false);
  const [search, setSearch] = useState('');
  const [historyRefusal, setHistoryRefusal] = useState<string | null>(null);
  const words = search.trim();
  const searching = searchable(words);
  // Whose conversations the history lists (ASKSCOPE1d, point 6): the window's workspace's, unless the person chose every
  // workspace's; a window on every workspace lists every one's. Its search runs over that scope alone. The choice is offered
  // where there is another workspace to see; a machine whose one workspace is `default` lists every one's, unlabelled, as before,
  // so a conversation of a workspace since gone is still reached, and named once it is opened.
  const [everyWorkspace, setEveryWorkspace] = useState(false);
  const offerEvery = typeof here === 'string' && ((registered?.length ?? 0) > 1 || !sameWorkspace(here, DEFAULT_WORKSPACE));
  const listIn = typeof here === 'string' && offerEvery && !everyWorkspace ? here : null;
  const historyScope = here === undefined ? undefined
    : here === null ? { workspace: null }
      : offerEvery ? { workspace: listIn, here, onEvery: setEveryWorkspace } : undefined;
  // Each a page at a time, every page read joined (ASKHIST1d2); asked once the window's workspace is known.
  const rows = useHelpConversations('', helper !== null && here !== undefined, listIn);
  const found = useHelpConversations(words, helper !== null && here !== undefined && historyOpen && searching, listIn);
  const listing = searching ? found : rows;
  // The last list answered, held while a newer one comes, so a search typed never blanks the rows (UX §4; ASKHIST1c).
  const [lastListing, setLastListing] = useState<typeof rows.data>(undefined);
  useEffect(() => {
    if (listing.data) setLastListing(listing.data);
  }, [listing.data]);
  const shownListing = listing.data ?? lastListing;
  // A conversation's row (ASKHIST1d2): from the whole list's pages read, else the search's, which reaches every conversation
  // past them (ASKHIST1d1); else the row the person opened it from, once no list being asked can name it.
  const [openedRow, setOpenedRow] = useState<HelpConversationRow | null>(null);
  const listed = (id: string) => rows.data?.conversations.find((row) => row.session === id)
    ?? found.data?.conversations.find((row) => row.session === id) ?? null;
  const rowOf = (id: string) => listed(id) ?? (openedRow?.session === id ? openedRow : null);
  // Whether an ended one shown can go on in itself is the history's answer, so until it answers it is being checked
  // (ASKHIST1c): read as "cannot", words typed under one that could would have started a fresh conversation without its own.
  const checking = shown !== null && listed(shown.id) === null && (rows.isPending || rows.isFetching);
  const known = shown ? rowOf(shown.id) : null;
  const shownRow = checking ? null : known;
  // An ended one the person chose goes on in itself with their words, where its own conversation was kept.
  const goesOn = shown !== null && !live && picked !== null && shownRow?.resumable === true;
  // One the person chose that has ended and does not go on, or is still being checked: read, and written in only once they
  // choose a new conversation from it, or a blank one.
  const readOnly = shown !== null && !live && picked !== null && !goesOn;
  // Where the person left the list from (ASKHIST1c): the row they opened and the list's scroll, put back as they return; and
  // what is put back the next time the list opens.
  const [left, setLeft] = useState<{ row: string; scroll: number } | null>(null);
  const [restore, setRestore] = useState<{ row: string | null; scroll: number } | null>(null);
  // Where the focus goes next, once: the open conversation's heading as a row is opened, or its box as a new one starts.
  // Each is let go after the drawing that takes it, so a later drawing never takes the focus back.
  const [focusHead, setFocusHead] = useState(0);
  const [focusBox, setFocusBox] = useState(0);
  useEffect(() => {
    if (focusHead) setFocusHead(0);
  }, [focusHead]);
  useEffect(() => {
    if (focusBox) setFocusBox(0);
  }, [focusBox]);
  // A conversation started from an earlier one takes the focus in its box once the list holds it: the driver answers before the
  // sessions the state hub asks again name it (STATEHUB1h, D169), and a box asked for before then would take nothing. Let go if
  // the person chose another meanwhile.
  const [focusWhenListed, setFocusWhenListed] = useState<string | null>(null);
  useEffect(() => {
    if (focusWhenListed === null) return;
    if (chosen !== focusWhenListed) {
      setFocusWhenListed(null);
    } else if (picked?.id === focusWhenListed) {
      setFocusWhenListed(null);
      setFocusBox((was) => was + 1);
    }
  }, [focusWhenListed, chosen, picked?.id]);
  // Whether the reader left the conversation's tail, and the way back (ASKHIST1c): drawn in a strip above the box.
  const [tail, setTail] = useState<TailState | null>(null);
  // A conversation started from an earlier one, which says so until the person has spoken in it.
  const [startedFrom, setStartedFrom] = useState<{ session: string; title: string } | null>(null);
  const rename = useRenameHelp();
  const pin = usePinHelp();
  const startFrom = useHelpStartFrom();
  const remove = useDeleteSession();
  // Not in the list yet is still opening, which is alive; one the list says ended can take no words.
  const readiedRow = helpSessions.find((session) => session.id === readied);
  const readiedLive = readied !== null && (readiedRow === undefined || SESSION_ACTIVE.has(readiedRow.state));

  const start = useStartHelp();
  // Its own mutation, so opening ahead never reads as the person's send pending, which disables the box.
  const readying = useStartHelp();
  const send = useSendMessage();
  const end = useEndChat();
  const stop = useStopSession();
  const cancelTurn = useCancelTurn();
  const editQueued = useEditQueued(shown?.id ?? null);
  const roster = useHarnesses();
  const held = useChatTurns(shown && live ? [shown.id] : []);
  const turns = held[shown?.id ?? ''] ?? NO_TURNS;
  const structured = Array.isArray(roster.data?.harnesses)
    ? roster.data.harnesses.find((row) => row.harness === shown?.adapter)?.structured
    : undefined;

  // What the person is writing, kept per conversation and apart for a new one (ASKHIST1c): its words and its files, held here
  // rather than in the box, so going through the history and back loses neither, and one conversation's draft never follows
  // the person into another. The box writes the conversation it would send to: the one shown where words reach it, else a
  // new one's.
  const [drafts, setDrafts] = useState<Record<string, Draft>>({});
  const draftKey = shown && (live || picked) ? shown.id : NEW_DRAFT;
  const draft = drafts[draftKey] ?? NO_DRAFT;
  const editDraft = (key: string, change: (was: Draft) => Draft) =>
    setDrafts((all) => ({ ...all, [key]: change(all[key] ?? NO_DRAFT) }));
  const [refusal, setRefusal] = useState<string | null>(null);
  // The conversation the person's own first words opened, called by them until the history names it (ASKHIST1c).
  const [spoken, setSpoken] = useState<{ session: string; title: string } | null>(null);
  // The words that open a conversation (HELP4): opening one takes seconds — the room written, the
  // agent spawned — and they showed nowhere meanwhile, so they read as lost. Held here until the
  // driver's queue answers for the conversation they opened, which shows them from then on: let go
  // any sooner, they vanished while the session list caught up. One that ended at once has no queue
  // to answer, and its record says what became of them. `late` where the page stopped waiting on the start (SENDLOST1).
  const [firstWords, setFirstWords] = useState<FirstWords | null>(null);
  const handedOver = firstWords?.session !== undefined && shown?.id === firstWords.session
    && (!live || held[firstWords.session] !== undefined);
  useEffect(() => {
    if (handedOver) setFirstWords(null);
  }, [handedOver]);
  const waiting = firstWords && !handedOver ? firstWords.words : null;
  // Words on their way to a conversation (SENDLOST1): shown the moment they are sent, until it holds them.
  const [sent, setSent] = useState<Sent[]>([]);
  const sends = useRef(0);
  // The box shown now: a message given back after the list moved on goes where the person can see it. Words to a
  // conversation the list then shows ended were given back to a box no longer drawn, and read as lost (SENDLOST1).
  const box = useRef(draftKey);
  useEffect(() => {
    box.current = draftKey;
  }, [draftKey]);
  // 🔴 A message that did not arrive goes back into the box, its files with it, never lost (the composer's own rule).
  const putBack = (text: string, files: File[]) => editDraft(box.current, (was) => ({
    text: [text, was.text.trim()].filter(Boolean).join('\n\n'),
    carry: { ...was.carry, files: [...files, ...was.carry.files] },
  }));
  const giveBack = (text: string, files: File[], why: string) => {
    setRefusal(why);
    putBack(text, files);
  };
  // A start asked for on a window that shows several workspaces (ASKSCOPE1d, point 6): which workspace is asked first. The words
  // wait in the box meanwhile, and a new conversation from an earlier one's words waits by that one's id.
  const [choosing, setChoosing] = useState<{ from?: string; unknown?: boolean } | null>(null);

  // The last preface each conversation was handed, so an unchanged screen is not said again.
  const told = useRef(new Map<string, string>());
  /**
   * The person's words to one conversation. Words to the one running are shown at once, as sending; first words are shown as
   * opening it already, and `lost` lets them go. 🔴 A send the page stopped waiting on (the bridge's TIMEOUT) may still be taken
   * by the machine: its words are kept, saying so, and never put back in the box, where a second send would say them twice
   * (SENDLOST1). They settle by the conversation's own record or the list's next read.
   */
  const deliver = (id: string, text: string, files: File[], lost?: () => void) => {
    const now = where ? prefaceOf(where) : undefined;
    const preface = now && told.current.get(id) !== now ? now : undefined;
    const n = ++sends.current;
    const words: ChatMessage = { text, files: files.map((file) => file.name) };
    const at = Date.now();
    if (!lost) setSent((all) => [...all, { n, words, files, session: id, at, late: false }]);
    const done = () => setSent((all) => all.filter((each) => each.n !== n));
    send.mutate({ id, text, files, ...(preface ? { preface } : {}) }, {
      onSuccess: (answer) => {
        done();
        if (!answer.sent) {
          lost?.();
          giveBack(text, files, t('help.notSent'));
          return;
        }
        if (preface) told.current.set(id, preface);
        // A conversation the history has not listed yet is listed once it is spoken in, and named by its first question: the
        // word said declares the history, which the state hub asks again (HELPHISTORYHUB1, D169).
        // Kept on an ended record (ASKHIST1): the same conversation goes on with them once the driver takes them up, which
        // the list is asked again for until it says so.
        if (answer.reaches === 'resume') setGoingOn(id);
      },
      onError: (error) => {
        if (stoppedWaiting(error)) {
          setSent((all) => [...all.filter((each) => each.n !== n), { n, words, files, session: id, at, late: true }]);
          lost?.();
          void sessions.refetch();
          return;
        }
        done();
        lost?.();
        giveBack(text, files, sentence(error));
      },
    });
  };

  // Words whose send the page stopped waiting on, to the conversation shown (SENDLOST1), settled by its own record: let go
  // once its record or its queue holds them, and given back, said so, once it ended without them, since they never arrived.
  const lateShown = shown ? sent.filter((each) => each.late && each.session === shown.id) : [];
  const record = useSessionEvents(lateShown.length > 0 ? shown!.id : null);
  useEffect(() => {
    if (lateShown.length === 0) return;
    const holds = (each: Sent) => turns.queued.some((queued) => queued.text === each.words.text)
      || record.events.some((event) => event.kind === 'user' && event.origin === 'person' && event.text === each.words.text
        && Date.parse(event.at) >= each.at - SAID_SKEW);
    const arrived = lateShown.filter(holds);
    const never = !live && record.loaded ? lateShown.filter((each) => !holds(each)) : [];
    if (arrived.length === 0 && never.length === 0) return;
    const settled = new Set([...arrived, ...never].map((each) => each.n));
    setSent((all) => all.filter((each) => !settled.has(each.n)));
    for (const each of never) giveBack(each.words.text, each.files, t('help.notSent'));
    // Asked again as the record, the queue or the list moves; the words themselves are read from this render.
  }, [record.events, record.loaded, turns.queued, live, lateShown.length]);

  // The opening ahead still on its way (HELP5): what it answers, the conversation it opened or null; and the workspace it was
  // opened in (ASKSCOPE1d), whose words alone it takes.
  const ahead = useRef<Promise<string | null> | null>(null);
  const aheadIn = useRef<string | null>(null);
  // First words waiting on that answer: the box holds its fire, as it does while a send opens one.
  const [joining, setJoining] = useState(false);

  // First words handed to the conversation that takes them: seen from now on, in both hosts, however late an opening's
  // answer arrives, and shown as the conversation now, whichever the person had chosen (ASKHIST1).
  const handFirst = (id: string, words: ChatMessage, files: File[], circle?: string) => {
    help.spoke(id);
    setCleared(null);
    // A window moved to another workspace while the start was under way (ASKSCOPE1d, point 1): the conversation stays in the
    // workspace of its press, so it is shown by its id, named as that workspace's, rather than as this one's newest.
    const moved = circle !== undefined && typeof hereNow.current === 'string' && !sameWorkspace(circle, hereNow.current);
    setChosen(moved ? id : null);
    setFirstWords({ words, files, session: id, circle });
    // Called by its first question, as the driver will call it, until the history says so (ASKHIST1c).
    const first = words.text.split('\n', 1)[0]!.trim();
    if (first) setSpoken({ session: id, title: first });
    deliver(id, words.text, files, () => setFirstWords(null));
  };

  // First words whose start the page stopped waiting on (SENDLOST1), settled by the list's next read: the driver may have
  // opened their conversation all the same, and the one it lists now that it did not when they were said takes them, once.
  // Where it lists none, nothing took them, and they go back in the box with why.
  useEffect(() => {
    const words = firstWords;
    if (!words?.late || words.session !== undefined || sessions.dataUpdatedAt <= words.late.at) return;
    const late = words.late;
    // Of the workspace the start was in alone (ASKSCOPE1d): one opened meanwhile in another is not theirs.
    const opened = helpSessions.find((session) => SESSION_ACTIVE.has(session.state) && !late.before.includes(session.id)
      && (words.circle === undefined || sameWorkspace(workspaceOf(session), words.circle)));
    if (opened) {
      handFirst(opened.id, words.words, words.files, words.circle);
      return;
    }
    setFirstWords(null);
    giveBack(words.words.text, words.files, late.why);
    // Only the list's next read settles them.
  }, [sessions.dataUpdatedAt, firstWords?.late]);

  const onSend = (text: string, files: File[]) => {
    logEvent('message.sent', { kind: 'help', length: text.length, files: files.length, ...(shown && (live || goesOn) ? { session: shown.id } : {}) });
    setRefusal(null);
    // The one running, or one that ended the person chose and that goes on in itself (ASKHIST1); never another workspace's
    // (ASKSCOPE1d), whose box is not drawn, and a question handed in from another door starts one in this window's instead.
    if (shown && (live || goesOn) && !elsewhere) {
      if (startedFrom?.session === shown.id) setStartedFrom(null);
      deliver(shown.id, text, files);
      return;
    }

    // A start is in the window's workspace, read now (ASKSCOPE1d, point 1). A window that shows several asks which first, and
    // never starts one in `default` by itself: the words go back in the box once the composer has cleared it, and wait there.
    if (typeof here !== 'string') {
      queueMicrotask(() => putBack(text, files));
      setChoosing({ unknown: here === undefined });
      return;
    }
    startWith(text, files, here);
  };

  // The workspace chosen for a start on a window that shows several: the words waiting in the box go to a new conversation there,
  // or the earlier conversation waiting by its id is started from. The window stays where it is.
  const startChosen = (circle: string) => {
    const waiting = choosing;
    setChoosing(null);
    if (waiting?.from) {
      beginFrom(waiting.from, circle);
      return;
    }
    const kept = drafts[NEW_DRAFT] ?? NO_DRAFT;
    if (!kept.text.trim() && kept.carry.files.length === 0) return;
    setDrafts(({ [NEW_DRAFT]: _taken, ...rest }) => rest);
    startWith(kept.text.trim(), kept.carry.files, circle);
  };
  // Asked only because this machine's workspaces were still being read: once they name one to be in, the words go there. Read
  // as several, the question stands.
  useEffect(() => {
    if (choosing?.unknown && typeof here === 'string') startChosen(here);
    // Only the workspaces' answer settles it; the words are read from this render.
  }, [choosing, here]);

  // The conversation opened ahead in `circle` that nobody has spoken in and that still runs, or null.
  const readiedFor = (circle: string) => {
    const id = help.readiedIn(circle);
    const row = id === null ? undefined : helpSessions.find((session) => session.id === id);
    return id !== null && (row === undefined || SESSION_ACTIVE.has(row.state)) ? id : null;
  };

  /** A new conversation's first words, in `circle`, the workspace read at the press. */
  const startWith = (text: string, files: File[], circle: string) => {
    const said = { text, files: files.map((file) => file.name) };
    // The conversations listed as the words are said: one listed later is one opened for them.
    const before = helpSessions.map((session) => session.id);
    setFirstWords({ words: said, files, circle });
    const handTo = (id: string) => handFirst(id, said, files, circle);
    const begin = () => start.mutate(startIn(circle), {
      // The driver's own sentence when it cannot: no agent named, the agent signed out, one already
      // running elsewhere. Shown where the person pressed send, whole.
      onSuccess: (answer) => {
        if (!answer.sessionId) {
          setFirstWords(null);
          giveBack(text, files, answer.message);
        } else {
          handTo(answer.sessionId);
        }
      },
      onError: (error) => {
        if (stoppedWaiting(error)) {
          // SENDLOST1: kept, saying the machine has not answered yet, and the list read again.
          setFirstWords({ words: said, files, circle, late: { before, at: Date.now(), why: sentence(error) } });
          void sessions.refetch();
          return;
        }
        setFirstWords(null);
        giveBack(text, files, sentence(error));
      },
    });

    // The conversation opened ahead takes the words (HELP5): open already, or still opening, in which case
    // they wait for it. One that could not open leaves the send to open one, and to say why it cannot. Only one opened in the
    // start's own workspace (ASKSCOPE1d); one still opening in another is left to finish there.
    const ready = readiedFor(circle);
    const fits = aheadIn.current !== null && sameWorkspace(aheadIn.current, circle);
    const pending = fits ? ahead.current : null;
    if (fits) ahead.current = null;
    if (ready !== null) {
      handTo(ready);
    } else if (pending) {
      setJoining(true);
      void pending.then((id) => {
        setJoining(false);
        if (id) handTo(id);
        else begin();
      });
    } else {
      begin();
    }
  };

  // Open the conversation as the panel shows (HELP5): the room written, the agent spawned and its session
  // opened while the person reads and types, rather than after they press send. Once per showing, and
  // again after *New conversation*; never while one runs, which is carried on, nor while words are on
  // their way to one; never twice under React's development double-mount, which keeps this ref. One that
  // is refused says nothing and is not asked again: the send asks for itself, and says why.
  //
  // In the window's workspace (ASKSCOPE1d): idle is none of its own running, and a window that switches opens its new
  // workspace's once per round. A window that shows several opens none, since which workspace is the person's to say.
  const [round, setRound] = useState(0);
  const openedAhead = useRef(new Set<string>());
  const idle = helper !== null && sessions.isSuccess && typeof here === 'string'
    && !mine.some((session) => SESSION_ACTIVE.has(session.state))
    && !readiedLive && !start.isPending && firstWords === null;
  useEffect(() => {
    if (!idle || typeof here !== 'string') return;
    const key = `${round} ${here.toLowerCase()}`;
    if (openedAhead.current.has(key)) return;
    openedAhead.current.add(key);
    const circle = here;
    const answer = readying.mutateAsync(startIn(circle)).then(
      // One the driver hands back as running is carried on, not opened ahead: the list shows it as it is.
      (started) => (started.sessionId && !started.running ? started.sessionId : null),
      () => null,
    );
    ahead.current = answer;
    aheadIn.current = circle;
    void answer.then((id) => {
      // Kept as opened ahead in its workspace: words that took it meanwhile were handed to it by the send that took it, which
      // marked it spoken in just after, and one opened in a workspace the window has left waits there for its words.
      if (id) help.ready(id, circle);
      if (ahead.current === answer) ahead.current = null;
    });
    // Only a new round, a workspace newly shown, or the panel coming idle, opens one; the mutation is not a reason to open again.
  }, [idle, round, here]);

  // Each opening once: a re-render, or React mounting an effect twice in development, must not ask twice. A draft (CTX1's
  // *Ask Daoris about it*) goes into the box for the person's question instead, and sends nothing.
  const opened = useRef<number | null>(null);
  // Until the list has answered, which conversation is shown is not known, and a draft put in the wrong box stays there (NEW1g).
  const listed0 = !sessions.isPending;
  // Nor, until this machine's workspaces are read, which workspace a question would start in (ASKSCOPE1d).
  const placed = here !== undefined;
  useEffect(() => {
    if (!opening || opened.current === opening.id) return;
    if (opening.draft && !listed0) return;
    if (!opening.draft && !placed) return;
    opened.current = opening.id;
    if (opening.draft) {
      // A draft that will start a new conversation shows one (NEW1g): where the one shown is not live, the panel clears to a
      // blank one first, ending nothing, and the draft goes in the new conversation's box.
      const key = shown && live ? shown.id : NEW_DRAFT;
      if (key === NEW_DRAFT && shown) clear();
      // Above the words already there: CTX1's quote ends on a blank line for the person's question; a draft that ends on
      // its own words (the composer's hand-over, NEW1d-b) is kept apart from them by one.
      const apart = /\n\s*$/.test(opening.text) ? '' : '\n\n';
      editDraft(key, (was) => ({ ...was, text: [opening.text, was.text.trim()].filter(Boolean).join(apart) }));
    } else onSend(opening.text, []);
    // From the effect, never the render (frontend-architecture §4b): the holder clears what was sent.
    onOpened?.();
    // Only a new opening asks; `onSend` reads this render's session and is not a reason to ask again.
  }, [opening?.id, listed0, placed]);

  // The panel cleared to a blank new conversation, ending nothing: the one shown and the newest are set aside, the next opens
  // ahead, and the box takes the focus, since writing is what comes next.
  const clear = () => {
    if (shown) {
      // Set aside the newest too, so clearing an older conversation opens a blank panel.
      setCleared(newest?.id ?? shown.id);
      // The next opens ahead too (HELP5), once the one finished here has gone.
      setRound((was) => was + 1);
    }
    setChosen(null);
    setStartedFrom(null);
    setHistoryOpen(false);
    setRefusal(null);
    setFocusBox((was) => was + 1);
  };

  // A blank new conversation: the one running is finished and the panel cleared, from the conversation's head, the history's,
  // or the line under one that cannot go on.
  const onNew = () => {
    if (end.isPending) return;
    setChoosing(null);
    // Another workspace's running conversation is that workspace's to finish (ASKSCOPE1d): the panel only clears.
    if (shown && live && !elsewhere) {
      // The host owns the ending. Keep its conversation and draft until it accepts the request.
      end.mutate(shown.id, {
        onSuccess: (answer) => {
          if (answer.ended) clear();
          else setRefusal(t('help.notFinished'));
        },
        onError: (error) => setRefusal(sentence(error)),
      });
    } else clear();
  };

  // The history's acts (ASKHIST1), each through the bridge's one owner of it.
  const choose = (id: string) => {
    setChosen(id);
    setCleared(null);
    setHistoryOpen(false);
    setRefusal(null);
  };
  // A row opened from the history (ASKHIST1c): its heading takes the focus, and the list remembers where it was left.
  const openRow = (id: string, scroll: number) => {
    if (end.isPending) return;
    setLeft({ row: id, scroll });
    setOpenedRow(shownListing?.conversations.find((row) => row.session === id) ?? null);
    choose(id);
    setFocusHead((was) => was + 1);
  };
  // The history opened over the conversation: it opens on the row it was left from, else the conversation shown.
  const openHistory = () => {
    setHistoryRefusal(null);
    setRestore({ row: left?.row ?? shown?.id ?? null, scroll: left?.scroll ?? 0 });
    setHistoryOpen(true);
  };
  // Back to the conversation the history was opened over, its heading taking the focus.
  const closeHistory = () => {
    setHistoryOpen(false);
    setFocusHead((was) => was + 1);
  };
  // In the window's workspace, read at the press (ASKSCOPE1d), whatever the earlier one's was; a window that shows several asks
  // which first, the history closed so the question is seen.
  const beginFrom = (id: string, chosenIn?: string) => {
    setHistoryRefusal(null);
    const circle = chosenIn ?? (typeof here === 'string' ? here : null);
    if (circle === null) {
      setChoosing({ from: id });
      setHistoryOpen(false);
      return;
    }
    const title = rowOf(id)?.title ?? id;
    startFrom.mutate({ id, workspace: startIn(circle) }, {
      onSuccess: (answer) => {
        if (!answer.sessionId) {
          setHistoryRefusal(answer.message);
          setRefusal(answer.message);
          return;
        }
        // Spoken in by the person's choice: never readied ahead, and shown from now on, its box taking the focus once listed.
        help.spoke(answer.sessionId);
        setStartedFrom({ session: answer.sessionId, title });
        choose(answer.sessionId);
        setFocusWhenListed(answer.sessionId);
      },
      onError: (error) => {
        setHistoryRefusal(sentence(error));
        setRefusal(sentence(error));
      },
    });
  };
  const history = (
    <AskHistory
      rows={shownListing?.conversations ?? []}
      total={shownListing?.total}
      // A first answer on its way; a newer one over the last rows; a list that could not be read, with why (UX §4).
      loading={!shownListing && (listing.isPending || listing.isFetching) && !listing.isError}
      refreshing={listing.isFetching && listing.data === undefined && shownListing !== undefined}
      error={listing.isError && !listing.isFetching && !listing.isFetchNextPageError ? sentence(listing.error) : null}
      onRetry={() => void listing.refetch()}
      // The next page at the driver's `next` (ASKHIST1d2), offered only under the rows it follows; a page that could not be
      // read is said beside its press, which asks for it again.
      onMore={listing.data && listing.hasNextPage ? () => void listing.fetchNextPage() : undefined}
      loadingMore={listing.isFetchingNextPage}
      moreError={listing.isFetchNextPageError && !listing.isFetching ? sentence(listing.error) : null}
      search={search}
      scope={historyScope}
      shown={shown?.id ?? null}
      busy={startFrom.isPending || pin.isPending || end.isPending}
      refusal={historyRefusal ?? (historyOpen ? refusal : null)}
      restore={restore}
      onSearch={setSearch}
      onNew={onNew}
      onClose={closeHistory}
      onOpen={openRow}
      onRename={(id, name, answered) => rename.mutate({ id, name }, {
        onSuccess: () => answered.done(),
        onError: (error) => answered.refused(sentence(error)),
      })}
      onPin={(id, pinned) => {
        setHistoryRefusal(null);
        pin.mutate({ id, pinned }, { onError: (error) => setHistoryRefusal(sentence(error)) });
      }}
      onStartFrom={beginFrom}
      onDelete={(id, answered) => remove.mutate(id, {
        onSuccess: () => {
          answered.done();
          if (chosen === id) setChosen(null);
          if (left?.row === id) setLeft(null);
          // Its draft goes with it.
          setDrafts(({ [id]: _gone, ...kept }) => kept);
        },
        onError: (error) => answered.refused(sentence(error)),
      })}
    />
  );

  // Ask Daoris's access (HELPMODE1b, D183 points 2-4; the modes design §1, §5, §6). The chip shows the mode the conversation in view
  // runs under, its record's, or the mode chosen before any start; a shell that answers no mode offers no choice, and no *What it
  // has*, which came after it.
  const driver = useDriver();
  const offered = driver.data !== undefined && 'helpMode' in driver.data;
  const selectedMode = helpModeOf(driver.data?.helpMode);
  const runningMode = shown && known?.mode !== undefined ? helpModeOf(known.mode) : null;
  const setMode = useSetHelpMode();
  // Its own mutation, so ending the one opened ahead never reads as the person's New conversation pending.
  const retire = useEndChat();
  const [chipOpen, setChipOpen] = useState(0);
  const choice = useModeChoice(selectedMode, (mode) => setMode.mutate({ mode }, {
    onSuccess: () => {
      // The conversation opened ahead (HELP5) was started under the mode before: nobody has spoken in it, so it is ended and the
      // next opens under the mode chosen, rather than the person's first words reaching one the chip no longer names.
      if (readied === null || !readiedLive) return;
      retire.mutate(readied, { onSuccess: () => setRound((was) => was + 1) });
    },
    onError: (error) => setRefusal(sentence(error)),
  }));
  const [wiredOpen, setWiredOpen] = useState(false);
  useEffect(() => {
    setWiredOpen(false);
  }, [shown?.id]);
  const wired = useHelpWired(shown?.id ?? null, offered && (wiredOpen || choice.confirming));
  // What full access would add is read only while it is asked: the plugins' servers waiting for an approval, and what the
  // newest start of the conversation in view lacked.
  const plugins = usePlugins({ enabled: choice.confirming });
  const addsKnown = (plugins.isSuccess || plugins.isError) && (shown === null || wired.isSuccess || wired.isError);
  const adds = addsKnown ? fullAdds(wired.data?.receipts[0], plugins.data?.plugins) : null;
  const access = offered ? (
    <div className="grid min-w-0 gap-1.5">
      <div className="flex min-w-0 items-center gap-2">
        <HelpModeChip selected={selectedMode} running={runningMode} busy={setMode.isPending} open={chipOpen} onPick={choice.pick} />
        {shown && (
          <Button
            variant="ghost"
            aria-pressed={wiredOpen}
            className="ml-auto h-7 shrink-0 px-1.5 text-small"
            onClick={() => setWiredOpen((was) => !was)}
          >
            {t('help.wired.open')}
          </Button>
        )}
      </div>
      {choice.confirming && (
        <FullAccessConfirm adds={adds} busy={setMode.isPending} onEnable={choice.enable} onCancel={choice.cancel} />
      )}
    </div>
  ) : undefined;
  const whatItHas = offered && wiredOpen && shown ? (
    <WhatItHas
      wired={wired.data}
      loading={wired.isPending}
      error={wired.isError ? sentence(wired.error) : null}
      onRetry={() => void wired.refetch()}
      onDoor={(door) => onGo?.(door)}
      onMode={() => setChipOpen((was) => was + 1)}
      onClose={() => setWiredOpen(false)}
    />
  ) : undefined;

  const conversation = shown ? (
    <SessionConversation
      session={shown.id}
      adapter={shown.adapter}
      chat
      tree={shown.tree}
      live={live}
      turnRunning={live ? turns.taking : undefined}
      scroller={scroller}
      onTail={setTail}
    />
  ) : null;

  // The words on their way shown with the conversation they were said to, or with none while the list has not listed it yet.
  const going = sent.filter((each) => each.session === shown?.id || !helpSessions.some((session) => session.id === each.session));
  const unanswered = Boolean(firstWords?.late) || going.some((each) => each.late);

  // No box under one the person chose that cannot go on, or is still being checked (ASKHIST1c): words typed there started a
  // fresh conversation without its own, the press offering one from its words below the transcript, perhaps out of view. None
  // under another workspace's either (ASKSCOPE1d): its words are written once the window has switched to it.
  const composer = readOnly || elsewhere ? null : (
    <Composer
      key={draftKey}
      // A message always reaches something: the one running, or the one it starts.
      live
      placeholder={t(goesOn ? 'help.placeholderGoOn' : 'help.placeholder')}
      sending={send.isPending || start.isPending || joining || end.isPending}
      refusal={refusal}
      // Only a conversation that runs has an ending to choose.
      endings={live && !end.isPending}
      draft={draft.text}
      onDraft={(text) => editDraft(draftKey, (was) => ({ ...was, text }))}
      carried={draft.carry}
      onCarry={(carry) => editDraft(draftKey, (was) => ({ ...was, carry }))}
      focus={focusBox}
      queued={[...(waiting ? [waiting] : []), ...going.map((each) => each.words), ...turns.queued]}
      // Words the machine has not answered for are said so (SENDLOST1); words waiting on the conversation opening, rather
      // than on a turn, are said as Ask Daoris opening; words on their way to the one running, as sending.
      queuedLabel={unanswered ? t('help.unanswered')
        : waiting ? t('help.opening')
          : going.length > 0 ? t('help.sending')
            : turns.opening || !turns.taking ? t('help.opening') : undefined}
      taking={turns.taking}
      stoppable={structured === true}
      stopping={cancelTurn.isPending}
      onStopTurn={() => { if (shown) cancelTurn.mutate(shown.id); }}
      onRemove={editQueued.remove}
      onEdit={editQueued.reword}
      onSend={onSend}
      onFinish={() => { if (shown) end.mutate(shown.id); }}
      onStop={() => { if (shown) stop.mutate(shown.id); }}
    />
  );

  // What the conversation proposes (HELP1c): each card is judged by the route before it arrives, and
  // Apply or Not now goes back into the conversation as the person's next message.
  const proposals = useHelpProposals(shown?.id ?? null, live);
  const settle = useSettleHelp();
  // The Agents screen's running action, where the application holds one (HELP6).
  const runs = useHeldHarnessRun();
  const cards = proposals.data ?? [];
  // What an Apply did beyond the driver's sentence (HELP6): a go opens its place through the starters'
  // door, and an update or a pin is followed by the Agents screen, its console and its end said there.
  const applied = (answer: HelpSettled) => {
    // The plan moved since the card was drawn (HELPAPPLY1): nothing was applied, and the card is drawn again with the new plan.
    if (answer.changed) setRefusal(answer.message);
    const door = answer.go ? placeDoor(answer.go) : null;
    if (door) onGo?.(door);
    if (answer.harnessAction) runs?.follow(answer.harnessAction.harness, answer.harnessAction.action);
  };
  const proposed = cards.length > 0 ? (
    <ul aria-label={t('help.proposal.list')} className="m-0 mt-3 grid list-none gap-2.5 p-0">
      {cards.map((proposal) => (
        <ProposalCard
          key={proposal.id}
          proposal={proposal}
          pending={settle.isPending}
          onApply={(id) => settle.mutate({ id, apply: true }, { onSuccess: applied, onError: (error) => setRefusal(sentence(error)) })}
          onDismiss={(id) => settle.mutate({ id, apply: false }, { onError: (error) => setRefusal(sentence(error)) })}
        />
      ))}
    </ul>
  ) : null;

  // What the next words do, said right above the box (ASKHIST1, as ASKHIST1c placed it): where one started from; whether one
  // that ended goes on in itself; that one the person chose is being checked, or cannot go on, with a new conversation from
  // its words first and a blank one beside it. A title it names breaks inside a word too wide for the dock (ASKHIST1b).
  const line = (key: string, values?: Record<string, string>) => (
    <Prose className="m-0 text-small text-ink-soft wrap-anywhere">{t(key, values)}</Prose>
  );
  const press = (key: string, onClick: () => void) => (
    <Button variant="ghost" className="px-0 text-small text-accent" disabled={startFrom.isPending} onClick={onClick}>{t(key)}</Button>
  );
  const ended = shown !== null && !live;
  const own: ReactNode = !shown ? undefined
    : startedFrom?.session === shown.id ? line('help.startedFrom', { title: startedFrom.title })
      : !ended ? undefined
        : picked !== null ? (
          checking ? <p role="status" className="m-0 text-small text-ink-soft">{t('help.checking')}</p>
            : goesOn ? line('help.endedGoesOn')
              : (
                <>
                  {line('help.endedAnew')}
                  <div className="flex flex-wrap gap-2">
                    <Button variant="primary" disabled={startFrom.isPending} onClick={() => beginFrom(shown.id)}>
                      {t('help.history.startFrom')}
                    </Button>
                    <Button disabled={startFrom.isPending} onClick={onNew}>{t('help.new')}</Button>
                  </div>
                </>
              )
        )
          // The newest that ended, shown by default: a message starts a new one (HELP5), and it may be gone on in instead.
          : shownRow ? (
            <>
              {line('help.ended')}
              {shownRow.resumable ? press('help.goOn', () => { choose(shown.id); setFocusBox((was) => was + 1); })
                : press('help.history.startFrom', () => beginFrom(shown.id))}
            </>
          ) : undefined;

  // ASKSCOPE1d (D158's ASKSCOPE1 note, points 1 and 6), ahead of what the next words do: which workspace a start on a window
  // that shows several is in, asked with a press per workspace; another workspace's conversation named, with the switch that
  // goes on in it, where this window can show that workspace; and on a window that shows every workspace, whose each is.
  const switchable = shownIn !== null && registered?.find((name) => sameWorkspace(name, shownIn));
  const goesOnThere = elsewhere && (live || goesOn || (picked !== null && checking));
  const note: ReactNode = choosing ? (
    <>
      {line('help.scope.which')}
      <div className="flex flex-wrap gap-2">
        {registered === undefined
          ? <p role="status" className="m-0 text-small text-ink-soft">{t('help.scope.reading')}</p>
          : registered.map((name) => (
            <Button key={name} disabled={start.isPending || startFrom.isPending} onClick={() => startChosen(name)}>
              {t('help.scope.startIn', { workspace: name })}
            </Button>
          ))}
        <Button variant="ghost" onClick={() => setChoosing(null)}>{t('common.cancel')}</Button>
      </div>
    </>
  ) : elsewhere ? (
    <>
      {line('help.scope.elsewhere', { workspace: shownIn!, here: here as string })}
      {!goesOnThere ? own : switchable ? (
        <Button
          variant="primary"
          onClick={() => {
            setWorkspace(switchable);
            setFocusBox((was) => was + 1);
          }}
        >
          {t('help.scope.switch', { workspace: switchable })}
        </Button>
      ) : (
        <>
          {line('help.scope.notOffered', { workspace: shownIn!, here: here as string })}
          {press('help.history.startFrom', () => beginFrom(shown!.id))}
        </>
      )}
    </>
  ) : shown && here === null ? (
    <>
      {line('help.scope.in', { workspace: shownIn! })}
      {own}
    </>
  ) : own;

  // What the open conversation's head calls it: the history's title, else the person's first words, else a conversation.
  const title = !shown ? null
    : known?.title || (spoken?.session === shown.id ? spoken.title : t('help.head.untitled'));

  const slot: AskConversationSlot = {
    body: conversation, proposals: proposed, composer, ended,
    note: end.isPending ? <p role="status" className="m-0 text-small text-ink-soft">{t('help.finishing')}</p> : note,
    onNew: shown ? onNew : undefined,
    newPending: end.isPending,
    tail: tail && !tail.atTail ? <BackToBottom onPress={tail.toTail} /> : undefined,
    title,
    focusHead,
    // Escape goes back to the list where the conversation shown was opened from it.
    escapeToList: shown !== null && left?.row === shown.id,
    history, historyOpen,
    onHistory: end.isPending ? undefined : openHistory,
    access, wired: whatItHas, full: offered && selectedMode === 'full',
  };
  return slot;
}
