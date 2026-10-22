import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../locales/i18n';
import type { Channel, ChannelItem } from '../work/channel';
import { ChannelPanel, ChannelSection } from './ChannelSection';

// An ask's Channel and a session's panel of it (SESSONE2c, D167 points 6 to 8; the co-working design §2.4): messages and Daoris's
// coded lines in the reader's language, an open question marked, the person's composer with To and Reply to, the unread count
// and the mark. Props only, like every molecule here: every state is reached by passing a channel.

const at = (minute: number) => `2026-10-14T09:${String(minute).padStart(2, '0')}:00Z`;

const LINE = (code: string, minute: number, over: Partial<ChannelItem> = {}): ChannelItem => (
  { kind: 'line', at: at(minute), code, quest: 'q1', name: 'Read the figure', from: 'ask #a1', to: 'db', ...over });
const MESSAGE = (id: string, minute: number, over: Partial<ChannelItem> = {}): ChannelItem => (
  { kind: 'message', at: at(minute), id, person: false, session: 's1', repository: 'db', agent: 'claude-code', entry: 'ask:a1/db', text: `words of ${id}`, ...over });

const ITEMS: ChannelItem[] = [
  LINE('published', 0),
  LINE('taken', 1),
  MESSAGE('m1', 2, { question: true, open: true, text: 'Which column holds the figure?' }),
  MESSAGE('m2', 5, { person: true, session: null, repository: null, agent: null, entry: null, to: 'db', re: 'm1', text: 'The v3 one.' }),
  MESSAGE('m3', 6, { repository: 'api', entry: 'ask:a1/api', session: 's2', agent: 'codex', to: 'web', re: 'gone', text: 'Noted.' }),
  LINE('done', 7, { quest: 'q2', name: null, to: 'api', from: 'web' }),
];

const CHANNEL = (over: Partial<Channel> = {}): Channel => ({ ask: 'a1', entry: 'person', items: ITEMS, unread: 0, mark: null, ...over });

/** A Radix select opens on the keyboard, as the other pages' tests open it. */
const open = async (name: string) => {
  const user = userEvent.setup();
  screen.getByRole('combobox', { name }).focus();
  await user.keyboard('{Enter}');
};
const choose = async (name: string, option: string) => {
  await open(name);
  await userEvent.click(await screen.findByRole('option', { name: option }));
};

const list = () => screen.getByRole('list', { name: 'Channel' });
const row = (text: string | RegExp) => within(list()).getByText(text).closest('li')!;

