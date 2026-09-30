import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { getBridge, useShenora } from '@shenora/react';
import { keys } from '../queries';

// Which deployment serves each workspace here (MOD3): the machine's wiring, over `remotes.json`.

/**
 * The machine's wiring: which deployment serves each workspace here (D48 §5, D50).
 *
 * @remarks
 * Shell-only, like every control — and for a sharper reason than the others. This is machine-local
 * state with a credential in it, so the service deliberately has no route onto it: a browser over a
 * keyed remote must never be able to read where a machine syncs, let alone re-point it.
 *
 * The key travels IN — the person pastes one here, on their own machine — and never comes back out:
 * `remotes[].key` is the audit prefix the deployment's own `keys list` prints, nothing more.
 */
export type MachineWiring = {
  path: string;
  /** True when the environment names the machine's remote, whole — the file is not read at all. */
  fromEnvironment: boolean;
  remotes: { workspace: string; url: string; key: string }[];
};

const callRemotes = <TData,>(type: string, payload?: Record<string, unknown>): Promise<TData> =>
  getBridge().invoke<TData>('DAORIS.REMOTES', type, payload ? { payload } : {});

export const useRemotes = () => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.remotes,
    queryFn: () => callRemotes<MachineWiring>('STATE'),
    enabled: isAvailable,
  });
};

/** Wire a workspace to its deployment, or take it off the map. Both answer with the whole wiring. */
function useWiringChange<TVariables extends Record<string, unknown>>(type: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (variables: TVariables) => callRemotes<MachineWiring>(type, variables),
    onSuccess: (state) => client.setQueryData(keys.remotes, state),
  });
}

export const useWireRemote = () =>
  useWiringChange<{ workspace: string; url: string; key: string }>('SET');

export const useUnwireRemote = () => useWiringChange<{ workspace: string }>('REMOVE');
