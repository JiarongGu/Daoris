import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import { accountAct, accountState, readLine, stateWhen, stateWord } from './agents';
import { heldLine, nextLine, type NextStart, USE_DEFAULTS } from '../settings/accounts/accounts';

// The person's switch is a fact beside an account's state, never in its place: the window keeps saying what its agent last said.

const READ = '2026-11-24T03:00:00.000Z';
const scope = {
  workspace: null, default: null, list: ['account-1', 'account-2'], begins: 'account-1', use: USE_DEFAULTS, unknown: [], problem: null, near: [],
};
const labelOf = (name: string) => name;

describe('an account the person switched off', () => {
  beforeEach(() => i18n.changeLanguage('en'));
  afterEach(() => i18n.changeLanguage('en'));

  it('keeps the state its agent last said, with its reading, and holds no work', () => {
    const off = accountState({ login: 'out', read: READ, off: true }, null, true);

    expect(off).toMatchObject({ state: 'out', read: READ, off: true, holdsWork: false });
    expect(stateWord(off)).toEqual({ label: 'signed out', tone: 'neutral' });
    expect(readLine(off)).toMatch(/^read /);
    expect(stateWhen(off)).not.toBe('');
    expect(accountState({ login: 'in', read: READ, off: true }, null, true)).toMatchObject({ state: 'in', off: true });
    expect(accountState({ login: 'in', read: READ }, null, true).holdsWork).toBe(true);
  });

  it('is offered one act, switching it on, whatever its state', () => {
    for (const login of ['in', 'out', 'unknown'] as const) {
      expect(accountAct(accountState({ login, read: READ, off: true }, null, true), { signsIn: true, present: true }))
        .toEqual({ act: 'switchOn', loud: false });
    }
  });

  it('says in the held line that it is switched off, and in a wait that switching it on starts the start', async () => {
    const waits: NextStart = {
      account: null, reason: 'waits', over: null, others: [{ account: 'account-1', hold: 'off' }, { account: 'account-2', hold: 'off' }],
    };

    expect(nextLine(waits, scope, labelOf)).toBe(
      "None of Daoris's accounts is ready, and none comes ready by itself: the next start waits for you. Switching account-1, account-2 on starts it.",
    );
    expect(heldLine(waits, labelOf)).toBe('account-1 is switched off; account-2 is switched off.');
    await i18n.changeLanguage('zh');
    expect(nextLine(waits, scope, labelOf)).toContain('启用 account-1');
  });
});
