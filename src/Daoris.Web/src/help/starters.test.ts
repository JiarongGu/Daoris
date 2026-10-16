import { describe, expect, it } from 'vitest';
import { starters } from './starters';
import type { Tool } from '../agents/tools';

// HELP1d (D89): Ask Daoris's no-agent tier — what this machine lacks, each with the screen that fixes
// it and the terminal command that does the same. Pure: every machine is an argument.

const tool = (over: Partial<Tool> = {}): Tool => ({
  name: 'claude-code', product: 'Claude Code', maker: 'Anthropic', doors: [], accounts: [],
  machineDefault: null, present: true, ownLogin: 'in', ownAccount: null, ownRead: null, takesRules: false, workspaceDefaults: [], ...over,
  // A signed-in account of Daoris's, which the other starters' machines need to be quiet about the agent (OWNSPACE1b-C).
  ...(over.accounts ? {} : { accounts: [{ name: 'work', home: '', login: 'in' }] as Tool['accounts'] }),
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
      repositories: [], drivable: [], tools: [tool({ accounts: [] })], waiting: 2, unnamedLines: [], helper: null,
    });

    expect(all.map((starter) => starter.id)).toEqual(['waiting', 'nothing-registered', 'agent-signed-out', 'helper-off']);
    // ENTRY1b: the sessions waiting on the person are Sessions' *Waiting on you*, which its door brings into view.
    expect(all[0]).toMatchObject({ values: { count: 2 } });
    expect(all[0]!.door).toEqual({ view: 'sessions', group: 'you' });
    expect(all[1]).toMatchObject({ door: { view: 'projects' }, command: 'daoris connect' });
  });

  it('says nothing is driven only where something is registered to drive', () => {
    const lacking = starters({ ...READY, drivable: [] });

    expect(lacking).toEqual([expect.objectContaining({
      id: 'nothing-driven', door: { view: 'projects' }, command: 'daoris driver drive <repository>',
    })]);
  });

  it('names a tool no Daoris account is signed in to: none, or each a definite out (OWNSPACE1b-C)', () => {
    const work = (login: string) => [{ name: 'work', home: '', login }] as Tool['accounts'];
    const out = starters({ ...READY, tools: [tool({ ownLogin: 'in', accounts: [] }), tool({ name: 'codex', product: 'Codex', accounts: work('unknown') })] });

    expect(out).toEqual([expect.objectContaining({
      id: 'agent-signed-out', values: { tool: 'Claude Code' }, door: { view: 'agents', item: 'claude-code', agentPart: 'accounts' },
      command: 'daoris agent login claude-code',
    })]);
    // Each account out is none signed in, whatever the tool's own home says; one in, or not known, is an account.
    expect(starters({ ...READY, tools: [tool({ ownLogin: 'in', accounts: work('out') })] })).toHaveLength(1);
    expect(starters({ ...READY, tools: [tool({ ownLogin: 'out', accounts: work('in') })] })).toEqual([]);
  });

  it('names the repositories with no line git can name, and where to set one', () => {
    expect(starters({ ...READY, unnamedLines: ['engine', 'game', 'tools'] })).toEqual([expect.objectContaining({
      // The first one's Setup, where its line is set (UX6f, UX6g).
      id: 'no-line', values: { count: 3, first: 'engine' }, door: { view: 'projects', item: 'engine', tab: 'setup' },
      command: 'daoris driver line engine <branch>',
    })]);
  });
});
