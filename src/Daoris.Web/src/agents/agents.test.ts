import { beforeEach, describe, expect, it } from 'vitest';
import i18n from '../i18n';
import { ago, clockOf, moment } from '../format';
import { byTool, type ToolDoor } from '../tools';
import { scopeOf, THREE } from '../settings/accountsFixtures';
import type { AgentAccounts } from '../settings/accounts';
import {
  accountAct, accountName, accountState, accountWho, agentRows, doorsSummary, heldBy, joinChoices, nextStartSaid, ownRunsFor,
  ownSaid, ownState, readLine, rulesSummary, runsFor, runsForLine, settingsSummary, signedOutHeld, stateWhen, stateWord,
  usageCells, usageSummary, useSummary, versionOnly, workspacesSummary,
} from './agents';

// The Agents place's words and facts (UX6e, D150 §5), pure: an account's last known state with when it was read, the list's
// rows, and each folded section's line. No bridge, so every state is an argument.

const TEN = '2026-10-04T10:42:00.000Z';
const NOW = new Date('2026-10-04T12:00:00.000Z');

const claude = (over: Partial<ToolDoor> = {}): ToolDoor => ({
  harness: 'claude-code', product: 'Claude Code', maker: 'Anthropic', present: true, version: '2.1.4 (Claude Code)',
  wire: 'pipe', machineDefault: 'account-1', ownLogin: 'unknown', ownRead: null,
  workspaceDefaults: [{ workspace: 'forge', profile: 'account-2' }],
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
  it('is an account a default names, or a list holds, the machine’s or a workspace’s', () => {
    const [tool] = byTool([claude()]);
    expect(heldBy(tool!, THREE, 'account-1')).toBe(true); // the machine's default
    expect(heldBy(tool!, THREE, 'account-2')).toBe(true); // forge's default, and lists
    expect(heldBy(tool!, { ...THREE, scopes: [scopeOf()] }, 'account-3')).toBe(false);
  });

  it('counts the signed-out accounts that hold work, the badge’s number', () => {
    const [tool] = byTool([claude()]);
    expect(signedOutHeld(tool!, THREE)).toBe(2);
    expect(signedOutHeld(tool!, { ...THREE, scopes: [scopeOf()] })).toBe(1);
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

  it("counts the tool's own sign-in too while starts run on it and it reads signed out", () => {
    const [tool] = byTool([claude({ machineDefault: null, ownLogin: 'out', profiles: [], workspaceDefaults: [] })]);
    const none: AgentAccounts = { agent: 'claude-code', speaks: false, own: {}, accounts: [], scopes: [scopeOf()] };
    expect(ownState(tool!, none).holdsWork).toBe(true);
    expect(signedOutHeld(tool!, none)).toBe(1);
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

  it('names an agent with no account of its own by its own sign-in', () => {
    const rows = agentRows(byTool([claude({ profiles: [], machineDefault: null, workspaceDefaults: [] })]), undefined);
    expect(rows[0]!.phrase).toBe('its own sign-in');
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

describe('where an account runs (ACCT1)', () => {
  beforeEach(() => i18n.changeLanguage('en'));

  it('reads the roster’s places where the shell gives them, this machine first and where it starts first marked', () => {
    const [tool] = byTool([claude({
      profiles: [{
        name: 'account-2', home: 'h/2', login: 'in', places: [
          { workspace: 'work', list: true, default: true }, { workspace: null, list: true, default: false },
        ],
      }],
    })]);
    const runs = runsFor(tool!, THREE, 'account-2');
    expect(runs).toEqual([{ workspace: null, first: false }, { workspace: 'work', first: true }]);
    expect(runsForLine(runs)).toBe('this machine · work (first)');
  });

  /**
   * ACCTUX4b (the second opinion's line 125): *first* is the account the scope's next start takes (`scope.next`), not its
   * default or its list's head. THREE's machine begins on account-1, which cools, so its next start takes account-3.
   */
  it('marks first the scope whose next start takes it, not the one whose list begins on it', () => {
    const [tool] = byTool([claude({
      profiles: [
        { name: 'account-1', home: 'h/1', login: 'out', places: [{ workspace: null, list: true, default: true }] },
        { name: 'account-3', home: 'h/3', login: 'out', places: [{ workspace: null, list: true, default: false }] },
      ],
    })]);
    expect(runsFor(tool!, THREE, 'account-1')).toEqual([{ workspace: null, first: false }]);
    const runs = runsFor(tool!, THREE, 'account-3');
    expect(runs).toEqual([{ workspace: null, first: true }]);
    // Said where it runs in one place too: the mark says a fact of that scope, not which of its places comes first.
    expect(runsForLine(runs)).toBe('this machine (first)');
  });

  it('works it out from the lists and defaults where the shell is older, the scope whose next start takes it first', () => {
    const [tool] = byTool([claude()]);
    // forge names account-2 its default and has no scope, so no next start of its is known, and it is not marked.
    expect(runsFor(tool!, THREE, 'account-2')).toEqual([
      { workspace: null, first: false }, { workspace: 'forge', first: false }, { workspace: 'work', first: true },
    ]);
    expect(runsFor(tool!, THREE, 'account-1')[0]).toEqual({ workspace: null, first: false });
    expect(runsFor(tool!, THREE, 'account-3')).toEqual([{ workspace: null, first: true }]);
  });

  it('marks no scope first where the shell names no next start', () => {
    const [tool] = byTool([claude()]);
    const older = { ...THREE, scopes: THREE.scopes.map((scope) => ({ ...scope, next: undefined })) };
    const runs = runsFor(tool!, older, 'account-2');
    expect(runs.some((place) => place.first)).toBe(false);
    expect(runsForLine(runs)).toBe('this machine · forge · work');
  });

  it('says no workspace where nothing holds it, as the roster’s nowhere says', () => {
    const [tool] = byTool([claude({ profiles: [{ name: 'acct-1a2b3c4d', home: 'h/9', login: 'in', places: [], nowhere: true }] })]);
    const runs = runsFor(tool!, THREE, 'acct-1a2b3c4d');
    expect(runs).toEqual([]);
    expect(runsForLine(runs)).toBe('no workspace');
  });

  it("says the workspaces your own sign-in runs, those naming no account, while this machine names none", () => {
    const [tool] = byTool([claude({ machineDefault: null, workspaceDefaults: [] })]);
    const use: AgentAccounts = { ...THREE, scopes: [scopeOf(), THREE.scopes[1]!] };
    expect(ownRunsFor(tool!, use, ['forge', 'work'])).toEqual([{ workspace: 'forge', first: false }]);
    expect(ownRunsFor(tool!, use, ['work'])).toEqual([{ workspace: null, first: false }]);
    expect(ownRunsFor(byTool([claude()])[0]!, THREE, ['forge', 'work'])).toEqual([]);
  });
});

/** ACCTUX4b (the second opinion's lines 128-133): this machine's next start, said above the accounts in the fold's words. */
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
      list, begins: 'account-1',
      next: { account: 'account-3', reason: 'near', over: 'account-1', others: [{ account: 'account-1', hold: 'near' }] },
    });
    expect(nextStartSaid(near, labelOf)).toEqual({
      takes: 'The next start takes account-3: account-1 is near its limit, so it goes last.', held: null,
    });
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

  it('says nothing where it runs on your own sign-in, or where the shell is older than the next start', () => {
    expect(nextStartSaid(scopeOf({ next: { account: null, reason: 'own', others: [] } }), labelOf)).toBeNull();
    expect(nextStartSaid(scopeOf({ list: ['account-1'] }), labelOf)).toBeNull();
    expect(nextStartSaid(null, labelOf)).toBeNull();
  });
});

describe('the one act an account’s state asks for (D152 §4)', () => {
  const state = (name: 'in' | 'out' | 'unknown' | 'cooling' | 'keyed' | 'refused', holdsWork = true) => ({
    state: name, read: null, holdsWork,
    cooling: name === 'cooling' ? { until: TEN, stated: true, seen: TEN, assumedZone: false, notBelieved: false } : null,
  });

  it('is Sign in when signed out, Read when unknown, Try now when cooling', () => {
    expect(accountAct(state('out'), { signsIn: true, present: true, runs: 1 })).toEqual({ act: 'signIn', loud: true });
    expect(accountAct(state('unknown', false), { signsIn: true, present: true, runs: 1 })).toEqual({ act: 'read', loud: false });
    expect(accountAct(state('cooling'), { signsIn: true, present: true, runs: 1 })).toEqual({ act: 'tryNow', loud: false });
  });

  it('is Use in a workspace… when signed in and nothing runs on it, and none once something does', () => {
    expect(accountAct(state('in'), { signsIn: true, present: true, runs: 0 })).toEqual({ act: 'place', loud: false });
    expect(accountAct(state('keyed'), { signsIn: true, present: true, runs: 0 })).toEqual({ act: 'place', loud: false });
    expect(accountAct(state('in'), { signsIn: true, present: true, runs: 2 })).toBeNull();
  });

  it('offers no sign-in where the agent has none, nor a read where it is not installed', () => {
    expect(accountAct(state('out'), { signsIn: false, present: true, runs: 1 })).toBeNull();
    expect(accountAct(state('unknown'), { signsIn: true, present: false, runs: 1 })).toBeNull();
  });

  /**
   * ACCTUX1: a refused key is repaired by a key, never a sign-in: the page's own *Add an API key*, loud where it holds work.
   * Nothing puts a new key into the same account yet, so where the agent takes no key from Daoris there is no act to offer.
   */
  it('is Add an API key when its key was refused, and none where the agent takes no key', () => {
    expect(accountAct(state('refused'), { signsIn: false, present: true, runs: 1, takesKey: true })).toEqual({ act: 'newKey', loud: true });
    expect(accountAct(state('refused', false), { signsIn: false, present: true, runs: 1, takesKey: true }))
      .toEqual({ act: 'newKey', loud: false });
    expect(accountAct(state('refused'), { signsIn: true, present: true, runs: 1 })).toBeNull();
    expect(accountAct(state('refused'), { signsIn: false, present: false, runs: 1, takesKey: true })).toBeNull();
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
      { window: 'session', used: 0.88, reset: RESET, standing: 'near', credits: false, seen: SEEN },
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
        reading: '88% used, near its limit, by its own word', times: [`resets ${clockOf(RESET, NOW)}`, `said ${ago(SEEN)}`],
      },
      {
        window: 'weekly', name: 'weekly', known: true, used: 0.15, warned: false,
        reading: '15% used', times: [`resets ${clockOf(WEEK, NOW)}`, `said ${ago(EARLIER)}`],
      },
    ]);
    expect(usage.notes).toEqual([]);
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
      reading: 'limit reached, by its own word, drawing on usage credits', times: [`resets ${clockOf(WEEK, NOW)}`, `said ${ago(SEEN)}`],
    });
    const clear = { seen: SEEN, windows: [{ window: 'session', used: null, reset: RESET, standing: 'clear', credits: false, seen: SEEN }] };
    expect(usageCells({ said: clear }, signedIn, { now: NOW })!.cells[0])
      .toMatchObject({ known: true, used: null, warned: false, reading: 'clear, by its own word' });
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
      ['5 小时', '已用 88%，它自己说接近上限', `${clockOf(RESET, NOW)} 重置`, `${ago(SEEN)}报告`],
      ['每周', '已用 15%', `${clockOf(WEEK, NOW)} 重置`, `${ago(EARLIER)}报告`],
    ]);
    expect(usageCells({}, signedIn, { fixed: true, now: NOW })!.cells[0]!.reading).toBe('未知');
    expect(usageCells({}, cooling(false), { now: NOW })!.notes).toEqual(['重置时间未知']);
  });

  /**
   * CODEXUSE3: the tool's own sign-in's windows, read at a person's press, are answered beside the accounts as `own.said`,
   * and its row says them as an account's row says its own; a shell older than that answers none, and its row says nothing.
   */
  it('reads your own sign-in’s windows beside the accounts, and none where the shell answers none', () => {
    const use = (own: Record<string, unknown>): AgentAccounts => ({
      agent: 'codex', speaks: true, own: own as AgentAccounts['own'], accounts: [], scopes: [scopeOf()],
    });
    const codex = byTool([{ ...claude({ profiles: [] }), harness: 'codex-acp', product: 'Codex', maker: 'OpenAI', accountOf: 'codex' }])[0]!;

    expect(ownSaid(use({ said }))).toEqual(said);
    expect(usageCells({ said: ownSaid(use({ said })) }, ownState(codex, use({ said })), { now: NOW })!.cells
      .map((cell) => cell.reading)).toEqual(['88% used, near its limit, by its own word', '15% used']);
    expect(ownSaid(use({}))).toBeNull();
    expect(ownSaid(use({ said: null }))).toBeNull();
    expect(ownSaid(use({ said: { seen: SEEN } }))).toBeNull();
    expect(ownSaid(null)).toBeNull();
    expect(usageCells({ said: ownSaid(use({})) }, ownState(codex, use({})), { now: NOW })).toBeNull();
  });
});

