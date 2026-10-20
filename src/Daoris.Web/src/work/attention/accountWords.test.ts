import { describe, expect, it } from 'vitest';
import '../../locales/i18n';
import type { Quest } from '../../data/api';
import { clockOf, moment } from '../../locales/format';
import { byTool, type ToolDoor } from '../../agents/tools';
import type { PushedAccountFacts, PushedNamed } from './accountWords';
import { attentionOffers, attentionWorded, type PushedAttention } from './attention';

// *What needs you*'s accounts in words (UX6d, D150 §6.2–§6.3; TOOL4m's row). Which start waits on which accounts, in which state,
// is .NET's (`AttentionStateSliceTests.Accounts`, ported from this file's former judge cell for cell); the page names each account
// from the roster it holds and says the row's title and its line of why.

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

const named = (id: string | null, state: PushedNamed['state'], over: Partial<PushedNamed> = {}): PushedNamed =>
  ({ id, name: null, state, read: id === 'account-2' ? '2026-10-05T09:00:00Z' : READ, until: state === 'cooling' ? UNTIL : null, ...over });

const facts = (over: Partial<PushedAccountFacts> = {}): PushedAccountFacts => ({
  agent: 'claude-code', product: 'Claude Code', harness: 'claude-code', signsIn: true, named: [], outside: null, readFirst: null, ...over,
});

/** A start waiting on its accounts, as the shell pushes it. */
const waiting = (over: Partial<PushedAttention> = {}): PushedAttention => ({
  kind: 'account-wait', id: 'wait:claude-code/account-2', since: '2026-10-05T10:25:00Z', where: 'work', circle: true,
  holding: { quests: [], asks: ['a1', 'a2'] },
  account: facts({ named: [named('account-2', 'cooling', { name: 'home' }), named('account-1', 'out'), named('account-3', 'out')] }),
  ...over,
});

const quest = (over: Partial<Quest> = {}): Quest => ({
  id: 'q1', from: 'game', to: 'engine', title: 'Expose a streaming budget', body: 'b', status: 'Open',
  filed: '2026-10-05T10:30:00Z', updated: '2026-10-05T10:30:00Z', workspace: 'work', ...over,
});

const worded = (row: PushedAttention, doors: ToolDoor[] = [door()], quests: Quest[] = []) =>
  attentionWorded(row, { tools: byTool(doors), quests }, NOW);

