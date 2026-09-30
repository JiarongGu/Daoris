import { describe, expect, it } from 'vitest';
import { type Account, byTool, doorOf, toolOf, type ToolDoor } from './tools';

/**
 * The roster's grouping, after the owner read four adapter rows as four tools (2026-09-22).
 *
 * These are the assertions that matter because nothing else can make them: the UI renders whatever
 * this returns, so a wrong grouping is a *plausible* screen — four cards where there are three
 * tools looks exactly like a machine with four tools on it.
 */

const door = (over: Partial<ToolDoor> & { harness: string }): ToolDoor => ({
  present: false,
  ...over,
});

describe('byTool', () => {
  it('folds a door that borrows an account into the tool that owns it', () => {
    const tools = byTool([
      door({ harness: 'claude-code', present: true, version: '2.1.278' }),
      door({ harness: 'claude-code-acp', accountOf: 'claude-code', wire: 'acp' }),
    ]);

    expect(tools).toHaveLength(1);
    expect(tools[0]!.name).toBe('claude-code');
    expect(tools[0]!.doors.map((d) => d.harness)).toEqual(['claude-code', 'claude-code-acp']);
  });

  /**
   * 🔴 The case that makes naming-by-account right rather than merely tidy. `codex-acp` runs as
   * `codex`'s account and the driver has no native `codex` adapter at all — so the tool exists with
   * one door whose name is not its own. Grouping by any door's name would have produced a tool
   * called `codex-acp`, which is the confusion this was written to remove.
   */
  it('names a tool after its account even when no door carries that name', () => {
    const tools = byTool([door({ harness: 'codex-acp', accountOf: 'codex', wire: 'acp' })]);

    expect(tools.map((t) => t.name)).toEqual(['codex']);
    expect(tools[0]!.doors.map((d) => d.harness)).toEqual(['codex-acp']);
  });

  it('leaves a tool that owns its own account alone', () => {
    const tools = byTool([door({ harness: 'dsh', present: true, wire: 'acp' })]);

    expect(tools.map((t) => t.name)).toEqual(['dsh']);
    expect(tools[0]!.doors).toHaveLength(1);
  });

  it('keeps the roster’s order, so installing something does not reshuffle the surface', () => {
    const tools = byTool([
      door({ harness: 'dsh' }),
      door({ harness: 'claude-code' }),
      door({ harness: 'claude-code-acp', accountOf: 'claude-code' }),
    ]);

    expect(tools.map((t) => t.name)).toEqual(['dsh', 'claude-code']);
  });

  /**
   * One configuration home per tool by declaration, so two doors report the same account. Shown
   * twice it reads as two accounts, which is the same misreading one level down.
   */
  it('reports one account once, however many doors can see it', () => {
    const profiles: Account[] = [{ name: 'owner', home: '/profiles/owner', login: 'in' }];
    const tools = byTool([
      door({ harness: 'claude-code', profiles }),
      door({ harness: 'claude-code-acp', accountOf: 'claude-code', profiles }),
    ]);

    expect(tools[0]!.accounts).toHaveLength(1);
    expect(tools[0]!.accounts[0]!.name).toBe('owner');
  });

  it('deduplicates on the directory, because that is what an account IS', () => {
    const tools = byTool([
      door({ harness: 'claude-code', profiles: [{ name: 'work', home: '/p/one', login: 'in' }] }),
      door({
        harness: 'claude-code-acp',
        accountOf: 'claude-code',
        // The same directory under another name is the same account; a different one is not.
        profiles: [
          { name: 'renamed', home: '/p/one', login: 'in' },
          { name: 'other', home: '/p/two', login: 'out' },
        ],
      }),
    ]);

    expect(tools[0]!.accounts.map((a) => a.home)).toEqual(['/p/one', '/p/two']);
  });

  it('answers "have I got this" and "which account" across the doors', () => {
    const tools = byTool([
      door({ harness: 'claude-code', present: false }),
      door({ harness: 'claude-code-acp', accountOf: 'claude-code', present: true, machineDefault: 'work' }),
    ]);

    expect(tools[0]!.present).toBe(true);
    expect(tools[0]!.machineDefault).toBe('work');
  });

  /**
   * 🔴 The account a person actually has is the tool's own configuration home, and the roster
   * called a machine with no named profile "No accounts" while its owner was logged in. The
   * account-owning door answers for the tool's own home; a workspace's choice of account is
   * carried beside the machine's, once per circle however many doors report it.
   */
  it('carries the tool’s own login and each workspace’s choice of account', () => {
    const tools = byTool([
      door({ harness: 'claude-code', present: true, ownLogin: 'in', workspaceDefaults: [{ workspace: 'work', profile: 'office' }] }),
      door({ harness: 'claude-code-acp', accountOf: 'claude-code', ownLogin: 'unknown', workspaceDefaults: [{ workspace: 'work', profile: 'office' }] }),
    ]);

    expect(tools[0]!.ownLogin).toBe('in');
    expect(tools[0]!.workspaceDefaults).toEqual([{ workspace: 'work', profile: 'office' }]);
  });

  /**
   * Who is signed in (D66 §3) is the account-owning door's answer — the protocol door asks nobody —
   * so the account keeps that door's name for it, and the tool's own home is named the same way.
   */
  it('carries who is signed in, from the door that can ask', () => {
    const tools = byTool([
      door({
        harness: 'claude-code',
        ownAccount: 'owner@example.invalid',
        profiles: [{ name: 'account-1', home: '/p/one', login: 'in', account: 'someone@example.invalid' }],
      }),
      door({
        harness: 'claude-code-acp',
        accountOf: 'claude-code',
        ownAccount: null,
        profiles: [{ name: 'account-1', home: '/p/one', login: 'unknown', account: null }],
      }),
    ]);

    expect(tools[0]!.accounts).toEqual([
      { name: 'account-1', home: '/p/one', login: 'in', account: 'someone@example.invalid' },
    ]);
    expect(tools[0]!.ownAccount).toBe('owner@example.invalid');
    expect(byTool([door({ harness: 'dsh' })])[0]!.ownAccount).toBeNull();
  });

  /** AGT1: what a person calls the tool and whose it is — the first door that says, or nothing. */
  it('names the tool and its maker from the doors, and leaves both empty when none says', () => {
    const tools = byTool([
      door({ harness: 'codex-acp', accountOf: 'codex', product: 'Codex', maker: 'OpenAI' }),
      door({ harness: 'acme-agent' }),
    ]);

    expect([tools[0]!.name, tools[0]!.product, tools[0]!.maker]).toEqual(['codex', 'Codex', 'OpenAI']);
    expect([tools[1]!.product, tools[1]!.maker]).toEqual([null, null]);
  });

  it('answers unknown for a tool whose doors say nothing about their own home', () => {
    expect(byTool([door({ harness: 'dsh' })])[0]!.ownLogin).toBe('unknown');
    expect(byTool([door({ harness: 'dsh' })])[0]!.workspaceDefaults).toEqual([]);
  });

  it('treats a blank borrowed account as none at all', () => {
    expect(toolOf(door({ harness: 'dsh', accountOf: '  ' }))).toBe('dsh');
    expect(toolOf(door({ harness: 'codex-acp', accountOf: 'codex' }))).toBe('codex');
  });
});

