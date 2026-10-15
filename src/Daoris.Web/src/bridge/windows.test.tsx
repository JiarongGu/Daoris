import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook } from '@testing-library/react';

// The secondary windows' calls (MOD3: moved from `shell.test.tsx` beside the domain they call).

const { invoke, notifyReady, eventHandlers, scheme } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
  // The push channel's seam: handlers land here by "module.type", and a test fires them as the host.
  eventHandlers: new Map<string, (payload: unknown) => void>(),
  // The kit's colour-scheme client (THEMESCHEME1): what the engine every window renders on was told.
  scheme: vi.fn((_scheme: string) => Promise.resolve()),
}));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
  useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
    eventHandlers.set(`${module}.${type}`, handler);
  },
  ColorScheme: class {
    set = scheme;
  },
}));

import { setThemeChoice } from '../design/theme';
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
    scheme.mockClear();
  });

  it('tells its own frame the theme on arrival and each time the choice changes', () => {
    invoke.mockResolvedValue({ applied: true });
    setThemeChoice('light');
    renderHook(() => useSecondaryWindowTheme('monitor'));

    expect(invoke).toHaveBeenLastCalledWith('DAORIS.WINDOWS', 'SET_THEME', { payload: { name: 'monitor', dark: false } });
    act(() => setThemeChoice('dark'));
    expect(invoke).toHaveBeenLastCalledWith('DAORIS.WINDOWS', 'SET_THEME', { payload: { name: 'monitor', dark: true } });
  });

  /**
   * THEMESCHEME1: the engine's own colour scheme follows the viewer's choice, not the OS, so Chromium's own UI and
   * `prefers-color-scheme` in this window agree with the page. The choice itself, never the effective theme: a scheme
   * held at the OS's present value would stop a viewer on System following the OS.
   */
  it('tells the engine the viewer\'s choice on arrival and each time it changes', () => {
    invoke.mockResolvedValue({ applied: true });
    setThemeChoice('dark');
    renderHook(() => useSecondaryWindowTheme('session:a1b2c3d4'));

    expect(scheme).toHaveBeenLastCalledWith('dark');
    act(() => setThemeChoice('light'));
    expect(scheme).toHaveBeenLastCalledWith('light');
    act(() => setThemeChoice('system'));
    expect(scheme).toHaveBeenLastCalledWith('system');
  });
});
