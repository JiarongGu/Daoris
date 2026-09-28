import { type RefObject, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { HELP_REPOSITORY } from '../api';
import { sentence } from '../format';
import { useHelpSessions } from '../queries';
import {
  NO_TURNS, useCancelTurn, useChatTurns, useEndChat, useHarnesses, useHelpProposals, useSendMessage, useSettleHelp,
  useStartHelp, useStopSession,
} from '../shell';
import { SESSION_ACTIVE } from '../ui';
import { Composer } from '../work/Composer';
import { SessionConversation } from '../work/SessionConversation';
import type { AskConversationSlot } from './AskPanel';
import { ProposalCard } from './ProposalCard';
import { type HelpWhere, prefaceOf } from './where';

/**
 * Ask Daoris's conversation (HELP1a, D89): the organism that holds the newest help session's record
 * and its composer, so the panel holds neither.
 *
 * @remarks
 * **One conversation per machine, carried on across opens** (design §2): the panel shows the newest help
 * session, running or ended. **A message with none running starts one** — the driver writes the room
 * from the machine as it stands and hands back the session, and the message goes to it — so there is
 * no start button to press before asking. *New conversation* finishes the one running and clears the
 * panel; the next message opens the next.
 *
 * **Where the person is goes ahead of their words** (HELP1b): the view, the workspace in scope, the
 * session they attend and what a parked one asks — said when it changed since the last message to that
 * conversation, so the agent is not handed the same line every turn.
 *
 * Its words reach nothing but the harness, as every conversation's do (D24): the driver makes no model
 * call. Desktop-only for the console's reason (D47 §4): the record arrives over the bridge.
 */
export function useAskConversation(scroller: RefObject<HTMLElement | null>, where?: HelpWhere) {
  const { t } = useTranslation();
  const sessions = useHelpSessions();
  // The session the person started again from: shown no longer, whatever the list still says.
  const [cleared, setCleared] = useState<string | null>(null);
  const newest = [...(sessions.data ?? [])]
    .filter((session) => session.repository === HELP_REPOSITORY)
    .sort((a, b) => b.created.localeCompare(a.created))[0] ?? null;
  const shown = newest && newest.id !== cleared ? newest : null;
  const live = shown ? SESSION_ACTIVE.has(shown.state) : false;

  const start = useStartHelp();
  const send = useSendMessage();
  const end = useEndChat();
  const stop = useStopSession();
  const cancelTurn = useCancelTurn();
  const roster = useHarnesses();
  const turns = useChatTurns(shown && live ? [shown.id] : [])[shown?.id ?? ''] ?? NO_TURNS;
  const structured = Array.isArray(roster.data?.harnesses)
    ? roster.data.harnesses.find((row) => row.harness === shown?.adapter)?.structured
    : undefined;

  const [draft, setDraft] = useState('');
  const [refusal, setRefusal] = useState<string | null>(null);
  // 🔴 A message that did not arrive goes back into the box, never lost (the composer's own rule).
  const giveBack = (text: string, why: string) => {
    setRefusal(why);
    if (text) setDraft((was) => [text, was.trim()].filter(Boolean).join('\n\n'));
  };

  // The last preface each conversation was handed, so an unchanged screen is not said again.
  const told = useRef(new Map<string, string>());
  const deliver = (id: string, text: string, files: File[]) => {
    const now = where ? prefaceOf(where) : undefined;
    const preface = now && told.current.get(id) !== now ? now : undefined;
    send.mutate({ id, text, files, ...(preface ? { preface } : {}) }, {
      onSuccess: (answer) => {
        if (!answer.sent) giveBack(text, t('help.notSent'));
        else if (preface) told.current.set(id, preface);
      },
      onError: (error) => giveBack(text, sentence(error)),
    });
  };

  const onSend = (text: string, files: File[]) => {
    setRefusal(null);
    if (shown && live) {
      deliver(shown.id, text, files);
      return;
    }

    start.mutate(undefined, {
      // The driver's own sentence when it cannot: no agent named, the agent signed out, one already
      // running elsewhere. Shown where the person pressed send, whole.
      onSuccess: (answer) => {
        if (!answer.sessionId) giveBack(text, answer.message);
        else {
          setCleared(null);
          deliver(answer.sessionId, text, files);
        }
      },
      onError: (error) => giveBack(text, sentence(error)),
    });
  };

  const onNew = () => {
    if (!shown) return;
    if (live) end.mutate(shown.id);
    setCleared(shown.id);
    setRefusal(null);
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
      sending={send.isPending || start.isPending}
      refusal={refusal}
      // Only a conversation that runs has an ending to choose.
      endings={live}
      draft={draft}
      onDraft={setDraft}
      queued={turns.queued}
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
