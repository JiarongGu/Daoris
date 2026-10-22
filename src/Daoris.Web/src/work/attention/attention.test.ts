import { describe, expect, it } from 'vitest';
import i18n from '../../locales/i18n';
import type { Ask, Quest, Session } from '../../data/api';
import type { RuleProposal } from '../../settings/repository/AgentRules';
import type { Consideration } from '../../data/signals';
import { byTool } from '../../agents/tools';
import type { AccountRowFacts, NamedAccount } from './accountWords';
import type { Attention } from './AttentionRow';
import type { SessionGrouping } from '../groups';
import {
  ASKS_ONCE, ATTENTION_GROUP, ATTENTION_PLACE, attentionActs, attentionGroup, attentionGroups, attentionInOrder, attentionOf, attentionOffers,
  attentionWorded, type AttentionNames, needsListed, placeCounts, placeOf, type PushedAttention, type QuietWait, staleGoAheads,
} from './attention';
import { entryDetail } from '../head/milestone';

// *What needs you* on the page (PUSH1d4): who needs the person is judged in .NET (`NeedsAPerson`, whose cases
// `AttentionStateSliceTests` ports from this file cell for cell); the page orders the pushed rows, names each from the records it
// holds and words it in the reader's language. The rows here are as the shell pushes them.

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4',
  repository: 'engine',
  adapter: 'claude-code',
  state: 'working',
  kind: 'driven',
  created: '2026-09-21T09:00:00Z',
  updated: '2026-09-21T10:00:00Z',
  ...over,
});

const quest = (over: Partial<Quest> = {}): Quest => ({
  id: '7a82cc',
  from: 'platform',
  to: 'engine',
  title: 'Expose a streaming budget on the chunk API',
  body: 'World streaming needs a per-frame cap.',
  status: 'Open',
  filed: '2026-09-14T09:00:00Z',
  updated: '2026-09-14T09:00:00Z',
  ...over,
});

const ask = (over: Partial<Ask> = {}): Ask => ({
  id: '7c1e9a04b2d5',
  workspace: 'aurora',
  sentence: 'The chunk streamer stalls on a cold cache.\n\nThe trace is attached.',
  state: 'Proposed',
  tier: 'declarations',
  asked: '2026-09-21T08:30:00Z',
  updated: '2026-09-21T08:30:00Z',
  links: [],
  attachments: [],
  proposal: [],
  quests: [],
  ...over,
});

/** A row as the shell pushes it. */
const row = (kind: PushedAttention['kind'], id: string, since: string | null, over: Partial<PushedAttention> = {}): PushedAttention =>
  ({ kind, id, since, ...over });

const words = (pushed: PushedAttention, names: AttentionNames = {}) => attentionWorded(pushed, names);
const keyed = (rows: readonly PushedAttention[]) => attentionInOrder(rows).map((each) => `${each.kind}:${each.id}`);

