import { describe, expect, it } from 'vitest';
import { agentOfDoor, usageOfTool } from './agentOfDoor';

// AGENTPROVIDER1 s-vii (the agent-provider design §7 row 10): the leading agent and usage are counted by agent, a move between one agent's
// two doors one agent, as the roster's sheet says which agent each door is.

const SHEETS = [
  { id: 'claude-code', doors: [{ door: 'claude-code', transport: 'native' as const, rows: [] }, { door: 'claude-code-acp', transport: 'protocol' as const, rows: [] }] },
  { id: 'p-a', doors: [{ door: 'p-a', transport: 'protocol' as const, rows: [] }] },
  { id: 'p-b', doors: [{ door: 'p-b', transport: 'protocol' as const, rows: [] }] },
];

describe('agentOfDoor', () => {
  it('reads a door as its agent by the sheet, two providers sharing a pool as two, and a door no sheet names as itself', () => {
    expect(agentOfDoor('claude-code-acp', SHEETS)).toBe('claude-code');
    expect(agentOfDoor('claude-code', SHEETS)).toBe('claude-code');
    expect(agentOfDoor('p-a', SHEETS)).not.toBe(agentOfDoor('p-b', SHEETS));
    expect(agentOfDoor('p-gone', SHEETS)).toBe('p-gone');
    expect(agentOfDoor('claude-code-acp', undefined)).toBe('claude-code-acp');
    expect(agentOfDoor('claude-code-acp', 'not a list' as never)).toBe('claude-code-acp');
  });
});

describe('usageOfTool', () => {
  it('gives a tool the shares of its own agent and of an agent that is one of its doors, never another agent’s', () => {
    const used = [
      { agent: 'claude-code', profile: 'work' },
      { agent: 'codex', profile: 'work' },
      { agent: 'p-a', profile: null },
    ];
    expect(usageOfTool({ name: 'claude-code', doors: [{ harness: 'claude-code' }, { harness: 'claude-code-acp' }] }, used))
      .toEqual([{ agent: 'claude-code', profile: 'work' }]);
    expect(usageOfTool({ name: 'claude-code-acp', doors: [{ harness: 'p-a' }, { harness: 'p-b' }] }, used))
      .toEqual([{ agent: 'p-a', profile: null }]);
  });
});
