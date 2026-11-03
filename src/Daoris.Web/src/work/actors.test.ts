import { beforeEach, describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import type { Session } from '../data/api';
import { actorRows, choiceChip, ranLine } from './actors';

const record = {
  actor: 'build', choice: 'before',
  asked: { agent: 'stub', model: { source: 'entry', value: 'default', carried: true }, effort: { source: 'entry', value: 'high', carried: false } },
  ran: { models: ['z-model', 'a-model'], more: 1, entries: [{ at: '2026-10-24T12:00:00Z', acknowledged: 'alias', effort: 'low' }], entriesMore: 2 },
} as Session;

beforeEach(async () => { await i18n.changeLanguage('en'); });
describe('D205 actor provenance', () => {
  it('keeps asked, acknowledged and ran apart, with first-use order and overflow', () => {
    const rows = actorRows(i18n.t, record);
    expect(rows.find((row) => row.label === 'Role')?.value).toBe('Build');
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
  it('wears the head chip only for a later choice or the person’s pick (MODELROLE1c2, D205 §4.6)', async () => {
    expect(['1', 'before', 'rule-default', 'handed', null].map((choice) => choiceChip(i18n.t, { choice }))).toEqual([null, null, null, null, null]);
    expect(choiceChip(i18n.t, { choice: '2' })).toBe('choice 2');
    expect(choiceChip(i18n.t, { choice: 'pick' })).toBe('your pick');
    await i18n.changeLanguage('zh');
    expect(choiceChip(i18n.t, { choice: '3' })).toBe('选择 3');
    expect(choiceChip(i18n.t, { choice: 'pick' })).toBe('你指定的');
  });
  it('says why the choice ran after the number, by its code or its condition (STAFFPOLICY1f)', async () => {
    const chip = (chosen: Session['chosen'], choice = '2') => choiceChip(i18n.t, { choice, chosen });
    expect(chip({ rule: 'weight', by: 'weight' })).toBe('choice 2 · by weight');
    expect(chip({ rule: 'weight', by: 'share' })).toBe('choice 2 · over its share');
    expect(chip({ rule: 'weight', by: 'budget' })).toBe('choice 2 · over its budget');
    expect(chip({ rule: 'weight', by: 'work' })).toBe('choice 2 · only fit');
    expect(chip({ rule: 'order', by: 'first' })).toBe('choice 2');
    expect(chip({ rule: 'order', by: 'moved', condition: 'limit', from: '1' })).toBe('choice 2 · after a limit');
    expect(chip({ rule: 'order', by: 'moved', condition: 'transient', from: '1' })).toBe('choice 2 · no answer');
    expect(chip({ rule: 'order', by: 'moved', condition: 'model-refused', from: '1' })).toBe('choice 2 · model refused');
    expect(chip({ rule: 'order', by: 'moved' })).toBe('choice 2 · moved');
    expect(chip({ rule: 'order', by: 'later-code', condition: 'later-condition' })).toBe('choice 2 · by policy');
    expect(chip({ rule: 'order', by: 'moved', condition: 'later-condition' })).toBe('choice 2 · moved');
    expect(chip({ rule: 'weight', by: 'weight' }, 'pick')).toBe('your pick');
    expect(chip({ rule: 'weight', by: 'weight' }, '1')).toBeNull();
    await i18n.changeLanguage('zh');
    expect(chip({ rule: 'weight', by: 'weight' })).toBe('选择 2 · 按权重');
    expect(chip({ rule: 'order', by: 'moved', condition: 'limit', from: '1' })).toBe('选择 2 · 达上限后');
    expect(chip({ rule: 'order', by: 'later-code' })).toBe('选择 2 · 按策略');
  });
  it('keeps every reason within the status budget (16 letters, 5 characters)', async () => {
    const codes = ['weight', 'share', 'budget', 'work', 'limit', 'signed-out', 'model-refused', 'transient', 'failed', 'quiet', 'moved', 'other'];
    for (const [language, max] of [['en', 16], ['zh', 5]] as const) {
      await i18n.changeLanguage(language);
      for (const code of codes) expect([...i18n.t(`work.role.chip.reason.${code}`)].length, `${language} ${code}`).toBeLessThanOrEqual(max);
    }
  });
  it('says the Chosen row in a sentence, or that the policy was not recorded', async () => {
    const row = (chosen?: Session['chosen']) => actorRows(i18n.t, { ...record, chosen }).find((each) => each.label === 'Chosen')?.value;
    expect(row()).toBe('the selection policy was not recorded');
    expect(row({ rule: 'weight', by: 'weight' })).toBe('Chosen by the weights of the role’s choices.');
    expect(row({ rule: 'order', by: 'first' })).toBe('Chosen as the first that could run, in the role’s order.');
    expect(row({ rule: 'order', by: 'moved', condition: 'limit', from: '1' })).toBe('Moved here from choice 1: every account of that choice reached its limit.');
    expect(row({ rule: 'order', by: 'moved', condition: 'later-condition', from: '1' })).toBe('Moved here from choice 1.');
    expect(row({ rule: 'order', by: 'later-code' })).toBe('Chosen by the role’s policy.');
    await i18n.changeLanguage('zh');
    expect(actorRows(i18n.t, { ...record, chosen: undefined }).find((each) => each.label === '选择依据')?.value).toBe('未记录选择策略');
    expect(actorRows(i18n.t, { ...record, chosen: { rule: 'order', by: 'moved', condition: 'limit', from: '1' } }).find((each) => each.label === '选择依据')?.value)
      .toBe('从选择 1 改用其他选择：该选择的所有账户都已用尽额度。');
  });
  it('translates the chrome while preserving the agent names', async () => {
    await i18n.changeLanguage('zh');
    expect(actorRows(i18n.t, record)[0]?.value).toBe('开发');
    expect(ranLine(i18n.t, record)).toBe('z-model · a-model · 另有 1 个');
    expect(ranLine(i18n.t, null)).toBe('未报告');
  });
});
