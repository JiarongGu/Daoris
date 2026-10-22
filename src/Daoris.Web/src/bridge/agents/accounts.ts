import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { ACCOUNTS_SLICE, keys } from '../../data/queries';
import type { AccountsAnswer, AccountUseChange, AgentAccounts } from '../../settings/accounts/accounts';
import { call } from '../call';
import { readSlice } from '../driver';
import { readPushed, type SliceSpec, usePushedLive } from '../state/slices';

// How each agent's accounts are used (TOOL4g; D125 §2.4, §3.7, §6; D130 §9, §16.6; D197): the set's order and how it is used,
// each account's cool-off, what its agent last said, its learned week and Daoris's sessions on it, and what the fold into one
// set changed; each account's use read again (SWITCH1d); and the edits the agent's page makes, the terminal's
// `daoris agent profile order|use|ready` over the same files (D50).

const isAgent = (value: unknown): value is AgentAccounts =>
  typeof value === 'object' && value !== null && typeof (value as { agent?: unknown }).agent === 'string';

/**
 * How each agent's accounts are used, as the shell pushes it (PUSH1e, D182): a partition per agent, named by its accounts' owner,
 * each its answer of `ACCOUNTS` whole with the verdicts .NET judges beside each reading (`AccountsStateSlice`), composed into the
 * route's answer, agents by name as C#'s ordinal compares them, as the route orders them. Nothing judged yet composes nothing, which
 * keeps the pull; judged with no agent, none.
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
 * How each agent's accounts are used, read from the files and never probed (TOOL4g). Where the shell pushes it (PUSH1e), read from
 * memory, never stale and asked on no focus, a probe's result arriving by one push; elsewhere asked with the roster and again at each
 * tick. Shell-only, like the roster: a profile name rides this bridge and no HTTP route (D47 §4).
 */
export const useAccounts = () => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const pushed = usePushedLive(ACCOUNTS_SLICE);
  return useQuery({
    queryKey: keys.accounts,
    queryFn: () => readPushed(client, ACCOUNTS_SLICE, () => call<AccountsAnswer>('ACCOUNTS')),
    enabled: isAvailable,
    ...(pushed ? { staleTime: Infinity, refetchOnWindowFocus: false } : {}),
  });
};

/**
 * Each account's use read again (SWITCH1d; the switch design §1.2): *Read now*, and the read as an agent's page opens. Each of
 * the agent's accounts whose reading is older than its question's floor is asked by its agent under its own home, with no
 * prompt and no model request (D165 point 1); the driver skips one cooling, refused, signed out, a key or busy. What it reads
 * lands in the accounts' files, which the shell's accounts slice judges at the roster's declared scope and pushes to every window
 * (PUSH1e), or which the state hub asks again under the roster's key where it is pulled, so this asks nothing again itself.
 */
export const useReadAccounts = () =>
  useMutation({
    mutationFn: ({ harness }: { harness: string }) => call<{ harness: string }>('ACCOUNTS_READ', { harness }),
  });

/**
 * The screen's edits to how an agent's accounts are used (TOOL4g), each refused as the terminal refuses it and written to
 * the same file: `order`, the set's order written whole (`daoris agent profile order`, the accounts named first and the rest
 * after); `use`, how the set is used (`profile use`); `ready`, a cool-off ended early, *Try now* (`profile ready`); and `said`,
 * *Got it* on what the fold into one set changed, by the receipts' ids (POOL1a; the account-pool design §6.3). One set serves
 * every workspace (D197), so no workspace is named: the bridge refuses one as `ACCOUNTS_ONE_SET`.
 *
 * The order, the cool-offs and what a start runs on sit under the roster's key, which the route publishes and the state hub
 * asks again in every window (STATEHUB1h, D169).
 */
export const useAccountUse = () =>
  useMutation({
    mutationFn: ({ harness, action, ...rest }: {
      harness: string;
      action: 'order' | 'use' | 'ready' | 'said';
      /** For `order`: the order, the accounts named first. */
      accounts?: string[];
      /** For `ready`: the account whose cool-off ends. */
      profile?: string;
      /** For `said`: the receipts the page showed, by id. */
      ids?: string[];
    } & AccountUseChange) =>
      call<{ harness: string; action: string; profile?: string | null; ended?: boolean }>('ACCOUNT_USE', { harness, action, ...rest }),
  });
