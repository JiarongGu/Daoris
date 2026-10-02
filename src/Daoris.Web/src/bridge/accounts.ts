import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../queries';
import type { AccountsAnswer, AccountUseChange } from '../settings/accounts';
import { call } from './call';

// How each agent's accounts are used (TOOL4g; D125 §2.4, §3.7, §6; D130 §3.2, §9, §16.6): each scope's list and how it is
// used, each account's cool-off, what its agent last said, its learned week and Daoris's sessions on it; and the edits
// Settings → Agents makes, the terminal's `daoris agent profile order|use|ready` over the same files (D50).

/**
 * How each agent's accounts are used, read from the files and never probed (TOOL4g), so it is asked with the roster and
 * again at each tick. Shell-only, like the roster: a profile name rides this bridge and no HTTP route (D47 §4).
 */
export const useAccounts = () => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.accounts,
    queryFn: () => call<AccountsAnswer>('ACCOUNTS'),
    enabled: isAvailable,
  });
};

/**
 * The screen's edits to how an agent's accounts are used (TOOL4g), each refused as the terminal refuses it and written to
 * the same file: `order`, a scope's list written whole (`daoris agent profile order`, none clearing it); `use`, how it is
 * used (`profile use`); `ready`, a cool-off ended early, *Try now* (`profile ready`, `own` for the tool's own sign-in); and
 * `inherit`, a workspace returned to this machine's accounts, its default, list and settings cleared at once.
 */
export const useAccountUse = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ harness, action, workspace, ...rest }: {
      harness: string;
      action: 'order' | 'use' | 'ready' | 'inherit';
      workspace?: string;
      /** For `order`: the list, in order; none clears it. */
      accounts?: string[];
      /** For `ready`: the account whose cool-off ends, or `own` for the tool's own sign-in. */
      profile?: string;
      own?: boolean;
    } & AccountUseChange) =>
      call<{ harness: string; action: string; workspace?: string | null; profile?: string | null; ended?: boolean }>(
        'ACCOUNT_USE', { harness, action, ...(workspace ? { workspace } : {}), ...rest }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: keys.accounts });
      // A workspace's default may have gone with its list, and a start may run elsewhere now (MAP1b).
      void client.invalidateQueries({ queryKey: keys.harnesses });
    },
  });
};
