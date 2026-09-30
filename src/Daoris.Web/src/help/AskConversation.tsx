import { type RefObject, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { HELP_REPOSITORY } from '../api';
import { sentence } from '../format';
import { useHelpSessions } from '../queries';
import {
  NO_TURNS, useCancelTurn, useChatTurns, useEndChat, useHarnesses, useHelpProposals, useHelpReadied, useSendMessage,
  useSettleHelp, useStartHelp, useStopSession,
} from '../shell';
import { SESSION_ACTIVE } from '../ui';
import type { ChatMessage } from '../work/conversation';
import { Composer } from '../work/Composer';
import { SessionConversation } from '../work/SessionConversation';
import type { AskConversationSlot } from './AskPanel';
import { ProposalCard } from './ProposalCard';
import { type HelpWhere, prefaceOf } from './where';
import { logEvent } from '../shell';

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
 */
export function useAskConversation(
  scroller: RefObject<HTMLElement | null>,
  where?: HelpWhere,
  opening?: { text: string; id: number } | null,
  /** The agent Ask Daoris runs on, or null: with none named, nothing is opened ahead of the person. */
  helper: string | null = null,
  /**
   * Told once an opening has been sent (SETUP1b), so its holder lets it go: this organism is drawn again
   * with its tab or its box, and a question still held would be asked again by the next drawing.
   */
  onOpened?: () => void,
) {
  const { t } = useTranslation();
  const sessions = useHelpSessions();
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
  const shown = newest && newest.id !== cleared ? newest : null;
  const live = shown ? SESSION_ACTIVE.has(shown.state) : false;
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
        } else if (preface) told.current.set(id, preface);
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
    logEvent('message.sent', { kind: 'help', length: text.length, files: files.length, ...(shown && live ? { session: shown.id } : {}) });
    setRefusal(null);
    if (shown && live) {
      deliver(shown.id, text, files);
      return;
    }

    const words = { text, files: files.map((file) => file.name) };
    setFirstWords({ words });
    const handTo = (id: string) => {
      // Seen from now on, in both hosts, however late an opening's answer arrives.
      help.spoke(id);
      setCleared(null);
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

  // Each opening once: a re-render, or React mounting an effect twice in development, must not ask twice.
  const opened = useRef<number | null>(null);
  useEffect(() => {
    if (!opening || opened.current === opening.id) return;
    opened.current = opening.id;
    onSend(opening.text, []);
    // From the effect, never the render (frontend-architecture §4b): the holder clears what was sent.
    onOpened?.();
    // Only a new opening asks; `onSend` reads this render's session and is not a reason to ask again.
  }, [opening?.id]);

  const onNew = () => {
    if (!shown) return;
    if (live) end.mutate(shown.id);
    setCleared(shown.id);
    setRefusal(null);
    // The next opens ahead too (HELP5), once the one finished here has gone.
    setRound((was) => was + 1);
  };

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
      placeholder={t('help.placeholder')}
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
  const cards = proposals.data ?? [];
  const proposed = cards.length > 0 ? (
    <ul aria-label={t('help.proposal.list')} className="m-0 mt-3 grid list-none gap-2.5 p-0">
      {cards.map((proposal) => (
        <ProposalCard
          key={proposal.id}
          proposal={proposal}
          pending={settle.isPending}
          onApply={(id) => settle.mutate({ id, apply: true }, { onError: (error) => setRefusal(sentence(error)) })}
          onDismiss={(id) => settle.mutate({ id, apply: false }, { onError: (error) => setRefusal(sentence(error)) })}
        />
      ))}
    </ul>
  ) : null;

  const slot: AskConversationSlot = {
    body: conversation, proposals: proposed, composer, ended: shown !== null && !live, onNew: shown ? onNew : undefined,
  };
  return slot;
}
