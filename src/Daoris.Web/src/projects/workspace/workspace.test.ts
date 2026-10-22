import { describe, expect, it } from 'vitest';
import { THREE } from '../../settings/accounts/accountsFixtures';
import { byTool, type Tool, type ToolDoor } from '../../agents/tools';
import { accountsHere, hostOf } from './workspace';

// What a workspace's page works out from answers it already holds (UX6g, D150 §4.3).

const tool = (over: Partial<Tool> = {}): Tool => ({
  name: 'claude-code', product: 'Claude Code', maker: 'Anthropic', doors: [], accounts: [], present: true,
  ownLogin: 'in', ownAccount: 'me@example.invalid', ownRead: null, takesRules: true, ...over,
});

const named = (owner: string, profile?: string | null) => (profile ? `${profile}@${owner}` : `own@${owner}`);
const accounts = (...names: string[]) => names.map((name) => ({ name, home: `h/${name}`, login: 'in' as const }));

/** POOL1c (D197 point 1): one set of Daoris's accounts serves every workspace, so every workspace's page names the same. */
describe('the accounts each agent may run in a workspace', () => {
  it("names every account of Daoris's, in the set's order", () => {
    expect(accountsHere([tool({ accounts: accounts('account-1', 'account-2', 'account-3') })], { agents: [THREE] }, named)).toEqual([{
      agent: 'claude-code', product: 'Claude Code', accounts: ['account-1@claude-code', 'account-2@claude-code', 'account-3@claude-code'],
    }]);
  });

  it('puts an account the order does not name yet last, and names the accounts where the order was not answered', () => {
    const four = tool({ accounts: accounts('account-4', 'account-1', 'account-2', 'account-3') });
    expect(accountsHere([four], { agents: [THREE] }, named)[0]!.accounts)
      .toEqual(['account-1@claude-code', 'account-2@claude-code', 'account-3@claude-code', 'account-4@claude-code']);
    expect(accountsHere([four], undefined, named)[0]!.accounts)
      .toEqual(['account-4@claude-code', 'account-1@claude-code', 'account-2@claude-code', 'account-3@claude-code']);
  });

  /** OWNSPACE1b-A (D175): never the tool's own sign-in; none where the agent has no account of Daoris's. */
  it('names none where the agent has no account of Daoris’s, and leaves out an agent not installed', () => {
    expect(accountsHere([tool(), tool({ name: 'codex', product: null, present: false })], { agents: [] }, named)).toEqual([
      { agent: 'claude-code', product: 'Claude Code', accounts: [] },
    ]);
    const [signedIn] = byTool([{
      harness: 'claude-code', product: 'Claude Code', maker: 'Anthropic', present: true, version: '2.1.4', wire: 'pipe', signsIn: true,
      installs: true, takesRules: true, ownLogin: 'in', ownAccount: null, ownRead: null,
      profiles: [{ name: 'acct-1', home: 'h/1', login: 'in' }],
    } as unknown as ToolDoor]);
    expect(accountsHere([signedIn!], { agents: [] }, named)[0]!.accounts).toEqual(['acct-1@claude-code']);
  });
});

describe('where a deployment is', () => {
  it('is its host, or the address whole where it is not a URL', () => {
    expect(hostOf('https://aurora.example.com/daoris')).toBe('aurora.example.com');
    expect(hostOf('aurora.local')).toBe('aurora.local');
  });
});
