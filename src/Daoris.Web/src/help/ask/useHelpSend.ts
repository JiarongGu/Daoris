import type * as React from 'react';
import { useEffect, useRef, useState } from 'react';
import type { useHelpReadied } from '../../bridge/help';
import { sentence } from '../../locales/format';
import { workspaceOf } from '../../data/workspaces';
import { useSessionEvents, stoppedWaiting, useGoOnBrief, logEvent } from '../../bridge/shell';
import { goOnBriefRefusal } from '../../work/say';
import { SESSION_ACTIVE } from '../../design/ui';
import { type ChatMessage, type Level } from '../../work/conversation/conversation';
import { type HelpConversationRow, sameWorkspace, type HelpListing } from '../history';
import { type HelpWhere, prefaceOf } from '../where';
import { draftToTell, draftWhere, noteTold, openDraftNow, type DraftSent } from '../openDraft';
import { SAID_SKEW, type FirstWords, type Sent, type Draft, NO_DRAFT, NEW_DRAFT, type AskOpening } from './askTypes';
import { type UseMutationResult, type UseQueryResult } from '@tanstack/react-query';
import { type WordsAnswer, type SessionTurns } from '../../bridge/sessions/conversation';
import { type TFunction } from 'i18next';
import { type Session } from '../../data/api';

