import { describe, expect, it } from 'vitest';
import { THREE } from '../settings/accountsFixtures';
import type { Tool } from '../tools';
import { accountsHere, hostOf } from './workspace';

// What a workspace's page works out from answers it already holds (UX6g, D150 §4.3).

const tool = (over: Partial<Tool> = {}): Tool => ({
  name: 'claude-code', product: 'Claude Code', maker: 'Anthropic', doors: [], accounts: [], machineDefault: null, present: true,
  ownLogin: 'in', ownAccount: 'me@example.invalid', ownRead: null, takesRules: true, workspaceDefaults: [], ...over,
});

const named = (owner: string, profile?: string | null) => (profile ? `${profile}@${owner}` : `own@${owner}`);

describe('the accounts each agent may run in a workspace', () => {
  it("names a workspace's own list, in its order, as its own", () => {
    expect(accountsHere([tool()], { agents: [THREE] }, 'work', named)).toEqual([
      { agent: 'claude-code', product: 'Claude Code', own: true, accounts: ['account-2@claude-code', 'account-1@claude-code'] },
    ]);
  });

  it("names this machine's list where the workspace has none of its own", () => {
    expect(accountsHere([tool()], { agents: [THREE] }, 'forge', named)).toEqual([{
      agent: 'claude-code', product: 'Claude Code', own: false,
      accounts: ['account-1@claude-code', 'account-2@claude-code', 'account-3@claude-code'],
    }]);
  });

  it("names the agent's own sign-in where nothing is named anywhere, and leaves out an agent not installed", () => {
    expect(accountsHere([tool(), tool({ name: 'codex', product: null, present: false })], { agents: [] }, 'forge', named)).toEqual([
      { agent: 'claude-code', product: 'Claude Code', own: false, accounts: ['own@claude-code'] },
    ]);
  });

  /** A shell older than the accounts' answer still carries a workspace's default on the roster (D49 §4). */
  it("reads a workspace's default off the roster where the accounts were not answered", () => {
    const older = tool({ machineDefault: 'personal', workspaceDefaults: [{ workspace: 'work', profile: 'team' }] });
    expect(accountsHere([older], undefined, 'work', named)[0]).toMatchObject({ own: true, accounts: ['team@claude-code'] });
    expect(accountsHere([older], undefined, 'forge', named)[0]).toMatchObject({ own: false, accounts: ['personal@claude-code'] });
  });
});

describe('where a deployment is', () => {
  it('is its host, or the address whole where it is not a URL', () => {
    expect(hostOf('https://aurora.example.com/daoris')).toBe('aurora.example.com');
    expect(hostOf('aurora.local')).toBe('aurora.local');
  });
});