describe('the lists a new account may join (D152 §4.5)', () => {
  beforeEach(() => i18n.changeLanguage('en'));
  const labelOf = (name: string) => name;

  it('offers each workspace with a list of its own, then this machine’s, saying who holds each', () => {
    const [tool] = byTool([claude({ workspaceDefaults: [] })]);
    const choices = joinChoices(tool!, THREE, ['forge', 'work'], labelOf, 'acct-1a2b3c4d');
    expect(choices.map((choice) => choice.workspace)).toEqual(['work', null]);
    expect(choices[0]).toMatchObject({ holds: ['account-2', 'account-1'], usedBy: [], moves: false });
    expect(choices[1]).toMatchObject({ holds: ['account-1', 'account-2', 'account-3'], usedBy: ['forge'], moves: false });
  });

  it('offers a workspace whose own default names one account, its list beginning there', () => {
    const [tool] = byTool([claude()]);
    const choices = joinChoices(tool!, THREE, ['forge', 'work'], labelOf, 'acct-1a2b3c4d');
    expect(choices.map((choice) => choice.workspace)).toEqual(['forge', 'work', null]);
    expect(choices[0]).toMatchObject({ holds: ['account-2'], listed: false });
    expect(choices[2]!.usedBy).toEqual([]);
  });

  it('says ticking this machine’s list moves its starts where it names nobody, and leaves out a list that holds it', () => {
    const [tool] = byTool([claude({ machineDefault: null, workspaceDefaults: [] })]);
    const use: AgentAccounts = { ...THREE, scopes: [scopeOf(), THREE.scopes[1]!] };
    const choices = joinChoices(tool!, use, ['forge', 'work'], labelOf, 'account-1');
    expect(choices.map((choice) => choice.workspace)).toEqual([null]);
    expect(choices[0]).toMatchObject({ holds: [], usedBy: ['forge'], moves: true });
  });
});