/**
 * Which door this machine's starts ride (INT3c): the configured adapter's own `wire`, as the roster
 * reports it. An unadopted repository is carried by the protocol door only (D70), so a surface that
 * offers to drive one says so on a machine whose door is direct — and says nothing when it cannot
 * tell, rather than guess.
 */
/**
 * AGT6: an account's own settings are one file whichever door reads it, so the tool offers the choices
 * the first door says — the protocol door answers for Claude Code as its pipe does — and a tool no door
 * offers them for offers none.
 */
describe('byTool and an account\'s own settings', () => {
  const choices = { models: ['default', 'opus'], efforts: ['low', 'high'] };

  it('takes the choices from the first door that offers them', () => {
    const tools = byTool([
      door({ harness: 'claude-code-acp', accountOf: 'claude-code', settingsChoices: choices }),
      door({ harness: 'claude-code', settingsChoices: null }),
    ]);

    expect(tools[0]!.settingsChoices).toEqual(choices);
  });

  it('offers none where the doors say none, and claims nothing for a shell that never said', () => {
    expect(byTool([door({ harness: 'dsh', settingsChoices: null })])[0]!.settingsChoices).toBeNull();
    expect(byTool([door({ harness: 'codex-acp', accountOf: 'codex' })])[0]!.settingsChoices).toBeUndefined();
  });
});

describe('doorOf', () => {
  const roster = [
    door({ harness: 'claude-code', present: true, wire: 'pipe' }),
    door({ harness: 'claude-code-acp', accountOf: 'claude-code', wire: 'acp' }),
  ];

  it('answers the configured adapter\'s own door', () => {
    expect(doorOf('claude-code', roster)).toBe('pipe');
    expect(doorOf('claude-code-acp', roster)).toBe('acp');
  });

  it('answers nothing it cannot read: no adapter, one the roster lacks, or a door it does not name', () => {
    expect(doorOf(undefined, roster)).toBeNull();
    expect(doorOf('dsh', roster)).toBeNull();
    expect(doorOf('claude-code', [door({ harness: 'claude-code' })])).toBeNull();
    expect(doorOf('claude-code', [door({ harness: 'claude-code', wire: 'smoke-signal' })])).toBeNull();
  });
});
