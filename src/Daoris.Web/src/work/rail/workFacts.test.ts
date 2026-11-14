import { afterEach, describe, expect, it } from 'vitest';
import i18n from '../../locales/i18n';
import type { Session } from '../../data/api';
import type { SessionGrouping } from '../groups';
import { passageSteps, turnLines, waitsOn, workSentence } from './workFacts';

// A session listed as work: its run's steps passed, current and to come, who it waits on, one sentence of where it stands, and its
// turns beneath it, a limit's move being the next turn of the same session.

const session = (over: Partial<Session> = {}): Session => ({
  id: 's2', quest: 'q1', repository: 'engine', door: 'claude-code', state: 'awaiting-step', created: '2026-11-15T09:00:00Z',
  updated: '2026-11-15T09:30:00Z', workspace: 'default', kind: 'driven', ...over,
} as Session);

const grouping = (over: Partial<SessionGrouping> = {}): SessionGrouping => ({
  session: 's2', group: 'working', shown: 'awaiting-step', archived: false, teammate: false,
  passage: {
    run: 'q1', workflow: 'integrated', version: 3,
    steps: [
      { node: 'content', kind: 'work', mark: 'passed' }, { node: 'check', kind: 'check', mark: 'current' },
      { node: 'holds', kind: 'holds', mark: 'next' }, { node: 'landing', kind: 'land', mark: 'next' },
    ],
  },
  ...over,
} as SessionGrouping);

describe('a session listed as work', () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  it('draws its bound version\'s steps as passed, current and to come', () => {
    expect(passageSteps(grouping()).map((step) => [step.node, step.mark])).toEqual([
      ['content', 'passed'], ['check', 'current'], ['holds', 'next'], ['landing', 'next'],
    ]);
    expect(passageSteps(grouping({ passage: null }))).toEqual([]);
  });

  it('waits on its step while a check runs, on you only where a step waits on you, else on its agent', () => {
    expect(waitsOn(session(), grouping())).toBe('step');
    expect(waitsOn(session({ state: 'working' }), grouping({ shown: 'working' }))).toBe('agent');
    expect(waitsOn(session({ state: 'completed' }), grouping({ group: 'needs-you', waits: [{ run: 'q1', node: 'look', why: 'accept' }] }))).toBe('you');
    expect(waitsOn(session({ state: 'completed' }), grouping({ group: 'done', shown: 'completed', passage: null }))).toBeNull();
  });

  it('says where it stands in one sentence, in both languages', async () => {
    expect(workSentence(i18n.t, session(), grouping())).toBe('Waits on its step check, step 2 of 4.');
    await i18n.changeLanguage('zh');
    expect(workSentence(i18n.t, session(), grouping())).toBe('等待步骤 check，第 2 步，共 4 步。');
  });

  it('lists its turns beneath it, a limit\'s move the next turn of the same session, with agent, account and usage', () => {
    const turns = [
      { number: 1, cause: 'start', at: '2026-11-15T09:00:00Z', part: 's1', agent: 'claude-code', account: 'work', input: 1200, output: 300 },
      { number: 2, cause: 'limit', at: '2026-11-15T09:20:00Z', part: 's2', agent: 'claude-code', account: 'home' },
    ];
    const lines = turnLines(i18n.t, turns, (id) => (id === 'home' ? 'Home' : id));

    expect(lines).toEqual([
      'Turn 1 · started · claude-code · work · 1,200 in · 300 out',
      'Turn 2 · account limit · claude-code · Home',
    ]);
  });

  it('says a turn\'s usage in Chinese with the counts its door reported', async () => {
    await i18n.changeLanguage('zh');
    const turns = [{ number: 1, cause: 'start', at: '2026-11-15T09:00:00Z', part: 's1', input: 5000 }];

    const [line] = turnLines(i18n.t, turns);
    expect(line).toContain('输入 5,000');
    expect(line).not.toContain('输出');
  });
});
