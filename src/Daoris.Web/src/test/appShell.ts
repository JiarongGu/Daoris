import { vi } from 'vitest';

// The whole window over a mocked shell, as the application's suites hold it: the real module, every slice's snapshot
// answered by `invoke` (`sliceShell.mockedShell`), no maximized window, and each event's newest handler kept in `heard`
// when a suite listens. A suite mocks the module with
// `vi.mock('@shenora/react', async () => (await import('../test/appShell')).appShellMock(invoke, { notifyReady }))`.

type Invoke = Parameters<typeof import('./sliceShell').mockedShell>[0];

export interface AppShellOptions {
  notifyReady: () => Promise<void>;
  /** Whether a slice's snapshot holds its last answer (the shell's default) or asks again each time. */
  holds?: boolean;
  /** Where each event's newest handler is kept, by its type; none heard where unset. */
  heard?: Map<string, (payload: unknown) => void>;
}

export async function appShellMock(invoke: Invoke, { notifyReady, holds, heard }: AppShellOptions) {
  const actual = await vi.importActual<typeof import('@shenora/react')>('@shenora/react');
  const { mockedShell } = await import('./sliceShell');
  return {
    ...actual,
    ...mockedShell(invoke, holds === undefined ? { notifyReady } : { notifyReady, holds }),
    useShenoraEvent: (_module: string, type: string, handler: (payload: unknown) => void) => { heard?.set(type, handler); },
    useWindowMaximized: () => false,
  };
}
