import { describe, expect, it } from 'vitest';
import '../i18n';
import type { Ask, Quest, Registration } from '../api';
import { clockOf, moment } from '../format';
import { type AccountScope, type AccountsAnswer, USE_DEFAULTS } from '../settings/accounts';
import type { AccountWaitTick, Consideration } from '../signals';
import { byTool, type ToolDoor } from '../tools';
import { accountAttention, type AccountsKnown } from './accountAttention';

// *What needs you*'s accounts (UX6d, D150 §6.2–§6.3; TOOL4m's row): a start waiting for accounts and a signed-out account a
// list holds, from what the page already holds. Pure, so every state is a fixture: nothing here asks the bridge anything.

const NOW = new Date('2026-10-05T10:50:00Z');
const READ = '2026-10-05T10:42:00Z';
const UNTIL = '2026-10-06T04:42:00Z';

const door = (over: Partial<ToolDoor> = {}): ToolDoor => ({
  harness: 'claude-code', present: true, product: 'Claude Code', maker: 'Anthropic', signsIn: true, wire: 'pipe',
  machineDefault: null, ownLogin: 'unknown', ownRead: null, workspaceDefaults: [],
  profiles: [
    { name: 'account-1', home: 'H/account-1', login: 'out', read: READ, account: 'you@work.example' },
    { name: 'account-2', home: 'H/account-2', login: 'in', read: '2026-10-05T09:00:00Z', displayName: 'home' },
    { name: 'account-3', home: 'H/account-3', login: 'out', read: READ },
    { name: 'account-4', home: 'H/account-4', login: 'in', read: '2026-10-05T09:00:00Z' },
  ],
  ...over,
});

const scope = (over: Partial<AccountScope> = {}): AccountScope => ({
  workspace: null, default: null, list: [], begins: null, use: USE_DEFAULTS, unknown: [], problem: null, near: [], ...over,
});

const COOLING = { until: UNTIL, stated: true, seen: '2026-10-05T10:00:00Z', assumedZone: false, notBelieved: false };

/** The owner's install as D150 §0 read it: `work` may run on all three accounts, one cooling, two signed out. */
const answer = (scopes: AccountScope[] = [scope(), scope({ workspace: 'work', list: ['account-2', 'account-1', 'account-3'], begins: 'account-2' })]): AccountsAnswer => ({
  agents: [{ agent: 'claude-code', speaks: true, own: {}, accounts: [{ name: 'account-2', cooling: COOLING }], scopes }],
});

const ask = (id: string, asked: string): Ask => ({
  id, workspace: 'work', sentence: `ask ${id}`, state: 'Proposed', tier: 'declarations', asked, updated: asked, links: [],
  attachments: [], proposal: [], quests: [],
});

const quest = (over: Partial<Quest> = {}): Quest => ({
  id: 'q1', from: 'game', to: 'engine', title: 'Expose a streaming budget', body: 'b', status: 'Open',
  filed: '2026-10-05T10:30:00Z', updated: '2026-10-05T10:30:00Z', workspace: 'work', ...over,
});

const REGISTRY: Registration[] = [{
  repository: 'engine', workspace: 'work', adopted: true, registered: true, owns: [], accepts: [], packs: [], entries: 1,
}];

/** The intake for two asks, held behind `account-2`'s cool-off, having passed the two signed out (D150 §6.3's drawing). */
const WAIT: AccountWaitTick = {
  agent: 'claude-code', account: 'account-2', name: 'home', workspace: 'work', until: UNTIL, stated: true,
  quests: [], asks: ['a1', 'a2'], signedOut: ['account-1', 'account-3'],
};
const ASKS = [ask('a1', '2026-10-05T10:25:00Z'), ask('a2', '2026-10-05T10:27:00Z')];

const known = (over: Partial<AccountsKnown> = {}): AccountsKnown => ({
  tools: byTool([door()]), use: answer(), waits: [], ...over,
});

const derive = (
  facts: AccountsKnown, considered: Consideration[] = [], quests: Quest[] = [], asks: Ask[] = ASKS,
) => accountAttention(facts, considered, quests, asks, REGISTRY, NOW);

