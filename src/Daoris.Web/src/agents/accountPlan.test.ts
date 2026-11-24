import { beforeEach, describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import { byTool, type ToolDoor } from './tools';
import { accountName, accountPlan } from './agents';

// The plan an account's agent named, said beside it once: the walk weighs the account's room in it.

describe('the plan beside an account', () => {
  beforeEach(() => i18n.changeLanguage('en'));

  const EMAIL = 'person@example.invalid';
  const claude = (profiles: ToolDoor['profiles']): ToolDoor => ({
    door: 'claude-code', product: 'Claude Code', maker: 'Anthropic', present: true, version: '2.1.4 (Claude Code)',
    transport: 'native', ownLogin: 'unknown', ownRead: null, profiles,
  });
  const tool = (profiles: ToolDoor['profiles']) => byTool([claude(profiles)])[0]!;
  const a = { name: 'acct-0000000a', home: 'h/a', login: 'in' as const, account: EMAIL, org: 'Org A', plan: 'max_20x' };
  const b = { name: 'acct-0000000b', home: 'h/b', login: 'in' as const, account: EMAIL, org: 'Org B', plan: 'pro' };

  it('says the plan beside a signed-in account, as agent list does', () => {
    expect(accountPlan(a, tool([a, b]))).toBe('max_20x');
    expect(accountPlan(a)).toBe('max_20x');
  });

  it('says none for a key, for an agent that named none, or for a name that is the plan', () => {
    expect(accountPlan({ ...a, plan: null })).toBeNull();
    expect(accountPlan({ ...a, key: '…wxyz' })).toBeNull();
    expect(accountName({ ...a, displayName: 'max_20x' })).toBe('max_20x');
    expect(accountPlan({ ...a, displayName: 'max_20x' })).toBeNull();
  });

  it('says it once where the plan is also what tells two sign-ins of one email apart', () => {
    const planned = { ...b, org: null };

    expect(accountPlan(planned, tool([a, planned]))).toBeNull();
  });
});
