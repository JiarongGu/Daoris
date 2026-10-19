import { describe, expect, it, vi } from 'vitest';

// How each agent's accounts are used, as the shell pushes it (PUSH1e, D182): a partition per agent, each its answer of `ACCOUNTS`
// whole with the verdicts .NET judges beside each reading, composed into the route's answer in its order.

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke: vi.fn() }),
  useShenora: () => ({ isAvailable: true, bridge: {} }),
  useShenoraEvent: () => {},
}));

import { ACCOUNTS_SLICE, keys } from '../../data/queries';
import { accountsSlice } from './accounts';

const agent = (name: string) => ({ agent: name, speaks: true, own: {}, accounts: [], scopes: [] });

describe('how each agent’s accounts are used, pushed (PUSH1e)', () => {
  it("feeds `ACCOUNTS`' own entry from the one snapshot route, each partition pushed whole", () => {
    expect(accountsSlice.name).toBe(ACCOUNTS_SLICE);
    expect(accountsSlice.key).toEqual(keys.accounts);
    expect(accountsSlice.merge).toBeUndefined();
  });

  it('composes nothing before it is judged, and no agent once it is judged with none', () => {
    expect(accountsSlice.compose(new Map(), false)).toBeUndefined();
    expect(accountsSlice.compose(new Map(), true)).toEqual({ agents: [] });
  });

  it("composes each agent in the route's order, by its name as C#'s ordinal compares it, and leaves a part of no shape out", () => {
    const composed = accountsSlice.compose(new Map<string, unknown>([
      ['dsh', agent('dsh')], ['Zed', agent('Zed')], ['claude-code', agent('claude-code')], ['codex', agent('codex')], ['broken', 'x'],
    ]), true) as { agents: { agent: string }[] };
    expect(composed.agents.map((each) => each.agent)).toEqual(['Zed', 'claude-code', 'codex', 'dsh']);
  });
});
