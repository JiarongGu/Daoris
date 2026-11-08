import { describe, expect, it } from 'vitest';
import { sheetsOf } from '../agents/agentsFixtures';
import type { ToolDoor } from '../agents/tools';
import { readMachine } from './machine';
import { starters } from './starters';

// The machine as the roster answers it: the sheet is what says an agent is held by a key alone, so the reading carries it.
const DSH = {
  door: 'dsh', product: 'DeepSeek Harness', maker: 'DeepSeek', transport: 'protocol', present: true, version: 'dsh 0.1.0',
  problem: null, pinned: null, managed: null, pinnable: true, signsIn: false, takesKey: true,
  profiles: [{ name: 'account-1', home: 'h/account-1', login: 'out' }],
} as unknown as ToolDoor;

describe('the machine read from a roster', () => {
  it('names a key-only agent with no key by its key, not a sign-in', () => {
    const machine = readMachine({
      attached: true, registry: [{ repository: 'game' }], driver: { drivable: ['game'], helperAgent: 'dsh' },
      harnesses: [DSH], agents: sheetsOf([DSH]),
    });

    expect(starters(machine)).toEqual([expect.objectContaining({ id: 'agent-no-key', command: 'daoris agent key dsh' })]);
  });
});
