import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { call } from '../call';
import { askHeld, isPending } from '../state/held';

// A repository's integration (INTEGRATE1h; the integration design §5.8): the Integration view's read and the owner's acts.

/** A request as the view says it: its unit, branch, frozen tip and where it stands. */
export type IntegrationRequestRow = {
  request: string;
  line: string;
  branch: string;
  tip: string;
  since: string;
  unit?: string | null;
  said?: string | null;
  /** Where an order moved a queued request (INTEGRATE1h): first or last; absent where it stands by arrival. */
  ordered?: 'first' | 'last' | null;
};

export type IntegrationMemberRow = { request: string; branch: string; state: string; said?: string | null };

export type IntegrationStepRow = { step: string; state: string };

/** The standing candidate of a line: its members, the fixed strip of steps, and what holds it with the act that releases it. */
export type IntegrationCandidateRow = {
  candidate: string;
  line: string;
  base: string;
  since: string;
  members: IntegrationMemberRow[];
  steps: IntegrationStepRow[];
  at?: string | null;
  /** What holds it (INTEGRATE1h adds `decide`: the policy held it for the owner's decide). */
  holds?: 'decide' | 'conflict' | 'promotion' | null;
  release?: string | null;
  by?: string | null;
  /** The judge's words a decide hold was made with. */
  why?: string | null;
  /** A decide recorded on it that the line's run has still to act on. */
  decided?: IntegrationDecide | null;
};

export type IntegrationSupersededRow = { request: string; commit: string; subject: string; by: string; files: number };

/** A landed branch whose integration waits for the owner's card (INTEGRATE1h), as the runtime last judged it. */
export type IntegrationWaitingRow = { unit: string; branch: string; tip: string; code: string; said: string; since?: string | null };

/** `INTEGRATION`'s answer for a repository. */
export type IntegrationLook = {
  repository: string;
  queue: IntegrationRequestRow[];
  candidates: IntegrationCandidateRow[];
  handedBack: IntegrationRequestRow[];
  superseded: IntegrationSupersededRow[];
  promoted: IntegrationRequestRow[];
  /** Absent from a driver before INTEGRATE1h, read as none. */
  waiting?: IntegrationWaitingRow[];
};

/** The owner's decide on a held candidate (§4.5, §8). */
export type IntegrationDecide = 'split' | 'hand-back' | 'land';

/** A unit's one line: its integration state, the line, and the step it is at. */
export type IntegrationHead = { state: 'queued' | 'in-candidate' | 'held' | 'handed-back' | 'settled' | 'dropped'; line: string; step?: string | null };

/** What `INTEGRATE` answers: done, or why not, with the driver's sentence. */
export type IntegrateAnswer = { done: boolean; why?: string | null; message: string };

const lookKey = (repository: string) => ['integration', repository] as const;

/** The queue, candidates and history of a repository, told by event (the record is a file) and asked again when its scope moves. */
export const useIntegration = (repository: string, enabled = true) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const queryKey = lookKey(repository);
  return useQuery({
    queryKey,
    queryFn: () => askHeld<IntegrationLook | null>('INTEGRATION', client, queryKey, { repository },
      (answer) => (isPending(answer) ? null : (answer as { look: IntegrationLook }).look)),
    enabled: isAvailable && enabled,
  });
};

/** A unit's one line from the same record (`INTEGRATION` with its `unit`), told by the same event; null where the unit never asked. */
export const useIntegrationHead = (unit: string | null | undefined) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const queryKey = ['integration', 'unit', unit ?? ''] as const;
  return useQuery({
    queryKey,
    queryFn: () => askHeld<IntegrationHead | null>('INTEGRATION', client, queryKey, { unit },
      (answer) => (isPending(answer) ? null : ((answer as { head?: IntegrationHead | null }).head ?? null))),
    enabled: isAvailable && Boolean(unit),
  });
};

/** An owner's act as `INTEGRATE` takes it: each act with the fields it names. */
export type IntegrateAct =
  | { repository: string; act: 'request'; branch: string; landed?: boolean }
  | { repository: string; act: 'drop'; request: string }
  | { repository: string; act: 'recheck' }
  | { repository: string; act: 'decide'; candidate: string; decide: IntegrationDecide }
  | { repository: string; act: 'order'; request: string; place: 'first' | 'last' }
  | { repository: string; act: 'answer'; request: string; commit: string; answer: 'superseded' | 'rebase' };

/**
 * The owner's act: a request, a drop or a recheck; and (INTEGRATE1h) a decide on a held candidate, a queued request's order and a
 * superseded commit's answer. The state hub asks the look again in every window.
 */
export const useIntegrate = () =>
  useMutation({
    mutationFn: (act: IntegrateAct) => call<IntegrateAnswer>('INTEGRATE', act),
  });
