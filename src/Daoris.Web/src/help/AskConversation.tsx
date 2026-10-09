import { type RefObject, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useQueryClient } from '@tanstack/react-query';
import { HELP_REPOSITORY } from '../api';
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
import { SessionConversation } from '../work/SessionConversation';
import { AskHistory } from './AskHistory';
import type { AskConversationSlot } from './AskPanel';
import { placeDoor } from './places';
import { ProposalCard } from './ProposalCard';
import type { StarterDoor } from './starters';
import { type HelpWhere, prefaceOf } from './where';
import { logEvent } from '../shell';

/** How often the list is asked again while words to an ended conversation wait for it to go on (ASKHIST1). */
const GOING_ON_POLL = 1500;

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
  const rows = useHelpConversations('', helper !== null);
  const found = useHelpConversations(search, helper !== null && historyOpen && search.trim().length >= 2);
  const shownRow = shown ? rows.data?.conversations.find((row) => row.session === shown.id) ?? null : null;
  // An ended one the person chose goes on in itself with their words, where its own conversation was kept.
  const goesOn = shown !== null && !live && picked !== null && shownRow?.resumable === true;
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

  const [draft, setDraft] = useState('');
  const [refusal, setRefusal] = useState<string | null>(null);
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
  // 🔴 A message that did not arrive goes back into the box, never lost (the composer's own rule).
  const giveBack = (text: string, why: string) => {
    setRefusal(why);
    if (text) setDraft((was) => [text, was.trim()].filter(Boolean).join('\n\n'));
  };

  // The last preface each conversation was handed, so an unchanged screen is not said again.
  const told = useRef(new Map<string, string>());
  const deliver = (id: string, text: string, files: File[], lost?: () => void) => {
    const now = where ? prefaceOf(where) : undefined;
    const preface = now && told.current.get(id) !== now ? now : undefined;
    send.mutate({ id, text, files, ...(preface ? { preface } : {}) }, {
      onSuccess: (answer) => {
        if (!answer.sent) {
          lost?.();
          giveBack(text, t('help.notSent'));
          return;
        }
        if (preface) told.current.set(id, preface);
        // Kept on an ended record (ASKHIST1): the same conversation goes on with them once the driver takes them up, which
        // the list is asked again for until it says so.
        if (answer.reaches === 'resume') {
          setGoingOn(id);
          void client.invalidateQueries({ queryKey: HELP_HISTORY });
        }
      },
      onError: (error) => {
        lost?.();
        giveBack(text, sentence(error));
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
    // The one running, or one that ended the person chose and that goes on in itself (ASKHIST1).
    if (shown && (live || goesOn)) {
      if (startedFrom?.session === shown.id) setStartedFrom(null);
      deliver(shown.id, text, files);
      return;
    }

    const words = { text, files: files.map((file) => file.name) };
    setFirstWords({ words });
    const handTo = (id: string) => {
      // Seen from now on, in both hosts, however late an opening's answer arrives.
      help.spoke(id);
      setCleared(null);
      // A new conversation is the one shown now, whichever the person had chosen (ASKHIST1).
      setChosen(null);
      setFirstWords({ words, session: id });
      deliver(id, text, files, () => setFirstWords(null));
    };
    const begin = () => start.mutate(undefined, {
      // The driver's own sentence when it cannot: no agent named, the agent signed out, one already
      // running elsewhere. Shown where the person pressed send, whole.
      onSuccess: (answer) => {
        if (!answer.sessionId) {
          setFirstWords(null);
          giveBack(text, answer.message);
        } else {
          handTo(answer.sessionId);
        }
      },
      onError: (error) => {
        setFirstWords(null);
        giveBack(text, sentence(error));
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
    if (opening.draft) setDraft((was) => [opening.text, was.trim()].filter(Boolean).join(''));
    else onSend(opening.text, []);
    // From the effect, never the render (frontend-architecture §4b): the holder clears what was sent.
    onOpened?.();
    // Only a new opening asks; `onSend` reads this render's session and is not a reason to ask again.
  }, [opening?.id]);

  const onNew = () => {
    if (!shown) return;
    if (live) end.mutate(shown.id);
    setCleared(shown.id);
    setChosen(null);
    setStartedFrom(null);
    setHistoryOpen(false);
    setRefusal(null);
    // The next opens ahead too (HELP5), once the one finished here has gone.
    setRound((was) => was + 1);
  };

  // The history's acts (ASKHIST1), each through the bridge's one owner of it.
  const choose = (id: string) => {
    setChosen(id);
    setCleared(null);
    setHistoryOpen(false);
    setRefusal(null);
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
        // Spoken in by the person's choice: never readied ahead, and shown from now on.
        help.spoke(answer.sessionId);
        setStartedFrom({ session: answer.sessionId, title });
        choose(answer.sessionId);
      },
      onError: (error) => {
        setHistoryRefusal(sentence(error));
        setRefusal(sentence(error));
      },
    });
  };
  const history = (
    <AskHistory
      rows={(search.trim().length >= 2 ? found.data?.conversations : rows.data?.conversations) ?? []}
      cut={(search.trim().length >= 2 ? found.data?.cut : rows.data?.cut) ?? false}
      loading={rows.isLoading || found.isFetching}
      search={search}
      shown={shown?.id ?? null}
      busy={startFrom.isPending || pin.isPending}
      refusal={historyRefusal}
      onSearch={setSearch}
      onOpen={choose}
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
    />
  ) : null;

  const composer = (
    <Composer
      key={shown?.id ?? 'none'}
      // A message always reaches something: the one running, or the one it starts.
      live
      placeholder={t(goesOn ? 'help.placeholderGoOn' : 'help.placeholder')}
      sending={send.isPending || start.isPending || joining}
      refusal={refusal}
      // Only a conversation that runs has an ending to choose.
      endings={live}
      draft={draft}
      onDraft={setDraft}
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

  // What the foot of the conversation says (ASKHIST1): where one started from, and whether one that ended goes on in itself,
  // each with its press. Nothing where the history has not answered for it yet: the panel's own line stands.
  const line = (key: string, values?: Record<string, string>) => (
    <Prose className="m-0 text-small text-ink-faint">{t(key, values)}</Prose>
  );
  const press = (key: string, onClick: () => void) => (
    <Button variant="ghost" className="px-0 text-small text-accent" disabled={startFrom.isPending} onClick={onClick}>{t(key)}</Button>
  );
  const ended = shown !== null && !live;
  const note = shown && startedFrom?.session === shown.id
    ? <div className="mt-3">{line('help.startedFrom', { title: startedFrom.title })}</div>
    : !ended || !shownRow
      ? undefined
      : goesOn
        ? <div className="mt-3">{line('help.endedGoesOn')}</div>
        : shownRow.resumable
          ? <div className="mt-3 grid justify-items-start gap-1">{line('help.ended')}{press('help.goOn', () => choose(shown!.id))}</div>
          : (
            <div className="mt-3 grid justify-items-start gap-1">
              {line(picked ? 'help.endedAnew' : 'help.ended')}
              {press('help.history.startFrom', () => beginFrom(shown!.id))}
            </div>
          );

  const slot: AskConversationSlot = {
    body: conversation, proposals: proposed, composer, ended, note, onNew: shown ? onNew : undefined,
    history, historyOpen,
    onHistory: () => {
      setHistoryRefusal(null);
      setHistoryOpen((open) => !open);
    },
  };
  return slot;
}
