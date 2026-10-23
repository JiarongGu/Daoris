import { beforeEach, describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import type { Session } from '../data/api';
import { actorRows, ranLine } from './actors';

const record = {
  actor: 'build', choice: 'before',
  asked: { agent: 'stub', model: { source: 'entry', value: 'default', carried: true }, effort: { source: 'entry', value: 'high', carried: false } },
  ran: { models: ['z-model', 'a-model'], more: 1, entries: [{ at: '2026-10-24T12:00:00Z', acknowledged: 'alias', effort: 'low' }], entriesMore: 2 },
} as Session;

beforeEach(async () => { await i18n.changeLanguage('en'); });
describe('D205 actor provenance', () => {
  it('keeps asked, acknowledged and ran apart, with first-use order and overflow', () => {
    const rows = actorRows(i18n.t, record);
    expect(rows.find((row) => row.label === 'Actor')?.value).toBe('Build');
    expect(rows.find((row) => row.label === 'Choice')?.value).toBe('as before');
    expect(rows.find((row) => row.label === 'Asked model · effort')?.value).toBe('default · high · not carried');
    expect(rows.find((row) => row.label === 'Acknowledged model')?.value).toBe('alias');
    expect(ranLine(i18n.t, record)).toBe('z-model · a-model · and 1 more');
  });
  it('never substitutes the requested model for an absent report', () => {
    expect(ranLine(i18n.t, { ...record, ran: null })).toBe('not reported');
    expect(actorRows(i18n.t, { ...record, actor: null })[0]?.value).toBe('not recorded');
  });
  it('distinguishes an explicit default alias from account default', () => {
    const changed = { ...record, asked: { ...record.asked!, model: { source: 'account-default' as const, carried: false } } };
    expect(actorRows(i18n.t, changed).find((row) => row.label === 'Asked model · effort')?.value).toContain('account default');
  });
  it('translates the chrome while preserving the agent names', async () => {
    await i18n.changeLanguage('zh');
    expect(actorRows(i18n.t, record)[0]?.value).toBe('开发');
    expect(ranLine(i18n.t, record)).toBe('z-model · a-model · 另有 1 个');
    expect(ranLine(i18n.t, null)).toBe('未报告');
  });
});
