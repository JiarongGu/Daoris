import { useState } from 'react';
import type { TFunction } from 'i18next';
import {
  logEvent, stoppedWaiting, type TurnStop, useCancelTurn, type useChatTurns, useEndChat, useHandOver, useMoveQueued, useSay,
  useSendMessage, useStartFrom, type WordsAnswer,
} from '../../bridge/shell';
import type { Session } from '../../data/api';
import type { useSessions } from '../../data/queries';
import { failure, type Notify } from '../../design/ui';
import { sentence } from '../../locales/format';
import type { entryOfSession, placeOf } from '../entries';
import { type boxOf, goOnBriefRefusal, moveSaid, neverSentence, type NewSessionAnswer, startFromRefusal } from '../say';
import type { Answered } from '../InlineConfirm';
import type { Level } from './conversation';
import { forgetDraft, keepDraft, type useDraft } from './drafts';
import { type Sent, useSending } from './sending';

/** A refusal said under the words of the session that gave it. */
export type Refusal = { session: string; text: string };

/**
 * The person's words to the attended session and what they came to: a conversation's message, a word
 * moved to the line's head, words said again or started anew elsewhere, a turn stopped, each counted once it went and given
 * back to the box where it did not arrive.
 */
export function useWords({
  t, notify, attended, here, oneTurn, conversation, live, box, chatTurns, draft, setDraft, setRefusal, sessions, attendedEntry,
  stands, attend,
}: {
  t: TFunction;
  notify: Notify;
  attended: Session | null;
  here: boolean;
  oneTurn: boolean;
  conversation: boolean;
  live: boolean;
  box: ReturnType<typeof boxOf>;
  chatTurns: ReturnType<typeof useChatTurns>;
  draft: string;
  setDraft: ReturnType<typeof useDraft>[1];
  setRefusal: (refusal: Refusal | null) => void;
  sessions: Pick<ReturnType<typeof useSessions>, 'refetch'>;
  attendedEntry: ReturnType<typeof entryOfSession>;
  stands: ReturnType<typeof placeOf> | null;
  attend: (id: string) => void;
}) {
  // What *Go on in a new session* came to, the session's own, as a refusal is: said under its words alone.
  const [newSession, setNewSession] = useState<{ session: string; answer: NewSessionAnswer } | null>(null);
  const send = useSendMessage();
  const end = useEndChat();
  const cancelTurn = useCancelTurn();
  const moveQueued = useMoveQueued();
  const onMoveWord = stands?.place === 'beside' && attendedEntry?.head && attended && box.kind === 'say' && box.mode === 'goOn'
    ? (word: string) => {
      const session = attended.id;
      moveQueued.mutate({ session, id: word, to: attendedEntry.head! }, {
        onSuccess: (moved) => {
          if (moved.moved) { setRefusal(null); notify(t('work.say.moved')); return; }
          setRefusal({ session, text: moveSaid(t, moved) });
        },
      });
    }
    : undefined;
  // *To the head*: the unsent draft moves to the head's own draft and its page opens; nothing is sent.
  const onDraftToHead = stands?.place === 'beside' && attendedEntry?.head && attended && draft.trim()
    ? () => {
      // Through the store: the draft hook re-reads it as the head is attended.
      forgetDraft(attended.id);
      keepDraft(attendedEntry.head!, draft);
      attend(attendedEntry.head!);
    }
    : undefined;
  // The person's words on their way to the attended session: shown the moment they are sent, kept
  // where the page stopped waiting on their send, and settled by its own queue and record (`SentSettler`, which reads it
  // only while some wait). A chat that ended without them gives them back, said so; a driven session keeps words once they
  // arrive.
  const sending = useSending(attended && here && !oneTurn
    ? { id: attended.id, queued: chatTurns[attended.id]?.queued, endedChat: conversation && !live, refused: box.kind === 'line' }
    : null);
  const giveBackSent = (sent: Sent) => {
    setDraft((was) => [sent.words.text, was.trim()].filter(Boolean).join('\n\n'));
    if (sent.words.files.length > 0) {
      notify(t('work.composer.filesNotSent', { count: 1, names: sent.words.files.join(', ') }), 'error');
    }
    // Where the driver's refusal comes after the page stopped waiting, the box is now a line, so the words are
    // said under it as well as kept for the box.
    setRefusal({
      session: sent.session,
      text: box.kind === 'line' ? t('work.composer.notSentLine', { text: sent.words.text }) : t('work.composer.notSent'),
    });
  };

  // Counted into the machine log once the words went, never their words: their length, how many files they carried,
  // and where they reach the session as the driver answered.
  const counted = (session: string, kind: string, text: string, files: number, said: WordsAnswer) =>
    logEvent('message.sent', { session, kind, length: text.length, files, ...(said.reaches ? { reach: said.reaches } : {}) });

  // Words nothing took, in the page's words: by the driver's code where it gave one, else that nothing took them.
  const untaken = (said: WordsAnswer, fallback: string) =>
    (said.why ? neverSentence(t, said.why, { quest: attended?.quest, to: said.to }) : fallback);

  // 🔴 A send the page stopped waiting on (the bridge's TIMEOUT) may still be taken by the machine: its words
  // are kept above the box, saying so, never put back in it, where a second send would say them twice, and the sessions are
  // read again; the session's own queue and record settle them. Any other failure lets them go, for the caller to give back.
  const stillTaking = (n: number, error: unknown) => {
    if (!stoppedWaiting(error)) {
      sending.answered(n);
      return false;
    }
    sending.unanswered(n);
    void sessions.refetch();
    return true;
  };

  // What words at *now* came to, said where the person pressed it: one line that the door could not, and
  // where the words go instead. Taken at once in the running turn says nothing here, since the conversation shows the words
  // where they were taken. No turn is stopped for them: the keeping stop is the person's own press.
  // `now: false` carries one of three codes: `no-interrupt`, said by where the words go instead, `behind` and
  // `opening`. A code not known here gets the generic line. `now: true` with no reach is an ordinary send (no turn is running).
  const nowSaid = (result: WordsAnswer) => {
    if (result.now !== false) return;
    notify(t(result.why === 'opening' ? 'work.steer.notNowOpening'
      : result.why === 'behind' ? 'work.steer.notNowBehind'
      : result.why === 'no-interrupt' ? (result.reaches === 'next-step' ? 'work.steer.notNowNextStep' : 'work.steer.notNowTurnEnd')
        : 'work.steer.notNow'));
  };

  const onSend = (text: string, files: File[] = [], level?: Level) => {
    if (!attended) return;
    const session = attended.id;
    // 🔴 The composer lets go of the words when it sends; a send that did not arrive hands them back,
    // into THIS session's draft (the setter is bound to it), and names the files — the page no longer
    // holds their bytes. Losing a paragraph to a refusal is the failure the composer exists to prevent.
    const giveBack = () => {
      if (text) setDraft((was) => [text, was.trim()].filter(Boolean).join('\n\n'));
      if (files.length > 0) {
        notify(t('work.composer.filesNotSent', { count: 1, names: files.map((file) => file.name).join(', ') }), 'error');
      }
    };
    // Shown at once, as sending, until the answer says what came of them.
    const n = sending.begin(session, { text, files: files.map((file) => file.name) });
    send.mutate({ id: session, text, files, ...(level ? { level } : {}) }, {
      // False is an answer: the session ended while they were typing. It lands on the composer
      // rather than in a toast, because that is where the person is looking — on THAT session's.
      onSuccess: (result) => {
        sending.answered(n);
        if (result.sent) {
          counted(session, 'chat', text, files.length, result);
          setRefusal(null);
          if (level === 'now') nowSaid(result);
          return;
        }
        giveBack();
        setRefusal({ session, text: untaken(result, t('work.composer.notListening')) });
      },
      onError: (error: unknown) => {
        if (stillTaking(n, error)) return;
        giveBack();
        notify(sentence(error), 'error');
      },
    });
  };

  // The person's words through the box at the foot: a working driven session's running door, a
  // park's answer, a session that ended, or one winding up. A send that did not arrive gives the words back to the box, as
  // a message does.
  const say = useSay();
  // *Say it again*: the person's own words that may have reached the agent before a
  // restart were never sent again; this says them anew, a new word, as the person's default. What the session would not take goes back
  // to the composer, said why, as a word said there does.
  const onSayAgain = (words: string[]) => {
    if (!attended || words.length === 0) return;
    const session = attended.id;
    const text = words.join('\n\n');
    const giveBack = () => setDraft((was) => [text, was.trim()].filter(Boolean).join('\n\n'));
    say.mutate({ id: session, text }, {
      onSuccess: (result) => {
        if (result.sent) return;
        giveBack();
        setRefusal({ session, text: untaken(result, t('work.say.notTaken')) });
      },
      onError: (error: unknown) => {
        giveBack();
        notify(sentence(error), 'error');
      },
    });
  };
  // *Start a conversation with these words*: a new chat in the repository, its first message the words
  // this session could not go on with, attended once it opens. One act of the driver's, which reads the words off
  // the record and takes them off it, so the words the page shows are not sent. A refusal is said by its code; the driver's
  // sentence when none could start is said whole.
  const startFrom = useStartFrom();
  const onStartFrom = () => {
    if (!attended || startFrom.isPending) return;
    startFrom.mutate({ session: attended.id }, {
      onSuccess: (started) => {
        if (started.why) {
          notify(startFromRefusal(t, started.why, { quest: attended.quest }), 'error');
          return;
        }
        if (!started.sessionId) {
          notify(started.message, 'error');
          return;
        }
        attend(started.sessionId);
        if (!started.sent) notify(t('work.say.notTaken'), 'error');
      },
      onError: failure(notify),
    });
  };
  // *Go on with Daoris's brief*: the same session goes on in a new part of itself, handed Daoris's brief
  // and then the words, attended once it opens. The hand-over's press naming no agent; a refusal is said by its code, as the fresh start's is.
  const goOnBrief = useHandOver();
  const onGoOnBrief = () => {
    if (!attended || goOnBrief.isPending) return;
    goOnBrief.mutate({ session: attended.id }, {
      onSuccess: (moved) => {
        if (moved.why) {
          notify(goOnBriefRefusal(t, moved.why, { quest: attended.quest }), 'error');
          return;
        }
        if (!moved.sessionId) {
          notify(moved.message, 'error');
          return;
        }
        attend(moved.sessionId);
        if (!moved.kept) notify(t('work.say.notTaken'), 'error');
      },
      onError: failure(notify),
    });
  };
  // *Go on in a new session*: words a cooling account holds go on in a new session at the driver's next
  // look. The driver judges and keeps the choice; what it came to is said under the words, in the page's words.
  const goOnNew = useHandOver();
  const onGoOnNew = () => {
    if (!attended || goOnNew.isPending) return;
    const session = attended.id;
    goOnNew.mutate({ session }, {
      onSuccess: ({ kept, why, message }) => setNewSession({ session, answer: { sent: kept, why, message } }),
      onError: failure(notify),
    });
  };

  // The keeping stop: the person's own press beside words held for the turn's end,
  // asked once, `CANCEL_TURN` with `keep` for a conversation and a driven session alike, whose stop already keeps what is held.
  // The turn stops and what waits goes next; nothing comes back to the box. No word presses it for them.
  const onKeepingStop = (answered: Answered) => {
    if (!attended) return;
    cancelTurn.mutate({ id: attended.id, keep: true }, {
      onSuccess: ({ cancelled }: TurnStop) => {
        answered.done();
        notify(cancelled ? t('work.composer.turnStopping') : t('work.composer.noTurn'));
      },
      onError: (error) => answered.refused(sentence(error)),
    });
  };

  // Stopping the turn and keeping the conversation. What is waiting comes back into the box,
  // ahead of whatever is being typed now, since it is written first; and the notice claims only the
  // asking — a stop that lands after the words and before the turn's end leaves it ending normally.
  const onStopTurn = () => {
    if (!attended) return;
    cancelTurn.mutate(attended.id, {
      onSuccess: ({ cancelled, withdrawn }: TurnStop) => {
        if (withdrawn.length > 0) {
          setDraft((was) => [...withdrawn.map((message) => message.text), was.trim()].filter(Boolean).join('\n\n'));
        }
        // A file cannot come back into the box — the page no longer holds its bytes — so it is named,
        // for the person to attach again.
        const unsent = withdrawn.flatMap((message) => message.files);
        const said = [
          cancelled ? t('work.composer.turnStopping') : null,
          withdrawn.length > 0 ? t('work.composer.withdrawn', { count: withdrawn.length }) : null,
          unsent.length > 0 ? t('work.composer.filesNotSent', { count: withdrawn.length, names: unsent.join(', ') }) : null,
        ].filter((sentence): sentence is string => Boolean(sentence));
        // Sentences are joined the catalogue's way: English puts a space after a full stop, and
        // Chinese does not (seen on the window).
        notify(said.length > 0
          ? said.reduce((first, second) => t('work.composer.twoSentences', { first, second }))
          : t('work.composer.noTurn'));
      },
      onError: failure(notify),
    });
  };

  return {
    newSession, send, end, cancelTurn, onMoveWord, onDraftToHead, sending, giveBackSent, counted, untaken, stillTaking, nowSaid, onSend,
    say, onSayAgain, onStartFrom, onGoOnBrief, goOnNew, onGoOnNew, onKeepingStop, onStopTurn,
  };
}