describe('an ask’s channel', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('reads the messages and Daoris’s lines in order, worded from the lines’ codes', () => {
    render(<ChannelSection channel={CHANNEL()} onPost={() => {}} />);

    const rows = within(list()).getAllByRole('listitem').map((item) => item.textContent ?? '');
    expect(rows[0]).toContain('Read the figure published to db');
    expect(rows[1]).toContain('Read the figure taken by db');
    expect(rows[2]).toContain('Which column holds the figure?');
    // A quest the list names by nothing is named by its id.
    expect(rows[5]).toContain('#q2 done by api');
    expect(within(row('Read the figure published to db')).getByText('Daoris')).toBeInTheDocument();
  });

  /** SESSONE2h: a teammate's ask's channel is read here and posted elsewhere, so it has no composer and no Reply, and says why. */
  it('read-only, draws the messages and the unread count with no composer, no reply and a sentence saying whose ask it is', () => {
    render(<ChannelSection channel={CHANNEL({ unread: 2 })} readOnly onPost={() => {}} />);

    expect(within(list()).getAllByRole('listitem')).toHaveLength(ITEMS.length);
    expect(screen.getByText('2 unread')).toBeInTheDocument();
    expect(screen.queryByRole('textbox')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Send' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Reply' })).toBeNull();
    expect(screen.getByText(/teammate’s ask, held on their machine/)).toBeInTheDocument();
  });

  it('words every code Daoris writes, in either language, and a code it has no words for as it was written', async () => {
    const lines = [LINE('declined', 0), LINE('awaiting', 1), LINE('strangled', 2)];
    const { rerender } = render(<ChannelSection channel={CHANNEL({ items: lines })} onPost={() => {}} />);
    expect(screen.getByText('Read the figure declined by db')).toBeInTheDocument();
    expect(screen.getByText('Read the figure is done and waits for your yes')).toBeInTheDocument();
    expect(screen.getByText('strangled: Read the figure')).toBeInTheDocument();

    await i18n.changeLanguage('zh');
    rerender(<ChannelSection channel={CHANNEL({ items: [LINE('published', 0), LINE('taken', 1), LINE('done', 2), ...lines.slice(0, 2)] })} onPost={() => {}} />);
    expect(screen.getByText('Read the figure 已发布给 db')).toBeInTheDocument();
    expect(screen.getAllByText('db 已接下 Read the figure')).toHaveLength(1);
    expect(screen.getByText('db 已完成 Read the figure')).toBeInTheDocument();
    expect(screen.getByText('db 已谢绝 Read the figure')).toBeInTheDocument();
    expect(screen.getByText('Read the figure 已完成，等你确认')).toBeInTheDocument();
  });

  it('says who wrote a message and to whom: you, a session by its repository and agent, everyone or one repository', () => {
    render(<ChannelSection channel={CHANNEL()} onPost={() => {}} />);

    expect(within(row('The v3 one.')).getByText('You')).toBeInTheDocument();
    expect(row('The v3 one.')).toHaveTextContent('to db');
    expect(row('Which column holds the figure?')).toHaveTextContent('db · claude-code');
    expect(row('Which column holds the figure?')).toHaveTextContent('to everyone');
    expect(row('Noted.')).toHaveTextContent('api · codex');
  });

  it('marks a question no message answers yet, and a question that is answered says only that it was one', () => {
    const { rerender } = render(<ChannelSection channel={CHANNEL()} onPost={() => {}} />);
    expect(within(row('Which column holds the figure?')).getByText('Open question')).toBeInTheDocument();

    rerender(<ChannelSection channel={CHANNEL({ items: ITEMS.map((item) => (item.id === 'm1' ? { ...item, open: false } : item)) })} onPost={() => {}} />);
    expect(within(row('Which column holds the figure?')).queryByText('Open question')).toBeNull();
    expect(within(row('Which column holds the figure?')).getByText('Question')).toBeInTheDocument();
  });

  it('quotes what a message answers on one line, and says so where the channel does not hold it', () => {
    render(<ChannelSection channel={CHANNEL()} onPost={() => {}} />);

    expect(row('The v3 one.')).toHaveTextContent('In reply to “Which column holds the figure?”');
    expect(row('Noted.')).toHaveTextContent('In reply to a message not shown here');
  });

  it('opens a line’s quest, and says nothing a person can press where no door is handed', async () => {
    const onOpenQuest = vi.fn();
    const { rerender } = render(<ChannelSection channel={CHANNEL()} onPost={() => {}} onOpenQuest={onOpenQuest} />);

    await userEvent.click(within(row('Read the figure published to db')).getByRole('button', { name: 'Read the figure published to db' }));
    expect(onOpenQuest).toHaveBeenCalledWith('q1');

    rerender(<ChannelSection channel={CHANNEL()} onPost={() => {}} />);
    expect(within(list()).queryByRole('button', { name: /Read the figure published/ })).toBeNull();
  });

  it('says what is on an empty channel, and says it is being read while it is', () => {
    const { rerender } = render(<ChannelSection channel={CHANNEL({ items: [] })} onPost={() => {}} />);
    expect(screen.getByText('Nothing on the channel yet.')).toBeInTheDocument();

    rerender(<ChannelSection channel={null} onPost={() => {}} />);
    expect(screen.getByText('Reading the channel…')).toBeInTheDocument();
    expect(screen.queryByRole('textbox')).toBeNull();
  });
});

