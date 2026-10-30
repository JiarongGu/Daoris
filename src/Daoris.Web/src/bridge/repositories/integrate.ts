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
  holds?: 'conflict' | 'promotion' | null;
  release?: string | null;
  by?: string | null;
};

export type IntegrationSupersededRow = { request: string; commit: string; subject: string; by: string; files: number };

/** `INTEGRATION`'s answer for a repository. */
export type IntegrationLook = {
  repository: string;
  queue: IntegrationRequestRow[];
  candidates: IntegrationCandidateRow[];
  handedBack: IntegrationRequestRow[];
  superseded: IntegrationSupersededRow[];
  promoted: IntegrationRequestRow[];
};

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

/** The owner's act: a request, a drop or a recheck; the state hub asks the look again in every window. */
export const useIntegrate = () =>
  useMutation({
    mutationFn: (act: { repository: string; act: 'request' | 'drop' | 'recheck'; branch?: string; request?: string; landed?: boolean }) =>
      call<IntegrateAnswer>('INTEGRATE', act),
  });
