import { useShenora } from '@shenora/react';
import { useTranslation } from 'react-i18next';
import { sentence } from '../format';
import { type OpinionPressed, useAskOpinion, useOpinionAnyway, useOpinionMyself, useSay, useStopOpinion } from '../shell';
import type { Notify } from '../ui';
import type { Answered } from './InlineConfirm';
import type { OpinionGate } from './opinion';
import type { OpinionGateActs } from './OpinionGate';

/** What one gate's presses act on: the working session whose work it reads, the gate as drawn, and the reviewer's session door. */
export type OpinionTarget = {
  /** The working session: what an ask, an answer and *Send back…* name. */
  session: string;
  /** The gate as drawn: its opinion is what *Stop…* names. */
  gate?: OpinionGate | null;
  /** The reviewer's own session, in Sessions. */
  openSession?: (session: string) => void;
};

/**
 * **The one owner of the second opinion's presses** (XAGENT1g; D155 point 10, the second-agent design §8.5, §9): the session's
 * head and *What needs you* each press this, so an ask, a stop and the person's answer have one implementation whichever door
 * pressed it, as `reviewActs.ts` is the one owner of a review's.
 *
 * @remarks
 * **An organism**: it holds the bridge's hooks, so the gate and the rows, which press them, hold none.
 *
 * **Each press is the driver's own** (`OpinionPresses`, the terminal's `daoris-driver opinion`): a refusal is an answer, the
 * driver's sentence said inside the gate that was pressed (UXFIX2), and a press taken says the driver's sentence. None is Ask
 * Daoris's (§9): asking spends an account at the person's choice, and going on or reading it themselves is a judgement.
 *
 * **Send back… is D137's box**: the person's own words to the working session as its next turn, through the door the
 * terminal's `sessions say` takes (`SESSION_INPUT`). A browser has no shell, so none of these is offered there.
 */
export function useOpinionActs({ notify }: { notify: Notify }) {
  const { t } = useTranslation();
  const { isAvailable } = useShenora();
  const ask = useAskOpinion();
  const anyway = useOpinionAnyway();
  const myself = useOpinionMyself();
  const stop = useStopOpinion();
  const say = useSay();

  /** A press told back to the gate that made it: the driver's sentence once taken, its refusal inside the gate. */
  const told = (answered: Answered) => ({
    onSuccess: (result: OpinionPressed) => {
      if (!result.done) {
        answered.refused(result.message);
        return;
      }
      notify(result.message);
      answered.done();
    },
    onError: (error: unknown) => answered.refused(sentence(error)),
  });

  const actsFor = ({ session, gate = null, openSession }: OpinionTarget): OpinionGateActs => (isAvailable ? {
    ask: (answered) => ask.mutate({ id: session }, told(answered)),
    sameAgent: (answered) => ask.mutate({ id: session, sameAgent: true }, told(answered)),
    ...(gate?.opinion ? { stop: (answered: Answered) => stop.mutate({ opinion: gate.opinion! }, told(answered)) } : {}),
    anyway: (words, answered) => anyway.mutate({ id: session, words }, told(answered)),
    myself: (words, answered) => myself.mutate({ id: session, words }, told(answered)),
    sendBack: (words, answered) => say.mutate({ id: session, text: words }, {
      onSuccess: () => {
        notify(t('opinion.said.sentBack', { session }));
        answered.done();
      },
      onError: (error: unknown) => answered.refused(sentence(error)),
    }),
    ...(openSession ? { openSession } : {}),
  } : {});

  return {
    /** The presses for one gate. */
    actsFor,
    /** A press on its way: the gate's presses wait for it. */
    busy: ask.isPending || anyway.isPending || myself.isPending || stop.isPending || say.isPending,
    /** Whether a shell is here: every press reaches the driver through it alone. */
    shell: isAvailable,
  };
}