describe('the composer', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  const box = () => screen.getByRole('textbox', { name: 'Write to the channel' });

  it('offers everyone or one of the repositories the channel names as To, and says who reads it', async () => {
    render(<ChannelSection channel={CHANNEL()} repositories={['db']} onPost={() => {}} />);

    expect(screen.getByText('Everyone working on this ask reads it.')).toBeInTheDocument();
    await open('To');
    expect((await screen.findAllByRole('option')).map((option) => option.textContent)).toEqual(['Everyone', 'db', 'api', 'web']);

    await userEvent.click(screen.getByRole('option', { name: 'api' }));
    expect(screen.getByText('The sessions in api read it; the others see it here.')).toBeInTheDocument();
  });

  it('sends the words with the recipient chosen, and the box is cleared once it was kept', async () => {
    const onPost = vi.fn((_post, answered) => answered.done());
    render(<ChannelSection channel={CHANNEL()} onPost={onPost} />);

    await choose('To', 'db');
    await userEvent.type(box(), '  Use the v3 column.  ');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    expect(onPost).toHaveBeenCalledWith({ text: 'Use the v3 column.', to: 'db', re: null }, expect.anything());
    expect(box()).toHaveValue('');
  });

  it('sends to everyone when no repository is chosen, and cannot send nothing', async () => {
    const onPost = vi.fn((_post, answered) => answered.done());
    render(<ChannelSection channel={CHANNEL()} onPost={onPost} />);
    expect(screen.getByRole('button', { name: 'Send' })).toBeDisabled();

    await userEvent.type(box(), 'Everyone: pause the migration.');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    expect(onPost).toHaveBeenCalledWith({ text: 'Everyone: pause the migration.', to: null, re: null }, expect.anything());
  });

  it('answers a message: a press on one fills Reply to, and Reply to lists the messages to choose another', async () => {
    const onPost = vi.fn((_post, answered) => answered.done());
    render(<ChannelSection channel={CHANNEL()} onPost={onPost} />);

    await userEvent.click(within(row('Which column holds the figure?')).getByRole('button', { name: 'Reply' }));
    expect(screen.getByRole('combobox', { name: 'Reply to' })).toHaveTextContent('Which column holds the figure?');
    // Replying to a message is saying it to the repository that wrote it, which the person may still change.
    expect(screen.getByRole('combobox', { name: 'To' })).toHaveTextContent('db');

    await userEvent.type(box(), 'The v3 one.');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));
    expect(onPost).toHaveBeenCalledWith({ text: 'The v3 one.', to: 'db', re: 'm1' }, expect.anything());

    await open('Reply to');
    expect((await screen.findAllByRole('option')).map((option) => option.textContent)).toEqual([
      'No message', 'db · claude-code: Which column holds the figure?', 'You: The v3 one.', 'api · codex: Noted.',
    ]);
  });

  /** A refusal is the service's code, said in the person’s language inside the composer, the words kept. */
  it.each([
    ['empty', 'Write something to send.', '请先写点内容再发送。'],
    ['too-long', 'That is too long for one message: the most is 4,000 characters.', '内容太长，一条消息最多 4,000 个字符。'],
    ['bad-to', 'That repository is not on this ask.', '该仓库不在本需求上。'],
    ['bad-re', 'That message is not on the channel.', '频道里没有那条消息。'],
    ['not-found', 'This ask is gone.', '找不到本需求。'],
    ['strange', 'The channel did not take it: strange', '频道没有接受：strange'],
  ])('says a refusal’s code %s in both languages and keeps the words', async (code, english, chinese) => {
    const onPost = vi.fn((_post, answered) => answered.refused(code));
    render(<ChannelSection channel={CHANNEL()} onPost={onPost} />);

    await userEvent.type(box(), 'Hello');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    expect(screen.getByText(english)).toBeInTheDocument();
    expect(box()).toHaveValue('Hello');

    await i18n.changeLanguage('zh');
    expect(await screen.findByText(chinese)).toBeInTheDocument();
  });

  it('waits while a post is on its way', async () => {
    render(<ChannelSection channel={CHANNEL()} onPost={() => {}} />);

    await userEvent.type(box(), 'Hello');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    expect(screen.getByRole('button', { name: 'Send' })).toBeDisabled();
  });

  it('reads in Chinese: the heading words, To, Reply to and Send', async () => {
    await i18n.changeLanguage('zh');
    render(<ChannelSection channel={CHANNEL()} repositories={['db']} onPost={() => {}} />);

    expect(screen.getByRole('combobox', { name: '发给' })).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: '回复' })).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: '写给频道' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '发送' })).toBeInTheDocument();
    expect(screen.getByText('本需求上的所有会话都会读到。')).toBeInTheDocument();
    const channel = screen.getByRole('list', { name: '频道' });
    expect(within(channel).getByText('待回答的提问')).toBeInTheDocument();
  });
});

