import { afterEach, describe, expect, it } from 'vitest';
import i18n from '../../locales/i18n';
import type { SessionGrouping } from '../groups';
import { rowRun } from '../attention/attention';
import { placedFact } from './SessionRow';

// A session whose quest closed done with part left: its row's line says so in each shipped language, and What needs you's `left` row opens it.

const placed = (over: Partial<SessionGrouping>): SessionGrouping => ({
  session: 's1', group: 'needs-you', state: 'needs-you', reason: 'left', shown: 'completed', archived: false, teammate: false, ...over,
});

const said = (grouping: SessionGrouping) => {
  const fact = placedFact(grouping, grouping.shown);
  return fact ? i18n.t(fact.line, fact.values) : null;
};

describe('a session whose quest closed done with part of the work left', () => {
  afterEach(() => i18n.changeLanguage('en'));

  it('says part is left where its done departed from a requirement, never the plain done', async () => {
    await i18n.changeLanguage('en');
    expect(said(placed({}))).toBe('reached done, but part of its work is left: open it to see what');
    await i18n.changeLanguage('zh');
    expect(said(placed({}))).toBe('已标为完成，但仍有部分未做完：打开查看');
  });

  it('names the run step that failed where its run ended failed after the done', async () => {
    const failed = placed({ waits: [{ run: 'q1', node: 'pull-request', why: 'left', since: '2026-11-10T09:30:00Z' }] });
    await i18n.changeLanguage('en');
    expect(said(failed)).toBe('reached done, but its run’s pull-request failed and what follows it never ran');
    await i18n.changeLanguage('zh');
    expect(said(failed)).toContain('pull-request');
  });

  it('is a row of What needs you that opens its session', () => {
    expect(rowRun({ kind: 'left', id: 's1', where: 'engine', since: null } as never)).toEqual({ session: 's1' });
  });
});
