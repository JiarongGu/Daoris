import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createRef } from 'react';
import '../i18n';

// SESS1 S9, the organism: a jump lands on an event, and where the page does not hold it, the pages
// before are loaded until it does. The bridge is mocked; the driver's answers are the test's.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true }),
  useShenoraEvent: () => {},
}));

import { SessionConversation } from './SessionConversation';

const at = '2026-09-28T00:00:00Z';
const target = { seq: 1, at, kind: 'user', origin: 'target', text: 'Your target is the quest…' };
const said = (seq: number, id: string, text: string) => ({ seq, at, kind: 'message', id, text });
const call = (seq: number, id: string, over: Record<string, unknown>) => ({ seq, at, kind: 'tool', id, ...over });

// The newest page: well past the ask and past the failure, which the driver names by where it began.
const NEWEST = [said(300, 'm9', 'Clean. Now the full gates.'), call(301, 'g1', { title: 'npm run gates', status: 'completed' })];
const EARLIER = [
  target,
  said(2, 'm1', 'Starting.'),
  call(3, 'r1', { title: 'Read loader.rs', status: 'completed' }),
  call(4, 'x1', { title: 'npm test', status: 'in_progress' }),
  call(5, 'x1', { status: 'failed', output: '3 failing' }),
  said(6, 'm2', 'Fixing the loader.'),
];

afterEach(() => invoke.mockReset());

describe('SessionConversation', () => {
  it('jumps to the first failure through the pages before it, and shows the failed call', async () => {
    invoke.mockImplementation(async (_module: string, type: string, body?: { payload?: { before?: number } }) => {
      if (type === 'SESSION_HISTORY') {
        return body?.payload?.before
          ? { session: 's1', events: EARLIER, earlier: false, latest: 301 }
          : { session: 's1', events: NEWEST, earlier: true, latest: 301, opening: target, firstFailure: 4 };
      }
      if (type === 'HARNESSES') return { harnesses: [] };
      return {};
    });
    const scroller = createRef<HTMLDivElement>();
    render(
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <Tooltip.Provider>
          <div ref={scroller}>
            <SessionConversation session="s1" live={false} scroller={scroller} />
          </div>
        </Tooltip.Provider>
      </QueryClientProvider>,
    );

    await userEvent.click(await screen.findByRole('button', { name: 'first failure' }));

    await waitFor(() => expect(screen.getByText('npm test')).toBeInTheDocument());
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_HISTORY', { payload: { id: 's1', before: 300 } });
    expect(screen.getByText('npm test').closest('[data-block]')!.className).toContain('outline-accent');
  });
});
