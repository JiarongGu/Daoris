import { type ReactNode, type RefObject, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useQueryClient } from '@tanstack/react-query';
import { HELP_REPOSITORY } from '../api';
import { type Carry, NO_CARRY } from '../compose/carry';
import { sentence } from '../format';
import { useHelpSessions } from '../queries';
import {
  HELP_HISTORY, type HelpSettled, NO_TURNS, useCancelTurn, useChatTurns, useDeleteSession, useEndChat, useHarnesses,
  useHelpConversations, useHelpProposals, useHelpReadied, useHelpStartFrom, usePinHelp, useRenameHelp, useSendMessage,
  useSettleHelp, useStartHelp, useStopSession,
} from '../shell';
import { useHeldHarnessRun } from '../harnessRuns';
import { Button, Prose, SESSION_ACTIVE } from '../ui';
import type { ChatMessage } from '../work/conversation';
import { Composer } from '../work/Composer';
import { BackToBottom, SessionConversation, type TailState } from '../work/SessionConversation';
import { AskHistory, SEARCH_FROM } from './AskHistory';
import type { AskConversationSlot } from './AskPanel';
import { placeDoor } from './places';
import { ProposalCard } from './ProposalCard';
import type { StarterDoor } from './starters';
import { type HelpWhere, prefaceOf } from './where';
import { logEvent } from '../shell';

