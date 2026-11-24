import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import type { RiskActEdit, RiskActsBridge, RiskActsChange, RiskActsView } from '../../settings/place/PlaceRiskActs';
import { call } from '../call';
import { askHeld } from '../state/held';

// A place's act table and the owner's own set of a row, over the policy `daoris settings risk` prints and sets: the window's press is the owner's own,
// written at the revision the table read. A refusal is the driver's sentence, verbatim.

const tableKey = (workspace: string | null, repository: string | null, edit: RiskActEdit | null) =>
  ['RISK_TABLE', workspace ?? '', repository ?? '', edit?.act ?? '', edit?.class ?? ''] as const;

/** The table an answer holds, or none while the shell holds nothing yet: its event brings the table. */
const readTable = (answer: unknown): RiskActsView | null =>
  typeof answer === 'object' && answer !== null && Array.isArray((answer as { rows?: unknown }).rows) ? (answer as RiskActsView) : null;

/** A place's act table, with the waits the pending edit would open or release; the shell tells it by event. */
export const useRiskTable = (workspace: string | null, repository: string | null, edit: RiskActEdit | null) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const queryKey = tableKey(workspace, repository, edit);
  return useQuery({
    queryKey,
    queryFn: () => askHeld('RISK_TABLE', client, queryKey, { workspace, repository, edits: edit ? [edit] : null }, readTable),
    select: (view) => view ?? undefined,
    enabled: isAvailable,
  });
};

/** The owner's own set of the edits, at the revision the table read; the tables are read again. */
export const useSetPolicy = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: async (change: RiskActsChange) => {
      const answer = (await call('POLICY_SET', change)) as { done?: boolean; message?: string } | null;
      if (answer?.done !== true) throw new Error(answer?.message ?? 'the policy was not changed');
      return answer;
    },
    onSuccess: () => client.invalidateQueries({ queryKey: ['RISK_TABLE'] }),
  });
};

/** The calls a place's act table is handed, so it holds no bridge of its own. */
export const riskActsBridge: RiskActsBridge = { useTable: useRiskTable, useSet: useSetPolicy };
