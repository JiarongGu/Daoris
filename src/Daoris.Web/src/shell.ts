import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { getBridge, isShenoraAvailable } from '@shenora/react';

// The shell's half of the platform (D46 §6). In a browser none of this exists — isShenoraAvailable()
// is false, the query never runs, and every control gated on it stays unrendered. That is the design,
// not a degradation: the controls act where a driver is attached, and only the desktop has one.

/** The driver's standing state, as the DAORIS.DRIVER module answers it. */
export type DriverState = {
  configPath: string;
  drivable: string[];
  holds: string[];
  cap: number;
  adapter: string;
  pollSeconds: number;
  /** Session ids with a live process right now — what "stop" can actually reach. */
  running: string[];
};

export const shellPresent = (): boolean => isShenoraAvailable();

const call = <TData,>(type: string, payload?: Record<string, unknown>): Promise<TData> =>
  getBridge().invoke<TData>('DAORIS.DRIVER', type, payload ? { payload } : {});

export const driverKey = ['driver'] as const;

export const useDriver = () =>
  useQuery({
    queryKey: driverKey,
    queryFn: () => call<DriverState>('STATE'),
    enabled: shellPresent(),
  });

/** One mutation shape for the two toggles: edit the file, and the loop looks now, not at the poll. */
function useDriverChange<TVariables extends Record<string, unknown>>(type: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (variables: TVariables) => call<DriverState>(type, variables),
    onSuccess: (state) => client.setQueryData(driverKey, state),
  });
}

export const useSetDrivable = () => useDriverChange<{ repository: string; drivable: boolean }>('SET_DRIVABLE');
export const useSetHold = () => useDriverChange<{ repository: string; held: boolean }>('SET_HOLD');

export const useStopSession = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => call<{ stopped: boolean }>('STOP_SESSION', { id }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ['sessions'] });
      void client.invalidateQueries({ queryKey: driverKey });
    },
  });
};