describe('each folded section’s line', () => {
  beforeEach(() => i18n.changeLanguage('en'));
  const labelOf = (name: string) => ({ 'account-1': 'you@work', 'account-2': 'you@home' } as Record<string, string>)[name] ?? name;

  it('says how accounts are used by naming each scope and what its starts run on (D152 §4.4)', () => {
    expect(useSummary({ ...THREE, scopes: [scopeOf(), THREE.scopes[1]!] }, labelOf))
      .toBe('This machine: your own sign-in · work: its own list of 2');
    expect(useSummary({ ...THREE, scopes: [scopeOf({ default: 'account-2' })] }, labelOf)).toBe('This machine: you@home');
    expect(useSummary(THREE, labelOf)).toBe('This machine: a list of 3 · work: its own list of 2');
  });

  it('says it tight in 中文, a name set off by its colon and never by spaces', () => {
    i18n.changeLanguage('zh');
    expect(useSummary({ ...THREE, scopes: [scopeOf(), THREE.scopes[1]!] }, labelOf)).toBe('本机：你自己的登录 · work：自有列表，2 个账户');
  });

  it('says a version as its number alone, without the product name the binary prints', () => {
    expect(versionOnly('2.1.4 (Claude Code)')).toBe('2.1.4');
    expect(versionOnly('0.9.1')).toBe('0.9.1');
    expect(versionOnly('(not asked)')).toBe('(not asked)');
  });

  it("says each workspace on its own accounts or this machine's", () => {
    expect(workspacesSummary(THREE, ['forge', 'work'], labelOf)).toBe("forge: this machine's · work: its own (you@home, you@work)");
    expect(workspacesSummary(THREE, [], labelOf)).toBe('No workspace yet');
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
