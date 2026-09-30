import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../queries';
import type { AgentRulesState, RuleListName, RuleScopeName } from '../settings/AgentRules';
import { call } from './call';

// What an agent Daoris starts may do (MOD3): the machine's `permissions.json`, and an agent's proposals (D72, D74).

/**
 * What an agent Daoris starts may do (PERM1, D72): Claude Code's own rules in Daoris's scopes, from the
 * one file under the home the driver composes each spawn from. Desktop only — the file is machine-local.
 */
export const useRules = () => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.rules,
    queryFn: () => call<AgentRulesState>('RULES'),
    enabled: isAvailable,
  });
};

/** The screen's half of `daoris agent rules` (D50): an edit to the same file, answered with the state after it. */
export const useRuleAction = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (action:
      | { action: 'add'; list: RuleListName; rule: string; scope: RuleScopeName; name?: string }
      | { action: 'remove'; rule: string; scope: RuleScopeName; name?: string }
      | { action: 'default'; id: string; on: boolean }) =>
      call<AgentRulesState>('RULE_ACTION', action),
    onSuccess: (state) => client.setQueryData(keys.rules, state),
  });
};

/**
 * The screen's half of `daoris agent rules accept|decline` (PERM2, D74): the person's answer to an
 * agent's proposal, answered with the rules after it. 🔴 The only way a widening an agent proposed applies.
 */
export const useRuleProposal = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (answer: { id: string; accept: boolean; note?: string }) =>
      call<AgentRulesState>('RULE_PROPOSAL', answer),
    onSuccess: (state) => client.setQueryData(keys.rules, state),
  });
};