describe('a start waiting for accounts, worded', () => {
  it('names what it holds and each account that would serve it as last known, with when each was read', () => {
    const row = worded(waiting());

    expect(row).toMatchObject({ kind: 'account-wait', title: 'Intake for 2 asks', where: 'work', circle: true, since: '2026-10-05T10:25:00Z' });
    expect(row.detail).toBe(`home cools until ${moment(UNTIL)}; account-1 and account-3 read signed out at ${clockOf(READ, NOW)}.`);
    expect(row.account).toMatchObject({ agent: 'claude-code', product: 'Claude Code', harness: 'claude-code', signsIn: true });
    expect(row.account!.named.map(({ id, label, state }) => [id, label, state])).toEqual([
      ['account-2', 'home', 'cooling'], ['account-1', 'account-1', 'out'], ['account-3', 'account-3', 'out'],
    ]);
  });

  /** One ask's intake is named as Sessions names it, and a quest by its own title; several are counted. */
  it('names one intake as its session is named, one quest by its title, and counts several', () => {
    expect(worded(waiting({ holding: { quests: [], asks: ['a1'] } })).title).toBe('Intake for ask #a1');
    expect(worded(waiting({ holding: { quests: ['q1'], asks: [] } }), [door()], [quest()]).title).toBe('Expose a streaming budget');
    expect(worded(waiting({ holding: { quests: ['q1'], asks: [] } })).title).toBe('#q1');
    expect(worded(waiting({ holding: { quests: ['q1', 'q2'], asks: ['a1', 'a2'] } })).title).toBe('2 quests and intake for 2 asks');
    expect(worded(waiting({ holding: { quests: ['q1', 'q2'], asks: [] } })).title).toBe('2 quests');
  });

  it('says accounts read signed out together, by when', () => {
    const row = worded(waiting({ account: facts({ named: [named('account-1', 'out'), named('account-3', 'out')] }) }));
    expect(row.detail).toBe(`account-1 and account-3 read signed out at ${clockOf(READ, NOW)}.`);
  });

  /** §6.3: an account the row names that no read has answered is said so, never guessed signed in or out. */
  it('says an account no read answered is unknown, never read', () => {
    const doors = [door({ profiles: [door().profiles![1]!, { name: 'account-5', home: 'H/account-5', login: 'unknown', read: null }] })];
    const row = worded(waiting({ account: facts({ named: [named('account-2', 'cooling'), named('account-5', 'unknown', { read: null })] }) }), doors);
    expect(row.detail).toBe(`home cools until ${moment(UNTIL)}; account-5 unknown, never read.`);
  });

  /** ACCTUX1: a refused key is said as refused, named by its handle, and offered no sign-in, which no key has. */
  it('says a key its start would run on as refused, with no sign-in for it', () => {
    const doors = [door({ profiles: door().profiles!.map((profile) => (profile.name === 'account-3' ? { ...profile, key: '…abcd' } : profile)) })];
    const row = worded(waiting({ account: facts({ named: [named('account-2', 'cooling'), named('account-1', 'out'), named('account-3', 'refused')] }) }), doors);

    expect(row.account!.named.map(({ id, label, state }) => [id, label, state])).toEqual([
      ['account-2', 'home', 'cooling'], ['account-1', 'account-1', 'out'], ['account-3', 'API key …abcd', 'refused'],
    ]);
    expect(row.detail).toBe(`home cools until ${moment(UNTIL)}; account-1 read signed out at ${clockOf(READ, NOW)}; API key …abcd: key refused.`);
    expect(attentionOffers(row).filter(({ act }) => act === 'sign-in')).toEqual([{ act: 'sign-in', account: 'account-1' }]);
  });

  /** The tool's own sign-in is named as the person knows it, and has no sign-in of Daoris's. */
  it('names the tool’s own sign-in where nothing names an account', () => {
    const row = worded(waiting({ account: facts({ named: [named(null, 'cooling', { read: null })] }) }));
    expect(row.account!.named).toEqual([expect.objectContaining({ id: null, label: 'Your own sign-in', state: 'cooling' })]);
    expect(row.detail).toBe(`Your own sign-in cools until ${moment(UNTIL)}.`);
  });

  /** Before the roster answers, the names the look read stand, else each account's id. */
  it('names each account by the look’s names before the roster answers', () => {
    const row = attentionWorded(waiting({ account: facts({ product: 'claude-code', harness: null, signsIn: false, named: waiting().account!.named }) }), {}, NOW);
    expect(row.account).toMatchObject({ product: 'claude-code', harness: null, signsIn: false });
    expect(row.account!.named.map(({ label }) => label)).toEqual(['home', 'account-1', 'account-3']);
  });

  /** D130 §3.3, UXFIX3: the one outside the list to let in, or to read first, named as the roster names it. */
  it('names the account outside the list it may let in, or read first', () => {
    const outside = worded(waiting({ account: facts({ named: [named('account-2', 'cooling')], outside: { id: 'account-4', list: 'work', workspace: 'work' } }) }));
    expect(outside.account!.outside).toEqual({ id: 'account-4', label: 'account-4', list: 'work', workspace: 'work' });
    expect(attentionOffers(outside)).toEqual([{ act: 'let-run', account: 'account-4' }]);

    const first = worded(waiting({ account: facts({ named: [named('account-1', 'out')], readFirst: { id: 'account-4' } }) }));
    expect(first.account!.readFirst).toEqual({ id: 'account-4', label: 'account-4' });
    expect(attentionOffers(first)).toEqual([{ act: 'sign-in', account: 'account-1' }, { act: 'read', account: 'account-4' }]);
  });

  /** OWNSPACE1b-B (D175 point 3): no Daoris account signed in names no account, says why, and offers *Sign in for Daoris*. */
  it('says no Daoris account is signed in, and offers Sign in for Daoris', () => {
    const row = worded(waiting({ id: 'wait:claude-code:no-account', holding: { quests: [], asks: ['a1'] }, account: facts({ noAccount: true }) }), [door({ profiles: [] })]);

    expect(row).toMatchObject({
      kind: 'account-wait', title: 'Intake for ask #a1', where: 'work', circle: true,
      detail: 'No Daoris account is signed in for Claude Code, so its starts wait until you sign in.',
    });
    expect(row.account).toMatchObject({ noAccount: true, signsIn: true, harness: 'claude-code' });
    expect(attentionOffers(row)).toEqual([{ act: 'sign-in-new' }]);
  });
});

