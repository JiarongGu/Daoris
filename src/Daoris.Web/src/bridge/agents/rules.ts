import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../../data/queries';
import type { AgentRulesState, RuleListName, RuleScopeName } from '../../settings/repository/AgentRules';
import { call } from '../call';
import { readSlice } from '../driver';
import { readPushed, type SliceSpec } from '../state/slices';

// What an agent Daoris starts may do: the machine's `permissions.json`, and an agent's proposals.

/** The rules' slice, as the shell's owner names it (`RulesStateSlice`). */
export const RULES_SLICE = 'rules';

/**
 * The rules as the shell pushes them: one partition, `RULES`'s answer whole, pushed when the rules move.
 * Nothing judged yet composes nothing, which is waited for.
 */
export const rulesSlice: SliceSpec = {
  name: RULES_SLICE,
  key: keys.rules,
  read: () => readSlice(RULES_SLICE),
  compose: (partitions, judged) => (judged ? partitions.get('rules') : undefined),
};

/**
 * What an agent Daoris starts may do: Claude Code's own rules in Daoris's scopes, from the
 * one file under the home the driver composes each spawn from. Desktop only — the file is machine-local. As the shell pushes them,
 * read from memory, never stale and asked on no focus.
 */
export const useRules = () => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  return useQuery({
    queryKey: keys.rules,
    queryFn: () => readPushed<AgentRulesState>(client, RULES_SLICE),
    enabled: isAvailable,
    staleTime: Infinity,
    refetchOnWindowFocus: false,
  });
};

/** The screen's half of `daoris agent rules`: an edit to the same file, answered with the state after it. */
export const useRuleAction = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (action:
      | { action: 'add'; list: RuleListName; rule: string; scope: RuleScopeName; name?: string }
      | { action: 'remove'; rule: string; scope: RuleScopeName; name?: string }
      | { action: 'default'; id: string; on: boolean }) =>
      call('RULE_ACTION', action) as Promise<AgentRulesState>,
    onSuccess: (state) => client.setQueryData(keys.rules, state),
  });
};

/**
 * The screen's half of `daoris agent rules accept|decline`: the person's answer to an
 * agent's proposal, answered with the rules after it. 🔴 The only way a widening an agent proposed applies.
 */
export const useRuleProposal = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (answer: { id: string; accept: boolean; note?: string }) =>
      call('RULE_PROPOSAL', answer) as Promise<AgentRulesState>,
    onSuccess: (state) => client.setQueryData(keys.rules, state),
  });
};
