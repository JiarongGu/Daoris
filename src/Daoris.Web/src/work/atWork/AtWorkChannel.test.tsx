import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react';
import i18n from '../../locales/i18n';
import { AtWorkView } from './AtWorkView';
import { LANES_ALONGSIDE_AND_UNTAKEN, LINE_SAID, SAID, THREE } from './atWork.fixtures';
import { type Channel, type ChannelItem, lastMessages, lastWords, lineChannelOf } from '../channel';

// The channel's last words: the page's own channel read, each message on the part whose
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

  /** A line on no ask held here draws its own channel; two alongside parts to each other alone, each named, and no panel to open. */
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

  /** The person posts on a line's own channel from the view: to everyone or a repository, the box clearing once it is kept. */
  it('lets the person post on a line’s own channel, to a repository its messages name, and clears the box once it is kept', () => {
    const post = vi.fn();
    render(<AtWorkView held={LANES_ALONGSIDE_AND_UNTAKEN} channel={{ words: LINE_SAID, onPost: post, recipients: ['checker'] }} />);

    const box = screen.getByRole('textbox', { name: 'Write to the channel' }) as HTMLTextAreaElement;
    expect((screen.getByRole('button', { name: 'Send' }) as HTMLButtonElement).disabled).toBe(true);
    fireEvent.change(box, { target: { value: '  Use v4 from now on.  ' } });
    fireEvent.click(screen.getByRole('button', { name: 'Send' }));

    expect(post).toHaveBeenCalledOnce();
    expect(post.mock.calls[0][0]).toEqual({ text: 'Use v4 from now on.', to: null });
    expect(screen.getByText('Your words, to every part of this line of work.')).toBeTruthy();
    act(() => post.mock.calls[0][1].done());
    expect(box.value).toBe('');
  });

  it('keeps the words and says the service’s refusal in the reader’s language', async () => {
    const post = vi.fn();
    await i18n.changeLanguage('zh');
    render(<AtWorkView held={LANES_ALONGSIDE_AND_UNTAKEN} channel={{ words: LINE_SAID, onPost: post }} />);

    const box = screen.getByRole('textbox', { name: '写给频道' }) as HTMLTextAreaElement;
    fireEvent.change(box, { target: { value: 'hello' } });
    fireEvent.click(screen.getByRole('button', { name: '发送' }));
    act(() => post.mock.calls[0][1].refused('too-long'));

    expect(screen.getByText('内容太长，一条消息最多 4,000 个字符。')).toBeTruthy();
    expect(box.value).toBe('hello');
    expect(screen.getByText('你的话，发给这条工作线的每一段。')).toBeTruthy();
  });

  it('draws no composer for a channel with no post door, the ask’s own', () => {
    render(<AtWorkView held={LANES_ALONGSIDE_AND_UNTAKEN} channel={{ words: SAID, onOpen: () => {} }} />);
    expect(screen.queryByRole('textbox')).toBeNull();
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
