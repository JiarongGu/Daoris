import { beforeEach, describe, expect, it } from 'vitest';
import i18n from '../i18n';
import { byTool, type ToolDoor } from '../tools';
import { scopeOf, THREE } from '../settings/accountsFixtures';
import type { AgentAccounts } from '../settings/accounts';
import {
  accountState, agentRows, doorsSummary, heldBy, ownState, readLine, rulesSummary, settingsSummary, signedOutHeld, stateWord,
  usageSummary, useSummary, workspacesSummary,
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
    expect(stateWord(state).label).toMatch(/^cooling until /);
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

describe('each folded section’s line', () => {
  beforeEach(() => i18n.changeLanguage('en'));
  const labelOf = (name: string) => ({ 'account-1': 'you@work', 'account-2': 'you@home' } as Record<string, string>)[name] ?? name;

  it('says how accounts are used: the mode and switching early, or where starts run with no list', () => {
    expect(useSummary(scopeOf({ list: ['account-1', 'account-2'] }), labelOf)).toBe('Make the most of them · switch before the limit');
    expect(useSummary(scopeOf({ list: ['account-1'], use: { use: 'order', keep: null, early: false, near: 90 } }), labelOf))
      .toBe('One by one, in order');
    expect(useSummary(scopeOf({ default: 'account-2' }), labelOf)).toBe('Starts run on you@home');
    expect(useSummary(scopeOf(), labelOf)).toBe("Starts run on the tool's own sign-in");
  });

  it("says each workspace on its own accounts or this machine's", () => {
    expect(workspacesSummary(THREE, ['forge', 'work'], labelOf)).toBe("forge: this machine's · work: its own (you@home, you@work)");
    expect(workspacesSummary(THREE, [], labelOf)).toBe('No workspace yet');
  });

  it('says each way in, its version, and which one driven work starts on', () => {
    const [tool] = byTool([claude(), { ...claude(), harness: 'claude-code-acp', wire: 'acp', accountOf: 'claude-code', version: '0.9.1' }]);
    expect(doorsSummary(tool!, 'claude-code')).toBe('direct 2.1.4 (Claude Code), for driven work · protocol 0.9.1');
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