describe('what is unread', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('counts what came after the person’s mark and marks those items new', () => {
    render(<ChannelSection channel={CHANNEL({ unread: 3, mark: at(5) })} onPost={() => {}} onRead={() => {}} />);

    expect(screen.getByText('3 unread')).toBeInTheDocument();
    expect(within(row('Noted.')).getByText('new')).toBeInTheDocument();
    expect(within(row('The v3 one.')).queryByText('new')).toBeNull();
    // BADGEUX1: news waits on nobody, so its count wears a neutral ink, never open's, the person's waits' alone.
    expect(screen.getByText('3 unread').className).not.toMatch(/st-open|ink-open/);
  });

  it('moves the mark through the newest item it showed, once, and none where nothing is unread', () => {
    const onRead = vi.fn();
    const { rerender } = render(<ChannelSection channel={CHANNEL({ unread: 3, mark: at(5) })} onPost={() => {}} onRead={onRead} />);
    rerender(<ChannelSection channel={CHANNEL({ unread: 3, mark: at(5) })} onPost={() => {}} onRead={onRead} />);
    expect(onRead).toHaveBeenCalledTimes(1);
    expect(onRead).toHaveBeenCalledWith(at(7));

    onRead.mockClear();
    render(<ChannelSection channel={CHANNEL({ unread: 0, mark: at(7) })} onPost={() => {}} onRead={onRead} />);
    expect(onRead).not.toHaveBeenCalled();
  });

  it('keeps showing what was new this visit after the mark has moved, until the page is left', () => {
    const { rerender } = render(<ChannelSection channel={CHANNEL({ unread: 3, mark: at(5) })} onPost={() => {}} onRead={() => {}} />);
    rerender(<ChannelSection channel={CHANNEL({ unread: 0, mark: at(7) })} onPost={() => {}} onRead={() => {}} />);

    expect(screen.getByText('3 unread')).toBeInTheDocument();
    expect(within(row('Noted.')).getByText('new')).toBeInTheDocument();
  });

  it('counts in Chinese', async () => {
    await i18n.changeLanguage('zh');
    render(<ChannelSection channel={CHANNEL({ unread: 2, mark: at(5) })} onPost={() => {}} onRead={() => {}} />);

    expect(screen.getByText('2 条未读')).toBeInTheDocument();
  });
});

describe('a session’s panel', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  const ONLY = { entry: 'ask:a1/db', repository: 'db' };

  it('is folded to its title and a count, and opens to the channel kept to its line of work', async () => {
    render(<ChannelPanel channel={CHANNEL()} only={ONLY} onPost={() => {}} />);

    const toggle = screen.getByRole('button', { name: /Channel/ });
    expect(toggle).toHaveAttribute('aria-expanded', 'false');
    expect(toggle).toHaveTextContent('Channel · 4');
    expect(screen.queryByRole('list', { name: 'Channel' })).toBeNull();

    await userEvent.click(toggle);
    expect(toggle).toHaveAttribute('aria-expanded', 'true');
    const rows = within(list()).getAllByRole('listitem').map((item) => item.textContent ?? '');
    expect(rows.some((text) => text.includes('Which column holds the figure?'))).toBe(true);
    expect(rows.some((text) => text.includes('Noted.'))).toBe(false);
    expect(rows.some((text) => text.includes('#q2 done by api'))).toBe(false);
  });

  /** The person's mark is the ask's page's to move: a panel reads a part of the channel, and counts nothing unread. */
  it('starts To on its repository, and counts nothing unread', async () => {
    const onPost = vi.fn((_post, answered) => answered.done());
    render(<ChannelPanel channel={CHANNEL({ unread: 3, mark: at(1) })} only={ONLY} onPost={onPost} />);
    await userEvent.click(screen.getByRole('button', { name: /Channel/ }));

    expect(screen.getByRole('combobox', { name: 'To' })).toHaveTextContent('db');
    await userEvent.type(screen.getByRole('textbox', { name: 'Write to the channel' }), 'On it.');
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    expect(onPost).toHaveBeenCalledWith({ text: 'On it.', to: 'db', re: null }, expect.anything());
    expect(screen.queryByText('3 unread')).toBeNull();
    expect(screen.queryByText('new')).toBeNull();
  });

  it('says there is nothing for its repository yet, and opens the ask’s whole channel', async () => {
    const onOpenAsk = vi.fn();
    render(<ChannelPanel channel={CHANNEL({ items: [] })} only={ONLY} onPost={() => {}} onOpenAsk={onOpenAsk} />);
    await userEvent.click(screen.getByRole('button', { name: /Channel/ }));

    expect(screen.getByText('Nothing for db on the channel yet.')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Open the whole channel' }));
    expect(onOpenAsk).toHaveBeenCalledOnce();
  });
});

