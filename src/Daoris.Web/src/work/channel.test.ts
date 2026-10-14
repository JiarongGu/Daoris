import { describe, expect, it } from 'vitest';
import {
  type ChannelItem, channelOf, elsewhereOf, forEntry, lineWords, newestAt, postedOf, repliesTo, repositoriesOf, sinceMark, unreadSum,
} from './channel';

// An ask's channel as the shell answers it (SESSONE2c, D167 points 6 to 8): the pure side of the page's Channel, so every
// channel a person could have is an argument. The words come from codes, in the reader's language (D142).

const line = (code: string, at: string, over: Partial<ChannelItem> = {}): ChannelItem => (
  { kind: 'line', at, code, quest: 'q1', name: 'Read the figure', from: 'ask #a1', to: 'db', ...over });
const message = (id: string, at: string, over: Partial<ChannelItem> = {}): ChannelItem => (
  { kind: 'message', at, id, person: false, session: 's1', repository: 'db', agent: 'claude-code', entry: 'ask:a1/db', text: 'words', ...over });

const ITEMS: ChannelItem[] = [
  line('published', '2026-10-14T09:00:00Z'),
  message('m1', '2026-10-14T09:02:00Z', { question: true, open: true, text: 'Which column?' }),
  message('m2', '2026-10-14T09:05:00Z', { person: true, session: null, repository: null, agent: null, entry: null, to: 'db', re: 'm1' }),
  message('m3', '2026-10-14T09:06:00Z', { repository: 'api', entry: 'ask:a1/api', session: 's2', to: 'web' }),
  line('done', '2026-10-14T09:07:00Z', { quest: 'q2', to: 'api', from: 'web' }),
];

const t = (key: string, options?: Record<string, unknown>) => `${key}${options ? ` ${JSON.stringify(options)}` : ''}`;

describe('an ask’s channel', () => {
  it('offers as recipients every repository the channel names, once, in the order it met them', () => {
    expect(repositoriesOf(ITEMS, ['db'])).toEqual(['db', 'api', 'web']);
    // An ask's own words as an asker are no repository.
    expect(repositoriesOf([line('published', '2026-10-14T09:00:00Z', { from: 'ask #a1', to: 'db' })], [])).toEqual(['db']);
    expect(repositoriesOf([], [])).toEqual([]);
  });

  it('keeps for a session’s line of work what was said to everyone, to its repository, by it, and its own quests’ lines', () => {
    const mine = forEntry(ITEMS, { entry: 'ask:a1/db', repository: 'db' });

    expect(mine.map((item) => item.id ?? item.code)).toEqual(['published', 'm1', 'm2']);
    // Case is the registry's, as the service matches a `to`.
    expect(forEntry(ITEMS, { entry: 'ask:a1/api', repository: 'API' }).map((item) => item.id ?? item.code)).toEqual(['m1', 'm3', 'done']);
  });

  it('finds the message another answers, and none where it is not held', () => {
    expect(repliesTo(ITEMS, 'm1')?.id).toBe('m1');
    expect(repliesTo(ITEMS, 'gone')).toBeNull();
    expect(repliesTo(ITEMS, null)).toBeNull();
  });

  it('knows the newest moment, and which items are past a mark', () => {
    expect(newestAt(ITEMS)).toBe('2026-10-14T09:07:00Z');
    expect(newestAt([])).toBeNull();
    expect(sinceMark(ITEMS, '2026-10-14T09:05:00Z').map((item) => item.id ?? item.code)).toEqual(['m3', 'done']);
    expect(sinceMark(ITEMS, null)).toHaveLength(5);
  });

  it('words each of Daoris’s lines from its code, and says a code it has no words for as it was written', () => {
    expect(lineWords(t, line('published', 'x'))).toBe('asks.channel.line.published {"quest":"Read the figure","to":"db"}');
    expect(lineWords(t, line('taken', 'x'))).toContain('asks.channel.line.taken');
    expect(lineWords(t, line('done', 'x'))).toContain('asks.channel.line.done');
    expect(lineWords(t, line('declined', 'x'))).toContain('asks.channel.line.declined');
    expect(lineWords(t, line('awaiting', 'x'))).toContain('asks.channel.line.awaiting');
    expect(lineWords(t, line('strangled', 'x'))).toBe('asks.channel.line.other {"code":"strangled","quest":"Read the figure"}');
    // A quest with no name is named by its id.
    expect(lineWords(t, line('done', 'x', { name: null, quest: 'q9' }))).toContain('"quest":"#q9"');
  });

  /** A shell before the channel answers something else, which is no channel: the section is then not drawn. */
  it('reads the shell’s answer as a channel, and an answer of another shape as none', () => {
    expect(channelOf({ ask: 'a1', entry: 'person', items: ITEMS, unread: 2, mark: '2026-10-14T09:00:00Z' }))
      .toEqual({ ask: 'a1', entry: 'person', items: ITEMS, unread: 2, mark: '2026-10-14T09:00:00Z' });
    expect(channelOf({ ask: 'a1', entry: 'person', items: [{ kind: 'message' }, ...ITEMS, 'junk'], unread: 'many' }))
      .toEqual({ ask: 'a1', entry: 'person', items: ITEMS, unread: 0, mark: null });
    expect(channelOf({ drivable: [] })).toBeNull();
    expect(channelOf(null)).toBeNull();
    expect(postedOf({ posted: false, code: 'bad-to' })).toEqual({ posted: false, code: 'bad-to', message: null });
    expect(postedOf({ posted: true, message: ITEMS[1] })).toEqual({ posted: true, code: null, message: ITEMS[1] });
    expect(postedOf('no')).toEqual({ posted: false, code: 'other', message: null });
  });

  /** SESSONE2h: the asks the mirrored messages name that this machine does not hold; a shell before the route answers something else. */
  it('reads the asks held elsewhere, and an answer of another shape as no list', () => {
    const held = { id: '7e6f51', workspace: 'default', origins: ['key-a'], latest: '2026-10-15T09:00:00Z', messages: 2, unread: 1 };
    expect(elsewhereOf({ asks: [held] })).toEqual([held]);
    // A row without its id or its moment is passed over; a count that is not a number is none; origins that are not names are dropped.
    expect(elsewhereOf({ asks: [{ workspace: 'default' }, { ...held, id: 'b2', messages: 'x', unread: null, origins: ['k', 3] }, 'junk'] }))
      .toEqual([{ ...held, id: 'b2', messages: 0, unread: 0, origins: ['k'] }]);
    expect(elsewhereOf({ drivable: [] })).toBeNull();
    expect(elsewhereOf(null)).toBeNull();
  });

  it('adds the unread each ask answers', () => {
    expect(unreadSum({ a1: 2, a2: 0, a3: 5 })).toBe(7);
    expect(unreadSum({})).toBe(0);
  });
});
