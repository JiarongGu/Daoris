import { useEffect, useState } from 'react';
import type { TFunction } from 'i18next';
import { doorOf } from '../../agents/tools';
import type { GoAheadAnswer } from '../../asks/GoAheadList';
import { useParkGoAhead, useResolveSession } from '../../bridge/shell';
import type { Session } from '../../data/api';
import { usePersonDone } from '../../data/queries';
import { failure, type Notify, SESSION_ACTIVE } from '../../design/ui';
import { sentence } from '../../locales/format';
import type { Answered } from '../InlineConfirm';
import { NATIVE_WORDS_LIMIT, tooLong } from '../say';
import type { QuestClose, Resolution } from '../waits/AwaitingPerson';
import { goAheadToast } from '../waits/parkGoAheads';
import { afterPress, choiceWords, openGoAheads } from '../waits/parkQuestion';
import type { Level } from './conversation';
import type { useAttendedBox } from './useAttendedBox';
import type { Refusal, useWords } from './useWords';

type Box = ReturnType<typeof useAttendedBox>;

/**
 * The words through the box at the foot and a park's card (MSG1f, PARKVIEW1, PARKANSWER1): a running door's word, a park's
 * answer with the choice selected in its question, its go-aheads, its finish or decline, and words said as it winds up,
 * each through the one say door and judged before anything is sent.
 */