describe('a signed-out account a list holds, worded', () => {
  const signedOut = (account: string, over: Partial<PushedAttention> = {}): PushedAttention => ({
    kind: 'signed-out', id: `signed-out:claude-code/${account}`, since: null, read: READ, where: 'Claude Code', runs: ['work'],
    account: facts({ named: [named(account, 'out')] }),
    ...over,
  });

  /** UXFIX3: only when it was read is known, so the row says *read …* and no wait. */
  it('names the account, where it runs, and when it was read, never how long it waited', () => {
    const row = worded(signedOut('account-1'));
    expect(row).toMatchObject({ kind: 'signed-out', title: 'account-1', where: 'Claude Code', since: null, read: READ, detail: 'It runs work in work.' });
    expect(row.account!.named).toEqual([expect.objectContaining({ id: 'account-1', state: 'out' })]);
    expect(attentionOffers(row)).toEqual([{ act: 'sign-in', account: 'account-1' }]);
  });

  /** ACCTUX1: a key its provider refused says so, and where it is repaired: its agent's page, where a new key is added. */
  it('says a key its provider refused, and where it is repaired', () => {
    const doors = [door({ profiles: door().profiles!.map((profile) => (profile.name === 'account-3' ? { ...profile, key: '…abcd' } : profile)) })];
    const row = worded(signedOut('account-3', { account: facts({ named: [named('account-3', 'refused')] }) }), doors);
    expect(row).toMatchObject({
      title: 'API key …abcd', detail: "Key refused: put a new API key into it on Claude Code's page. It runs work in work.",
    });
    expect(attentionOffers(row)).toEqual([]);
  });

  /** A signed-out account where no list names a workspace runs on this machine's, said as the Agents place says it. */
  it('says where it runs on this machine', () => {
    expect(worded(signedOut('account-1', { runs: [null] })).detail).toBe('It runs work in this machine.');
  });

  /** ACCOUNTID1b: two accounts signed in as one email in two organisations read alike; each row says its own. */
  it('tells two signed-out accounts of one email apart by their organisations', () => {
    const email = 'person@example.invalid';
    const doors = [door({
      profiles: [
        { name: 'acct-0000000a', home: 'H/a', login: 'out', read: READ, account: email, org: 'Org A' },
        { name: 'acct-0000000b', home: 'H/b', login: 'out', read: READ, account: email, org: 'Org B' },
      ],
    })];
    const rows = ['acct-0000000a', 'acct-0000000b'].map((account) => worded(signedOut(account), doors));

    expect(rows.map(({ title }) => title)).toEqual([`${email} · Org A`, `${email} · Org B`]);
    expect(rows[1]!.account!.named).toEqual([expect.objectContaining({ id: 'acct-0000000b', label: `${email} · Org B` })]);
  });
});
