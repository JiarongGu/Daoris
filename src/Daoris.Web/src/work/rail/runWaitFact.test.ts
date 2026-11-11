import { afterEach, describe, expect, it } from 'vitest';
import i18n from '../../locales/i18n';
import type { SessionGrouping } from '../groups';
import { rowRun } from '../attention/attention';
import type { Attention } from '../attention/AttentionRow';
import { placedFact } from './SessionRow';
import { opensRun } from './runWaitFact';

// A session the driver placed by its run's waits: its row and *What needs you*'s say what the run waits for, in
// each shipped language, and the band's row opens its run. Which session stands for which run, and in which group, is
// .NET's (`RunWaitGroupsTests`).

const placed = (over: Partial<SessionGrouping>): SessionGrouping => ({
  session: 's1', group: 'needs-you', state: 'needs-you', reason: 'review', shown: 'completed', archived: true, teammate: false, ...over,
});

const accept = placed({ waits: [{ run: 'q1', node: 'review', why: 'accept', since: '2026-11-10T09:30:00Z' }] });
const approval = placed({ group: 'needs-you', waits: [{ run: 'q1', node: 'approve', why: 'approval' }] });

const said = (grouping: SessionGrouping) => {
  const fact = placedFact(grouping, grouping.shown);
  return fact ? i18n.t(fact.line, fact.values) : null;
};

describe('a row its run waits placed', () => {
  afterEach(() => i18n.changeLanguage('en'));

  it('says the Accept its run waits for, ahead of its tree’s work, in English and Chinese', async () => {
    await i18n.changeLanguage('en');
    expect(said({ ...accept, work: { commits: 2, uncommitted: 0 } })).toBe('its run waits for your Accept');
    expect(said(approval)).toBe('its run waits for your approval at approve');
    expect(said(placed({ group: 'needs-you', waits: [{ run: 'q1', node: 'check', why: 'held' }] }))).toBe('its run’s check is held for your word');
    await i18n.changeLanguage('zh');
    expect(said(accept)).toBe('它的进展在等你采纳');
    expect(said(approval)).toBe('它的进展停在 approve，等你批准');
  });

  it('says the step that put it in Waiting on you where more than one waits, and a word a newer host adds plainly', async () => {
    await i18n.changeLanguage('en');
    const both = placed({ group: 'needs-you', waits: [{ run: 'q1', node: 'review', why: 'accept' }, { run: 'q1', node: 'go', why: 'go-ahead' }] });
    expect(said(both)).toBe('its run waits for your go-ahead at go');
    expect(said(placed({ group: 'needs-you', waits: [{ run: 'q1', node: 'next', why: 'newer' }] }))).toBe('its run waits for you at next');
  });

  it('is offered in What needs you as a review row that opens its run, and the list opens it on its run', () => {
    const row = { kind: 'review', id: 's1', placed: accept } as unknown as Attention;
    expect(rowRun(row)).toEqual({ session: 's1' });
    expect(opensRun([accept, placed({ session: 's2', group: 'done' })], 's1')).toBe(true);
    expect(opensRun([accept, placed({ session: 's2', group: 'done' })], 's2')).toBe(false);
  });
});
