import { describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import type { WorkflowStep } from './current';
import { BRANCH_YOU_PUSH, INTEGRATE_OWNER, LIMITS } from './fixtures';
import { edgeSaid, limitSaid, sourceSaid, stepSet, stepTitle } from './said';

// INTEGRATE1n part n1 (the integration design §17.4): a graph's `integrate` node is a step this page words, as the driver's
// `WorkflowRunWords.Title` words it in the terminal, so a workflow that names it does not read "a step this page has no words for".

const stepOf = (kind: string): WorkflowStep => ({
  id: kind, kind, participation: 'agent', executor: 'agent', press: null, limit: null, source: { rule: null, level: 'default' }, settings: {}, runtime: 'built',
});

describe('stepTitle', () => {
  it('names the integration step in English and in 中文', () => {
    expect(stepTitle(i18n.getFixedT('en'), stepOf('integrate'))).toBe('The integration');
    expect(stepTitle(i18n.getFixedT('zh'), stepOf('integrate'))).toBe('集成');
  });

  // INTEGRATE1r: Current's integrate step is named, said where it was read, and its limit said in the reader's words, not as recorded.
  it("says Current's integrate step: its press, its source and its limit in both languages", () => {
    for (const [language, title, limit] of [
      ['en', 'The integration', /^Not integrated yet: the rule carries `integrate`/],
      ['zh', '集成', /^尚未集成/],
    ] as const) {
      const t = i18n.getFixedT(language);
      expect(stepTitle(t, INTEGRATE_OWNER)).toBe(title);
      const said = limitSaid(t, INTEGRATE_OWNER, LIMITS);
      expect(said?.recorded).toBe(false);
      expect(said?.text).toMatch(limit);
      expect(edgeSaid(t, INTEGRATE_OWNER)).toBe(edgeSaid(t, BRANCH_YOU_PUSH));
      expect(sourceSaid(t, INTEGRATE_OWNER, 'repository', 'aurora')).toBe(sourceSaid(t, BRANCH_YOU_PUSH, 'repository', 'aurora'));
    }
  });

  // INTEGRATE1r part r3: the integrate step says what the rule asks, composed from its three settings as the terminal composes it
  // (`WorkflowWords`), so the tab no longer shows a step with a title and no setting.
  it("says the integrate step's settings: who requests it, its gates and its conflicts, in both languages", () => {
    const of = (settings: Record<string, string>): WorkflowStep => ({ ...INTEGRATE_OWNER, settings });
    const en = i18n.getFixedT('en');
    const zh = i18n.getFixedT('zh');

    expect(stepSet(en, INTEGRATE_OWNER)).toBe(
      'Integrates the landed branch into its line, requested by you, with no gates; a conflict goes back to its session.');
    expect(stepSet(en, of({ request: 'done', gates: 'declared', conflicts: 'hold' }))).toBe(
      "Integrates the landed branch into its line, requested once the quest's done, with its declared gates; a conflict is held.");
    expect(stepSet(en, of({ request: 'steward', gates: 'declared', conflicts: 'session' }))).toBe(
      'Integrates the landed branch into its line, requested by the steward, with its declared gates; a conflict goes back to its session.');
    // A value this page does not know reads as the terminal reads it: the owner's, no gates, back to its session.
    expect(stepSet(en, of({ request: 'someday' }))).toBe(stepSet(en, INTEGRATE_OWNER));

    expect(stepSet(zh, INTEGRATE_OWNER)).toBe('把已落地的分支集成进它的主线，由你发起，不跑关卡；冲突退回它的会话。');
    expect(stepSet(zh, of({ request: 'done', gates: 'declared', conflicts: 'hold' }))).toBe(
      '把已落地的分支集成进它的主线，委托完成时发起，跑它声明的关卡；冲突会被搁置。');
    expect(stepSet(zh, of({ request: 'steward', gates: 'declared', conflicts: 'session' }))).toBe(
      '把已落地的分支集成进它的主线，由管家发起，跑它声明的关卡；冲突退回它的会话。');
  });

  it('keeps the words of the kinds beside it', () => {
    const t = i18n.getFixedT('en');
    expect(stepTitle(t, stepOf('line'))).toBe('On the line');
    expect(stepTitle(t, stepOf('check'))).toBe('The checks');
  });
});
