import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';
import type { ReactNode } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));
vi.mock('@shenora/react', async (original) => ({
  ...await original<typeof import('@shenora/react')>(),
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true }),
  useShenoraEvent: () => {},
}));

import { ChannelPanel, ChannelSection } from '../asks/ChannelSection';
import { noticesSlice, useMessages } from '../bridge/notices';
import { useMarkChannel } from '../bridge/sessions/asks';
import { pushedOf } from '../bridge/state/slices';
import { WorkspaceScopeProvider } from '../data/scope';
import type { Channel } from '../work/channel';
import { NotificationsView } from './NotificationsView';

// Reading an ask's messages where the whole channel is read (NOTIFY2g, the notification design §2.4): the ask's page moves the
// person's mark through the newest once (`CHANNEL_MARK`), the shell pushes the ask's count as the service then says it, and its item
// leaves *Messages*; a session's channel panel shows a part of the channel and moves no mark, so the item stays; *Mark read* moves
// the same mark with the newest message's moment.

const NEWEST = { id: 'm2', at: '2026-10-22T09:05:00Z', who: 'agent', repository: 'db', line: 'Which column?', level: 'ordinary', toEveryone: true };
const ENTRY = { ask: 'a1', workspace: 'default', teammate: false, unread: 2, newest: NEWEST };
const CHANNEL: Channel = {
  ask: 'a1', entry: 'person', unread: 2, mark: '2026-10-22T09:00:00Z',
  items: [
    { kind: 'message', at: '2026-10-22T09:01:00Z', id: 'm1', person: false, repository: 'db', text: 'Which table?' },
    { kind: 'message', at: '2026-10-22T09:05:00Z', id: 'm2', person: false, repository: 'db', text: 'Which column?' },
  ],
};

/** A window whose `notices` slice is live with the ask's entry, as its snapshot installs it. */
async function live() {
  invoke.mockImplementation(async (_module: string, route: string) => (
    route === 'STATE_SLICE'
      ? {
        slice: 'notices', epoch: 'e1', status: 'ready',
        partitions: [
          { partition: 'news', seq: 1, status: 'ready', data: {} },
          { partition: 'messages', seq: 1, status: 'ready', data: { a1: ENTRY } },
        ],
      }
      : { ...CHANNEL, unread: 0, mark: NEWEST.at }));
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  pushedOf(client).register(noticesSlice);
  await pushedOf(client).resync('notices');
  invoke.mockClear();
  return client;
}

/** The shell's push of the ask's entry as the service counts it after the mark moved. */
const pushedRead = (client: QueryClient) => act(() => {
  pushedOf(client).heard({
    slice: 'notices', epoch: 'e1', partition: 'messages', seq: 2, baseSeq: 1, status: 'ready', data: { a1: { ...ENTRY, unread: 0 } },
  });
});

/** Messages in the view beside a place the ask's channel is read. */
function Window({ reader }: { reader: (mark: (through: string) => void) => ReactNode }) {
  const messages = useMessages();
  const mark = useMarkChannel();
  return (
    <>
      <NotificationsView
        presses={[]} needs={0} working={0} attention={null} onAnswer={async () => ({ answered: true })}
        messages={messages.asks} answered={messages.answered}
        onMarkAsk={(entry) => mark.mutate({ ask: entry.ask, through: entry.newest?.at })}
      />
      {reader((through) => mark.mutate({ ask: 'a1', through }))}
    </>
  );
}

const draw = (client: QueryClient, reader: (mark: (through: string) => void) => ReactNode) => render(
  <QueryClientProvider client={client}>
    <Tooltip.Provider>
      <WorkspaceScopeProvider initial={null}><Window reader={reader} /></WorkspaceScopeProvider>
    </Tooltip.Provider>
  </QueryClientProvider>,
);

const marks = () => invoke.mock.calls.filter(([, route]) => route === 'CHANNEL_MARK');

describe("reading an ask's messages", () => {
  afterEach(() => { invoke.mockReset(); });

  it("clears the ask's item where its page reads the channel, the mark moved through the newest", async () => {
    const client = await live();
    draw(client, (mark) => <ChannelSection channel={CHANNEL} onPost={() => {}} onRead={mark} />);

    await waitFor(() => expect(marks()).toEqual([['DAORIS.DRIVER', 'CHANNEL_MARK', { payload: { ask: 'a1', through: NEWEST.at } }]]));
    expect(screen.getByRole('region', { name: 'Messages' })).toBeInTheDocument();
    pushedRead(client);
    await waitFor(() => expect(screen.queryByRole('region', { name: 'Messages' })).toBeNull());
  });

  it("leaves the item where a session's panel shows its part of the channel", async () => {
    const client = await live();
    draw(client, () => <ChannelPanel channel={CHANNEL} only={{ entry: 'db-entry', repository: 'db' }} onPost={() => {}} />);

    await waitFor(() => expect(screen.getByRole('region', { name: 'Messages' })).toBeInTheDocument());
    await act(() => new Promise((settled) => { setTimeout(settled, 20); }));
    expect(marks()).toHaveLength(0);
    expect(screen.getByRole('region', { name: 'Messages' })).toHaveTextContent('2 unread');
  });

  it('moves the same mark by Mark read, with the newest message’s moment', async () => {
    const client = await live();
    draw(client, () => null);

    await userEvent.click(await screen.findByRole('button', { name: 'Mark read' }));
    await waitFor(() => expect(marks()).toEqual([['DAORIS.DRIVER', 'CHANNEL_MARK', { payload: { ask: 'a1', through: NEWEST.at } }]]));
  });
});
