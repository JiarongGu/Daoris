import { describe, expect, it } from 'vitest';
import { starters } from './starters';
import type { Tool } from '../tools';

// HELP1d (D89): Ask Daoris's no-agent tier — what this machine lacks, each with the screen that fixes
// it and the terminal command that does the same. Pure: every machine is an argument.

const tool = (over: Partial<Tool> = {}): Tool => ({
  name: 'claude-code', product: 'Claude Code', maker: 'Anthropic', doors: [], accounts: [],
  machineDefault: null, present: true, ownLogin: 'in', ownAccount: null, ownRead: null, takesRules: false, workspaceDefaults: [], ...over,
});

const READY = {
  repositories: ['engine', 'game'],
  drivable: ['engine'],
  tools: [tool()],
  waiting: 0,
  unnamedLines: [] as string[],
  helper: 'claude-code-acp',
};

describe('starters', () => {
  it('offers nothing to a machine that lacks nothing', () => {
    expect(starters(READY)).toEqual([]);
  });

  it('puts what waits on the person first, then what the machine lacks, in the order a setup goes', () => {
    const all = starters({
      repositories: [], drivable: [], tools: [tool({ ownLogin: 'out' })], waiting: 2, unnamedLines: [], helper: null,
    });

    expect(all.map((starter) => starter.id)).toEqual(['waiting', 'nothing-registered', 'agent-signed-out', 'helper-off']);
    expect(all[0]).toMatchObject({ values: { count: 2 }, door: { view: 'sessions' } });
    expect(all[1]).toMatchObject({ door: { view: 'projects' }, command: 'daoris connect' });
  });

  it('says nothing is driven only where something is registered to drive', () => {
    const lacking = starters({ ...READY, drivable: [] });

    expect(lacking).toEqual([expect.objectContaining({
      id: 'nothing-driven', door: { view: 'projects' }, command: 'daoris driver drive <repository>',
    })]);
  });

  it('names a tool no account is signed in to, only on a definite out', () => {
    const out = starters({ ...READY, tools: [tool({ ownLogin: 'out' }), tool({ name: 'codex', product: 'Codex', ownLogin: 'unknown' })] });

    expect(out).toEqual([expect.objectContaining({
      id: 'agent-signed-out', values: { tool: 'Claude Code' }, door: { view: 'agents', item: 'claude-code', agentPart: 'accounts' },
      command: 'daoris agent login claude-code',
    })]);
    // A named account signed in is an account, whatever the tool's own home says.
    expect(starters({ ...READY, tools: [tool({ ownLogin: 'out', accounts: [{ name: 'work', home: '', login: 'in' }] })] }))
      .toEqual([]);
  });

  it('names the repositories with no line git can name, and where to set one', () => {
    expect(starters({ ...READY, unnamedLines: ['engine', 'game', 'tools'] })).toEqual([expect.objectContaining({
      id: 'no-line', values: { count: 3, first: 'engine' }, door: { view: 'settings', section: 'workspace', anchor: 'lines' },
      command: 'daoris driver line engine <branch>',
    })]);
  });
});
