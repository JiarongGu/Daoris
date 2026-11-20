import { describe, expect, it } from 'vitest';
import type { Session } from '../../data/api';
import type { StateLine } from '../head/stateLine';
import { headFactsOf, modelRan } from './headFacts';

// The head's facts read from what the page already holds: the state from the driver's reader (the state line), never the record's
// word; the model that ran from the record's acknowledged models, never the one asked; the asked one only where it differs.

const SESSION = {
  id: 's1', repository: 'api-server', door: 'claude-code', state: 'working', kind: 'driven', created: '2026-11-20T09:12:00Z', updated: '2026-11-20T15:14:00Z',
  ran: { models: ['opus-4.1'], more: 0, entries: [
    { at: '2026-11-20T09:12:00Z', acknowledged: 'sonnet-4.5' }, { at: '2026-11-20T10:00:00Z', acknowledged: 'opus-4.1' },
  ], entriesMore: 0 },
  asked: { agent: 'claude-code', model: { source: 'entry', value: 'opus-4.1', carried: false }, effort: { source: 'entry', carried: false } },
} as unknown as Session;

const line = (kind: StateLine['kind'], cause: StateLine['cause'] = null): StateLine => ({
  kind, since: '2026-11-20T15:14:00Z', wait: null, cause, more: 0, disagrees: null,
});

const WHO = { title: 'token-bucket limits', ask: 'Rate-limit the public API', maker: 'Claude', role: 'Builder', account: 'home' };

describe('headFactsOf', () => {
  it('names the newest model the record acknowledged, and the asked one only where it differs', () => {
    expect(modelRan(SESSION)).toEqual({ model: 'opus-4.1', asked: null });
    expect(modelRan({ ...SESSION, asked: { ...SESSION.asked!, model: { source: 'entry', value: 'sonnet-4.5', carried: false } } }))
      .toEqual({ model: 'opus-4.1', asked: 'sonnet-4.5' });
    expect(modelRan({ ...SESSION, ran: null })).toEqual({ model: null, asked: 'opus-4.1' });
  });

  it('reads the state from the reader\'s line, its cause as what it does now', () => {
    const head = headFactsOf({ session: SESSION, line: line('working', { said: 'npm run e2e' }), ...WHO });
    expect(head).toMatchObject({ state: 'live', doing: 'npm run e2e', repository: 'api-server', model: 'opus-4.1', since: '2026-11-20T15:14:00Z' });
    expect(headFactsOf({ session: SESSION, line: line('needs-you'), ...WHO }).state).toBe('waiting');
    expect(headFactsOf({ session: SESSION, line: line('step', { said: 'reviewer: Codex' }), ...WHO })).toMatchObject({ state: 'waitsOn', waitsOn: 'reviewer: Codex' });
    expect(headFactsOf({ session: SESSION, line: line('failed'), ...WHO }).state).toBe('failed');
    expect(headFactsOf({ session: { ...SESSION, state: 'stopped' }, line: line('done'), ...WHO }).state).toBe('stopped');
    expect(headFactsOf({ session: { ...SESSION, state: 'completed' }, line: line('done'), ...WHO }).state).toBe('finished');
    expect(headFactsOf({ session: SESSION, line: line('unread'), ...WHO }).state).toBe('loading');
  });
});
