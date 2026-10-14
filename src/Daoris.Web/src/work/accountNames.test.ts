import { describe, expect, it } from 'vitest';
import type { ToolDoor } from '../agents/tools';
import { accountNames } from './accountNames';

/**
 * NOTENAME1: the names a conversation's notes say an account id by, read from the roster the page already holds, by the
 * rule the agent's page leads its row with (`accountName`). An id two tools name differently is left to its id.
 */
describe('the names a note says an account by', () => {
  const roster: ToolDoor[] = [
    {
      harness: 'claude-code', present: true,
      profiles: [
        { name: 'account-1', displayName: 'personal', home: 'h1', login: 'in' },
        { name: 'account-2', home: 'h2', login: 'in' },
        { name: 'shared', displayName: 'one', home: 'h3', login: 'in' },
      ],
    },
    {
      harness: 'codex', present: true,
      profiles: [{ name: 'shared', displayName: 'two', home: 'h4', login: 'in' }],
    },
  ];

  it('names an account the person named, and leaves one nobody named, and one two tools name differently, to its id', () => {
    const names = accountNames(roster);
    expect(names.get('account-1')).toBe('personal');
    expect(names.has('account-2')).toBe(false);
    expect(names.has('shared')).toBe(false);
    expect(names.has('nobody')).toBe(false);
  });

  it('is empty before the roster has answered', () => {
    expect(accountNames([]).size).toBe(0);
  });
});