describe('a start waiting for accounts', () => {
  it('names what it holds, where, since when, and each account that would serve it as last known', () => {
    const [row] = derive(known({ waits: [WAIT] }));

    expect(row).toMatchObject({
      kind: 'account-wait',
      title: 'Intake for 2 asks',
      where: 'work',
      circle: true,
      // Since the first ask was asked: its accounts were held before it.
      since: '2026-10-05T10:25:00Z',
    });
    expect(row!.detail).toBe(
      `home cools until ${moment(UNTIL)}; account-1 and account-3 read signed out at ${clockOf(READ, NOW)}.`);
    expect(row!.account).toMatchObject({ agent: 'claude-code', product: 'Claude Code', harness: 'claude-code', signsIn: true });
    expect(row!.account!.named.map(({ id, state }) => [id, state])).toEqual([
      ['account-2', 'cooling'], ['account-1', 'out'], ['account-3', 'out'],
    ]);
  });

  /** One ask's intake is named as Sessions names it, and a quest by its own title, where its repository is. */
  it('names one intake as its session is named, and one quest by its title in its repository', () => {
    expect(derive(known({ waits: [{ ...WAIT, asks: ['a1'] }] }))[0]).toMatchObject({ title: 'Intake for ask #a1' });

    const [row] = derive(known({ waits: [{ ...WAIT, asks: [], quests: ['q1'] }] }), [], [quest()]);
    expect(row).toMatchObject({ title: 'Expose a streaming budget', where: 'engine', circle: false, since: '2026-10-05T10:30:00Z' });
  });

  it('counts quests and intakes together where it holds both', () => {
    const [row] = derive(known({ waits: [{ ...WAIT, quests: ['q1', 'q2'] }] }), [], [quest(), quest({ id: 'q2', to: 'game' })]);
    expect(row!.title).toBe('2 quests and intake for 2 asks');
  });

  /**
   * Waiting is counted from what the start was asked for, or from the limit that held it where that came later: a quest
   * filed last week behind an account cooled an hour ago has waited an hour. A reading's time is when it was read, never
   * when the account signed out, so it moves nothing.
   */
  it('waits since its work was asked for, or since the limit that held it where that came later', () => {
    const [row] = derive(known({ waits: [{ ...WAIT, asks: [], quests: ['q1'] }] }), [], [quest({ filed: '2026-09-28T09:00:00Z' })]);
    expect(row!.since).toBe(COOLING.seen);
  });

  /** An older shell's tick carries no waits: a quest's own wait is read from its consideration, as Overview reads it. */
  it('is read from a held quest’s consideration where the tick carries no wait', () => {
    const held: Consideration = {
      quest: 'q1', repository: 'engine', verdict: 'Blocked', reason: 'cooling',
      waitsFor: { agent: 'claude-code', account: 'account-2', name: 'home', until: UNTIL, stated: true },
      signedOut: { agent: 'claude-code', accounts: ['account-1', 'account-3'], names: [null, null] },
    };

    const rows = derive(known(), [held, { ...held, quest: 'q2' }], [quest(), quest({ id: 'q2' })], []);

    expect(rows).toHaveLength(1);
    expect(rows[0]).toMatchObject({ kind: 'account-wait', title: '2 quests', where: 'engine', circle: false });
  });

  /** A quest the tick's wait already holds is that wait's, never a row of its own beside it. */
  it('lists a quest once, under the tick’s wait', () => {
    const held: Consideration = {
      quest: 'q1', repository: 'engine', verdict: 'Blocked', reason: 'cooling',
      waitsFor: { agent: 'claude-code', account: 'account-2', until: UNTIL, stated: true },
    };
    expect(derive(known({ waits: [{ ...WAIT, quests: ['q1'] }] }), [held], [quest()])).toHaveLength(1);
  });

  /** TOOL6g: a start held on accounts read signed out with none cooling waits for a person, not a time. */
  it('waits on signed-out accounts alone, with a sign-in for each', () => {
    const held: Consideration = {
      quest: 'q1', repository: 'engine', verdict: 'Blocked', reason: 'none ready',
      signedOut: { agent: 'claude-code', accounts: ['account-1', 'account-3'] },
    };
    const facts = known({ use: answer([scope(), scope({ workspace: 'work', list: ['account-1', 'account-3'] })]) });

    const [row] = derive(facts, [held], [quest()], []);

    expect(row!.detail).toBe(`account-1 and account-3 read signed out at ${clockOf(READ, NOW)}.`);
    expect(row!.account!.named.map(({ id }) => id)).toEqual(['account-1', 'account-3']);
  });

  /** §6.3: an account the row names that no read has answered is said so, never guessed signed in or out. */
  it('says an account no read answered is unknown, never read', () => {
    const tools = byTool([door({
      profiles: [
        { name: 'account-2', home: 'H/account-2', login: 'in', read: '2026-10-05T09:00:00Z', displayName: 'home' },
        { name: 'account-5', home: 'H/account-5', login: 'unknown', read: null },
      ],
    })]);
    const use = answer([scope(), scope({ workspace: 'work', list: ['account-2', 'account-5'] })]);

    const [row] = derive({ tools, use, waits: [{ ...WAIT, signedOut: [] }] });

    expect(row!.detail).toBe(`home cools until ${moment(UNTIL)}; account-5 unknown, never read.`);
    expect(row!.account!.named.find(({ id }) => id === 'account-5')).toMatchObject({ state: 'unknown', read: null });
  });

  /** D130 point 6: a driven start drops the account kept for conversations, so the row does not name it as one that serves. */
  it('leaves out the account kept for conversations', () => {
    const use = answer([scope(), scope({
      workspace: 'work', list: ['account-2', 'account-1', 'account-4'], use: { ...USE_DEFAULTS, keep: 'account-4' },
    })]);
    const [row] = derive(known({ use, waits: [{ ...WAIT, signedOut: ['account-1'] }] }));
    expect(row!.account!.named.map(({ id }) => id)).toEqual(['account-2', 'account-1']);
  });

  /**
   * D130 §3.3: every account the workspace may use is cooling and another is ready, so the row offers to let that one run
   * the workspace. Only an account the join would take: never one cooling, signed out, or already listed.
   */
  it('offers a ready account outside the workspace’s list, by the list the join would add it to', () => {
    const use = answer([scope(), scope({ workspace: 'work', list: ['account-2'], begins: 'account-2' })]);
    const [row] = derive(known({ use, waits: [{ ...WAIT, signedOut: [] }] }));
    expect(row!.account!.outside).toEqual({ id: 'account-4', label: 'account-4', list: 'work', workspace: 'work' });
  });

  /**
   * A workspace naming no account of its own runs on this machine's list (D130 §2), so the join is this machine's list's,
   * and the row still says which workspace waits.
   */
  it('offers to add it to this machine’s list where the workspace runs on it', () => {
    const use = answer([scope({ list: ['account-2'], begins: 'account-2' })]);
    const [row] = derive(known({ use, waits: [{ ...WAIT, signedOut: [] }] }));
    expect(row!.account!.outside).toEqual({ id: 'account-4', label: 'account-4', list: null, workspace: 'work' });
  });

  it('offers no account to let in where none is ready, or where the wait spans workspaces', () => {
    const use = answer([scope(), scope({ workspace: 'work', list: ['account-2'] })]);
    const tools = byTool([door({ profiles: door().profiles!.filter((profile) => profile.name !== 'account-4') })]);
    expect(derive({ tools, use, waits: [{ ...WAIT, signedOut: [] }] })[0]!.account!.outside).toBeNull();
    expect(derive(known({ use, waits: [{ ...WAIT, workspace: null, signedOut: [] }] }))[0]!.account!.outside).toBeNull();
  });

  /** The tool's own sign-in is named as the person knows it, and has no sign-in of Daoris's. */
  it('names the tool’s own sign-in where nothing names an account', () => {
    const [row] = derive(known({ use: answer([scope()]), waits: [{ ...WAIT, account: null, name: null, signedOut: [] }] }));
    expect(row!.account!.named).toEqual([expect.objectContaining({ id: null, label: 'Your own sign-in', state: 'cooling' })]);
    expect(row!.detail).toBe(`Your own sign-in cools until ${moment(UNTIL)}.`);
  });

  /** Before the roster and the accounts' files answer, the tick's own facts still say the wait; nothing can act on it. */
  it('says the wait from the tick alone before anything else answers', () => {
    const [row] = derive({ tools: [], use: null, waits: [WAIT] });
    expect(row).toMatchObject({ kind: 'account-wait', title: 'Intake for 2 asks' });
    expect(row!.account).toMatchObject({ product: 'claude-code', harness: null, signsIn: false });
    expect(row!.account!.named.map(({ id, label, state }) => [id, label, state])).toEqual([
      ['account-2', 'home', 'cooling'], ['account-1', 'account-1', 'out'], ['account-3', 'account-3', 'out'],
    ]);
  });
});