/** How often the list is asked again while words to an ended conversation wait for it to go on (ASKHIST1). */
const GOING_ON_POLL = 1500;

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
 * **One conversation per machine, carried on across opens** (design §2): the panel shows the newest help
 * session, running or ended. **With none running, one opens as the panel shows** (HELP5) — the driver
 * writes the room from the machine as it stands, spawns the agent and opens its session — so that is done
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
 * The newest that ended still starts a new one by default, as it did; *Go on in it* chooses it.
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
  const client = useQueryClient();
  // The conversation the person chose from the history, or went on in (ASKHIST1): shown in the newest's place.
  const [chosen, setChosen] = useState<string | null>(null);
  // Words said to an ended conversation, waiting for its record to go on: the list is asked again until it has.
  const [goingOn, setGoingOn] = useState<string | null>(null);
  const sessions = useHelpSessions(goingOn ? GOING_ON_POLL : false);
  // The session the person started again from: shown no longer, whatever the list still says.
  const [cleared, setCleared] = useState<string | null>(null);
  // The conversation opened as the panel showed (HELP5), which nobody has spoken in: not shown, so the
  // starters, or the conversation that ended, stay in front of the person until the first words go to it.
  const help = useHelpReadied();
  const { readied } = help;
  const helpSessions = (sessions.data ?? []).filter((session) => session.repository === HELP_REPOSITORY);
  const newest = helpSessions
    .filter((session) => session.id !== readied)
    .sort((a, b) => b.created.localeCompare(a.created))[0] ?? null;
  const picked = chosen ? helpSessions.find((session) => session.id === chosen) ?? null : null;
  const shown = picked ?? (newest && newest.id !== cleared ? newest : null);
  const live = shown ? SESSION_ACTIVE.has(shown.state) : false;
  useEffect(() => {
    if (goingOn && shown?.id === goingOn && live) setGoingOn(null);
  }, [goingOn, shown?.id, live]);

  // The history (ASKHIST1): open or not, its search, and each conversation's row, which says whether one that ended goes on
  // in itself. The panel's own rows are asked whenever an agent is named, so the conversation shown knows its own.
  const [historyOpen, setHistoryOpen] = useState(false);
  const [search, setSearch] = useState('');
  const [historyRefusal, setHistoryRefusal] = useState<string | null>(null);
  const words = search.trim();
  const searching = words.length >= SEARCH_FROM;
  const rows = useHelpConversations('', helper !== null);
  const found = useHelpConversations(words, helper !== null && historyOpen && searching);
  const listing = searching ? found : rows;
  // The last list answered, held while a newer one comes, so a search typed never blanks the rows (UX §4; ASKHIST1c).
  const [lastListing, setLastListing] = useState<typeof rows.data>(undefined);
  useEffect(() => {
    if (listing.data) setLastListing(listing.data);
  }, [listing.data]);
  const shownListing = listing.data ?? lastListing;
  const shownRow = shown ? rows.data?.conversations.find((row) => row.session === shown.id) ?? null : null;
  // Whether an ended one shown can go on in itself is the history's answer, so until it answers it is being checked
  // (ASKHIST1c): read as "cannot", words typed under one that could would have started a fresh conversation without its own.
  const checking = shownRow === null && (rows.isPending || rows.isFetching);
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
  // to answer, and its record says what became of them.
  const [firstWords, setFirstWords] = useState<{ words: ChatMessage; session?: string } | null>(null);
  const handedOver = firstWords?.session !== undefined && shown?.id === firstWords.session
    && (!live || held[firstWords.session] !== undefined);
  useEffect(() => {
    if (handedOver) setFirstWords(null);
  }, [handedOver]);
  const waiting = firstWords && !handedOver ? firstWords.words : null;
  // 🔴 A message that did not arrive goes back into the box it was written in, its files with it, never lost (the composer's
  // own rule).
  const giveBack = (key: string, text: string, files: File[], why: string) => {
    setRefusal(why);
    editDraft(key, (was) => ({
      text: [text, was.text.trim()].filter(Boolean).join('\n\n'),
      carry: { ...was.carry, files: [...files, ...was.carry.files] },
    }));
  };

  // The last preface each conversation was handed, so an unchanged screen is not said again.
  const told = useRef(new Map<string, string>());
  const deliver = (id: string, text: string, files: File[], key: string, lost?: () => void) => {
    const now = where ? prefaceOf(where) : undefined;
    const preface = now && told.current.get(id) !== now ? now : undefined;
    send.mutate({ id, text, files, ...(preface ? { preface } : {}) }, {
      onSuccess: (answer) => {
        if (!answer.sent) {
          lost?.();
          giveBack(key, text, files, t('help.notSent'));
          return;
        }
        if (preface) told.current.set(id, preface);
        // A conversation the history has not listed yet is listed once it is spoken in, and named by its first question.
        if (!rows.data?.conversations.some((row) => row.session === id)) void client.invalidateQueries({ queryKey: HELP_HISTORY });
        // Kept on an ended record (ASKHIST1): the same conversation goes on with them once the driver takes them up, which
        // the list is asked again for until it says so.
        if (answer.reaches === 'resume') {
          setGoingOn(id);
          void client.invalidateQueries({ queryKey: HELP_HISTORY });
        }
      },
      onError: (error) => {
        lost?.();
        giveBack(key, text, files, sentence(error));
      },
    });
  };

  // The opening ahead still on its way (HELP5): what it answers, the conversation it opened or null.
  const ahead = useRef<Promise<string | null> | null>(null);
  // First words waiting on that answer: the box holds its fire, as it does while a send opens one.
  const [joining, setJoining] = useState(false);

  const onSend = (text: string, files: File[]) => {
    logEvent('message.sent', { kind: 'help', length: text.length, files: files.length, ...(shown && (live || goesOn) ? { session: shown.id } : {}) });
    setRefusal(null);
    // The box the words came from, which takes them back if they do not arrive.
    const key = draftKey;
    // The one running, or one that ended the person chose and that goes on in itself (ASKHIST1).
    if (shown && (live || goesOn)) {
      if (startedFrom?.session === shown.id) setStartedFrom(null);
      deliver(shown.id, text, files, key);
      return;
    }

    const said = { text, files: files.map((file) => file.name) };
    setFirstWords({ words: said });
    const handTo = (id: string) => {
      // Seen from now on, in both hosts, however late an opening's answer arrives.
      help.spoke(id);
      setCleared(null);
      // A new conversation is the one shown now, whichever the person had chosen (ASKHIST1).
      setChosen(null);
      setFirstWords({ words: said, session: id });
      // Called by its first question, as the driver will call it, until the history says so (ASKHIST1c).
      const first = text.split('\n', 1)[0]!.trim();
      if (first) setSpoken({ session: id, title: first });
      deliver(id, text, files, key, () => setFirstWords(null));
    };
    const begin = () => start.mutate(undefined, {
      // The driver's own sentence when it cannot: no agent named, the agent signed out, one already
      // running elsewhere. Shown where the person pressed send, whole.
      onSuccess: (answer) => {
        if (!answer.sessionId) {
          setFirstWords(null);
          giveBack(key, text, files, answer.message);
        } else {
          handTo(answer.sessionId);
        }
      },
      onError: (error) => {
        setFirstWords(null);
        giveBack(key, text, files, sentence(error));
      },
    });

    // The conversation opened ahead takes the words (HELP5): open already, or still opening, in which case
    // they wait for it. One that could not open leaves the send to open one, and to say why it cannot.
    const pending = ahead.current;
    ahead.current = null;
    if (readied !== null && readiedLive) {
      handTo(readied);
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
  const [round, setRound] = useState(0);
  const openedAhead = useRef<number | null>(null);
  const idle = helper !== null && sessions.isSuccess
    && !helpSessions.some((session) => SESSION_ACTIVE.has(session.state))
    && !readiedLive && !start.isPending && firstWords === null;
  useEffect(() => {
    if (!idle || openedAhead.current === round) return;
    openedAhead.current = round;
    const answer = readying.mutateAsync(undefined).then(
      // One the driver hands back as running is carried on, not opened ahead: the list shows it as it is.
      (started) => (started.sessionId && !started.running ? started.sessionId : null),
      () => null,
    );
    ahead.current = answer;
    void answer.then((id) => {
      // Words took it meanwhile, and they are handed to it by the send that took it.
      if (ahead.current !== answer) return;
      ahead.current = null;
      if (id) help.ready(id);
    });
    // Only a new round, or the panel coming idle, opens one; the mutation is not a reason to open again.
  }, [idle, round]);

  // Each opening once: a re-render, or React mounting an effect twice in development, must not ask twice. A draft (CTX1's
  // *Ask Daoris about it*) goes into the box for the person's question instead, and sends nothing.
  const opened = useRef<number | null>(null);
  useEffect(() => {
    if (!opening || opened.current === opening.id) return;
    opened.current = opening.id;
    if (opening.draft) editDraft(draftKey, (was) => ({ ...was, text: [opening.text, was.text.trim()].filter(Boolean).join('') }));
    else onSend(opening.text, []);
    // From the effect, never the render (frontend-architecture §4b): the holder clears what was sent.
    onOpened?.();
    // Only a new opening asks; `onSend` reads this render's session and is not a reason to ask again.
  }, [opening?.id]);

  // A blank new conversation: the one running is finished and the panel cleared, from the conversation's head, the history's,
  // or the line under one that cannot go on. The box takes the focus, since writing is what comes next.
  const onNew = () => {
    if (end.isPending) return;
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
    if (shown && live) {
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
  const beginFrom = (id: string) => {
    setHistoryRefusal(null);
    const title = rows.data?.conversations.find((row) => row.session === id)?.title ?? id;
    startFrom.mutate(id, {
      onSuccess: (answer) => {
        if (!answer.sessionId) {
          setHistoryRefusal(answer.message);
          setRefusal(answer.message);
          return;
        }
        // Spoken in by the person's choice: never readied ahead, and shown from now on, its box taking the focus.
        help.spoke(answer.sessionId);
        setStartedFrom({ session: answer.sessionId, title });
        choose(answer.sessionId);
        setFocusBox((was) => was + 1);
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
      cut={shownListing?.cut ?? false}
      // A first answer on its way; a newer one over the last rows; a list that could not be read, with why (UX §4).
      loading={!shownListing && (listing.isPending || listing.isFetching) && !listing.isError}
      refreshing={listing.isFetching && listing.data === undefined && shownListing !== undefined}
      error={listing.isError && !listing.isFetching ? sentence(listing.error) : null}
      onRetry={() => void listing.refetch()}
      search={search}
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
          void client.invalidateQueries({ queryKey: HELP_HISTORY });
        },
        onError: (error) => answered.refused(sentence(error)),
      })}
    />
  );

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

  // No box under one the person chose that cannot go on, or is still being checked (ASKHIST1c): words typed there started a
  // fresh conversation without its own, the press offering one from its words below the transcript, perhaps out of view.
  const composer = readOnly ? null : (
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
      queued={waiting ? [waiting, ...turns.queued] : turns.queued}
      // Words waiting on the conversation opening, rather than on a turn, are said as Ask Daoris opening.
      queuedLabel={waiting || turns.opening || !turns.taking ? t('help.opening') : undefined}
      taking={turns.taking}
      stoppable={structured === true}
      stopping={cancelTurn.isPending}
      onStopTurn={() => { if (shown) cancelTurn.mutate(shown.id); }}
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
  const note: ReactNode = !shown ? undefined
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

  // What the open conversation's head calls it: the history's title, else the person's first words, else a conversation.
  const title = !shown ? null
    : shownRow?.title || (spoken?.session === shown.id ? spoken.title : t('help.head.untitled'));

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
  };
  return slot;
}
