import { beforeEach, describe, expect, it } from 'vitest';
import i18n from '../i18n';
import { ago, moment } from '../format';
import { byTool, type ToolDoor } from '../tools';
import { scopeOf, THREE } from '../settings/accountsFixtures';
import type { AgentAccounts } from '../settings/accounts';
import {
  accountAct, accountName, accountState, accountWho, agentRows, doorsSummary, heldBy, joinChoices, ownRunsFor, ownSaid, ownState,
  readLine, rulesSummary, runsFor, runsForLine, settingsSummary, signedOutHeld, stateWhen, stateWord, usageLine, usageSummary,
  useSummary, versionOnly, workspacesSummary,
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

  it('works it out from the lists and defaults where the shell is older, the scope that begins on it first', () => {
    const [tool] = byTool([claude()]);
    expect(runsFor(tool!, THREE, 'account-2')).toEqual([
      { workspace: null, first: false }, { workspace: 'forge', first: true }, { workspace: 'work', first: true },
    ]);
    expect(runsFor(tool!, THREE, 'account-1')[0]).toEqual({ workspace: null, first: true });
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
 * ACCTUX1: what an account's agent last said, as the row's second line, read beside its state. Each window's share is a reading
 * a person acts on, so it wears the ink; its reset and when it was said are times, and wear the soft ink. A reset is said
 * beside the window that reported it, and a cool-off whose length Daoris chose says the reset is unknown.
 */
describe('what an account’s agent last said, on its row', () => {
  beforeEach(() => i18n.changeLanguage('en'));
  const SEEN = '2026-10-04T11:00:00.000Z';
  const RESET = '2026-10-04T15:00:00.000Z';
  const WEEK = '2026-10-09T17:00:00.000Z';
  const said = {
    seen: SEEN,
    windows: [
      { window: 'weekly', used: 0.15, reset: WEEK, credits: false, seen: SEEN },
      { window: 'session', used: 0.88, reset: RESET, standing: 'near', credits: false, seen: SEEN },
    ],
  };
  const signedIn = accountState({ login: 'in', read: TEN }, null, true);
  const cooling = (stated: boolean) => accountState({ login: 'in', read: TEN }, {
    cooling: { until: '2026-10-04T16:02:00.000Z', stated, window: stated ? 'session' : null, seen: TEN, assumedZone: false, notBelieved: false },
  }, true);
  const text = (parts: ReturnType<typeof usageLine>) => (parts ?? []).map((part) => part.text).join('');

  it('says each window’s share in the ink, and its reset and when it was said in the soft ink', () => {
    const parts = usageLine({ said }, signedIn)!;
    expect(parts.filter((part) => part.tone === 'reading').map((part) => part.text)).toEqual([
      'near its five-hour limit, by its own word', '88% of its five-hour limit used', '15% of its weekly limit used',
    ]);
    expect(parts.filter((part) => part.tone === 'when').map((part) => part.text)).toEqual([
      `resets ${moment(RESET)}`, `resets ${moment(WEEK)}`, `said ${ago(SEEN)}`,
    ]);
    expect(text(parts)).toBe(
      `near its five-hour limit, by its own word; 88% of its five-hour limit used, resets ${moment(RESET)}; `
      + `15% of its weekly limit used, resets ${moment(WEEK)} · said ${ago(SEEN)}`);
  });

  it('says nothing where its agent said nothing and nothing holds it', () => {
    expect(usageLine({}, signedIn)).toBeNull();
    expect(usageLine({ said: { seen: SEEN, windows: [] } }, signedIn)).toBeNull();
  });

  it('says a cool-off’s reset unknown where Daoris chose the wait and no window reported one', () => {
    expect(text(usageLine({}, cooling(false)))).toBe('reset unknown');
    expect(usageLine({}, cooling(false))!.every((part) => part.tone === 'when')).toBe(true);
    // The agent named the time: the hold says it, and no reset is said apart from a window that reported one.
    expect(usageLine({}, cooling(true))).toBeNull();
    // A window that reported a reset says it beside itself, cooling or not.
    expect(text(usageLine({ said }, cooling(false)))).toContain(`88% of its five-hour limit used, resets ${moment(RESET)}`);
    expect(text(usageLine({ said }, cooling(false)))).not.toContain('reset unknown');
  });

  it('says a window its agent named a reset for with no share, beside its own word', () => {
    const reached = { seen: SEEN, windows: [{ window: 'weekly', used: null, reset: WEEK, standing: 'refused', credits: false, seen: SEEN }] };
    expect(text(usageLine({ said: reached }, cooling(false))))
      .toBe(`its weekly limit reached, by its own word, resets ${moment(WEEK)} · said ${ago(SEEN)}`);
  });

  it('says since when it is offered again, as a time', () => {
    const parts = usageLine({ offered: TEN }, signedIn)!;
    expect(parts).toEqual([{ text: `offered again since ${moment(TEN)}`, tone: 'when' }]);
  });

  it('says it in 中文, tight, its times set apart', () => {
    i18n.changeLanguage('zh');
    expect(text(usageLine({ said }, signedIn))).toBe(
      `它自己说接近5 小时上限；5 小时上限已用 88%，${moment(RESET)} 重置；每周上限已用 15%，${moment(WEEK)} 重置 · ${ago(SEEN)}报告`);
    expect(text(usageLine({}, cooling(false)))).toBe('重置时间未知');
  });

  /**
   * CODEXUSE3: the tool's own sign-in's windows, read at a person's press, are answered beside the accounts as `own.said`,
   * and its row says them as an account's row says its own; a shell older than that answers none, and its row says nothing.
   */
  it('reads your own sign-in’s windows beside the accounts, and none where the shell answers none', () => {
    const use = (own: AgentAccounts['own'] & { said?: unknown }): AgentAccounts => ({
      agent: 'codex', speaks: true, own: own as AgentAccounts['own'], accounts: [], scopes: [scopeOf()],
    });
    const codex = byTool([{ ...claude({ profiles: [] }), harness: 'codex-acp', product: 'Codex', maker: 'OpenAI', accountOf: 'codex' }])[0]!;

    expect(ownSaid(use({ said }))).toEqual(said);
    expect(text(usageLine({ said: ownSaid(use({ said })) }, ownState(codex, use({ said }))))).toBe(
      `near its five-hour limit, by its own word; 88% of its five-hour limit used, resets ${moment(RESET)}; `
      + `15% of its weekly limit used, resets ${moment(WEEK)} · said ${ago(SEEN)}`);
    expect(ownSaid(use({}))).toBeNull();
    expect(ownSaid(use({ said: null }))).toBeNull();
    expect(ownSaid(use({ said: { seen: SEEN } }))).toBeNull();
    expect(ownSaid(null)).toBeNull();
    expect(usageLine({ said: ownSaid(use({})) }, ownState(codex, use({})))).toBeNull();
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
