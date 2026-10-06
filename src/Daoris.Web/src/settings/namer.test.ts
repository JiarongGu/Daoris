import { describe, expect, it } from 'vitest';
import i18n from '../i18n';
import type { ToolDoor } from '../tools';
import { namer } from './namer';

/**
 * ACCTNAME1 (D152 §4.2, D125's ACCT2 note): the roster's one namer says an account as the agent's page leads its row with it,
 * so every card names an account the same way: the person's name (ACCT2), else a key's handle, else, for a fresh id nobody
 * named, who signed in, else its id. Looked up on the tool, since a door's accounts are its owner's (AGT7).
 */
describe('the roster’s one namer', () => {
  const roster: ToolDoor[] = [
    {
      harness: 'claude-code', present: true,
      ownAccount: 'you@example.com',
      profiles: [
        { name: 'acct-3f9c1a2b', displayName: 'work', home: 'h1', login: 'in', account: 'you@work.example' },
        { name: 'acct-77aa00ff', displayName: null, home: 'h2', login: 'in', account: 'spare@example.invalid' },
        { name: 'account-2', home: 'h3', login: 'in', account: 'you@home.example' },
        { name: 'acct-0badc0de', home: 'h4', login: 'in', key: 'sk-…a1b2' },
      ],
    },
    { harness: 'claude-code-acp', present: true, accountOf: 'claude-code' },
    { harness: 'codex-acp', present: true, accountOf: 'codex' },
  ];
  const nameOf = namer(i18n.t.bind(i18n), roster);

  it('says the person’s name, a key’s handle, who signed in for a fresh id nobody named, else the id', () => {
    expect(nameOf('claude-code', 'acct-3f9c1a2b')).toBe('work');
    expect(nameOf('claude-code', 'acct-77aa00ff')).toBe('spare@example.invalid');
    // An old `account-N` is the word every list and the terminal say (D152 §4.2): its id, as its row leads with it.
    expect(nameOf('claude-code', 'account-2')).toBe('account-2');
    expect(nameOf('claude-code', 'acct-0badc0de')).toBe(i18n.t('harness.profile.keyName', { handle: 'sk-…a1b2' }));
  });

  it('reads a door’s accounts as its owner’s, and says an account no roster holds by its id', () => {
    expect(nameOf('claude-code-acp', 'acct-3f9c1a2b')).toBe('work');
    expect(nameOf('claude-code', 'acct-gone0000')).toBe('acct-gone0000');
    expect(nameOf('codex-acp', 'team')).toBe('team');
  });

  it('says the tool’s own home as its row does: who signed in, else the agent’s own sign-in', () => {
    expect(nameOf('claude-code', null)).toBe('you@example.com');
    expect(nameOf('codex-acp')).toBe(i18n.t('harness.own'));
  });
});
