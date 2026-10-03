import { describe, expect, it } from 'vitest';
import i18n from '../i18n';
import type { Ask, GoAhead } from '../api';
import { goAheadsAsked, goAheadToast } from './parkGoAheads';

const goAhead = (number: number, ...sessions: string[]): GoAhead => ({
  number, kind: 'write', on: 'production', act: `act ${number}`, state: 'asked',
  asked: sessions.map((session) => ({ session, at: '2026-10-03T08:00:00Z', why: 'needed' })),
});

const ask = (id: string, goAheads?: GoAhead[]): Ask => ({
  id, workspace: 'default', sentence: 'ship it', state: 'Published', tier: 'declarations', asked: '2026-10-03T07:00:00Z',
  updated: '2026-10-03T08:00:00Z', links: [], attachments: [], proposal: [], quests: [], goAheads,
});

/** KNOWUSE1a2 (D135 §2): which go-aheads a park asked, read from its quest's ask by the requests that name it. */
describe('the go-aheads a park asked', () => {
  const asks = [ask('a1', [goAhead(1, 'p1'), goAhead(2, 'other'), goAhead(3, 'other', 'p1')]), ask('a2', [goAhead(1, 'p1')])];

  it('are those on its ask whose requests name it, a go-ahead it asked again among them, in the ask\'s order', () => {
    expect(goAheadsAsked(asks, 'a1', 'p1').map((each) => each.number)).toEqual([1, 3]);
  });

  it('are none with no ask, an ask not read, an ask from before go-aheads, or none of its own', () => {
    expect(goAheadsAsked(asks, null, 'p1')).toEqual([]);
    expect(goAheadsAsked(undefined, 'a1', 'p1')).toEqual([]);
    expect(goAheadsAsked(asks, 'a9', 'p1')).toEqual([]);
    expect(goAheadsAsked([ask('a1')], 'a1', 'p1')).toEqual([]);
    expect(goAheadsAsked(asks, 'a1', 'p9')).toEqual([]);
  });
});

/** What one press said: the yes or the no and that the same session goes on, or the go-ahead alone and why. */
describe('the toast for a go-ahead answered on a park\'s page', () => {
  const t = i18n.t.bind(i18n);
  const answer = (over: Partial<{ sent: boolean; reaches: string | null; why: string | null }>) =>
    ({ sent: true, reaches: 'resume', why: null, message: 'Answered.', ...over });

  it('says the yes or the no and that the same session goes on, where the park took its answer', () => {
    expect(goAheadToast(t, 2, true, answer({}), {})).toBe("Approved go-ahead #2; the same session goes on with it at the driver's next look.");
    expect(goAheadToast(t, 1, false, answer({}), {})).toBe("Refused go-ahead #1; the same session goes on with it at the driver's next look.");
  });

  it('says the go-ahead alone where the words reached a session still running', () => {
    expect(goAheadToast(t, 2, true, answer({ reaches: 'next-step' }), {})).toBe('Answered go-ahead #2.');
  });

  it('says why the park was not answered: by the box\'s code, or because nothing waited on the person', () => {
    expect(goAheadToast(t, 2, true, answer({ sent: false, reaches: null, why: 'stood-down' }), { quest: 'q1' }))
      .toBe("Answered go-ahead #2. It stood down: #q1 is someone else's, so it has nothing to go on with.");
    expect(goAheadToast(t, 2, true, answer({ sent: false, reaches: null }), {}))
      .toBe('Answered go-ahead #2. The session was no longer waiting on you, so the ask keeps your answer for every later start.');
  });

  it('speaks the active catalog, joining two sentences its way', async () => {
    await i18n.changeLanguage('zh');
    expect(goAheadToast(t, 2, true, answer({}), {})).toBe('已放行 #2；同一个会话会在驱动的下一轮带着它继续。');
    expect(goAheadToast(t, 2, true, answer({ sent: false, reaches: null }), {}))
      .toBe('已答复放行 #2。这个会话已不再等你，所以这个需求会保留你的答复，交给之后的每次启动。');
    await i18n.changeLanguage('en');
  });
});
