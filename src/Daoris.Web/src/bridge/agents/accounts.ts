import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { ACCOUNTS_SLICE, keys } from '../../data/queries';
import type { AccountsAnswer, AccountUseChange, AgentAccounts } from '../../settings/accounts/accounts';
import { call } from '../call';
import { readSlice } from '../driver';
import { readPushed, type SliceSpec } from '../state/slices';

// How each agent's accounts are used: the set's order and how it is used,
// each account's cool-off, what its agent last said, its learned week and Daoris's sessions on it, and what the fold into one
// set changed; each account's use read again; and the edits the agent's page makes, the terminal's
// `daoris agent profile order|use|ready` over the same files.

const isAgent = (value: unknown): value is AgentAccounts =>
  typeof value === 'object' && value !== null && typeof (value as { agent?: unknown }).agent === 'string';

/**
 * How each agent's accounts are used, as the shell pushes it: a partition per agent, named by its accounts' owner,
 * each its answer of `ACCOUNTS` whole with the verdicts .NET judges beside each reading (`AccountsStateSlice`), composed into the
 * route's answer, agents by name as C#'s ordinal compares them, as the route orders them. Nothing judged yet composes nothing, which
 * is waited for; judged with no agent, none.
 */
export const accountsSlice: SliceSpec = {
  name: ACCOUNTS_SLICE,
  key: keys.accounts,
  read: () => readSlice(ACCOUNTS_SLICE),
  compose: (partitions, judged) => {
    if (!judged) return undefined;
    const agents = [...partitions.values()].filter(isAgent).sort((a, b) => (a.agent < b.agent ? -1 : a.agent > b.agent ? 1 : 0));
    return { agents } satisfies AccountsAnswer;
  },
};

/**
 * How each agent's accounts are used, read from the files and never probed, as the shell pushes it: from memory,
 * never stale and asked on no focus, a probe's result arriving by one push. Shell-only, like the roster: a profile name rides this
 * bridge and no HTTP route.
 */
export const useAccounts = () => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  return useQuery({
    queryKey: keys.accounts,
    queryFn: () => readPushed<AccountsAnswer>(client, ACCOUNTS_SLICE),
    enabled: isAvailable,
    staleTime: Infinity,
    refetchOnWindowFocus: false,
  });
};

/**
 * Each account's use read again: *Read now*, which only a press asks. Each of
 * the agent's accounts whose reading is older than its question's floor is asked by its agent under its own home, with no
 * prompt and no model request; the driver skips one cooling, refused, signed out, a key or busy. What it reads
 * lands in the accounts' files, which the shell's accounts slice judges at the roster's declared scope and pushes to every window,
 * or which the state hub asks again under the roster's key where it is pulled, so this asks nothing again itself.
 */
export const useReadAccounts = () =>
  useMutation({
    mutationFn: ({ door }: { door: string }) => call('ACCOUNTS_READ', { door }) as Promise<{ door: string }>,
  });

/**
 * The screen's edits to how an agent's accounts are used, each refused as the terminal refuses it and written to
 * the same file: `order`, the set's order written whole (`daoris agent profile order`, the accounts named first and the rest
 * after); `use`, how the set is used (`profile use`); `ready`, a cool-off ended early, *Try now* (`profile ready`); and `said`,
 * *Got it* on what the fold into one set changed, by the receipts' ids. One set serves
 * every workspace, so no workspace is named: the bridge refuses one as `ACCOUNTS_ONE_SET`.
 *
 * The order, the cool-offs and what a start runs on sit under the roster's key, which the route publishes and the state hub
 * asks again in every window.
 */
export const useAccountUse = () =>
  useMutation({
    mutationFn: ({ door, action, ...rest }: {
      door: string;
      action: 'order' | 'use' | 'ready' | 'said';
      /** For `order`: the order, the accounts named first. */
      accounts?: string[];
      /** For `ready`: the account whose cool-off ends. */
      profile?: string;
      /** For `said`: the receipts the page showed, by id. */
      ids?: string[];
    } & AccountUseChange) =>
      call('ACCOUNT_USE', { door, action, ...rest }) as Promise<{ door: string; action: string; profile?: string | null; ended?: boolean }>,
  });