export function useHelpSend({ where, sends, setSent, send, giveBack, t, setGoingOn, sessions, shown, sent, turns, live, helpSessions, help, setCleared, hereNow, setChosen, setFirstWords, setSpoken, firstWords, goesOn, setRefusal, elsewhere, startedFrom, setStartedFrom, here, putBack, drafts, setDrafts, start, startIn, helper, mine, readiedLive, readying, opening, editDraft, onOpened, newest, setHistoryOpen, setFocusBox, end, setLeft, setOpenedRow, shownListing, setFocusHead, setHistoryRefusal, setRestore, left, rowOf, startFrom, setFocusWhenListed }: {
  where: HelpWhere | undefined;
  sends: React.RefObject<number>;
  setSent: React.Dispatch<React.SetStateAction<Sent[]>>;
  send: UseMutationResult<WordsAnswer, Error, { id: string; text: string; files?: File[] | undefined; preface?: string | undefined; draft?: DraftSent | undefined; level?: Level | undefined; }, unknown>;
  giveBack: (text: string, files: File[], why: string) => void;
  t: TFunction<"translation", undefined>;
  setGoingOn: React.Dispatch<React.SetStateAction<string | null>>;
  sessions: UseQueryResult<Session[], Error>;
  shown: Session | null;
  sent: Sent[];
  turns: SessionTurns;
  live: boolean;
  helpSessions: Session[];
  help: ReturnType<typeof useHelpReadied>;
  setCleared: React.Dispatch<React.SetStateAction<string | null>>;
  hereNow: React.RefObject<string | null | undefined>;
  setChosen: React.Dispatch<React.SetStateAction<string | null>>;
  setFirstWords: React.Dispatch<React.SetStateAction<FirstWords | null>>;
  setSpoken: React.Dispatch<React.SetStateAction<{ session: string; title: string; } | null>>;
  firstWords: FirstWords | null;
  goesOn: boolean;
  setRefusal: React.Dispatch<React.SetStateAction<string | null>>;
  elsewhere: boolean;
  startedFrom: { session: string; title: string; } | null;
  setStartedFrom: React.Dispatch<React.SetStateAction<{ session: string; title: string; } | null>>;
  here: string | null | undefined;
  putBack: (text: string, files: File[]) => void;
  drafts: Record<string, Draft>;
  setDrafts: React.Dispatch<React.SetStateAction<Record<string, Draft>>>;
  start: UseMutationResult<{ sessionId: string | null; message: string; running?: boolean | undefined; starting?: boolean | undefined; finishing?: boolean | undefined; }, Error, string | undefined, unknown>;
  startIn: (workspace: string) => string | undefined;
  helper: string | null;
  mine: Session[];
  readiedLive: boolean;
  readying: UseMutationResult<{ sessionId: string | null; message: string; running?: boolean | undefined; starting?: boolean | undefined; finishing?: boolean | undefined; }, Error, string | undefined, unknown>;
  opening: AskOpening | null | undefined;
  editDraft: (key: string, change: (was: Draft) => Draft) => void;
  onOpened: (() => void) | undefined;
  newest: Session;
  setHistoryOpen: React.Dispatch<React.SetStateAction<boolean>>;
  setFocusBox: React.Dispatch<React.SetStateAction<number>>;
  end: UseMutationResult<{ ended: boolean; }, Error, string, unknown>;
  setLeft: React.Dispatch<React.SetStateAction<{ row: string; scroll: number; } | null>>;
  setOpenedRow: React.Dispatch<React.SetStateAction<HelpConversationRow | null>>;
  shownListing: HelpListing | undefined;
  setFocusHead: React.Dispatch<React.SetStateAction<number>>;
  setHistoryRefusal: React.Dispatch<React.SetStateAction<string | null>>;
  setRestore: React.Dispatch<React.SetStateAction<{ row: string | null; scroll: number; } | null>>;
  left: { row: string; scroll: number; } | null;
  rowOf: (id: string) => HelpConversationRow | null;
  startFrom: UseMutationResult<{ sessionId: string | null; message: string; from: string; }, Error, { id: string; workspace?: string | undefined; }, unknown>;
  setFocusWhenListed: React.Dispatch<React.SetStateAction<string | null>>;
}) {
  // A start asked for on a window that shows several workspaces (ASKSCOPE1d, point 6): which workspace is asked first. The words
  // wait in the box meanwhile, and a new conversation from an earlier one's words waits by that one's id.
  const [choosing, setChoosing] = useState<{ from?: string; unknown?: boolean } | null>(null);

  // The last preface each conversation was handed, so an unchanged screen is not said again; and the fingerprint of the last workflow
  // draft it was told (FLOWCHART2d), so an unchanged draft is not sent again.
  const told = useRef(new Map<string, string>());
  const toldDraft = useRef(new Map<string, string>());
  /**
   * The person's words to one conversation. Words to the one running are shown at once, as sending; first words are shown as
   * opening it already, and `lost` lets them go. 🔴 A send the page stopped waiting on (the bridge's TIMEOUT) may still be taken
   * by the machine: its words are kept, saying so, and never put back in the box, where a second send would say them twice
   * (SENDLOST1). They settle by the conversation's own record or the list's next read.
   */
  const deliver = (id: string, text: string, files: File[], lost?: () => void) => {
    // The draft open as these words are sent: what the conversation is told of it is frozen with them.
    const open = openDraftNow();
    const now = where ? prefaceOf({ ...where, draft: draftWhere(open) }) : undefined;
    const preface = now && told.current.get(id) !== now ? now : undefined;
    const draft = draftToTell(open, toldDraft.current.get(id));
    const n = ++sends.current;
    const words: ChatMessage = { text, files: files.map((file) => file.name) };
    const at = Date.now();
    if (!lost) setSent((all) => [...all, { n, words, files, session: id, at, late: false }]);
    const done = () => setSent((all) => all.filter((each) => each.n !== n));
    send.mutate({ id, text, files, ...(preface ? { preface } : {}), ...(draft ? { draft } : {}) }, {
      onSuccess: (answer) => {
        done();
        if (!answer.sent) {
          lost?.();
          giveBack(text, files, t('help.notSent'));
          return;
        }
        if (preface) told.current.set(id, preface);
        if (draft) {
          toldDraft.current.set(id, draft.fingerprint);
          // FLOWCHART2f: kept by its fingerprint, which a proposal against it names, and the editor takes its changes against.
          noteTold(id, draft);
        }
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
  // HELPNEWCHAT1: the conversations *New conversation* finished here, which wind up before they end; and a start the driver
  // answered with one of them still running, asked again once the list says it ended. Its first words are never that one's.
  const finished = useRef(new Set<string>());
  const afterEnd = useRef<{ id: string; begin: () => void } | null>(null);
  useEffect(() => {
    const waiting = afterEnd.current;
    if (!waiting) return;
    const row = helpSessions.find((session) => session.id === waiting.id);
    if (row && SESSION_ACTIVE.has(row.state)) return;
    afterEnd.current = null;
    waiting.begin();
    // Only the list's next read says it ended; the rest is read from this render.
  }, [sessions.dataUpdatedAt]);

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
        } else if (answer.running && (answer.finishing || finished.current.has(answer.sessionId))) {
          // HELPNEWCHAT1: the one finished here, still winding up, is no new conversation. The words wait as this one opening
          // until it has ended, then open their own.
          afterEnd.current = { id: answer.sessionId, begin };
          void sessions.refetch();
        } else {
          handTo(answer.sessionId);
        }
      },
      onError: (error) => {
        if (stoppedWaiting(error)) {
          // SENDLOST1: kept, saying the machine has not answered yet. The list is the shell's push, which a read here would only
          // answer from memory at once, as the list that does not hold the conversation yet: its next push settles them.
          setFirstWords({ words: said, files, circle, late: { before, at: Date.now(), why: sentence(error) } });
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
      (started) => (started?.sessionId && !started.running ? started.sessionId : null),
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
      // HELPNEWCHAT1: first words handed to it are its own, set aside with it; left, the new one read as opening and never opened.
      if (firstWords?.session === shown.id) setFirstWords(null);
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
          if (answer.ended) {
            finished.current.add(shown.id);
            clear();
          } else setRefusal(t('help.notFinished'));
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
  // SESSCTX1c part B (the steward's choice: Ask Daoris follows the same rule in its own room): words that wait for the person on a
  // conversation in view go on in a new conversation of its room, handed Daoris's brief and the words, shown once it opens; a refusal is
  // said by its code in the composer's line.
  const goOnBrief = useGoOnBrief();
  const onGoOnBrief = (id: string) => {
    if (goOnBrief.isPending) return;
    setRefusal(null);
    goOnBrief.mutate({ session: id }, {
      onSuccess: (moved) => {
        if (moved.why) {
          setRefusal(goOnBriefRefusal(t, moved.why, {}));
          return;
        }
        if (!moved.sessionId) {
          setRefusal(moved.message);
          return;
        }
        help.spoke(moved.sessionId);
        choose(moved.sessionId);
        setFocusWhenListed(moved.sessionId);
      },
      onError: (error) => setRefusal(sentence(error)),
    });
  };
  return { onNew, closeHistory, openRow, beginFrom, setRound, onGoOnBrief, joining, onSend, choose, choosing, startChosen, setChoosing, openHistory };
}