// COWORK1d (D188; the message-urgency design §7, §8.4): each message names its level where it is urgent, says what it reached in each
// repository it addresses, never more than was observed, and offers *Send to its session* where it waits or never reached one.
describe('what a message reached', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  const REACHED = MESSAGE('m9', 8, {
    urgent: true,
    text: 'Stop: the column moved.',
    receipts: [
      { repository: 'web', state: 'not-reached' },
      { repository: 'api', state: 'taken' },
      { repository: 'docs', state: 'waiting' },
      { repository: 'cli', state: 'answered' },
    ],
  });

  it('names an urgent message so, in words, and says nothing of an ordinary one or one from before', () => {
    render(<ChannelSection channel={CHANNEL({ items: [REACHED, MESSAGE('m8', 9, { urgent: false, text: 'When you can.' }), MESSAGE('m7', 10, { text: 'From before.' })] })} onPost={() => {}} />);

    expect(within(row('Stop: the column moved.')).getByText('urgent')).toBeInTheDocument();
    expect(within(row('When you can.')).queryByText('urgent')).toBeNull();
    expect(within(row('From before.')).queryByText('urgent')).toBeNull();
  });

  it('says what it reached in each repository, and offers Send to its session only where it waits or never reached one', async () => {
    const onForward = vi.fn((_message, _repository, answered) => answered.done());
    render(<ChannelSection channel={CHANNEL({ items: [REACHED] })} onPost={() => {}} onForward={onForward} />);

    const message = row('Stop: the column moved.');
    expect(within(message).getByText('It never reached web’s session: the ask’s work ended first.')).toBeInTheDocument();
    expect(within(message).getByText('taken by api')).toBeInTheDocument();
    expect(within(message).getByText('waiting for docs')).toBeInTheDocument();
    expect(within(message).getByText('answered by cli')).toBeInTheDocument();
    const presses = within(message).getAllByRole('button', { name: /Send to its session/ });
    expect(presses).toHaveLength(2);

    await userEvent.click(presses[0]);
    expect(onForward).toHaveBeenCalledWith('m9', 'web', expect.anything());
    expect(within(message).getByText('Sent to its session.')).toBeInTheDocument();
  });

  it('says why a message was not sent on, by the code the shell answered', async () => {
    const onForward = vi.fn((_message, _repository, answered) => answered.refused('no-part'));
    render(<ChannelSection channel={CHANNEL({ items: [REACHED] })} onPost={() => {}} onForward={onForward} />);

    await userEvent.click(within(row('Stop: the column moved.')).getAllByRole('button', { name: /Send to its session/ })[0]);

    expect(screen.getByText('No session of web’s is on this machine to send it to.')).toBeInTheDocument();
  });

  it('offers nothing to send on in a read-only channel, or where no press is handed', () => {
    render(<ChannelSection channel={CHANNEL({ items: [REACHED] })} readOnly onPost={() => {}} onForward={() => {}} />);
    expect(screen.queryByRole('button', { name: /Send to its session/ })).toBeNull();
  });

  it('reads in Chinese: the level, each receipt and the press', async () => {
    await i18n.changeLanguage('zh');
    render(<ChannelSection channel={CHANNEL({ items: [REACHED] })} onPost={() => {}} onForward={() => {}} />);

    const message = within(screen.getByRole('list', { name: '频道' })).getByText('Stop: the column moved.').closest('li')!;
    expect(within(message).getByText('紧急')).toBeInTheDocument();
    expect(within(message).getByText('它没有送达 web 的会话：需求的工作先结束了。')).toBeInTheDocument();
    expect(within(message).getByText('api 已收下')).toBeInTheDocument();
    expect(within(message).getAllByRole('button', { name: /发给它的会话/ })).toHaveLength(2);
  });
});
