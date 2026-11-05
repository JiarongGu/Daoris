import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import i18n from '../../locales/i18n';
import { AtWorkView } from './AtWorkView';
import { LANES_ALONGSIDE_AND_UNTAKEN, LINE_SAID, SAID, THREE } from './atWork.fixtures';
import { type Channel, type ChannelItem, lastMessages, lastWords, lineChannelOf } from '../channel';

// COWORKVIEW1c: the channel's last words (the cowork view design §3.3): the page's own channel read, each message on the part whose
// session posted it, *All messages* opening the panel; the view reads no channel and holds no field of it.

afterEach(async () => {
  cleanup();
  await i18n.changeLanguage('en');
});

const rowHolding = (words: string) => screen.getAllByRole('listitem').find((li) => li.textContent?.includes(words) && li.querySelector('button'))!;

describe('the channel’s last words in the view', () => {
  it('places a message on the part whose session posted it, and the person’s in the block under it', () => {
    render(<AtWorkView held={LANES_ALONGSIDE_AND_UNTAKEN} channel={{ words: SAID, onOpen: () => {} }} />);

    // The lane part (number 2) and the lead (number 1) each hold the message their session posted.
    expect(rowHolding('The core lane is done').querySelector('button')!.textContent).toContain('2');
    expect(rowHolding('Waiting on the docs lane.').querySelector('button')!.textContent).toContain('1');
    // The person's words sit on no part: they are in the block, whole, and the parts' messages are not repeated there.
    const block = screen.getByText('On the channel').closest('section')!;
    expect(block.textContent).toContain('Start with the index, then the notes.');
    expect(block.textContent).toContain('You');
    expect(block.textContent).not.toContain('The core lane is done');
  });

  it('opens the panel on All messages', () => {
    const open = vi.fn();
    render(<AtWorkView held={LANES_ALONGSIDE_AND_UNTAKEN} channel={{ words: SAID, onOpen: open }} />);

    fireEvent.click(screen.getByRole('button', { name: 'All messages' }));
    expect(open).toHaveBeenCalledOnce();
  });

  /** COMMS1k: a line on no ask held here draws its own channel; two alongside parts to each other alone, each named, and no panel to open. */
  it('draws a line’s own channel: each message on the part that posted it, naming the part it is to alone, with no All messages', async () => {
    render(<AtWorkView held={LANES_ALONGSIDE_AND_UNTAKEN} channel={{ words: LINE_SAID }} />);

    expect(rowHolding('The contract changed').textContent).toContain('to part s2 alone');
    expect(rowHolding('Taken; the index reads the region now.').textContent).toContain('to part c1 alone');
    expect(screen.getByText('On the channel')).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'All messages' })).toBeNull();

    cleanup();
    await i18n.changeLanguage('zh');
    render(<AtWorkView held={LANES_ALONGSIDE_AND_UNTAKEN} channel={{ words: LINE_SAID }} />);
    expect(rowHolding('The contract changed').textContent).toContain('只发给段 s2');
  });

  it('draws no block for a session with no ask, and says nothing yet for a channel that is empty', () => {
    const { rerender } = render(<AtWorkView held={LANES_ALONGSIDE_AND_UNTAKEN} />);
    expect(screen.queryByText('On the channel')).toBeNull();

    rerender(<AtWorkView held={LANES_ALONGSIDE_AND_UNTAKEN} channel={{ words: [], onOpen: () => {} }} />);
    expect(screen.getByText('Nothing on the channel yet.')).toBeTruthy();
  });

  it('puts a lone part’s message in the block, since the parts list is not drawn', () => {
    const lone: ChannelItem[] = [{ kind: 'message', id: 'm1', at: '2026-10-31T14:01:00Z', session: 's1', repository: 'game', agent: 'claude-code', text: 'Started.' }];
    render(<AtWorkView held={THREE} channel={{ words: lone, onOpen: () => {} }} />);

    expect(screen.getByText('On the channel').closest('section')!.textContent).toContain('Started.');
  });

  it('says the block in Chinese', async () => {
    await i18n.changeLanguage('zh');
    render(<AtWorkView held={LANES_ALONGSIDE_AND_UNTAKEN} channel={{ words: SAID, onOpen: () => {} }} />);

    expect(screen.getByText('频道上')).toBeTruthy();
    expect(screen.getByRole('button', { name: '全部消息' })).toBeTruthy();
  });
});

describe('lastWords', () => {
  const message = (id: string, at: string, over: Partial<ChannelItem> = {}): ChannelItem => ({ kind: 'message', id, at, text: id, ...over });
  const only = { entry: 'e1', repository: 'game' };
  const channel = (items: ChannelItem[]): Channel => ({ ask: 'a1', entry: 'e1', items, unread: 0 });

  it('is the newest three messages of what the panel holds, oldest first, Daoris’s own lines left out', () => {
    const items: ChannelItem[] = [
      message('m1', '2026-11-02T08:00:00Z'),
      message('m2', '2026-11-02T08:01:00Z'),
      { kind: 'line', at: '2026-11-02T08:02:00Z', to: 'game', code: 'x' },
      message('m4', '2026-11-02T08:03:00Z'),
      message('m3', '2026-11-02T08:02:30Z'),
      message('m5', '2026-11-02T08:04:00Z'),
    ];

    expect(lastWords(channel(items), only).map((item) => item.id)).toEqual(['m3', 'm4', 'm5']);
  });

  it('keeps to the panel’s own part of the channel: another line of work’s message to another repository is not read', () => {
    const items = [message('m1', '2026-11-02T08:00:00Z', { entry: 'other', to: 'elsewhere' }), message('m2', '2026-11-02T08:01:00Z', { entry: 'e1' })];

    expect(lastWords(channel(items), only).map((item) => item.id)).toEqual(['m2']);
    expect(lastWords(null, only)).toEqual([]);
  });
});

describe('a line’s own channel, read', () => {
  it('reads the shell’s CHANNEL_LINE answer, and none where the line has no channel of its own or the shell answered another shape', () => {
    const line = lineChannelOf({ origin: 'session:c1', items: [...LINE_SAID, { kind: 'message' }] });

    expect(line?.origin).toBe('session:c1');
    expect(line?.items.map((item) => item.id)).toEqual(['aaaaaaaaa1', 'aaaaaaaaa2', 'aaaaaaaaa3']);
    expect(lastMessages(line, 2).map((item) => item.id)).toEqual(['aaaaaaaaa2', 'aaaaaaaaa3']);
    expect(lineChannelOf({ origin: null, items: [] })).toBeNull();
    expect(lineChannelOf('something else')).toBeNull();
  });
});
