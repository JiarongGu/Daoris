import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, renderHook } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));
vi.mock('@shenora/react', async () => {
  const actual = await vi.importActual<typeof import('@shenora/react')>('@shenora/react');
  return { ...actual, ...(await import('../test/sliceShell')).mockedShell(invoke) };
});

import { keys } from '../data/queries';
import { WorkspaceScopeProvider } from '../data/scope';
import { composeAttention, useAttention } from './attention';

const rows = {
  'departure:q1': { kind: 'departure', id: 'q1', since: '2026-11-12T09:00:00Z', workspace: 'games' },
  'integration:declaration:engine': { kind: 'integration', id: 'declaration:engine', since: '2026-11-12T09:01:00Z', workspace: 'games' },
  'work:b1': { kind: 'work', id: 'b1', since: '2026-11-12T09:02:00Z', workspace: 'northwind' },
};

/** The window's list and `daoris notices` read the same rows: a window looking at one workspace is not shown fewer waits for the person's word. */
describe('What needs you in a window with a workspace scope', () => {
  afterEach(() => cleanup());

  it('holds every row the shell pushed, whatever workspace the window looks at', () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    client.setQueryData(keys.attention, composeAttention(new Map<string, unknown>([['rows', rows], ['sessions', {}]]), true));
    const wrapper = ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={client}><WorkspaceScopeProvider initial="northwind">{children}</WorkspaceScopeProvider></QueryClientProvider>
    );

    const { result } = renderHook(() => useAttention(), { wrapper });

    expect(result.current.state).toBe('known');
    expect(result.current.state === 'known' ? result.current.rows.map((row) => row.kind).sort() : []).toEqual(['departure', 'integration', 'work']);
  });
});