describe('what needs a person, worded', () => {
  it('names a parked session by its derived identity, with the analysis it is waiting on', () => {
    const pushed = row('parked', 's1a2b3c4', '2026-09-21T10:00:00Z', { workspace: 'default', where: 'engine', note: { note: 'two ways forward.' } });

    expect(words(pushed, { sessions: [session({ quest: '7a82cc', state: 'awaiting-person' })], quests: [quest({ status: 'Taken' })] })).toEqual({
      id: 's1a2b3c4',
      kind: 'parked',
      title: 'Expose a streaming budget on the chunk API',
      where: 'engine',
      since: '2026-09-21T10:00:00Z',
      note: { note: 'two ways forward.' },
    });
  });

  /** LANG1b: the record's note travels whole, its parts beside its English, for the row to word in the reader's language. */
  it('hands the row a parked session’s note with its parts, never an English sentence made of them', () => {
    const noteParts = [{ code: 'ended.parked-asked', values: {}, text: 'It stopped with its quest still taken, to ask you:' }];
    const item = words(row('parked', 's1a2b3c4', null, { where: 'engine', note: { note: 'It stopped…', parts: noteParts } }));
    expect(item.note).toEqual({ note: 'It stopped…', parts: noteParts });
    expect(item.detail).toBeUndefined();
    // A record not in hand is named by its id.
    expect(item.title).toBe('#s1a2b3c4');
  });

  /**
   * CONFIRMSEEN1: a terminal's press waiting for the window, named as its card names it, by its act where the page words its route and
   * by its route otherwise; the terminal as the system names it, and until when; never a value; no act of its own, in each language.
   */
  it("names a terminal's press as its card does, says what sent it and until when, and offers no act", async () => {
    const waiting = row('confirmation', 'c1', '2026-10-20T09:00:00Z', {
      confirmation: {
        module: 'DAORIS.DRIVER', route: 'SESSION_ARCHIVE', asker: { process: 'daoris-driver', pid: 4242 },
        expires: '2026-10-20T09:10:00Z', shownAt: null,
      },
    });
    const item = words(waiting);
    expect(item.title).toBe('A terminal asks to archive sessions on this machine, or bring them back.');
    expect(item.detail).toContain('Sent by daoris-driver, process 4242.');
    // NOTIFY2b: its card is in Notifications, never at the top of the window.
    expect(item.detail).toContain('It runs only once you confirm it on its card in Notifications');
    expect(attentionActs(item)).toEqual([]);
    // Every route the census makes the person's is worded since NOTIFY2b; one no catalogue words is named by its route.
    expect(words(row('confirmation', 'c2', null, { confirmation: { ...waiting.confirmation!, route: 'SET_MYSTERY', asker: null } })))
      .toMatchObject({ title: 'A terminal asks for SET_MYSTERY.', detail: expect.stringContaining('Daoris could not name what sent it.') });
    await i18n.changeLanguage('zh');
    try {
      expect(words(waiting).title).toBe('一个终端请求在本机归档会话，或将其恢复。');
      expect(words(waiting).detail).toContain('只有你在通知中它的卡片上确认后它才会运行');
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  /**
   * KEEP1f (the cleanup design §3.2): a volume still below its floor, named by its free space where it is mounted; its floor, what Daoris
   * freed of what it can make again, and what *Free up space…* could free; no act of its own, its door the press, in each language.
   */
  it('names a volume below its floor by its free space, says its floor, what Daoris freed and what the press could, and offers no act', async () => {
    const gb = 2 ** 30;
    const low = row('space', '\\\\?\\Volume{A}\\', '2026-10-22T09:00:00Z', {
      space: { root: 'C:\\', free: 4 * gb, total: 100 * gb, floor: 10 * gb, freed: 2 * gb, could: 3 * gb },
    });
    const item = words(low);
    expect(item.title).toBe('4 GB free of 100 GB on C:\\');
    expect(item.detail).toBe(
      'Below the floor of 10 GB Daoris keeps free. Daoris freed 2 GB of what it can make again; the rest is yours to choose. '
      + 'Free up space… could free about 3 GB more.');
    expect(attentionActs(item)).toEqual([]);
    expect(attentionGroup(item)).toBe('word');
    // Nothing freed, and no list read: said so, and no press's size.
    expect(words(row('space', 'v2', null, { space: { ...low.space!, freed: 0, could: null } })).detail)
      .toBe('Below the floor of 10 GB Daoris keeps free. Daoris found nothing it can make again to free; the rest is yours to choose.');
    await i18n.changeLanguage('zh');
    try {
      expect(words(low).title).toBe('C:\\ 共 100 GB，可用 4 GB');
      expect(words(low).detail).toContain('道衍已释放 2 GB 可重新生成的内容');
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  /**
   * GITAUTH1a (the git auth design §5.2, §7.4): a held git remote, said once: its host and why as its title, per class; its repositories,
   * that Daoris does not try it again until *Try again* or a change, and git's own words, as written; *Try again* its one act; listed
   * with what waits for the person's word, after a volume below its floor, in each language.
   */
  it('names a held git remote by its host and why, says its repositories and git’s words, and offers Try again', async () => {
    const git: NonNullable<PushedAttention['git']> = {
      host: 'github.com', class: 'refused', holds: 'all', remote: 'origin', repositories: ['engine', 'game'],
      words: 'git@github.com: Permission denied (publickey).',
    };
    const held = row('git-held', 'default|github.com|refused|all', '2026-10-22T09:00:00Z', { workspace: 'default', git });
    const item = words(held);
    expect(item).toMatchObject({
      kind: 'git-held', title: 'github.com refused the sign-in git has here', where: 'origin', git, workspace: 'default',
      detail: 'engine, game: Daoris does not try this git remote again until you press Try again, or its address or the git Daoris runs '
        + 'changes. git said: git@github.com: Permission denied (publickey).',
    });
    expect(attentionActs(item)).toEqual(['git-try-again']);
    expect(attentionGroup(item)).toBe('word');
    const titled = (each: typeof git.class) => words(row('git-held', each, null, { git: { ...git, class: each } })).title;
    expect(titled('write-refused')).toBe('github.com refused a push with the sign-in git has here');
    expect(titled('host-unknown')).toBe("ssh does not know github.com's key yet");
    expect(titled('host-changed')).toBe("github.com's key changed since ssh last trusted it");
    // GITAUTH1b: a repository's own ssh command, held before any act, is lifted by removing it, and git said nothing.
    const own = words(row('git-held', 'own', null, { git: { ...git, class: 'own-input', words: '' } }));
    expect(own.title).toBe("github.com is reached by the repository's own ssh command, past Daoris's door");
    expect(own.detail).toMatch(/^engine, game: the repository names its own ssh command \(core\.sshCommand\).*until that setting is removed from the repository\.$/);
    // A row from a shell that sent no group offers nothing to lift.
    expect(attentionActs(words(row('git-held', 'bare', null)))).toEqual([]);
    // After a volume below its floor, before a quest nobody can take, where they waited as long.
    const at = '2026-10-22T09:00:00Z';
    expect(keyed([row('unanswerable', 'q', at), held, row('space', 'D:', at)]))
      .toEqual(['space:D:', `git-held:${held.id}`, 'unanswerable:q']);
    await i18n.changeLanguage('zh');
    try {
      expect(words(held).title).toBe('github.com 拒绝了这里 Git 的登录');
      expect(titled('write-refused')).toBe('github.com 拒绝了用这里 Git 的登录推送');
      expect(titled('host-unknown')).toBe('ssh 还不认识 github.com 的主机密钥');
      expect(titled('host-changed')).toBe('github.com 的主机密钥自 ssh 上次信任后已改变');
      expect(words(held).detail).toBe('engine, game：在你按“重试”、或它的地址或 Daoris 运行的 Git 改变之前，Daoris 不会再尝试这个远程仓库。'
        + 'Git 的原话：git@github.com: Permission denied (publickey).');
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  it('explains an unanswerable quest, in the active language', async () => {
    const pushed = row('unanswerable', '7a82cc', '2026-09-14T09:00:00Z', { where: 'retired' });
    expect(words(pushed).detail).toContain('retired');
    await i18n.changeLanguage('zh');
    try {
      expect(words(pushed).detail).toContain('本部署');
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  /** A parked session is holding a working tree while it waits; a sitting quest is holding nothing. */
  it('puts parked sessions ahead of quests, and the oldest first within each', () => {
    expect(keyed([
      row('parked', 'newer', '2026-09-21T11:00:00Z'), row('parked', 'older', '2026-09-21T08:00:00Z'),
      row('unanswerable', 'q-new', '2026-09-20T00:00:00Z'), row('unanswerable', 'q-old', '2026-09-01T00:00:00Z'),
    ])).toEqual(['parked:older', 'parked:newer', 'unanswerable:q-old', 'unanswerable:q-new']);
  });
});

describe('an ask that waits on a person, worded', () => {
  it('is a proposal: its first line, its circle, and what was proposed', () => {
    expect(words(row('proposal', '7c1e9a04b2d5', '2026-09-21T08:30:00Z', { where: 'aurora', publishTo: ['engine', 'game'], choices: [] }), { asks: [ask()] }))
      .toEqual({
        id: '7c1e9a04b2d5',
        kind: 'proposal',
        title: 'The chunk streamer stalls on a cold cache.',
        where: 'aurora',
        since: '2026-09-21T08:30:00Z',
        detail: 'The declarations propose engine, game. Nothing is published until you choose.',
        publishTo: ['engine', 'game'],
        choices: [],
      });
  });

  it('carries the refused receiver\'s sentence verbatim rather than the proposal', () => {
    const note = '`newcomer` is not an adopted repository in `aurora` — only an adopter can be asked.';
    expect(words(row('proposal', '7c1e9a04b2d5', null, { detail: note, publishTo: [] })).detail).toBe(note);
  });

  it('says so when the declarations proposed nobody', () => {
    expect(words(row('proposal', '7c1e9a04b2d5', null, { publishTo: [] })).detail).toMatch(/No repository's declarations share its words/);
  });

  it('is the ask’s one row where its intake parked asking, with the intake’s own note', () => {
    const note = 'published nothing: the declarations did not settle ask `#7c1e9a04b2d5`, so it asks you rather than guess.';
    expect(words(row('intake', '7c1e9a04b2d5', '2026-09-21T09:45:00Z', { where: 'aurora', note: { note } }), { asks: [ask()] })).toEqual({
      id: '7c1e9a04b2d5', kind: 'intake', title: 'The chunk streamer stalls on a cold cache.', where: 'aurora', since: '2026-09-21T09:45:00Z', note: { note },
    });
  });

  /**
   * UX6c (design §6.2): an ask waits for the person's word, after what holds work, and within that group what has waited
   * longest comes first, whatever its kind: a quest nobody can take, sitting since September, before this morning's asks.
   */
  it('waits for the person’s word after what holds work, the longest waiting first within the group', () => {
    expect(keyed([
      row('parked', 'parked', '2026-09-21T11:00:00Z'), row('unanswerable', 'sitting', '2026-09-01T00:00:00Z'),
      row('proposal', 'later', '2026-09-21T10:00:00Z'), row('intake', '7c1e9a04b2d5', '2026-09-21T07:00:00Z'),
    ])).toEqual(['parked:parked', 'unanswerable:sitting', 'intake:7c1e9a04b2d5', 'proposal:later']);
  });

  it('explains a proposal in the active language', async () => {
    await i18n.changeLanguage('zh');
    try {
      const item = words(row('proposal', '7c1e9a04b2d5', null, { publishTo: ['engine', 'game'] }));
      expect(item.detail).toContain('engine, game');
      expect(item.detail).not.toContain('declarations');
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});

describe('a folder waiting on the person\'s trust, worded', () => {
  const hold = { folder: 'C:/somewhere/engine', trustFile: 'C:/somewhere/data/harnesses/claude-code/work/.claude.json' };
  const trusted = (over: Partial<PushedAttention> = {}) =>
    row('trust', `${hold.trustFile}\n${hold.folder}`, '2026-09-10T09:00:00Z', { where: 'engine', trust: hold, quest: 'q1', ...over });

  it('names the folder, where it holds, and why only the person grants it', () => {
    const item = words(trusted());
    expect(item).toMatchObject({ kind: 'trust', title: 'C:/somewhere/engine', where: 'engine', since: '2026-09-10T09:00:00Z', trust: hold, quest: 'q1' });
    expect(item.detail).toContain('permissions.allow');
  });

  /**
   * POOL1c (D197 point 3; the account-pool design §3 point 4, §4.3): a hold on your own sign-in's file, the row's `trust.own`,
   * offers no grant, since Daoris writes nothing there, and says to trust the folder with the tool itself, at your terminal.
   */
  it('offers no grant on your own sign-in’s file, and says to trust it with the tool itself', () => {
    const own = { ...hold, own: true };
    const item = words(trusted({ trust: own }));
    expect(item.trust).toEqual(own);
    expect(item.detail).toBe(
      "This conversation goes on in your own sign-in, outside Daoris's accounts, so Daoris writes nothing in its file: trust the folder with the tool itself, at your terminal.",
    );
    expect(attentionActs(item)).toEqual([]);
    expect(attentionActs(words(trusted()))).toEqual(['trust']);
  });

  it('names an intake\'s room by its ask, and a quest not in hand by its id', () => {
    expect(words(trusted({ where: null, quest: undefined, ask: '7c1e9a04b2d5' }))).toMatchObject({ where: 'ask #7c1e9a04b2d5', ask: '7c1e9a04b2d5' });
    expect(words(trusted({ where: null })).where).toBe('#q1');
  });

  /** Nothing it holds can start until it is granted, so it holds work, ahead of every ask; beside a park, the one waiting longer first. */
  it('holds work, ahead of the asks, beside the parked sessions by how long each has waited', () => {
    expect(attentionInOrder([
      row('proposal', '7c1e9a04b2d5', '2026-09-21T08:30:00Z'), row('parked', 'parked', '2026-09-21T09:00:00Z'), trusted({ since: '2026-09-14T09:00:00Z' }),
    ]).map((each) => each.kind)).toEqual(['trust', 'parked', 'proposal']);
  });
});

describe('a proposal to widen the rules, worded', () => {
  const proposal: RuleProposal = {
    id: 'p0000002', state: 'waiting', action: 'add', scope: 'machine', list: 'allow', rule: 'WebFetch',
    why: 'The docs it needs are on the web.', session: 'i9n8t7k6', ask: 'a1b2c3', proposed: '2026-09-24T11:00:00Z',
  };

  it('says what it would change, who proposed it and why', () => {
    const item = words(row('rule', 'p0000002', '2026-09-24T11:00:00Z', { detail: proposal.why, proposal }));

    expect(item).toMatchObject({ kind: 'rule', id: 'p0000002', since: '2026-09-24T11:00:00Z', proposal });
    expect(item.title).toBe('allow WebFetch for every session on this machine');
    expect(item.where).toBe('session i9n8t7k6 (ask #a1b2c3)');
    expect(item.detail).toBe('The docs it needs are on the web.');
  });

  /** It waits for the person's word beside the asks and the quests nobody can take, the longest waiting first (UX6c). */
  it('waits for the person’s word, with the asks and the quests nobody can take, the longest waiting first', () => {
    expect(attentionInOrder([
      row('rule', 'p0000002', '2026-09-24T11:00:00Z'), row('proposal', '7c1e9a04b2d5', '2026-09-21T08:30:00Z'),
      row('unanswerable', '7a82cc', '2026-09-14T09:00:00Z'),
    ]).map((each) => each.kind)).toEqual(['unanswerable', 'proposal', 'rule']);
  });
});

describe('a quest parked on its failed sessions, worded', () => {
  const parked: Consideration = {
    quest: '7a82cc', repository: 'engine', verdict: 'Exhausted', strikes: 3, since: '2026-10-01T09:21:00+00:00',
    reason: '3 session(s) have failed on `#7a82cc` without landing anything — parked, because trying again spends an account rather than making progress. `daoris driver retry 7a82cc` starts it again once you know why.',
  };
  const pushed = row('parked-quest', '7a82cc', '2026-10-01T09:21:00+00:00', { where: 'engine', sitting: parked });

  it('says the quest, where it is addressed, since its last session ended, and why it sits in the driver’s words', () => {
    expect(words(pushed, { quests: [quest({ status: 'Taken' })] })).toEqual({
      id: '7a82cc', kind: 'parked-quest', title: 'Expose a streaming budget on the chunk API', where: 'engine',
      since: '2026-10-01T09:21:00+00:00', detail: parked.reason,
    });
    expect(words(pushed).title).toBe('#7a82cc');
  });

  it('holds work beside the parked sessions and the folders held for trust, the longest waiting first', () => {
    expect(keyed([
      row('parked', 'asking', '2026-10-01T12:00:00Z'),
      row('trust', 'C:/somewhere/.claude.json\nC:/somewhere/engine', '2026-09-14T09:00:00Z'),
      row('parked-quest', 'q-new', '2026-10-01T10:00:00+00:00'), row('parked-quest', 'q-old', '2026-09-30T10:00:00+00:00'),
    ])).toEqual(['trust:C:/somewhere/.claude.json\nC:/somewhere/engine', 'parked-quest:q-old', 'parked-quest:q-new', 'parked:asking']);
  });

  /** Why it sits, in the reader's language, by the verdict and the number the tick carries (never its English). */
  it('says why it sits in 中文', async () => {
    await i18n.changeLanguage('zh');
    try {
      const item = words(pushed);
      expect(item.detail).toMatch(/3 个会话/);
      expect(item.detail).toContain('`daoris driver retry 7a82cc`');
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});

describe('a go-ahead asked on an ask, worded', () => {
  const goAhead = { kind: 'push', on: 'prod', act: 'the report’s menu entries' };
  const pushed = (over: Partial<PushedAttention> = {}) => row('go-ahead', '7c1e9a04b2d5#1', '2026-09-21T09:10:00Z', {
    ask: '7c1e9a04b2d5', number: 1, goAhead, detail: 'The menu ships with the release.', session: 's1a2b3c4', ...over,
  });

  it('names the act, its ask, and why in the session’s words', () => {
    expect(words(pushed())).toEqual({
      id: '7c1e9a04b2d5#1', kind: 'go-ahead', ask: '7c1e9a04b2d5', number: 1, title: 'push on prod: “the report’s menu entries”',
      where: 'ask #7c1e9a04b2d5', since: '2026-09-21T09:10:00Z', detail: 'The menu ships with the release.', session: 's1a2b3c4',
    });
  });

  /** A kind this page has no word for is shown as the service wrote it; one it has is said in the reader's language. */
  it('names the act’s kind in the reader’s language where it can, and as written where it cannot', async () => {
    expect(words(pushed({ goAhead: { ...goAhead, kind: 'migrate' } })).title).toBe('migrate on prod: “the report’s menu entries”');
    await i18n.changeLanguage('zh');
    try {
      const item = words(pushed());
      expect(item.title).toBe('在 prod 上推送：“the report’s menu entries”');
      expect(item.where).toBe('需求 #7c1e9a04b2d5');
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  it('holds work: after a parked session that waited longer, before every ask', () => {
    expect(attentionInOrder([
      row('proposal', 'proposed', '2026-09-20T09:00:00Z'), pushed(), row('parked', 'parked', '2026-09-21T09:00:00Z'),
    ]).map((each) => each.kind)).toEqual(['parked', 'go-ahead', 'proposal']);
  });
});

/**
 * GOAHEADSTALE1 (the owner's case, 2026-10-21): go-aheads sat in What needs you for days after their asks' work was done another way.
 * .NET marks one nothing on its ask could still use; the page says so with its age, lists it where nothing is held, and offers only
 * the refusal, which runs nothing.
 */
describe('a stale go-ahead, worded', () => {
  const goAhead = { kind: 'write', on: 'production', act: 'the dashboard configuration' };
  const NOW = new Date('2026-10-01T12:00:00Z');
  const stale = (since: string | null = '2026-09-25T09:00:00Z', over: Partial<PushedAttention> = {}) =>
    row('go-ahead', '7c1e9a04b2d5#2', '2026-09-21T09:10:00Z', {
      workspace: 'aurora', ask: '7c1e9a04b2d5', number: 2, goAhead, detail: 'The tile reads its target there.', session: 's1a2b3c4',
      stale: { since }, ...over,
    });

  it('says it is stale, for how long nothing on its ask has needed it, before why its session asked', async () => {
    const item = attentionWorded(stale(), {}, NOW);
    expect(item).toMatchObject({ kind: 'go-ahead', stale: { since: '2026-09-25T09:00:00Z' }, since: '2026-09-21T09:10:00Z' });
    expect(item.detail).toBe('Stale for 6d 3h: no session on ask #7c1e9a04b2d5 runs or waits, and none of its quests is open. '
      + 'The tile reads its target there.');
    expect(attentionWorded(stale(null, { detail: null }), {}, NOW).detail)
      .toBe('Stale: no session on ask #7c1e9a04b2d5 runs or waits, and none of its quests is open.');
    await i18n.changeLanguage('zh');
    try {
      expect(attentionWorded(stale(), {}, NOW).detail).toBe('已过时 6 天 3 小时：需求 #7c1e9a04b2d5 上没有会话在运行或等待，也没有仍开放的委托。'
        + 'The tile reads its target there.');
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  it('offers only the refusal: a yes nothing on its ask waits for would be handed to whatever starts there next', () => {
    expect(attentionActs(attentionWorded(stale(), {}, NOW))).toEqual(['refuse']);
    expect(attentionActs(attentionWorded(stale(undefined, { stale: undefined }), {}, NOW))).toEqual(['approve', 'refuse']);
  });

  /** It holds no session's act, so it waits for the person's word with the asks, and is counted there: the list and the count agree. */
  it('is listed under Waiting for your word, where nothing it holds is running', () => {
    expect(attentionGroup(stale())).toBe('word');
    expect(attentionGroup(stale(undefined, { stale: undefined }))).toBe('holding');
    const rows = attentionOf(attentionInOrder([stale(), row('proposal', 'proposed', '2026-09-22T09:00:00Z'), row('parked', 'p1', '2026-09-30T09:00:00Z')]), {}, NOW);
    expect(attentionGroups(rows).map(({ group, items }) => [group, items.map((item) => item.id)])).toEqual([
      ['holding', ['p1']], ['word', ['7c1e9a04b2d5#2', 'proposed']],
    ]);
  });

  it('gathers the stale go-aheads a list holds, in its order, for the one refusal', () => {
    const rows = attentionOf([stale(), row('go-ahead', 'b#1', '2026-09-22T09:00:00Z', { ask: 'b', number: 1, goAhead }), stale(undefined, { id: 'c#3', ask: 'c', number: 3 })], {}, NOW);
    expect(staleGoAheads(rows).map((item) => item.id)).toEqual(['7c1e9a04b2d5#2', 'c#3']);
  });
});

describe('a departure that waits for your yes, worded', () => {
  it('says the departed requirement with its reason, or how many it departed from', () => {
    const one = row('departure', '7a82cc', '2026-09-22T09:00:00Z', { where: 'engine', departed: { count: 1, number: 2, reason: 'the scheduler holds the budget instead' } });
    expect(words(one, { quests: [quest()] })).toEqual({
      id: '7a82cc', kind: 'departure', title: 'Expose a streaming budget on the chunk API', where: 'engine', since: '2026-09-22T09:00:00Z',
      detail: 'Requirement 2 departed: the scheduler holds the budget instead',
    });
    expect(words({ ...one, departed: { count: 2, number: 1, reason: 'no per-frame cap' } }).detail).toBe('2 requirements departed; requirement 1: no per-frame cap');
  });
});

describe('work to review, worded', () => {
  const placed: SessionGrouping = {
    session: 'r3v13w00', group: 'review', shown: 'completed', archived: false, teammate: false, work: { commits: 3, uncommitted: 0 },
  };

  it('names the session from its record, and says what its tree holds', () => {
    expect(words(row('review', 'r3v13w00', '2026-09-21T11:00:00Z', { where: 'engine', placed }), {
      sessions: [session({ id: 'r3v13w00', quest: '7a82cc', state: 'completed' })], quests: [quest({ status: 'Done' })],
    })).toEqual({
      id: 'r3v13w00', kind: 'review', title: 'Expose a streaming budget on the chunk API', where: 'engine', since: '2026-09-21T11:00:00Z',
      detail: '3 commits to review',
    });
    expect(words(row('review', 'r3v13w00', null, { where: '', placed }))).toMatchObject({ title: '#r3v13w00', where: '' });
  });

  it('comes last: it holds nothing and waits on nobody', () => {
    expect(keyed([row('review', 'r3v13w00', '2026-09-21T08:00:00Z'), row('parked', 'parked', '2026-09-21T12:00:00Z')]))
      .toEqual(['parked:parked', 'review:r3v13w00']);
  });
});

/** UX6c (design §6.2): the three groups, in the order a person acts on them, and none shown empty. */
describe('the groups', () => {
  it('puts each kind in its group', () => {
    expect(ATTENTION_GROUP).toEqual({
      parked: 'holding', 'parked-quest': 'holding', 'go-ahead': 'holding', permission: 'holding', answer: 'holding',
      confirmation: 'holding', trust: 'holding', quiet: 'holding',
      'account-wait': 'holding', 'signed-out': 'holding',
      proposal: 'word', intake: 'word', departure: 'word', 'set-up': 'word', opinion: 'word', rule: 'word', helper: 'word',
      'hand-over': 'word', unanswerable: 'word', review: 'ready', work: 'word', space: 'word', 'git-held': 'word',
    });
  });

  /** NOTIFY2e: an agent's ask to hand its session over holds work once its session stands still on it, its turn ended. */
  it('puts an ask to hand a session over in Holding work once its session stands still on it', () => {
    expect(attentionGroup(row('hand-over', 'h1', null, { stands: true }))).toBe('holding');
    expect(attentionGroup(row('hand-over', 'h2', null, { stands: false }))).toBe('word');
    expect(attentionGroup(row('hand-over', 'h3', null))).toBe('word');
    const ordered = attentionInOrder([
      row('hand-over', 'h2', '2026-09-21T08:00:00Z', { stands: false }), row('hand-over', 'h1', '2026-09-21T09:00:00Z', { stands: true }),
    ]);
    expect(ordered.map(({ id }) => id)).toEqual(['h1', 'h2']);
    expect(attentionGroups(attentionOf(ordered)).map(({ group, items }) => [group, items.map(({ id }) => id)]))
      .toEqual([['holding', ['h1']], ['word', ['h2']]]);
  });

  /**
   * OVERVIEWNOISE1b: a tree its session recorded and no branch record follows is named by that session as Sessions names it, and its
   * block is worded by its code in the reader's language, the reason said once, the tree by its folder's name and never its path.
   */
  it('names a unit by the session that recorded its tree, and words its block by its code, once, in each language', async () => {
    const reopened = row('work', 'c0nv0001', null, {
      where: 'engine', session: 'c0nv0001', title: 's-e0a8e0fd',
      blocked: {
        unknown: false,
        entries: [{
          kind: 'tree', resource: 'X:/daoris/trees/default/engine/s-e0a8e0fd', code: 'unfollowed', facts: { branch: 'daoris/s-e0a8e0fd' },
          detail: 'its session recorded it, but no branch record follows `daoris/s-e0a8e0fd`, so the look’s tidy never takes it',
        }],
      },
    });
    const names = { sessions: [session({ id: 'c0nv0001', quest: '7a82cc' })], quests: [quest()] };

    const item = words(reopened, names);
    expect(item).toMatchObject({ title: 'Expose a streaming budget on the chunk API', where: 'engine', session: 'c0nv0001' });
    expect(item.detail).toBe(
      'Blocked: tree s-e0a8e0fd, its session recorded it, but no branch record follows daoris/s-e0a8e0fd, so the look never cleans it up: '
      + 'the session branches on its workspace’s Branches remove it');
    expect(attentionActs(item)).toEqual([]);
    // A session the page does not hold: the tree's folder, as the row was titled.
    expect(words(reopened).title).toBe('s-e0a8e0fd');

    await i18n.changeLanguage('zh');
    try {
      const said = words(reopened, names).detail ?? '';
      expect(said).toBe('受阻：工作树 s-e0a8e0fd，它的会话记录了它，但没有分支记录跟随 daoris/s-e0a8e0fd，所以查看时不会清理它：在它所在工作区的“分支”中，会话分支列表可以移除它');
      expect(said).not.toMatch(/[a-z]{3,} [a-z]{3,}/);
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  /** OVERVIEWNOISE1b: an owner nothing records is said once, by the entry's own code, with the press that removes it. */
  it('words an unknown owner by its code once, in each language, with the press that removes it', async () => {
    const orphan = row('work', 'unknown-branch:engine/daoris/orphan', null, {
      where: 'engine', session: null, title: 'daoris/orphan',
      blocked: {
        unknown: true,
        entries: [{
          kind: 'session-branch', resource: 'daoris/orphan', code: 'unrecorded', facts: { branch: 'daoris/orphan' },
          detail: 'its owner is unknown: Daoris did not record `daoris/orphan`, so nothing here is cleaned',
        }],
      },
    });

    const item = words(orphan);
    expect(item).toMatchObject({ title: 'daoris/orphan', session: null });
    expect(item.detail).toBe(
      'Blocked: session branch daoris/orphan, Daoris did not record daoris/orphan, so nothing of it is cleaned up: Discard branch… '
      + 'in the session branches on its workspace’s Branches removes it');

    await i18n.changeLanguage('zh');
    try {
      expect(words(orphan).detail).toBe(
        '受阻：会话分支 daoris/orphan，Daoris 没有记录 daoris/orphan，所以它的内容都不会被清理：在它所在工作区的“分支”中，按会话分支列表里的“丢弃分支…”即可移除');
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  /**
   * FABRIC-UNKNOWN1c: an unknown owner's `daoris/<folder>` branch names the session whose tree was cut for it, in the same repository,
   * which its door opens; a branch no session's tree names, or one in another repository, names none.
   */
  it('names the session an unknown owner’s branch was cut for, and none where no tree in its repository names it', () => {
    const unknown = (branch: string) => row('work', `unknown-branch:engine/${branch}`, null, {
      where: 'engine', session: null, title: branch,
      blocked: { unknown: true, entries: [{ kind: 'session-branch', resource: branch, code: 'unrecorded', facts: { branch }, detail: 'as recorded' }] },
    });
    const sessions = [
      session({ id: 'c0nv0001', repository: 'engine', tree: 'trees/engine/s-c0nv0001' }),
      session({ id: 'c0nv0002', repository: 'game', tree: 'trees\\game\\s-elsewhere' }),
      session({ id: 'c0nv0003', repository: 'engine', tree: null }),
    ];

    expect(words(unknown('daoris/s-c0nv0001'), { sessions })).toMatchObject({ title: 'daoris/s-c0nv0001', session: null, cutFor: 'c0nv0001' });
    expect(words(unknown('daoris/s-elsewhere'), { sessions }).cutFor).toBeUndefined();
    expect(words(unknown('daoris/s-orphan'), { sessions }).cutFor).toBeUndefined();
    expect(words(unknown('daoris/s-c0nv0001')).cutFor).toBeUndefined();
  });

  /** OVERVIEWNOISE1b: the tidy's guard said once by its code, and a retry promised only where the guard frees by itself. */
  it('words a guard the clean-up kept a branch by once, promising a retry only where one comes', () => {
    const kept = (why: string) => entryDetail(i18n.t, { detail: 'the driver’s clause', code: 'kept', facts: { why, at: '2026-10-20T09:00:00Z' } });

    expect(kept('nested')).toBe('the look kept it, since its tree holds another repository');
    expect(kept('busy')).toBe('the look kept it, since something on this machine holds its tree’s folder open; it is tried again at the next look');
    // A guard or a code the page has no words for is said as recorded.
    expect(kept('newer')).toBe('the driver’s clause');
    expect(entryDetail(i18n.t, { detail: 'as recorded', code: 'later', facts: {} })).toBe('as recorded');
    expect(entryDetail(i18n.t, { detail: 'as recorded' })).toBe('as recorded');
  });

  /** FABRIC-UNKNOWN1 (design §D3): an unrecorded daoris/* branch from a shell before the codes says its owner, then the driver's reason. */
  it('words an unrecorded daoris branch as an unknown owner, with the reason and the press', () => {
    const why = 'its owner is unknown: Daoris did not record `daoris/orphan`, so nothing here is cleaned; only your Discard branch press removes it';
    const item = words(row('work', 'unknown-branch:engine/daoris/orphan', null, {
      where: 'engine', session: null, title: 'daoris/orphan', blocked: { unknown: true, entries: [{ kind: 'session-branch', resource: 'daoris/orphan', detail: why }] },
    }));

    expect(item).toMatchObject({ title: 'daoris/orphan', where: 'engine', session: null });
    expect(item.detail).toContain('Nothing records whose work this is');
    expect(item.detail).toContain(why);
  });

  /** MIXAGENT2c (D168): an agent's ask for a helper, the session that asked named as Sessions names it, with its presses. */
  it('words an agent’s ask for a helper, with the session that asked and its presses', () => {
    const asked = words(row('helper', 'a1', '2026-09-21T09:30:00Z', { where: 'engine', session: 'c0nv0001', title: 'Which test covers the loader?' }), {
      sessions: [session({ id: 'c0nv0001', quest: '7a82cc' })], quests: [quest()],
    });
    expect(asked).toMatchObject({
      title: 'Which test covers the loader?', where: 'engine', since: '2026-09-21T09:30:00Z', session: 'c0nv0001',
      detail: 'Expose a streaming budget on the chunk API asked for another agent’s help. Nothing is asked until you let it.',
    });
    // A session the page does not hold is named by its id.
    expect(words(row('helper', 'a2', null, { where: '', session: 'g0n3', title: 'Is the cap right?' })))
      .toMatchObject({ where: '', detail: '#g0n3 asked for another agent’s help. Nothing is asked until you let it.' });
    expect(attentionActs(asked)).toEqual(['let-ask', 'not-now']);
  });

  /**
   * NOTIFY2e: an agent's ask to hand its session over, by the ask's own id: its note's first line, the session as Sessions names it,
   * and the agent as the roster names it, with *Hand over to {agent}* and *Not now*, in each language.
   */
  it('words an agent’s ask to hand its session over, with the session, the agent and its presses', async () => {
    const tools = byTool([{ harness: 'codex-acp', accountOf: 'codex', present: true, product: 'Codex', signsIn: true, profiles: [] }]);
    const pushed = row('hand-over', 'h1', '2026-09-21T09:30:00Z', {
      where: 'engine', session: 'c0nv0001', title: 'Near my limit.', agent: 'codex-acp', stands: true,
    });
    const names = { sessions: [session({ id: 'c0nv0001', quest: '7a82cc' })], quests: [quest()], tools };
    const asked = words(pushed, names);
    expect(asked).toMatchObject({
      id: 'h1', title: 'Near my limit.', where: 'engine', since: '2026-09-21T09:30:00Z', session: 'c0nv0001', agent: 'Codex', stands: true,
      detail: 'Its agent asks to hand Expose a streaming budget on the chunk API over to Codex. Nothing goes until you say yes.',
    });
    expect(attentionActs(asked)).toEqual(['hand-over', 'keep-agent']);
    // An agent the roster does not hold is named by its id; a session the page does not hold, by its.
    expect(words(row('hand-over', 'h2', null, { where: '', session: 'g0n3', title: 'Gone.', agent: 'dsh' })))
      .toMatchObject({ agent: 'dsh', detail: 'Its agent asks to hand #g0n3 over to dsh. Nothing goes until you say yes.' });
    // With no agent named there is nobody to hand it to: no yes is offered.
    expect(attentionActs(words(row('hand-over', 'h3', null, { session: 'g0n3', title: 'Gone.' })))).toEqual([]);
    await i18n.changeLanguage('zh');
    try {
      expect(words(pushed, names).detail).toBe('Expose a streaming budget on the chunk API 的智能体要求把它转交给 Codex。你同意之前不会转交。');
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  /**
   * COWORK1e (D188 point 4; the message-urgency design §8.3): a third answer in a row with nobody between holds its session's work,
   * named as Sessions names the session, said by the agent that answered, with *Go on with this answer* and *Not now*; after a held call.
   */
  it('words a third answer alone held for the person, with its two acts', () => {
    const answer = { session: 'c0nv0001', agent: 'codex-acp', words: ['w3'], since: '2026-09-21T09:30:00Z' };
    const rows = attentionOf(attentionInOrder([
      row('answer', 'c0nv0001', answer.since, { where: 'engine', session: 'c0nv0001', answer }),
      row('answer', 'g0n3', answer.since, { where: '', session: 'g0n3', answer: { ...answer, session: 'g0n3', agent: null } }),
    ]), { sessions: [session({ id: 'c0nv0001', quest: '7a82cc' })], quests: [quest()] });

    expect(rows[0]).toMatchObject({
      kind: 'answer', title: 'Expose a streaming budget on the chunk API', where: 'engine', session: 'c0nv0001',
      detail: 'codex-acp’s answer would take this work on a third time with nobody between. It waits for you.',
    });
    expect(rows[1]).toMatchObject({ title: '#g0n3', detail: 'Another agent’s answer would take this work on a third time with nobody between. It waits for you.' });
    expect(rows.map(attentionActs)).toEqual([['go-on-answer', 'keep-answer'], ['go-on-answer', 'keep-answer']]);
    expect(keyed([row('answer', 'a1', '2026-09-21T09:00:00Z'), row('permission', 'p1', '2026-09-21T09:00:00Z')])).toEqual(['permission:p1', 'answer:a1']);
  });

  /**
   * FABRIC-PERFORM1j (the fabric events design §D3): a node record that does not read stops everything on its unit, so it is a row of the
   * person's, named by the unit it still names or else by its file, saying nothing moves until it is mended, with its folder's press;
   * in 中文 too.
   */
  it('words a node record that does not read by its unit or its file, with its folder opened, in each language', async () => {
    const file = '0a1b2c3d4e5f60718293a4b5c6d7e8f9.json';
    const named = row('work', `record#${file}`, '2026-10-17T09:00:00Z', { where: 'quest:q7', unreadable: { file, unit: 'quest:q7', said: 'x' } });
    const unnamed = row('work', 'record#b.json', '2026-10-17T09:00:00Z', { where: null, unreadable: { file: 'b.json', unit: null, said: 'x' } });

    expect(words(named)).toMatchObject({
      kind: 'work', where: 'quest:q7', title: 'The record of quest:q7 does not read',
      detail: `Nothing on this work is judged or started until its record, ${file}, is mended where it is kept. `
        + 'Daoris never reads it as empty, since a step it holds could then be sent twice.',
    });
    expect(words(unnamed)).toMatchObject({ where: '', title: 'A work record does not read (b.json)' });
    expect(attentionActs(words(named))).toEqual(['open-records']);
    expect(ATTENTION_GROUP.work).toBe('word');

    await i18n.changeLanguage('zh');
    try {
      expect(words(named)).toMatchObject({
        title: 'quest:q7 的记录无法读取',
        detail: `在它的记录 ${file} 于存放处修好之前，这项工作不会被判断，也不会开始。Daoris 从不把它当作空记录，否则它记着的步骤可能被发送两次。`,
      });
      expect(words(unnamed).title).toBe('有一份工作记录无法读取（b.json）');
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  /** AUTONOMY1b3c (D184 point 3): a tool call a session holds for the person, a press per choice its agent offered. */
  it('words a tool call a session holds for the person, with a press per offered choice', () => {
    const held = {
      session: 'c0nv0001', request: '900', call: 'tc-1', title: 'npm test', kind: 'execute', since: '2026-09-21T09:30:00Z',
      until: '2026-09-21T09:45:00Z',
      choices: [{ id: 'allow', name: 'Allow', kind: 'allow_once' }, { id: 'reject', name: 'Reject', kind: 'reject_once' }],
    };
    const untitled = {
      session: 'g0n3', request: '7', call: null, title: null, kind: 'edit', since: '2026-09-21T09:10:00Z', until: null,
      choices: [{ id: 'no', name: 'Reject', kind: 'reject_once' }, { id: 'later', name: 'Later', kind: 'snooze' }],
    };
    const rows = attentionOf(attentionInOrder([
      row('permission', 'c0nv0001#900', held.since, { where: 'engine', session: 'c0nv0001', permission: held }),
      row('permission', 'g0n3#7', untitled.since, { where: '', session: 'g0n3', permission: untitled }),
    ]), { sessions: [session({ id: 'c0nv0001', quest: '7a82cc' })], quests: [quest()] });

    expect(rows.map((item) => `${item.kind}:${item.id}`)).toEqual(['permission:g0n3#7', 'permission:c0nv0001#900']);
    expect(rows[1]).toMatchObject({
      title: 'npm test', where: 'engine', since: '2026-09-21T09:30:00Z', session: 'c0nv0001',
      detail: 'Expose a streaming budget on the chunk API waits on your answer before this tool call runs; it is refused if nobody '
        + 'answers within 15 minutes.',
    });
    expect(rows[0]).toMatchObject({ title: 'An untitled tool call', where: '' });
    // PERMUNTIL1: the bound is the route's for each request; where a host said none, none is said.
    expect(rows[0]!.detail).toBe('#g0n3 waits on your answer before this tool call runs; it is refused if nobody answers in time.');
    const sooner = attentionOf([row('permission', 'c0nv0001#900', held.since, {
      where: 'engine', session: 'c0nv0001', permission: { ...held, until: '2026-09-21T09:40:00Z' },
    })], { sessions: [session({ id: 'c0nv0001', quest: '7a82cc' })], quests: [quest()] });
    expect(sooner[0]!.detail).toMatch(/within 10 minutes\.$/);
    expect(rows.map(attentionActs)).toEqual([['refuse-once'], ['allow-once', 'refuse-once']]);
    expect(attentionOffers(rows[1]!)).toEqual([{ act: 'allow-once', option: 'allow' }, { act: 'refuse-once', option: 'reject' }]);
    expect(ASKS_ONCE.has('allow-once')).toBe(false);
  });

  /** XAGENT1g (the second-agent design §9): a second opinion's row says what its gate says, named by its quest. */
  it('words a second opinion that waits on the person, with what its gate says and its presses', () => {
    const disputed = {
      session: 's42', quest: 'q1', repository: 'engine', since: '2026-09-21T09:00:00Z', auto: false,
      opinion: { state: 'disputed', holds: true, product: 'Codex', maker: 'OpenAI', disputes: 2, answers: 'o1:t:disputed:2:0' },
    };
    const none = { ...disputed, session: 's43', since: '2026-09-21T08:00:00Z', opinion: { state: 'unavailable', holds: true, required: true, code: 'no-reviewer' } };
    const rows = attentionOf(attentionInOrder([
      row('opinion', 's42', disputed.since, { where: 'engine', opinion: disputed }),
      row('opinion', 's43', none.since, { where: 'engine', opinion: none }),
      row('opinion', 's44', disputed.since, { where: 'engine', opinion: { ...disputed, session: 's44', auto: true } }),
    ]), { quests: [quest({ id: 'q1', to: 'engine', title: 'Cap hydration per frame' })] });

    expect(rows.map((item) => `${item.kind}:${item.id}`)).toEqual(['opinion:s43', 'opinion:s42', 'opinion:s44']);
    expect(rows[1]).toMatchObject({ title: 'Cap hydration per frame', where: 'engine' });
    expect(rows[1]!.detail).toBe('2 findings by Codex (OpenAI) are disputed: each a must the working session did not fix and no recheck '
      + 'withdrew. Your Accept, or your Reviewed where a look is required, answers them.');
    expect(rows[0]!.detail).toBe('No second opinion: no listed reviewer of another maker is installed. The rule requires one, so the work waits for you.');
    expect(rows.map(attentionActs)).toEqual([['opinion-anyway', 'opinion-again'], ['opinion-again'], ['opinion-anyway', 'opinion-again']]);
  });

  it('splits the rows into the groups that hold any, in order, keeping each group’s order', () => {
    const rows = attentionOf(attentionInOrder([
      row('review', 'r3v13w00', null), row('unanswerable', 'q1', '2026-09-14T09:00:00Z'), row('parked', 'parked', '2026-09-21T10:00:00Z'),
    ]));
    expect(attentionGroups(rows).map(({ group, items }) => [group, items.map((item) => item.id)])).toEqual([
      ['holding', ['parked']], ['word', ['q1']], ['ready', ['r3v13w00']],
    ]);
    expect(attentionGroups([])).toEqual([]);
  });

  /** Two rows that waited exactly as long keep §6.2's order of kinds, then their keys: a held attempt after a unit's blocked clean-up. */
  it('orders rows that waited as long by their kinds’ order, then by their keys', () => {
    const at = '2026-09-21T09:00:00Z';
    expect(keyed([
      row('work', 'u1#deploy#2', at, { held: { unit: 'u1' } as PushedAttention['held'] }), row('work', 's9', at),
      row('rule', 'p2', at), row('rule', 'p1', at), row('intake', 'a2', at), row('proposal', 'a1', at),
    ])).toEqual(['intake:a2', 'proposal:a1', 'rule:p1', 'rule:p2', 'work:s9', 'work:u1#deploy#2']);
  });

  /**
   * NEEDSSCOPE1: the bell, the taskbar, *Notifications*' group and Overview's summary each filtered the window's rows apart. One
   * answer for what is listed and counted: every row but a press's own, whose card is drawn from the confirmation channel (NOTIFY2b).
   */
  it('lists and counts every row but a press’s own, in the order given', () => {
    const at = '2026-09-21T09:00:00Z';
    expect(needsListed([row('review', 'r1', null), row('confirmation', 'c1', at), row('parked', 'p1', at)]).map((each) => `${each.kind}:${each.id}`))
      .toEqual(['review:r1', 'parked:p1']);
    expect(needsListed([])).toEqual([]);
  });
});

/**
 * BADGEUX1 (the UI/UX design §3.10): a number on a place counts only what waits on the person, the bell's rows, each at the one place
 * its door opens, where its step is taken; so the places' numbers sum to what the bell counts of the rows, and a press is the bell's.
 */
describe('the place each wait is counted at', () => {
  it('is the place its door opens, for every kind', () => {
    expect(ATTENTION_PLACE).toEqual({
      parked: 'sessions', review: 'sessions', opinion: 'sessions', helper: 'sessions', 'hand-over': 'sessions',
      permission: 'sessions', answer: 'sessions', quiet: 'sessions',
      proposal: 'quests', intake: 'quests', unanswerable: 'quests', 'parked-quest': 'quests', 'go-ahead': 'quests',
      departure: 'quests', 'set-up': 'quests', trust: 'quests',
      work: 'projects',
      rule: 'agents', 'account-wait': 'agents', 'signed-out': 'agents',
      space: 'settings',
      confirmation: null,
      'git-held': null,
    });
  });

  it('counts every listed row at one place, so the places sum to what the bell counts of them', () => {
    const at = '2026-09-21T09:00:00Z';
    const rows = [
      row('parked', 'p1', at), row('review', 'r1', null), row('permission', 'p1#9', at), row('proposal', 'a1', at),
      row('parked-quest', 'q1', at), row('work', 'u1', at), row('rule', 'r9', at), row('signed-out', 'claude-code/a', at),
      row('space', 'D:', at), row('confirmation', 'c1', at),
    ];
    const counted = placeCounts(rows);
    expect(counted).toEqual({ sessions: 3, quests: 2, projects: 1, agents: 2, settings: 1 });
    expect(Object.values(counted).reduce((sum, each) => sum + each, 0)).toBe(needsListed(rows).length);
    expect(placeCounts([])).toEqual({});
  });

  /** GITAUTH1a: a held git remote's one act is on its row, so no place counts it until Settings → Git holds it (GITAUTH1e); the bell does. */
  it('counts a held git remote at no place, though the bell counts it', () => {
    const held = row('git-held', 'default|github.com|refused|all', '2026-10-22T09:00:00Z');
    expect(placeOf(held, undefined)).toBeNull();
    expect(placeCounts([held, row('space', 'D:', null)])).toEqual({ settings: 1 });
    expect(needsListed([held])).toHaveLength(1);
  });

  it('counts a kind this build does not know at no place, though the bell counts it', () => {
    const later = { kind: 'later-kind', id: 'x1', since: null } as unknown as PushedAttention;
    expect(placeCounts([later])).toEqual({});
    expect(needsListed([later])).toHaveLength(1);
  });

  /**
   * Codex's read (§3.10.8): a unit of work's door is the row's, not its kind's. A held attempt's presses are on its row alone, which the
   * bell's view holds; a branch cut for a session the page holds opens that session (FABRIC-UNKNOWN1c); any other opens Branches.
   */
  it('places a unit of work where its own door opens', () => {
    const at = '2026-09-21T09:00:00Z';
    const sessions = [session({ id: 'c4a7c4a7', repository: 'engine', tree: 'trees/engine/s-c4a7c4a7' })];
    const cut = row('work', 'unknown-branch:engine/daoris/s-c4a7c4a7', at, {
      where: 'engine', title: 'daoris/s-c4a7c4a7', blocked: { unknown: true, entries: [] },
    });
    const held = row('work', 'u1#deploy#2', at, { held: { unit: 'u1' } as PushedAttention['held'] });
    const plain = row('work', 'u-orphan', at, { where: 'engine', title: 'daoris/s-orphan', blocked: { unknown: true, entries: [] } });

    expect(placeOf(cut, sessions)).toBe('sessions');
    expect(placeOf(cut, [])).toBe('projects');
    expect(placeOf(held, sessions)).toBeNull();
    expect(placeOf(plain, sessions)).toBe('projects');
    expect(placeCounts([cut, held, plain], sessions)).toEqual({ sessions: 1, projects: 1 });
    // The door the band draws is the same judgement.
    expect(attentionWorded(cut, { sessions }).cutFor).toBe('c4a7c4a7');
  });
});

/**
 * UX6c (design §6.3): an act is on a row only where one press is safe and the row says what it does; one that widens what
 * Daoris may do asks once; one that needs reading first is the row's door, never an act. One rule per kind, so every row
 * of a kind is offered the same acts.
 */
describe('what a row offers', () => {
  const offered = (kind: Attention['kind'], over: Partial<Attention> = {}): Attention =>
    ({ id: 'x', kind, title: 't', where: 'w', since: '2026-09-21T09:00:00Z', ...over });

  it('publishes where its declarations named receivers, and chooses otherwise', () => {
    expect(attentionActs(offered('proposal', { publishTo: ['engine'] }))).toEqual(['publish', 'choose']);
    expect(attentionActs(offered('proposal', { publishTo: [] }))).toEqual(['choose']);
  });

  it('tries a parked quest again, accepts a departure, and answers a go-ahead, a folder and a widening', () => {
    expect(attentionActs(offered('parked-quest'))).toEqual(['retry']);
    expect(attentionActs(offered('departure'))).toEqual(['accept-departure']);
    expect(attentionActs(offered('go-ahead'))).toEqual(['approve', 'refuse']);
    expect(attentionActs(offered('trust', { trust: { folder: 'f', trustFile: 'g' } }))).toEqual(['trust']);
    expect(attentionActs(offered('rule'))).toEqual(['accept-rule', 'decline-rule']);
    // MIXAGENT2c: an agent's ask for a helper is answered as its session's page answers it, in one press each.
    expect(attentionActs(offered('helper'))).toEqual(['let-ask', 'not-now']);
    // NOTIFY2e: an agent's ask to hand its session over, to the agent the look named.
    expect(attentionActs(offered('hand-over', { agent: 'Codex' }))).toEqual(['hand-over', 'keep-agent']);
  });

  /** Answering a park or an intake's question, and accepting a review, need reading: each is its door alone. */
  it('offers nothing where reading comes first, or where nothing here can act', () => {
    for (const kind of ['parked', 'intake', 'review', 'unanswerable'] as const) expect(attentionActs(offered(kind))).toEqual([]);
    expect(attentionActs(offered('trust'))).toEqual([]);
  });

  it('asks once before what widens what Daoris may do, and before a choice', () => {
    expect([...ASKS_ONCE].sort())
      .toEqual(['accept-rule', 'approve', 'choose', 'not-yet', 'opinion-anyway', 'quiet-carry-on', 'quiet-stop', 'refuse', 'trust']);
  });

  /**
   * SESSWATCH1a (D200, the session-watch design §2.3, §5.2): a quiet quest session offers *End and carry on*, *Hand over…*, *Stop…* and
   * *Leave it running*; a busy one nothing is heard from never *End and carry on*; an intake *Stop…* and *Leave it running* alone.
   */
  it('offers a quiet row the person\'s presses, never an end to a busy one, and an intake no carry-on or hand-over', () => {
    const quiet = (watch: Partial<QuietWait>) => ({
      id: 's1', kind: 'quiet' as const, title: 's1', where: 'engine', since: null,
      quiet: { minutes: 30, busy: false, intake: false, words: false, ...watch },
    });
    expect(attentionActs(quiet({}))).toEqual(['quiet-carry-on', 'quiet-hand-over', 'quiet-stop', 'quiet-leave']);
    expect(attentionActs(quiet({ busy: true }))).toEqual(['quiet-hand-over', 'quiet-stop', 'quiet-leave']);
    expect(attentionActs(quiet({ intake: true }))).toEqual(['quiet-stop', 'quiet-leave']);
    expect(attentionActs({ ...quiet({}), quiet: undefined })).toEqual([]);
  });
});

/**
 * UX6d (design §6.2–§6.3): an account's row offers what settles it in one press, *Sign in* to an account read signed out and
 * *Read* for one no read answered. Two at most beside the door, so a row holds three controls (§9.3). *Let … run …* went with
 * POOL1c (the account-pool design §4.3): every account of Daoris's serves every workspace, so none is outside a start's list.
 */
describe('what an account’s row offers', () => {
  const named = (id: string | null, state: NamedAccount['state'], read: string | null = '2026-10-05T10:42:00Z'): NamedAccount =>
    ({ id, label: id ?? 'Your own sign-in', state, read, until: null });
  const facts = (over: Partial<AccountRowFacts> = {}): AccountRowFacts => ({
    agent: 'claude-code', product: 'Claude Code', harness: 'claude-code', signsIn: true, named: [], ...over,
  });
  const accountRow = (kind: 'account-wait' | 'signed-out', account: AccountRowFacts): Attention =>
    ({ id: 'x', kind, title: 't', where: 'w', since: '2026-10-05T10:00:00Z', account });

  it('holds work, whichever it is', () => {
    expect(ATTENTION_GROUP['account-wait']).toBe('holding');
    expect(ATTENTION_GROUP['signed-out']).toBe('holding');
  });

  it('signs in each account a waiting start passed signed out, in the order it names them', () => {
    const item = accountRow('account-wait', facts({ named: [named('account-2', 'cooling'), named('account-1', 'out'), named('account-3', 'out')] }));
    expect(attentionOffers(item)).toEqual([{ act: 'sign-in', account: 'account-1' }, { act: 'sign-in', account: 'account-3' }]);
    expect(attentionActs(item)).toEqual(['sign-in', 'sign-in']);
  });

  it('reads one no read answered, after the sign-ins, two at most', () => {
    expect(attentionOffers(accountRow('account-wait', facts({ named: [named('account-2', 'cooling'), named('account-5', 'unknown', null)] }))))
      .toEqual([{ act: 'read', account: 'account-5' }]);
    expect(attentionOffers(accountRow('account-wait', facts({
      named: [named('account-1', 'out'), named('account-3', 'out'), named('account-6', 'out')],
    })))).toEqual([{ act: 'sign-in', account: 'account-1' }, { act: 'sign-in', account: 'account-3' }]);
  });

  /**
   * POOL1c (the account-pool design §4.3): no account is outside the set, so a row never offers to let one run, whatever an
   * older answer carries beside it.
   */
  it('never offers to let an account run: every account of Daoris’s serves every workspace', () => {
    const older = { ...facts({ named: [named('account-2', 'cooling')] }), outside: { id: 'account-4', list: 'work', workspace: 'work' }, readFirst: { id: 'account-4' } };
    expect(attentionOffers(accountRow('account-wait', older as AccountRowFacts))).toEqual([]);
    expect(attentionActs(accountRow('account-wait', older as AccountRowFacts))).not.toContain('let-run');
  });

  /** A read that failed is a reading: *Read* is for an account never read (§6.3), as the agent's page offers it. */
  it('reads only an account never read, and signs in nothing where Daoris runs no sign-in', () => {
    expect(attentionOffers(accountRow('account-wait', facts({ named: [named('account-5', 'unknown')] })))).toEqual([]);
    expect(attentionOffers(accountRow('account-wait', facts({ signsIn: false, named: [named('account-1', 'out')] })))).toEqual([]);
  });

  /** OWNSPACE1b-B (D175 points 6-7): the hold for no Daoris account offers *Sign in for Daoris*, then sign-ins it passed. */
  it('signs in for Daoris where no Daoris account is signed in, first, and nowhere Daoris runs no sign-in', () => {
    expect(attentionOffers(accountRow('account-wait', facts({ noAccount: true })))).toEqual([{ act: 'sign-in-new' }]);
    expect(attentionActs(accountRow('account-wait', facts({ noAccount: true })))).toEqual(['sign-in-new']);
    expect(attentionOffers(accountRow('account-wait', facts({ noAccount: true, named: [named('account-1', 'out'), named('account-3', 'out')] }))))
      .toEqual([{ act: 'sign-in-new' }, { act: 'sign-in', account: 'account-1' }]);
    expect(attentionOffers(accountRow('account-wait', facts({ noAccount: true, signsIn: false })))).toEqual([]);
    expect(attentionOffers(accountRow('account-wait', facts({ named: [named('account-2', 'cooling')] })))).toEqual([]);
  });

  it('signs a signed-out account in, and never the tool’s own sign-in, which is the person’s', () => {
    expect(attentionOffers(accountRow('signed-out', facts({ named: [named('account-1', 'out')] }))))
      .toEqual([{ act: 'sign-in', account: 'account-1' }]);
    expect(attentionOffers(accountRow('signed-out', facts({ named: [named(null, 'out')] })))).toEqual([]);
    expect(attentionOffers(accountRow('signed-out', facts({ signsIn: false, named: [named('account-1', 'out')] })))).toEqual([]);
  });
});

/**
 * UX6d: the account rows are What needs you's holding work, the longest waiting first among the rest (§6.2). UXFIX3: a
 * signed-out account's row knows when it was read, not since when it holds work, so it has no wait to be sorted by: it comes
 * after the rows that say how long, wherever a reading moves its time.
 */
describe('what needs a person, with the accounts as last known', () => {
  const tools = byTool([{
    harness: 'claude-code', present: true, product: 'Claude Code', signsIn: true,
    profiles: [{ name: 'account-1', home: 'H/account-1', login: 'out', read: '2026-09-21T11:58:00Z' }, { name: 'account-2', home: 'H/account-2', login: 'out', read: '2026-09-21T08:00:00Z' }],
  }]);
  const signedOut = (account: string, read: string) => row('signed-out', `signed-out:claude-code/${account}`, null, {
    read, where: 'Claude Code',
    account: { agent: 'claude-code', product: 'Claude Code', harness: 'claude-code', signsIn: true, named: [{ id: account, name: null, state: 'out', read, until: null }] },
  });

  it('lists a signed-out account after a parked session, which says how long it waited, and never moves it by a reading', () => {
    const rows = attentionOf(attentionInOrder([
      signedOut('account-2', '2026-09-21T08:00:00Z'), signedOut('account-1', '2026-09-21T11:58:00Z'), row('parked', 's1a2b3c4', '2026-09-21T10:00:00Z'),
    ]), { tools });

    expect(rows.map(({ id }) => id)).toEqual(['s1a2b3c4', 'signed-out:claude-code/account-1', 'signed-out:claude-code/account-2']);
    expect(attentionGroups(rows).map(({ group }) => group)).toEqual(['holding']);
    expect(rows.filter(({ kind }) => kind === 'signed-out').map(({ since, title }) => [since, title])).toEqual([[null, 'account-1'], [null, 'account-2']]);
  });
});
