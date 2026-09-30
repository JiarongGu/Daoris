import { getBridge } from '@shenora/react';

// The bridge's own helper, shared by its domains and re-exported by none: a page reaches the driver
// through a domain's named calls, never by naming a route itself (MOD3).

/** One call onto the driver's module, with its payload when it has one. */
export const call = <TData,>(type: string, payload?: Record<string, unknown>): Promise<TData> =>
  getBridge().invoke<TData>('DAORIS.DRIVER', type, payload ? { payload } : {});