export function useBoxWords({
  t, notify, attended, box, answering, question, goAheads, parkAsk, draft, setDraft, setRefusal, roster, words,
}: {
  t: TFunction;
  notify: Notify;
  attended: Session | null;
  box: Box['box'];
  answering: boolean;
  question: Box['question'];
  goAheads: Box['goAheads'];
  parkAsk: string | null;
  draft: string;
  setDraft: Box['setDraft'];
  setRefusal: (refusal: Refusal | null) => void;
  roster: Parameters<typeof doorOf>[1];
  words: ReturnType<typeof useWords>;
}) {
  const { sending, say, counted, untaken, stillTaking, nowSaid } = words;
  const resolve = useResolveSession();
  // The person's done on the quest a finish at a checkpoint leaves (QUESTCLOSE1): the service's own door for it.
  const personDone = usePersonDone();
  const parkGoAhead = useParkGoAhead();
  // QUESTIONUI1a (the supervisor design §8.4): the answer selected in the park's question, the frame's beside the draft, in memory, so a
  // reload starts with none and the words kept. Drawn selected only on the question it was selected on: a replaced question is drawn
  // with none, so a kept label never answers a question the park no longer asks.
  const [parkChoice, setParkChoice] = useState<{ session: string; question: string; label: string } | null>(null);
  const parkPicked = attended && answering && question && parkChoice?.session === attended.id && parkChoice.question === question.id
    && question.choices.some((choice) => choice.label === parkChoice.label) ? parkChoice.label : null;
  const onParkPick = (label: string | null) => {
    setParkChoice(label !== null && attended && question ? { session: attended.id, question: question.id, label } : null);
  };
  // Words said as the attended session winds up, held for its record to end (D137 §2.1), shown above its box until then.
  const [ending, setEnding] = useState<{ session: string; words: string[] } | null>(null);
  useEffect(() => {
    if (!attended || ending?.session !== attended.id || !SESSION_ACTIVE.has(attended.state)) setEnding(null);
  }, [attended, ending?.session]);

  // A door that takes fewer characters at once than these says so before anything is sent, and the words stay in the box
  // (D137 §2.4): a driven session on the native door, whose resumed run takes them as one argument.
  // Why these words are not sent, or null: the box's refusal and the card's alike (PARKANSWER1).
  const unaccepted = (text: string): string | null => {
    // PARKVIEW1 (the park's ask design §4): where its question waits beside go-aheads still open, the go-aheads come first, each
    // keeping the park for this answer, which then sends it on with them; the words stay in the box, said why.
    if (attended && answering && question && openGoAheads(goAheads).length > 0) return t('work.park.goAheadsFirst');
    if (!attended || !tooLong(text, { door: doorOf(attended.door, roster), kind: attended.kind })) return null;
    return t('work.say.tooLong', { count: NATIVE_WORDS_LIMIT });
  };
  const accepts = (text: string) => {
    const why = unaccepted(text);
    if (why && attended) setRefusal({ session: attended.id, text: why });
    return why === null;
  };
  // The box's judge (QUESTIONUI1a): its words with the answer selected, as they would go; refused alike for a choice and free words while
  // the question's go-aheads are open, through Enter and *Send* (Codex's round-2 finding 14).
  const boxAccepts = (text: string) => accepts(parkPicked ? choiceWords(parkPicked, text) : text);

  const onSay = (text: string, level?: Level) => {
    if (!attended || box.kind === 'none' || box.kind === 'line' || box.kind === 'chat') return;
    // QUESTIONUI1a: the box's *Send* with an answer selected sends it, then the words, naming its question.
    if (parkPicked && question) { onChoice(parkPicked, question.id, text); return; }
    if (!text) return;
    const session = attended.id;
    const kind = box.kind === 'steer' ? 'steer' : box.mode === 'answer' ? 'answer' : 'say';
    const winding = box.kind === 'say' && box.mode === 'ending';
    const giveBack = () => setDraft((was) => [text, was.trim()].filter(Boolean).join('\n\n'));
    const n = sending.begin(session, { text, files: [] });
    say.mutate({ id: session, text, ...(level ? { level } : {}) }, {
      onSuccess: (result) => {
        sending.answered(n);
        if (!result.sent) {
          giveBack();
          setRefusal({ session, text: untaken(result, t('work.say.notTaken')) });
          return;
        }
        counted(session, kind, text, 0, result);
        setRefusal(null);
        if (level === 'now') nowSaid(result);
        if (kind === 'answer') notify(t('work.awaiting.answered'));
        // Held until its record ends, and shown above the box meanwhile: the conversation shows them once kept.
        if (winding) setEnding((was) => ({ session, words: [...(was?.session === session ? was.words : []), text] }));
      },
      onError: (error: unknown) => {
        if (stillTaking(n, error)) return;
        giveBack();
        notify(sentence(error), 'error');
      },
    });
  };

  // The person's answer to a parked session (design §4). The refusal — a move that is not theirs,
  // a decline with nothing in it — is the host's own sentence and reaches them word for word.
  // A finish that closes its quest (QUESTCLOSE1) sends the person's done once the finish stood: the session first, as the
  // driver moves the process before the record, then the quest, whose refusal is the service's sentence, the finish kept.
  // The ask the finish or decline was pressed in hears how the session's move ended (UXFIX2b2b): the refusal is said inside
  // it, and it closes once the session landed, the quest's done being its own notice.
  const onResolve = (state: Resolution, note: string | null, close?: QuestClose, answered?: Answered) => {
    if (!attended) return;
    const closing = state === 'completed' && close?.as === 'done' ? attended.quest ?? null : null;
    resolve.mutate({ id: attended.id, state, note: note ?? undefined }, {
      onSuccess: () => {
        answered?.done();
        notify(t('work.awaiting.resolved', { id: attended.id, state: t(`sessionState.${state}`) }));
        if (closing) {
          personDone.mutate({ id: closing, note: close!.note }, {
            onSuccess: (result) => notify(result.message),
            onError: failure(notify),
          });
        }
      },
      onError: answered ? (error) => answered.refused(sentence(error)) : failure(notify),
    });
  };

  // PARKANSWER1 (the owner report, 2026-10-23): a park's card answers it through the box's own say door (MSG1f), the one door that
  // answers a park (ONEWAY1k), and as the box would (its refusals, a question changed under the page, words on their way). A choice
  // names its question (PARKVIEW1). Settles with the sentence a refusal is said in, for the card to say beside its words.
  const sayFromCard = (text: string, choice?: { label: string; question: string }) => new Promise<string | null>((settle) => {
    if (!attended) { settle(null); return; }
    const why = unaccepted(text);
    if (why) { settle(why); return; }
    const session = attended.id;
    const n = sending.begin(session, { text, files: [] });
    say.mutate({ id: session, text, ...(choice ? { question: choice.question } : {}) }, {
      onSuccess: (result) => {
        sending.answered(n);
        if (!result.sent) {
          settle(result.why === 'question-changed' ? t('work.park.changed') : untaken(result, t('work.say.notTaken')));
          return;
        }
        counted(session, 'answer', text, 0, result);
        notify(choice ? t('work.park.chose', { label: choice.label }) : t('work.awaiting.answered'));
        settle(null);
      },
      // A send the page stopped waiting on may still land (SENDLOST1b): it is shown on its way, so it leaves the card.
      onError: (error: unknown) => settle(stillTaking(n, error) ? null : sentence(error)),
    });
  });
  const answerFromCard = (words: string) => sayFromCard(words);
  const choiceFromCard = (label: string, words: string) =>
    (question ? sayFromCard(choiceWords(label, words), { label, question: question.id }) : Promise.resolve(null));

  // A go-ahead the park asked, answered on its page (KNOWUSE1a2, D135 §2): through one door, which sends the same session
  // on once none of the go-aheads it asked is open, as the ask's page does (GOAHEAD2b). The person's words are the
  // go-ahead's, never a message to the park, so nothing is counted as one; the toast names any go-ahead still open from the
  // list as it stood at the press.
  // A refusal is said beside the go-ahead that was answered (UXFIX2b3b), not toasted.
  // PARKVIEW1 (the park's ask design §4): where the park's question waits unanswered, a go-ahead keeps the park for that answer
  // (`goesOn: false`, GOAHEAD2's provision for a caller that answers the park itself), which then sends it on with them all.
  const onGoAhead: GoAheadAnswer = (number, approved, words, answered) => {
    if (!attended || !parkAsk) { answered.done(); return; }
    const sessionQuest = attended.quest;
    const asked = goAheads;
    parkGoAhead.mutate({ id: attended.id, ask: parkAsk, number, approved, words, ...(question ? { goesOn: false } : {}) }, {
      onSuccess: (answer) => { notify(goAheadToast(t, number, approved, answer, { quest: sessionQuest, asked })); answered.done(); },
      onError: (error) => answered.refused(sentence(error)),
    });
  };

  // PARKVIEW1 (the park's ask design §4): a press in the park's box takes the box's words with it. They leave the box only once the
  // door kept them, and only where it still begins with them, so words typed while the press was on its way stay; a refusal is said
  // in the box, the words kept.
  const onBoxGoAhead = (number: number, approved: boolean) => {
    if (!attended) return;
    const session = attended.id;
    const words = draft;
    setRefusal(null);
    onGoAhead(number, approved, words.trim() || undefined, {
      done: () => setDraft((was) => afterPress(was, words)),
      refused: (said) => setRefusal({ session, text: said }),
    });
  };
  // One of the answers its question offered, selected (QUESTIONUI1a): its label, then the words the box's *Send* took, as the park's answer
  // through the box's one door, naming the question so a label never answers one the park no longer asks (`question-changed`). The
  // box has let its words go: a refusal gives them back with the selection kept; a question changed keeps the words and draws the new
  // one with none selected, as its id is another.
  const onChoice = (label: string, asked: string, words: string) => {
    if (!attended) return;
    const session = attended.id;
    const text = choiceWords(label, words);
    const giveBack = () => { if (words) setDraft((was) => [words, was.trim()].filter(Boolean).join('\n\n')); };
    setRefusal(null);
    const n = sending.begin(session, { text, files: [] });
    say.mutate({ id: session, text, question: asked }, {
      onSuccess: (result) => {
        sending.answered(n);
        if (!result.sent) {
          giveBack();
          setRefusal({ session, text: result.why === 'question-changed' ? t('work.park.changed') : untaken(result, t('work.say.notTaken')) });
          return;
        }
        setParkChoice(null);
        counted(session, 'answer', text, 0, result);
        notify(t('work.park.chose', { label }));
      },
      onError: (error: unknown) => {
        // A send the page stopped waiting on may still land (SENDLOST1b): its words are shown on their way, so they stay out of the box.
        if (stillTaking(n, error)) return;
        giveBack();
        setRefusal({ session, text: sentence(error) });
      },
    });
  };

  return {
    resolve, personDone, parkGoAhead, parkPicked, onParkPick, ending, boxAccepts, onSay, onResolve, answerFromCard, choiceFromCard,
    onGoAhead, onBoxGoAhead,
  };
}
