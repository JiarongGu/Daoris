import { beforeEach, describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import { clockOf } from '../locales/format';
import { accountAct, accountState, readLine, stateWhen, stateWord } from './agents';

/** An account's sign-in state on its page, from the reading the start's walk ranks by. */
describe('an account’s sign-in state', () => {
  const now = new Date('2026-11-16T10:00:00Z');
  const base = { login: 'unknown' as const, read: '2026-11-16T09:57:00Z', key: null };

  beforeEach(async () => {
    await i18n.changeLanguage('en');
  });

  it('says a check that did not answer as such, with Sign in beside it, never as unknown', () => {
    const state = accountState({ ...base, signIn: { state: 'unanswered', since: base.read, notByPerson: false } }, null, true);
    expect(state.state).toBe('unanswered');
    expect(stateWord(state).label).toBe('check not answered');
    expect(readLine(state, now)).toContain('its sign-in check did not answer');
    expect(accountAct(state, { signsIn: true, present: true })).toEqual({ act: 'signIn', loud: false });
  });

  it('says a sign-out since when, however often it is read again since', () => {
    const since = '2026-11-16T08:00:00Z';
    const state = accountState(
      { ...base, login: 'out', read: '2026-11-16T09:59:00Z', signIn: { state: 'out', since, notByPerson: true } }, null, true);
    expect(state.state).toBe('out');
    expect(state.notByPerson).toBe(true);
    expect(stateWhen(state, now)).toBe(`since ${clockOf(since, now)}`);
    expect(accountAct(state, { signsIn: true, present: true })).toEqual({ act: 'signIn', loud: true });
  });

  it('says the same in 中文', async () => {
    await i18n.changeLanguage('zh');
    const state = accountState({ ...base, signIn: { state: 'unanswered', since: base.read, notByPerson: false } }, null, true);
    expect(stateWord(state).label).toBe('登录查询未应答');
  });

  it('reads an older driver, which sends no sign-in state, as before', () => {
    expect(accountState(base, null, true).state).toBe('unknown');
  });
});
