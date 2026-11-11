import { useHeldHarnessRun } from '../agents/harnessRuns';
import type { Confirmation, ConfirmationAnswer } from './Confirmations';

type Answer = (waiting: Confirmation, confirm: boolean) => Promise<ConfirmationAnswer>;

/** The tool and the action of a press that is `daoris agent update|pin`, read from the view its card shows; null for any other. */
function agentAction(waiting: Confirmation): { harness: string; action: 'update' | 'pin' } | null {
  const view = waiting.view;
  const harness = view?.facts.agent;
  if (!view || !harness || waiting.route !== 'HARNESS_ACTION') return null;
  if (view.kind.startsWith('agent-update')) return { harness, action: 'update' };
  return view.kind.startsWith('agent-pin') ? { harness, action: 'pin' } : null;
}

/**
 * The answer to a press, with the Agents screen's running action following an update or a pin once the person's Confirm
 * is answered: the press started it, so the console and the end belong under the tool as for one started on the screen.
 */
export function useFollowedAgentPress(answer: Answer): Answer {
  const runs = useHeldHarnessRun();
  return async (waiting, confirm) => {
    const answered = await answer(waiting, confirm);
    const started = confirm && answered.answered ? agentAction(waiting) : null;
    if (started) runs?.follow(started.harness, started.action);
    return answered;
  };
}
