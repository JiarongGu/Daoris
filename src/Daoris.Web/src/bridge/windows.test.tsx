import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook } from '@testing-library/react';

// The secondary windows' calls (MOD3: moved from `shell.test.tsx` beside the domain they call).

const { invoke, notifyReady, eventHandlers } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
  // The push channel's seam: handlers land here by "module.type", and a test fires them as the host.
  eventHandlers: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
  useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
    eventHandlers.set(`${module}.${type}`, handler);
  },
}));

import { setThemeChoice } from '../theme';
import { useSecondaryWindowTheme } from './windows';

/**
 * WINDOW2: a secondary window's page tells its own frame which theme it is in, by the name it was
 * opened under — Shenora's window commands route a second window's theme nowhere, so the native caption
 * stayed on the OS's theme over a page on the viewer's choice.
 */
describe('a secondary window\'s caption (WINDOW2)', () => {
  afterEach(() => {
    setThemeChoice('system');
    invoke.mockReset();
  });

  it('tells its own frame the theme on arrival and each time the choice changes', () => {
    invoke.mockResolvedValue({ applied: true });
    setThemeChoice('light');
    renderHook(() => useSecondaryWindowTheme('monitor'));

    expect(invoke).toHaveBeenLastCalledWith('DAORIS.WINDOWS', 'SET_THEME', { payload: { name: 'monitor', dark: false } });
    act(() => setThemeChoice('dark'));
    expect(invoke).toHaveBeenLastCalledWith('DAORIS.WINDOWS', 'SET_THEME', { payload: { name: 'monitor', dark: true } });
  });
});