describe('a signed-out account a list holds', () => {
  it('is a row of its own where no waiting start names it, saying when it was read and where it runs', () => {
    const rows = derive(known());

    expect(rows.map(({ id }) => id)).toEqual(['signed-out:claude-code/account-1', 'signed-out:claude-code/account-3']);
    expect(rows[0]).toMatchObject({
      kind: 'signed-out', title: 'account-1', where: 'Claude Code', since: READ,
      detail: `Read signed out at ${clockOf(READ, NOW)}. It runs work in work.`,
    });
    expect(rows[0]!.account!.named).toEqual([expect.objectContaining({ id: 'account-1', state: 'out' })]);
  });

  /** §6.3: a start waiting only on signed-out accounts carries their Sign in, and they are not rows of their own. */
  it('is listed once: a waiting start that names it stands for it', () => {
    const rows = derive(known({ waits: [WAIT] }));
    expect(rows.map(({ kind }) => kind)).toEqual(['account-wait']);
  });

  it('is no row where nothing holds it, or where the agent is not installed', () => {
    expect(derive(known({ use: answer([scope()]) }))).toEqual([]);
    expect(derive(known({ tools: byTool([door({ present: false })]) }))).toEqual([]);
  });

  /** The tool's own sign-in holds the starts where no account is named (D125 §3.7), so reading signed out it needs the person. */
  it('includes the tool’s own sign-in while the starts run on it', () => {
    const tools = byTool([door({ profiles: [], ownLogin: 'out', ownRead: READ })]);
    const [row] = derive({ tools, use: answer([scope()]), waits: [] });
    expect(row).toMatchObject({
      id: 'signed-out:claude-code/', kind: 'signed-out', title: 'Your own sign-in', where: 'Claude Code', since: READ,
      detail: `Read signed out at ${clockOf(READ, NOW)}. Daoris's starts run on it, since no account is named.`,
    });
  });
});

describe('what a page holds no answer for', () => {
  it('lists nothing in a browser, which has no tick, no roster and no accounts', () => {
    expect(accountAttention({ tools: [], use: null, waits: [] }, [], [], [], [], NOW)).toEqual([]);
  });
});
