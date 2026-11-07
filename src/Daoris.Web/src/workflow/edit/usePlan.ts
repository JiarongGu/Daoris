import { useEffect, useRef, useState } from 'react';
import { sentence } from '../../locales/format';
import { type PlanState, planToken } from '../place/ChangePlan';
import type { WorkflowUseAnswer, WorkflowUseAsk } from './answers';

/**
 * The plan of one choice (`WORKFLOW_USE {plan: true}`, which writes nothing), read once `wanted` and again after `again` is asked, the newest
 * request alone heard. The token is the plan's where it is read for this very ask and the driver does not refuse it.
 */
export function usePlan(
  ask: WorkflowUseAsk | null, wanted: boolean, sender: ((ask: WorkflowUseAsk) => Promise<WorkflowUseAnswer>) | undefined,
): { plan: PlanState | null; token: string | null; again: () => void } {
  const [plan, setPlan] = useState<PlanState | null>(null);
  const [rounds, setRounds] = useState(0);
  const asked = useRef(0);
  const send = useRef(sender);
  send.current = sender;
  const key = ask === null ? '' : JSON.stringify(ask);

  useEffect(() => {
    const mine = ++asked.current;
    const post = send.current;
    if (!wanted || ask === null || !post) { setPlan(null); return undefined; }
    setPlan({ key, state: 'reading' });
    post({ ...ask, plan: true }).then(
      (answer) => { if (mine === asked.current) setPlan({ key, state: 'read', answer }); },
      (failure) => { if (mine === asked.current) setPlan({ key, state: 'failed', why: sentence(failure) }); },
    );
    return () => { asked.current += 1; };
    // `ask` is its key's content; the sender is read through a ref, since the bridge hands a new one each render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key, wanted, rounds]);

  return { plan, token: planToken(plan, key), again: () => setRounds((was) => was + 1) };
}
