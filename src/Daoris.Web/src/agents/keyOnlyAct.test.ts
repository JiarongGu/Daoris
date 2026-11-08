import { describe, expect, it } from 'vitest';
import { accountAct } from './agents';

describe('the act of an account of an agent held by a key alone', () => {
  const out = { state: 'out', read: null, holdsWork: true, cooling: null } as const;

  it('is Set key when signed out, since the agent has no sign-in', () => {
    expect(accountAct(out, { signsIn: false, present: true, takesKey: true })).toEqual({ act: 'setKey', loud: true });
  });

  it('is none where the agent takes no key either', () => {
    expect(accountAct(out, { signsIn: false, present: true })).toBeNull();
  });

  it('is Sign in where the agent signs in, key or not', () => {
    expect(accountAct(out, { signsIn: true, present: true, takesKey: true })).toEqual({ act: 'signIn', loud: true });
  });
});
