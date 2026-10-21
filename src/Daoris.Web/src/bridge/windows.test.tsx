import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, render, renderHook } from '@testing-library/react';

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

import type { ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { setThemeChoice } from '../design/theme';
import { useScope, WorkspaceScopeProvider, type WindowStore } from '../data/scope';
import i18n from '../locales/i18n';
import { useNoticeWords, useOpenWindow, useSecondaryWindowTheme, useTaskbarCount } from './windows';

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

  /** THEMESCHEME1: the engine is told the choice itself, never the effective theme (`useEngineTheme` has why). */
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

/** NOTIFY1b, NOTIFY2c: each main window's taskbar button counts what waits in its workspace; the whole window's case is `sessionsBadge.test.tsx`. */
describe('the taskbar\'s count (NOTIFY1b)', () => {
  afterEach(() => {
    invoke.mockReset();
    window.history.replaceState(null, '', '/');
  });

  const told = () => invoke.mock.calls.filter(([, type]) => type === 'SET_BADGE');

  it('tells the frame on arrival and when the count or its words change, and not otherwise', () => {
    invoke.mockResolvedValue({ applied: true });
    const { rerender } = renderHook(({ count, words }) => useTaskbarCount(count, words),
      { initialProps: { count: 2, words: '2 waiting on you' } });

    rerender({ count: 2, words: '2 waiting on you' });
    rerender({ count: 0, words: '0 waiting on you' });
    rerender({ count: 0, words: '0 个等你处理' });

    expect(told().map(([, , request]) => request)).toEqual([
      { payload: { count: 2, description: '2 waiting on you', order: expect.any(Number) } },
      { payload: { count: 0, description: '0 waiting on you', order: expect.any(Number) } },
      { payload: { count: 0, description: '0 个等你处理', order: expect.any(Number) } },
    ]);
  });

  /**
   * SESSBADGE1: each count is told without waiting, so two told close together may reach the frame in either order; the title once
   * kept 5 while the band read 4. Each carries a later order than the one before, even within one millisecond and across a reload of
   * the page, and the frame never wears a count told before the one it wears (`WindowsModuleTests`).
   */
  it('tells each count with a later order than the one before', () => {
    invoke.mockResolvedValue({ applied: true });
    const now = vi.spyOn(Date, 'now').mockReturnValue(1_760_950_000_000);
    const { rerender } = renderHook(({ count }) => useTaskbarCount(count, `${count} waiting on you`), { initialProps: { count: 5 } });
    rerender({ count: 4 });
    rerender({ count: 5 });
    now.mockRestore();

    const orders = told().map(([, , request]) => (request as { payload: { order: number } }).payload.order);
    expect(orders).toHaveLength(3);
    expect(orders[0]).toBeGreaterThanOrEqual(1_760_950_000_000);
    expect(orders[1]).toBeGreaterThan(orders[0]!);
    expect(orders[2]).toBeGreaterThan(orders[1]!);
  });

  it('says nothing from a secondary window, and a frame that cannot be told is not a failure', async () => {
    invoke.mockRejectedValue(new Error('NO_ROUTE'));
    renderHook(() => useTaskbarCount(1, '1 waiting on you'));
    window.history.replaceState(null, '', '/?window=session%3Aa1b2c3d4');
    renderHook(() => useTaskbarCount(4, '4 waiting on you'));

    expect(told()).toHaveLength(1);
    await act(async () => {});
  });

  /**
   * NOTIFY2c: another main window a later launch opened (ONEINSTANCE1) holds its own workspace, so it counts its own and wears it on
   * its own taskbar button, told by the name it was opened under as its title is.
   */
  it('tells another main window\'s own frame its own count, by its name', () => {
    invoke.mockResolvedValue({ applied: true });
    window.history.replaceState(null, '', '/?window=main%3A2');
    renderHook(() => useTaskbarCount(3, '3 waiting on you'));

    expect(told().map(([, , request]) => request)).toEqual([
      { payload: { name: 'main:2', count: 3, description: '3 waiting on you', order: expect.any(Number) } },
    ]);
  });
});

/**
 * NOTIFY2d (the notification design §3.3): a main window's page hands the shell's notifier the notices' words in its language, at
 * its load and at each change of language, so a balloon behind the window speaks as the window does.
 */
describe('the notices\' words told to the shell (NOTIFY2d)', () => {
  afterEach(async () => {
    invoke.mockReset();
    window.history.replaceState(null, '', '/');
    await i18n.changeLanguage('en');
  });

  const told = () => invoke.mock.calls.filter(([, type]) => type === 'SET_NOTICE_WORDS')
    .map(([module, , request]) => [module, (request as { payload: { words: Record<string, string> } }).payload.words]);

  it('tells them at load and again in the new language, and not otherwise', async () => {
    invoke.mockResolvedValue({ applied: true });
    const { rerender } = renderHook(() => useNoticeWords());
    rerender();

    expect(told()).toHaveLength(1);
    expect(told()[0]![0]).toBe('DAORIS.WINDOWS');
    expect(told()[0]![1]['notifications.system.parked.title']).toBe('{{repository}}: a session waits on you');

    await act(async () => { await i18n.changeLanguage('zh'); });

    expect(told()).toHaveLength(2);
    expect(told()[1]![1]['notifications.system.parked.title']).toBe('{{repository}}：一个会话等你处理');
    expect(told()[1]![1]['person.route.SESSION_INPUT']).toBe('以你的名义对会话说话');
  });

  it('says nothing from a utility window, and a shell that cannot be told is not a failure', async () => {
    invoke.mockRejectedValue(new Error('NO_ROUTE'));
    renderHook(() => useNoticeWords());
    window.history.replaceState(null, '', '/?window=monitor');
    renderHook(() => useNoticeWords());

    expect(told()).toHaveLength(1);
    await act(async () => {});
  });
});

/**
 * MAINWINDOWS3 (D171's note): a window opened from a page follows that page's window, or holds its session's own
 * workspace, so the page says which before it asks the shell for the window, and the new page finds it on arrival.
 */
describe('opening a window from a main window', () => {
  const memory = (): WindowStore => {
    const kept = new Map<string, string>();
    return { read: (key) => kept.get(key) ?? null, write: (key, value) => void (value === null ? kept.delete(key) : kept.set(key, value)) };
  };

  afterEach(() => {
    invoke.mockReset();
    window.localStorage.clear();
  });

  /** The opener at `search` holding `workspace`, its `useOpenWindow`, and what a window opened under `name` holds. */
  function opener(search: string, workspace: string) {
    const client = new QueryClient();
    const wrapper = ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={client}>
        <WorkspaceScopeProvider search={search} own={memory()}>{children}</WorkspaceScopeProvider>
      </QueryClientProvider>
    );
    const { result } = renderHook(() => ({ open: useOpenWindow(), scope: useScope() }), { wrapper });
    act(() => result.current.scope.setWorkspace(workspace));
    return result;
  }

  function holds(name: string): string | null {
    const held: { workspace?: string | null } = {};
    function Probe() {
      held.workspace = useScope().workspace;
      return null;
    }
    render(<WorkspaceScopeProvider search={`?window=${encodeURIComponent(name)}`} own={memory()}><Probe /></WorkspaceScopeProvider>);
    return held.workspace ?? null;
  }

  it('says the window follows this one before it asks the shell for it', async () => {
    // Which windows were said to follow which when the shell was asked: the new page reads it on arrival.
    let asked: string | null = null;
    invoke.mockImplementation(async () => {
      asked = window.localStorage.getItem('daoris.workspace.opened');
      return { opened: true, windows: ['monitor'] };
    });
    const result = opener('?window=main%3A2', 'games');
    window.localStorage.setItem('daoris.workspace', 'work');

    await act(async () => {
      await result.current.open.mutateAsync('monitor');
    });

    expect(invoke).toHaveBeenCalledWith('DAORIS.WINDOWS', 'OPEN', { payload: { name: 'monitor' } });
    expect(JSON.parse(asked ?? '{}')).toEqual({ monitor: { follows: 'main:2' } });
    expect(holds('monitor')).toBe('games');
  });

  it('opens a session into its own workspace where the session names one, and asks the shell by its name alone', async () => {
    invoke.mockResolvedValue({ opened: true, windows: ['session:abc12345'] });
    const result = opener('', 'games');

    await act(async () => {
      await result.current.open.mutateAsync({ name: 'session:abc12345', workspace: 'studio' });
    });

    expect(invoke).toHaveBeenCalledWith('DAORIS.WINDOWS', 'OPEN', { payload: { name: 'session:abc12345' } });
    expect(holds('session:abc12345')).toBe('studio');
  });
});
