import { describe, expect, it } from 'vitest';
import { THREE } from '../settings/accountsFixtures';
import { byTool, type Tool, type ToolDoor } from '../agents/tools';
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

  /** OWNSPACE1b-A (D175): nothing named means Daoris's accounts in turn, or the wait; never the tool's own sign-in. */
  it("says Daoris's accounts, in turn, where nothing is named anywhere, or the wait with none, and leaves out an agent not installed", () => {
    expect(accountsHere([tool(), tool({ name: 'codex', product: null, present: false })], { agents: [] }, 'forge', named)).toEqual([
      {
        agent: 'claude-code', product: 'Claude Code', own: false,
        accounts: ['No Daoris account is signed in for it; its starts wait until you sign in.'],
      },
    ]);
    const [signedIn] = byTool([{
      harness: 'claude-code', product: 'Claude Code', maker: 'Anthropic', present: true, version: '2.1.4', wire: 'pipe', signsIn: true,
      installs: true, takesRules: true, ownLogin: 'in', ownAccount: null, ownRead: null,
      profiles: [{ name: 'acct-1', home: 'h/1', login: 'in' }], workspaceDefaults: [], machineDefault: null,
    } as unknown as ToolDoor]);
    expect(accountsHere([signedIn!], { agents: [] }, 'forge', named)[0]!.accounts).toEqual(["Daoris's accounts, in turn"]);
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
