import { beforeEach, describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import { ago, clockOf, moment } from '../locales/format';
import { byTool, type ToolDoor } from './tools';
import { scopeOf, THREE } from '../settings/accounts/accountsFixtures';
import type { AgentAccounts } from '../settings/accounts/accounts';
import {
  accountAct, accountLabel, accountName, accountState, accountStates, accountWho, agentRows, ownWho, doorsSummary, heldBy, nextStartSaid,
  pooledLines, readLine, rulesSummary, settingsSummary, signedOutHeld, stateWhen, stateWord, usageCells, usageSummary, useSummary,
  versionOnly,
} from './agents';
import type { PoolReceipt } from '../settings/accounts/accounts';

// The Agents place's words and facts (UX6e, D150 §5), pure: an account's last known state with when it was read, the list's
// rows, and each folded section's line. No bridge, so every state is an argument.

const TEN = '2026-10-04T10:42:00.000Z';
const NOW = new Date('2026-10-04T12:00:00.000Z');

const claude = (over: Partial<ToolDoor> = {}): ToolDoor => ({
  harness: 'claude-code', product: 'Claude Code', maker: 'Anthropic', present: true, version: '2.1.4 (Claude Code)',
  wire: 'pipe', ownLogin: 'unknown', ownRead: null,
  profiles: [
    { name: 'account-1', home: 'h/1', login: 'out', account: 'you@work', read: TEN },
    { name: 'account-2', home: 'h/2', login: 'in', account: 'you@home', read: TEN },
    { name: 'account-3', home: 'h/3', login: 'out', account: 'spare@', read: TEN },
  ],
  ...over,
});

describe('an account’s state, as last known', () => {
  beforeEach(() => i18n.changeLanguage('en'));

  it('says signed out where the last read said so, in open’s hue only while a list or a default holds it', () => {
    const held = accountState({ login: 'out', read: TEN }, null, true);
    expect(held).toMatchObject({ state: 'out', read: TEN, holdsWork: true });
    expect(stateWord(held)).toEqual({ label: 'signed out', tone: 'open' });
    expect(stateWord(accountState({ login: 'out', read: TEN }, null, false)).tone).toBe('neutral');
  });

  it('says signed in in the neutral hue, never done’s', () => {
    expect(stateWord(accountState({ login: 'in', read: TEN }, null, true))).toEqual({ label: 'signed in', tone: 'neutral' });
  });

  it('says cooling until the time its agent named, before a sign-in it still has', () => {
    const cooling = { until: '2026-10-06T04:42:00.000Z', stated: true, window: 'weekly', seen: TEN, assumedZone: false, notBelieved: false };
    const state = accountState({ login: 'in', read: TEN }, { cooling }, true);
    expect(state.state).toBe('cooling');
    // The word on its pill, and the hold's end beside it in the column; the tip says until when, how long and why.
    expect(stateWord(state).label).toBe('cooling');
    expect(stateWhen(state, NOW)).toMatch(/^until /);
    expect(readLine(state, NOW)).toMatch(/^Cooling until .+ · in /);
  });

  /**
   * ACCTUX1: the cool-off is a hold, and where Daoris chose its length because the agent named no reset, *resets …* claimed a
   * reset nobody reported. The column says until when it holds, in either language, whoever chose the time.
   */
  it('says a cool-off as a hold until its end, never a reset, whoever chose the time', () => {
    const chosen = { until: '2026-10-06T04:42:00.000Z', stated: false, window: null, seen: TEN, assumedZone: false, notBelieved: false };
    const state = accountState({ login: 'in', read: TEN }, { cooling: chosen }, true);
    expect(stateWhen(state, NOW)).toMatch(/^until /);
    expect(stateWhen(state, NOW)).not.toMatch(/reset/);
    i18n.changeLanguage('zh');
    expect(stateWhen(state, NOW)).toMatch(/^至 /);
    expect(stateWhen(state, NOW)).not.toMatch(/重置/);
  });

  /**
   * ACCTUX1: a key its provider refused reads signed out, and saying it *unchecked* told the person nothing was wrong. A
   * refusal is kept whatever the account is, ahead of a cool-off as a sign-out is, since a reset frees neither.
   */
  it('says a key its provider refused as Key refused, ahead of a cool-off, in open’s hue while it holds work', () => {
    const cooling = { until: '2026-10-06T04:42:00.000Z', stated: true, window: null, seen: TEN, assumedZone: false, notBelieved: false };
    const refused = accountState({ login: 'out', read: TEN, key: '…abcd' }, null, true);
    expect(refused).toMatchObject({ state: 'refused', read: TEN, holdsWork: true });
    expect(accountState({ login: 'out', read: TEN, key: '…abcd' }, { cooling }, true).state).toBe('refused');
    expect(stateWord(refused)).toEqual({ label: 'key refused', tone: 'open' });
    expect(stateWord(accountState({ login: 'out', read: TEN, key: '…abcd' }, null, false)).tone).toBe('neutral');
    // A key the tool merely says is signed in, as it does for any key, stays unchecked.
    expect(accountState({ login: 'in', read: TEN, key: '…abcd' }, null, true).state).toBe('keyed');
    i18n.changeLanguage('zh');
    expect(stateWord(refused).label).toBe('密钥被拒绝');
  });

  it('says when beside the word: the clock alone, never read, or that the last read failed', () => {
    expect(stateWhen(accountState({ login: 'in', read: TEN }, null, false), NOW)).toMatch(/^\d{2}:\d{2}$/);
    expect(stateWhen(accountState({ login: 'unknown', read: null }, null, false), NOW)).toBe('never read');
    expect(stateWhen(accountState({ login: 'unknown', read: TEN }, null, false), NOW)).toMatch(/^the last read failed, /);
  });

  it('keeps signed out ahead of a cool-off: a sign-in is the person’s, and the reset frees nothing', () => {
    const cooling = { until: '2026-10-06T04:42:00.000Z', stated: true, window: null, seen: TEN, assumedZone: false, notBelieved: false };
    expect(accountState({ login: 'out', read: TEN }, { cooling }, true).state).toBe('out');
  });

  it('says unknown, never signed in, where nothing has answered: absent is never zero', () => {
    const state = accountState({ login: 'unknown', read: null }, null, true);
    expect(state.state).toBe('unknown');
    expect(readLine(state, NOW)).toBe('never read');
    expect(readLine(accountState({ login: 'unknown', read: TEN }, null, true), NOW)).toMatch(/^the last read failed, /);
  });

  it('says an API key by its handle and no sign-in: the tool says signed in for any key', () => {
    expect(accountState({ login: 'in', read: TEN, key: 'abcd' }, null, true).state).toBe('keyed');
  });

  it('says when each word was read, today by its clock', () => {
    expect(readLine(accountState({ login: 'in', read: '2026-10-04T10:42:00.000Z' }, null, false), NOW)).toMatch(/^read .*\d{2}:\d{2}/);
  });
});

describe('which accounts hold work', () => {
  /**
   * POOL1c (D197 point 1; the account-pool design §4.2): every account signed in for Daoris serves every workspace, so each one
   * holds work, whatever an order names, and a signed-out one is loud. An account the agent does not have holds nothing.
   */
  it('is every account of Daoris’s, one set for every workspace, whatever the order names', () => {
    const [tool] = byTool([claude()]);
    for (const name of ['account-1', 'account-2', 'account-3']) expect(heldBy(tool!, name)).toBe(true);
    expect(heldBy(tool!, 'account-9')).toBe(false);
    expect([...accountStates(tool!, { ...THREE, scopes: [scopeOf()] }).values()].every((state) => state.holdsWork)).toBe(true);
  });

  it('counts every signed-out account, the badge’s number, whatever the order names', () => {
    const [tool] = byTool([claude()]);
    expect(signedOutHeld(tool!, THREE)).toBe(2);
    expect(signedOutHeld(tool!, { ...THREE, scopes: [scopeOf()] })).toBe(2);
    expect(signedOutHeld(tool!, null)).toBe(2);
  });

  /** ACCTUX1: a refused key holding work waits on the person as a signed-out account does, so the badge counts it. */
  it('counts a refused key that holds work, and says it in the list’s phrase', () => {
    const [tool] = byTool([claude({
      profiles: [
        { name: 'account-1', home: 'h/1', login: 'out', account: 'you@work', read: TEN },
        { name: 'account-2', home: 'h/2', login: 'out', key: '…abcd', read: TEN },
      ],
    })]);
    expect(signedOutHeld(tool!, THREE)).toBe(2);
    expect(agentRows([tool!], { agents: [THREE] })[0]!.phrase).toBe('2 accounts · 1 signed out · 1 key refused');
  });

  /** OWNSPACE1b-A (D175), POOL1c (D197 point 3): nothing starts in the tool's own sign-in, so it is never counted. */
  it("never counts the tool's own sign-in, even signed out with no account of Daoris's", () => {
    const [tool] = byTool([claude({ ownLogin: 'out', profiles: [] })]);
    const none: AgentAccounts = { agent: 'claude-code', speaks: false, own: {}, accounts: [], scopes: [scopeOf()] };
    expect(signedOutHeld(tool!, none)).toBe(0);
  });
});

describe('the list', () => {
  beforeEach(() => i18n.changeLanguage('en'));

  it('lists each agent once, whatever doors reach it, with its accounts in a phrase', () => {
    const doors: ToolDoor[] = [
      claude(),
      { ...claude(), harness: 'claude-code-acp', wire: 'acp', accountOf: 'claude-code' },
      { harness: 'codex-acp', accountOf: 'codex', product: 'Codex', maker: 'OpenAI', present: true, wire: 'acp', profiles: [{ name: 'account-1', home: 'c/1', login: 'in' }] },
      { harness: 'dsh', product: 'DeepSeek Harness', maker: 'DeepSeek', present: false, profiles: [] },
    ];
    const rows = agentRows(byTool(doors), { agents: [THREE] });
    expect(rows.map((row) => row.name)).toEqual(['claude-code', 'codex', 'dsh']);
    expect(rows[0]).toMatchObject({ phrase: '3 accounts · 2 signed out', waiting: 2, installed: true });
    expect(rows[1]!.phrase).toBe('1 account');
    expect(rows[2]).toMatchObject({ phrase: 'not installed', installed: false });
  });

  /**
   * ACCTUX2: the strip drew each agent's first character, so Claude Code and Codex were both `C` (the UX7 design §4.6). A
   * mark its declaration gives leads; else the first letters, each word's initial or a lone word's first two; and none
   * repeats a mark above it in the list.
   */
  /** INSTALLDOOR1: a row says whether Daoris has an installer for it; a shell that sends no `installs` reads as one that does. */
  it('says whether Daoris installs each agent, an absent field reading as it does', () => {
    const rows = (door: Partial<ToolDoor>) => agentRows(byTool([{ ...claude(), present: false, ...door }]), undefined)[0]!;
    expect(rows({}).installs).toBe(true);
    expect(rows({ installs: true }).installs).toBe(true);
    expect(rows({ installs: false }).installs).toBe(false);
  });

  it('gives each agent a mark of its own: a declared one, else its first letters, never one already worn', () => {
    const doors: ToolDoor[] = [
      claude(),
      { harness: 'codex-acp', accountOf: 'codex', product: 'Codex', present: true, wire: 'acp', profiles: [] },
      { harness: 'dsh', product: 'DeepSeek Harness', present: false, profiles: [] },
      { harness: 'cody', product: 'Cody', present: true, profiles: [] },
      { harness: 'acme-agent', present: true, profiles: [] },
    ];
    expect(agentRows(byTool(doors), undefined).map((row) => row.mark)).toEqual(['CC', 'Co', 'DH', 'Cd', 'Ac']);

    // A mark the agent's declaration gives is its own, as written, a letter or two of it.
    const declared = doors.map((door) => (door.harness === 'codex-acp' ? { ...door, mark: ' Cx ' } : door));
    expect(agentRows(byTool(declared), undefined).map((row) => row.mark)).toEqual(['CC', 'Cx', 'DH', 'Co', 'Ac']);
    expect(agentRows(byTool([{ ...claude(), mark: 'Claude' }]), undefined)[0]!.mark).toBe('Cl');
    // INSTALLDOOR1: a null mark falls back to the letters.
    expect(agentRows(byTool([{ ...claude(), mark: null }]), undefined)[0]!.mark).toBe('CC');

    // A 中文 name gives its first two characters whole.
    expect(agentRows(byTool([{ harness: 'yinqing', product: '引擎', present: true, profiles: [] }]), undefined)[0]!.mark).toBe('引擎');
  });

  it('says an agent with no Daoris account has none yet, not that it runs on its own sign-in', () => {
    const rows = agentRows(byTool([claude({ profiles: [] })]), undefined);
    expect(rows[0]!.phrase).toBe('no Daoris account yet');
    // A plugin's agent that signs in nowhere is installed, and nothing more is claimed of it.
    const declared = agentRows(byTool([{ harness: 'acme-agent', present: true, signsIn: false, profiles: [] }]), undefined);
    expect(declared[0]!.phrase).toBe('installed');
  });
});

describe('an account’s name, as a person reads it (ACCT2)', () => {
  beforeEach(() => i18n.changeLanguage('en'));

  it('is the person’s name where they gave one, with who signed in beside it only where the two differ', () => {
    const named = { name: 'acct-1a2b3c4d', displayName: 'spare', account: 'spare@example.invalid' };
    expect(accountName(named)).toBe('spare');
    expect(accountWho(named)).toBe('spare@example.invalid');
    const same = { ...named, displayName: 'spare@example.invalid' };
    expect(accountName(same)).toBe('spare@example.invalid');
    expect(accountWho(same)).toBeNull();
  });

  /**
   * ACCTNAME1's page half: the owner named each account by the email it signs in as, so a name and its identity that differ
   * only in case, or in the spaces around them, are one name said once, as every name here is compared without case.
   */
  it('says a name that is its identity once, whatever its case', () => {
    expect(accountWho({ name: 'Gmail', displayName: 'You@Example.invalid', account: 'you@example.invalid' })).toBeNull();
    expect(accountWho({ name: 'Gmail', displayName: ' you@example.invalid', account: 'you@example.invalid ' })).toBeNull();
    expect(accountWho({ name: 'Gmail', displayName: 'you@example.invalid', account: 'me@example.invalid' })).toBe('me@example.invalid');
  });

  it('never leads with a fresh id the person never chose: who signed in stands for it until they name it', () => {
    const fresh = { name: 'acct-1a2b3c4d', displayName: null, account: 'spare@example.invalid' };
    expect(accountName(fresh)).toBe('spare@example.invalid');
    expect(accountWho(fresh)).toBeNull();
    expect(accountName({ name: 'acct-1a2b3c4d' })).toBe('acct-1a2b3c4d');
  });

  it('keeps an old account-N as its name, the word every list and session head says, with who signed in beside it', () => {
    const old = { name: 'account-1', account: 'you@work.example' };
    expect(accountName(old)).toBe('account-1');
    expect(accountWho(old)).toBe('you@work.example');
  });

  it('says a key by its handle, and no one signed in beside it', () => {
    expect(accountName({ name: 'acct-0000ffff', key: '…wxyz', account: 'x@y' })).toBe('API key …wxyz');
    expect(accountWho({ name: 'acct-0000ffff', key: '…wxyz' })).toBeNull();
  });
});

/**
 * ACCOUNTID1b: one email signed in to two organisations is two accounts that read alike. The organisation is said where it
 * tells two of one agent's sign-ins apart, after the email and each said once (ACCTNAME1's rule); else the plan; else nothing.
 */
describe('two sign-ins of one email, told apart (ACCOUNTID1b)', () => {
  beforeEach(() => i18n.changeLanguage('en'));

  const EMAIL = 'person@example.invalid';
  const tool = (profiles: ToolDoor['profiles'], over: Partial<ToolDoor> = {}) => byTool([claude({ profiles, ...over })])[0]!;
  const a = { name: 'acct-0000000a', home: 'h/a', login: 'in' as const, account: EMAIL, org: 'Org A', plan: 'max' };
  const b = { name: 'acct-0000000b', home: 'h/b', login: 'in' as const, account: EMAIL, org: 'Org B', plan: 'pro' };

  it('carries the organisation and plan of the tool’s own sign-in from its door', () => {
    const both = tool([a], { ownAccount: EMAIL, ownOrg: 'Org B', ownPlan: 'pro' });
    expect([both.ownOrg, both.ownPlan]).toEqual(['Org B', 'pro']);
    expect(both.accounts[0]).toMatchObject({ org: 'Org A', plan: 'max' });
  });

  it('says the organisation after a name that is the email, and once beside it', () => {
    const among = tool([a, b]);
    expect(accountName(a)).toBe(EMAIL);
    expect(accountWho(a, among)).toBe('Org A');
    expect(accountWho(b, among)).toBe('Org B');
    expect(accountLabel(a, among)).toBe(`${EMAIL} · Org A`);
    expect(accountLabel(b, among)).toBe(`${EMAIL} · Org B`);
  });

  it('repeats neither in a name that says both, as the sign-in’s end offers it', () => {
    const named = { ...b, displayName: `${EMAIL}·Org-B` };
    const among = tool([a, named]);
    expect(accountWho(named, among)).toBeNull();
    expect(accountLabel(named, among)).toBe(`${EMAIL}·Org-B`);
  });

  it('says the email and its organisation beside a name of the person’s own, and the name alone where one line is room', () => {
    const named = { ...b, displayName: 'work' };
    const among = tool([a, named]);
    expect(accountWho(named, among)).toBe(`${EMAIL} · Org B`);
    expect(accountLabel(named, among)).toBe('work');
  });

  it('says the plan where the answer named no organisation, and nothing where it named neither', () => {
    const planned = { ...b, org: null };
    expect(accountLabel(planned, tool([a, planned]))).toBe(`${EMAIL} · pro`);
    const bare = { ...b, org: null, plan: null };
    expect(accountWho(bare, tool([a, bare]))).toBeNull();
    expect(accountLabel(bare, tool([a, bare]))).toBe(EMAIL);
  });

  /** As `agent list` says it (the CLI's `apartBy`): a part both sign-ins share tells nothing apart, so the next is said. */
  it('says the plan where both sign-ins are in one organisation, and nothing where neither part differs', () => {
    const same = { ...b, org: 'Org A' };
    expect(accountLabel(same, tool([a, same]))).toBe(`${EMAIL} · pro`);
    const twin = { ...b, org: 'Org A', plan: 'max' };
    expect(accountLabel(twin, tool([a, twin]))).toBe(EMAIL);
    expect(accountWho(twin, tool([a, twin]))).toBeNull();
  });

  it('says no organisation where no other sign-in of the agent has the email, in any case', () => {
    const other = { ...b, account: 'other@example.invalid' };
    const among = tool([a, other]);
    expect(accountWho(a, among)).toBeNull();
    expect(accountLabel(a, among)).toBe(EMAIL);
    const shouted = { ...b, account: EMAIL.toUpperCase() };
    expect(accountLabel(a, tool([a, shouted]))).toBe(`${EMAIL} · Org A`);
    // A list handed none tells nothing apart.
    expect(accountLabel(a)).toBe(EMAIL);
  });

  it('counts the tool’s own sign-in among them, and tells it apart too', () => {
    const among = tool([b], { ownAccount: EMAIL, ownOrg: 'Org A', ownPlan: 'max' });
    expect(accountLabel(b, among)).toBe(`${EMAIL} · Org B`);
    expect(ownWho(among)).toBe(`${EMAIL} · Org A`);
    expect(ownWho(tool([b], { ownAccount: 'other@example.invalid', ownOrg: 'Org A' }))).toBe('other@example.invalid');
  });

  it('never tells a key apart: it names no one signed in', () => {
    const key = { name: 'acct-0000000c', home: 'h/c', login: 'in' as const, key: '…wxyz', account: EMAIL, org: 'Org C' };
    expect(accountLabel(key, tool([a, key]))).toBe('API key …wxyz');
    expect(accountLabel(a, tool([a, key]))).toBe(EMAIL);
  });
});

/** ACCTUX4b (the second opinion's lines 128-133): the set's next start, said above the accounts in the fold's words. */
describe('the next start, above the accounts', () => {
  beforeEach(() => i18n.changeLanguage('en'));
  const labelOf = (name: string) => name;

  it('says which account it takes and why, then what holds the account the list begins on', () => {
    const machine = THREE.scopes[0]!;
    expect(nextStartSaid(machine, labelOf)).toEqual({
      takes: "The next start takes account-3: it runs fewer of Daoris's sessions than account-2.",
      held: `account-1 is cooling until ${moment(machine.next!.others[0]!.until!)}.`,
    });
  });

  it('says only what holds the account the list begins on, the one a person expects it to take; the fold says the rest', () => {
    const until = '2026-10-04T14:20:00.000Z';
    const list = ['account-1', 'account-2', 'account-3'];
    const others = [
      { account: 'account-1', hold: 'cooling' as const, until }, { account: 'account-2', hold: 'signedOut' as const },
      { account: 'account-4', hold: 'outside' as const },
    ];
    const passed = scopeOf({ list, begins: 'account-1', next: { account: 'account-3', reason: 'onlyReady', others } });
    expect(nextStartSaid(passed, labelOf)?.held).toBe(`account-1 is cooling until ${moment(until)}.`);
    // It takes the list's first: nothing passed over to explain, whatever holds the rest.
    const head = scopeOf({ list, begins: 'account-3', next: { account: 'account-3', reason: 'onlyReady', others } });
    expect(nextStartSaid(head, labelOf)?.held).toBeNull();
    // The reason names the list's first already: said once, not again as its hold.
    const near = scopeOf({
      list, begins: 'account-1', near: [{ account: 'account-1', window: 'weekly', by: 'number' }],
      next: { account: 'account-3', reason: 'near', over: 'account-1', others: [{ account: 'account-1', hold: 'near' }] },
    });
    expect(nextStartSaid(near, labelOf)).toEqual({
      takes: 'The next start takes account-3: account-1 is over its curve on its weekly window, so it goes last.', held: null,
    });
    // Passed while another reason names a third account: its hold says which of the three holds it (SWITCH1b-iii).
    const held = scopeOf({
      list, begins: 'account-1', near: [{ account: 'account-1', window: 'session', by: 'refused' }],
      next: { account: 'account-3', reason: 'fewest', over: 'account-2', others: [{ account: 'account-1', hold: 'near' }] },
    });
    expect(nextStartSaid(held, labelOf)?.held).toBe('account-1 has reached its five-hour limit.');
  });

  it('says a wait and what frees it, and not again account by account, which the wait’s sentence already names', () => {
    const until = '2026-10-04T14:20:00.000Z';
    const scope = scopeOf({
      list: ['account-1', 'account-2'], begins: 'account-1',
      next: {
        account: null, reason: 'waits', when: until,
        others: [{ account: 'account-1', hold: 'cooling', until }, { account: 'account-2', hold: 'signedOut' }],
      },
    });
    expect(nextStartSaid(scope, labelOf)).toEqual({
      takes: `No account here is ready, so the next start waits until ${moment(until)}. Signing in to account-2 starts it sooner.`,
      held: null,
    });
  });

  it('says nothing where it runs in a test double\'s own home, or where the shell is older than the next start', () => {
    expect(nextStartSaid(scopeOf({ next: { account: null, reason: 'own', others: [] } }), labelOf)).toBeNull();
    expect(nextStartSaid(scopeOf({ list: ['account-1'] }), labelOf)).toBeNull();
    expect(nextStartSaid(null, labelOf)).toBeNull();
  });

  /** OWNSPACE1b-A (D175 point 4): no Daoris account signed in is its own line, said apart from a wait. */
  it('says no Daoris account is signed in, once, where the reason arrives', () => {
    expect(nextStartSaid(scopeOf({ next: { account: null, reason: 'noAccount', others: [] } }), labelOf)).toEqual({
      takes: 'No Daoris account is signed in for it; its starts wait until you sign in.', held: null,
    });
  });

  it('says it in one sentence in 中文, with no space after a full-width mark', async () => {
    await i18n.changeLanguage('zh');
    const said = nextStartSaid(THREE.scopes[0]!, labelOf)!;
    expect(said.takes).not.toMatch(/[：，。] /);
    expect(said.takes).not.toMatch(/本机/);
  });
});

describe('the one act an account’s state asks for (D152 §4)', () => {
  const state = (name: 'in' | 'out' | 'unknown' | 'cooling' | 'keyed' | 'refused', holdsWork = true) => ({
    state: name, read: null, holdsWork,
    cooling: name === 'cooling' ? { until: TEN, stated: true, seen: TEN, assumedZone: false, notBelieved: false } : null,
  });

  it('is Sign in when signed out, Read when unknown, Try now when cooling', () => {
    expect(accountAct(state('out'), { signsIn: true, present: true })).toEqual({ act: 'signIn', loud: true });
    expect(accountAct(state('unknown', false), { signsIn: true, present: true })).toEqual({ act: 'read', loud: false });
    expect(accountAct(state('cooling'), { signsIn: true, present: true })).toEqual({ act: 'tryNow', loud: false });
  });

  /** POOL1c (the account-pool design §4.2): *Use in a workspace…* goes, since every account runs for every workspace. */
  it('is none for a signed-in account or a key: every account serves every workspace', () => {
    expect(accountAct(state('in'), { signsIn: true, present: true })).toBeNull();
    expect(accountAct(state('keyed'), { signsIn: true, present: true })).toBeNull();
  });

  it('offers no sign-in where the agent has none, nor a read where it is not installed', () => {
    expect(accountAct(state('out'), { signsIn: false, present: true })).toBeNull();
    expect(accountAct(state('unknown'), { signsIn: true, present: false })).toBeNull();
  });

  /**
   * ACCTUX1: a refused key is repaired by a key, never a sign-in: *New API key* into the same account (KEYREPLACE1b), loud
   * where it holds work. Where the agent takes no key from Daoris there is no act to offer.
   */
  it('is New API key when its key was refused, and none where the agent takes no key', () => {
    expect(accountAct(state('refused'), { signsIn: false, present: true, takesKey: true })).toEqual({ act: 'newKey', loud: true });
    expect(accountAct(state('refused', false), { signsIn: false, present: true, takesKey: true }))
      .toEqual({ act: 'newKey', loud: false });
    expect(accountAct(state('refused'), { signsIn: true, present: true })).toBeNull();
    expect(accountAct(state('refused'), { signsIn: false, present: false, takesKey: true })).toBeNull();
  });
});

/**
 * ACCTUX4 (the second opinion's account section, adopted): what an account's agent last said, as stable cells, a window each,
 * the same two for both makers, `session` then `weekly`, then any other a reading named. Each says its exact share, its
 * agent's own word, its reset and how long ago its own reading was; a window its agent did not report is unknown, in words,
 * with no bar and never 0% (D130 §5.2). One sentence of every window compared badly across windows and makers. ACCTUX1 holds:
 * a share is a reading in the ink, a time the soft ink, and a cool-off whose length Daoris chose says the reset is unknown.
 */
describe('what an account’s agent last said, a cell a window', () => {
  beforeEach(() => i18n.changeLanguage('en'));
  const SEEN = '2026-10-04T11:00:00.000Z';
  const EARLIER = '2026-10-04T09:30:00.000Z';
  const RESET = '2026-10-04T15:00:00.000Z';
  const WEEK = '2026-10-09T17:00:00.000Z';
  const said = {
    seen: SEEN,
    windows: [
      { window: 'weekly', used: 0.15, reset: WEEK, credits: false, seen: EARLIER },
      { window: 'session', used: 0.88, reset: RESET, standing: 'warned', credits: false, seen: SEEN },
    ],
  };
  // SWITCH1d: the five-hour window read from its agent, the week said by a session, 62% of it used; each against its scope's curve
  // as .NET judges it and the accounts' answer carries it (PUSH1e): the smart curve's 90% on the session, and 43 hours of the week's
  // 168 gone, so the smart week stands at 46% of it and 62% is over it.
  const measured = {
    seen: SEEN,
    windows: [
      { window: 'session', used: 0.88, reset: RESET, credits: false, seen: SEEN, source: 'read' as const, curve: 90, over: false },
      { window: 'weekly', used: 0.62, reset: WEEK, credits: false, seen: EARLIER, source: 'said' as const, curve: 46, over: true },
    ],
  };
  const signedIn = accountState({ login: 'in', read: TEN }, null, true);
  const cooling = (stated: boolean) => accountState({ login: 'in', read: TEN }, {
    cooling: { until: '2026-10-04T16:02:00.000Z', stated, window: stated ? 'session' : null, seen: TEN, assumedZone: false, notBelieved: false },
  }, true);

  it('says each window in a cell of its own, the session first: its exact share, its own word, its reset and its own reading’s age', () => {
    const usage = usageCells({ said }, signedIn, { now: NOW })!;
    expect(usage.cells).toEqual([
      {
        window: 'session', name: 'five-hour', known: true, used: 0.88, warned: true,
        reading: '88% used, ahead of its pace, by its own word', times: [`resets ${clockOf(RESET, NOW)}`, `said by a session ${ago(SEEN)}`],
      },
      {
        window: 'weekly', name: 'weekly', known: true, used: 0.15, warned: false,
        reading: '15% used', times: [`resets ${clockOf(WEEK, NOW)}`, `said by a session ${ago(EARLIER)}`],
      },
    ]);
    expect(usage.notes).toEqual([]);
  });

  // USAGEPROBE1b: the projection to now beside the reading, said as projected and only where it differs or cannot be made.
  describe('its projected use beside its reading', () => {
    const projectedOf = (window: Record<string, unknown>) =>
      usageCells({ said: { seen: SEEN, windows: [{ window: 'session', used: 0.2, reset: RESET, credits: false, seen: SEEN, source: 'read', ...window }] } }, signedIn, { now: NOW })!
        .cells[0]!.projected;

    it('says a measured projection that differs from the reading, as projected from Daoris’s own turns', () => {
      expect(projectedOf({ projected: 0.234, projectedBy: 'measured' }))
        .toBe("about 23% by now, projected from Daoris's own turns since");
    });

    it('says it cannot project where Daoris cannot weigh its turns since', () => {
      expect(projectedOf({ projected: 0.2, projectedBy: 'unweighed' })).toBe('not projected: Daoris cannot weigh its turns since');
    });

    it('says nothing where the projection is the reading, rounds to it, or an older shell sent none', () => {
      expect(projectedOf({ projected: 0.2, projectedBy: 'reading' })).toBeUndefined();
      expect(projectedOf({ projected: 0.2004, projectedBy: 'measured' })).toBeUndefined();
      expect(projectedOf({})).toBeUndefined();
    });

    it('says each in 中文', async () => {
      await i18n.changeLanguage('zh');
      expect(projectedOf({ projected: 0.234, projectedBy: 'measured' })).toBe('此刻约 23%，按 Daoris 此后自己的轮次推算');
      expect(projectedOf({ projected: 0.2, projectedBy: 'unweighed' })).toBe('未推算：Daoris 无法衡量此后自己的轮次');
      expect(projectedOf({ projected: 0.2, projectedBy: 'reading' })).toBeUndefined();
    });
  });

  it('says a window its agent did not report unknown, in words with no bar, never 0%', () => {
    const weekOnly = { seen: SEEN, windows: [said.windows[0]!] };
    const [session, weekly] = usageCells({ said: weekOnly }, signedIn, { now: NOW })!.cells;
    expect(session).toEqual({ window: 'session', name: 'five-hour', known: false, used: null, warned: false, reading: 'unknown', times: [] });
    expect(weekly).toMatchObject({ window: 'weekly', known: true, used: 0.15, reading: '15% used' });
  });

  it('draws the same two cells where its agent speaks, each unknown until read, and another window after them', () => {
    expect(usageCells({}, signedIn, { fixed: true, now: NOW })!.cells.map((cell) => [cell.window, cell.reading, cell.used]))
      .toEqual([['session', 'unknown', null], ['weekly', 'unknown', null]]);
    // Where nothing could be said (a key, an agent that does not speak), nothing said is not said (D152 §4.2).
    expect(usageCells({}, signedIn, { now: NOW })).toBeNull();
    expect(usageCells({ said: { seen: SEEN, windows: [] } }, signedIn, { now: NOW })).toBeNull();
    // A Codex window of another length (CODEXUSE1) after the two, in a cell of its own, named as the driver names it.
    const odd = { seen: SEEN, windows: [{ window: '90-minute', used: 0.4, reset: RESET, credits: false, seen: SEEN }] };
    expect(usageCells({ said: odd }, signedIn, { now: NOW })!.cells.map((cell) => [cell.window, cell.name, cell.reading]))
      .toEqual([['session', 'five-hour', 'unknown'], ['weekly', 'weekly', 'unknown'], ['90-minute', '90-minute', '40% used']]);
  });

  it('says a window its agent gave no share for by its own word, with no bar', () => {
    const reached = { seen: SEEN, windows: [{ window: 'weekly', used: null, reset: WEEK, standing: 'refused', credits: true, seen: SEEN }] };
    expect(usageCells({ said: reached }, signedIn, { now: NOW })!.cells[1]).toEqual({
      window: 'weekly', name: 'weekly', known: true, used: null, warned: true,
      reading: 'limit reached, by its own word, drawing on usage credits', times: [`resets ${clockOf(WEEK, NOW)}`, `said by a session ${ago(SEEN)}`],
    });
    const clear = { seen: SEEN, windows: [{ window: 'session', used: null, reset: RESET, standing: 'clear', credits: false, seen: SEEN }] };
    expect(usageCells({ said: clear }, signedIn, { now: NOW })!.cells[0])
      .toMatchObject({ known: true, used: null, warned: false, reading: 'clear, by its own word' });
  });

  /**
   * SWITCH1d (the switch design §5): each window against its scope's curve now, the curve's value said, a window over it said so
   * and drawn in the warning hue; and where its reading came from, read from its agent or said by a session, with its age.
   */
  it('says each window against its scope’s curve now as .NET judged it, and where its reading came from', () => {
    const [session, weekly] = usageCells({ said: measured }, signedIn, { now: NOW, product: 'Claude Code' })!.cells;
    expect(session).toMatchObject({
      curve: 'its curve allows 90% now', over: false, warned: false,
      times: [`resets ${clockOf(RESET, NOW)}`, `read from Claude Code ${ago(SEEN)}`],
    });
    expect(weekly).toMatchObject({
      curve: 'over its curve\'s 46% now', over: true, warned: false,
      times: [`resets ${clockOf(WEEK, NOW)}`, `said by a session ${ago(EARLIER)}`],
    });
    // The person's own curve under `curve`, as judged: the session flat at 80, so 88% is over it.
    const own = { ...measured, windows: [{ ...measured.windows[0]!, curve: 80, over: true }] };
    expect(usageCells({ said: own }, signedIn, { now: NOW })!.cells[0]).toMatchObject({ curve: 'over its curve\'s 80% now', over: true });
    // Nothing judged against a curve, as your own sign-in's row, which no list holds, or a shell older than PUSH1e: no curve.
    expect(usageCells({ said }, signedIn, { now: NOW })!.cells[0]).not.toHaveProperty('curve');
    // A window this build has no curve for is not measured: its verdicts are null.
    const odd = { seen: SEEN, windows: [{ window: '90-minute', used: 0.4, reset: RESET, credits: false, seen: SEEN, curve: null, over: null }] };
    expect(usageCells({ said: odd }, signedIn, { now: NOW })!.cells[2]).not.toHaveProperty('curve');
  });

  it('says a cool-off’s reset unknown where Daoris chose the wait and no window reported one, and since when it is offered again', () => {
    expect(usageCells({}, cooling(false), { now: NOW })).toEqual({ cells: [], notes: ['reset unknown'] });
    expect(usageCells({}, cooling(false), { fixed: true, now: NOW })!.notes).toEqual(['reset unknown']);
    // The agent named the time: the hold says it, and no reset is said apart from a window that reported one.
    expect(usageCells({}, cooling(true), { now: NOW })).toBeNull();
    // A window that reported a reset says it in its own cell, cooling or not.
    expect(usageCells({ said }, cooling(false), { now: NOW })!.notes).toEqual([]);
    expect(usageCells({ offered: TEN }, signedIn, { now: NOW })).toEqual({ cells: [], notes: [`offered again since ${moment(TEN)}`] });
  });

  it('says it in 中文', () => {
    i18n.changeLanguage('zh');
    expect(usageCells({ said }, signedIn, { now: NOW })!.cells.map((cell) => [cell.name, cell.reading, ...cell.times])).toEqual([
      ['5 小时', '已用 88%，它自己说用量超前于节奏', `${clockOf(RESET, NOW)} 重置`, `${ago(SEEN)}由会话报告`],
      ['每周', '已用 15%', `${clockOf(WEEK, NOW)} 重置`, `${ago(EARLIER)}由会话报告`],
    ]);
    // SWITCH1d: against the curve, and read from its agent.
    const [session, weekly] = usageCells({ said: measured }, signedIn, { now: NOW, product: 'Claude Code' })!.cells;
    expect([session!.curve, session!.times[1], weekly!.curve]).toEqual(['曲线此刻允许 90%', `${ago(SEEN)}从 Claude Code 读取`, '超出其曲线此刻的 46%']);
    expect(usageCells({}, signedIn, { fixed: true, now: NOW })!.cells[0]!.reading).toBe('未知');
    expect(usageCells({}, cooling(false), { now: NOW })!.notes).toEqual(['重置时间未知']);
  });

});

/**
 * POOL1c (D197 point 7; the account-pool design §4.2, §6.3): what the fold into one set changed, each receipt's lines in the
 * reader's language, from its kinds and values, in the order the terminal's `agent list` says them (`AgentListCommand.PoolLines`).
 */
describe('what the fold into one set changed', () => {
  beforeEach(() => i18n.changeLanguage('en'));
  const labelOf = (name: string) => ({ 'account-1': 'you@work', 'account-2': 'you@home' } as Record<string, string>)[name] ?? name;
  const receipt = (over: Partial<PoolReceipt> = {}): PoolReceipt => ({
    id: 'r1', at: TEN, scopes: [], order: ['account-1', 'account-2', 'account-3'], moved: [], joined: [], inOrder: null, notKept: [],
    gone: [], ...over,
  });

  it('says each scope that named fewer accounts than the set, a workspace by name and the machine’s as the workspaces naming none', () => {
    const lines = pooledLines(receipt({
      scopes: [
        { workspace: 'work', default: null, list: ['account-1', 'account-2'], begins: 'account-1' },
        { workspace: 'forge', default: 'account-2', list: [], begins: 'account-2' },
        { workspace: null, default: null, list: ['account-1', 'account-3'], begins: 'account-1' },
        { workspace: 'lab', default: null, list: ['account-1', 'account-2', 'account-3'], begins: 'account-1' },
        { workspace: 'empty', default: null, list: [], begins: null },
      ],
    }), labelOf);
    expect(lines).toEqual([
      'work had its own accounts (you@work, you@home); it now runs on all 3.',
      'forge ran on you@home alone; it now runs on all 3.',
      'Workspaces that named no accounts ran on you@work, account-3; they now run on all 3.',
    ]);
  });

  it('says a machine scope of one account, that its starts still begin there, each first moved, an account placed, a setting not kept and a name gone', () => {
    const lines = pooledLines(receipt({
      scopes: [{ workspace: null, default: 'account-2', list: [], begins: 'account-2' }],
      inOrder: 'account-2',
      moved: [{ workspace: 'work', account: 'account-1', place: 2 }, { workspace: null, account: 'account-3', place: 3 }],
      joined: [{ account: 'account-3', place: 3 }],
      notKept: [
        { workspace: 'work', setting: 'use', value: '"goal"' },
        { workspace: null, setting: 'keep', value: '"account-1"' },
        { workspace: 'lab', setting: 'switch', value: '"off"' },
      ],
      gone: ['old-account'],
    }), labelOf);
    expect(lines).toEqual([
      'Workspaces that named no accounts ran on you@home alone; they now run on all 3.',
      'Their starts still begin at you@home, since the accounts are now used One by one, in order.',
      "work's starts began at you@work; it is number 2 in the order now.",
      'Starts of workspaces that named no accounts began at account-3; it is number 3 in the order now.',
      "account-3 was not in the order; it is number 3 now, and every workspace's work may run on it.",
      "Not kept: work's way of using accounts (Make the most of them).",
      'Not kept: account kept for conversations (you@work), set for workspaces that named no accounts.',
      "Not kept: lab's switching of accounts (Off).",
      'old-account was named but is no account here, so it was left out.',
    ]);
  });

  it('says each line in 中文, the machine’s scope by the workspaces that named none, never this machine', async () => {
    await i18n.changeLanguage('zh');
    const lines = pooledLines(receipt({
      scopes: [{ workspace: 'work', default: null, list: ['account-1'], begins: 'account-1' }],
      moved: [{ workspace: null, account: 'account-2', place: 2 }],
    }), labelOf);
    expect(lines).toEqual([
      'work 原来只在 you@work 上运行；现在它可以在全部 3 个账户上运行。',
      '没有指定账户的工作区的启动原来从 you@home 开始；现在它在顺序中排第 2 位。',
    ]);
    expect(lines.join('')).not.toMatch(/本机/);
  });
});

describe('each folded section’s line', () => {
  beforeEach(() => i18n.changeLanguage('en'));

  /** POOL1c (the account-pool design §4.2's `agents.use.summary.all`): the set's, every workspace's, and how it is used. */
  it('says how accounts are used as the one set’s: every account, for every workspace, and the way they are used', () => {
    expect(useSummary(THREE, 3)).toBe('All 3 accounts, for every workspace · Make the most of them');
    expect(useSummary({ ...THREE, scopes: [scopeOf({ use: { ...THREE.scopes[0]!.use, use: 'order' } })] }, 1))
      .toBe('1 account, for every workspace · One by one, in order');
  });

  it('says it tight in 中文, never this machine', () => {
    i18n.changeLanguage('zh');
    expect(useSummary(THREE, 3)).toBe('全部 3 个账户，用于所有工作区 · 充分利用');
  });

  it('says a version as its number alone, without the product name the binary prints', () => {
    expect(versionOnly('2.1.4 (Claude Code)')).toBe('2.1.4');
    expect(versionOnly('0.9.1')).toBe('0.9.1');
    expect(versionOnly('(not asked)')).toBe('(not asked)');
  });

  it('says each way in, its version, and which one driven work starts on', () => {
    const [tool] = byTool([claude(), { ...claude(), harness: 'claude-code-acp', wire: 'acp', accountOf: 'claude-code', version: '0.9.1' }]);
    expect(doorsSummary(tool!, 'claude-code')).toBe('direct 2.1.4, for driven work · protocol 0.9.1');
  });

  it('keeps a Chinese word tight to the Chinese word beside it, and a number apart (UX7d-2)', () => {
    i18n.changeLanguage('zh');
    const [tool] = byTool([claude({ present: false }), { ...claude(), harness: 'claude-code-acp', wire: 'acp', accountOf: 'claude-code', version: '0.9.1' }]);
    expect(doorsSummary(tool!, 'claude-code')).toBe('直连未安装 · 协议 0.9.1');
    expect(usageSummary(3)).toBe('已测量 3 个会话');
  });

  it('says what it may do: the defaults on, the person’s rules and the proposals waiting', () => {
    const rules = {
      path: 'p', defaults: [
        { id: 'a', list: 'deny' as const, rules: [], why: '', on: true },
        { id: 'b', list: 'deny' as const, rules: [], why: '', on: false },
      ],
      scopes: [{ scope: 'machine' as const, allow: ['Bash(npm test)'], ask: [], deny: [] }],
      proposals: [{ id: '1', state: 'waiting' as const, action: 'add' as const, scope: 'machine' as const, why: '', proposed: TEN }],
    };
    expect(rulesSummary(rules)).toBe("Daoris's 1 default on · 1 rule of yours · 1 proposal waiting");
  });

  it('says the model and effort as the tool’s own where no account sets its own', () => {
    const [tool] = byTool([claude()]);
    expect(settingsSummary(tool!)).toBe("the tool's own defaults");
  });

  it('says usage by the sessions measured, and nothing measured as nothing, never zero', () => {
    expect(usageSummary(3)).toBe('3 sessions measured');
    expect(usageSummary(0)).toBe('nothing measured yet');
  });
});
