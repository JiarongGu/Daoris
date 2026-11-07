import { describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import type { WorkflowStep } from './current';
import { BRANCH_YOU_PUSH, INTEGRATE_OWNER, LIMITS } from './fixtures';
import { edgeSaid, limitSaid, partSaid, sourceSaid, stepSet, stepTitle } from './said';

// A graph's `integrate` node is a step this page words, as the driver's
// `WorkflowRunWords.Title` words it in the terminal, so a workflow that names it does not read "a step this page has no words for".

const stepOf = (kind: string): WorkflowStep => ({
  id: kind, kind, participation: 'agent', executor: 'agent', press: null, limit: null, source: { rule: null, level: 'default' }, settings: {}, runtime: 'built',
});

// The pipeline design §4.5: a verification's check whose failure waits is the person's part, its *Ask for a fix…*; one set to `failed: ask`
// is their standing say-so, said in their part as a fix asked with no press of theirs; `stop` asks nothing.
describe('partSaid on a verification', () => {
  const check = (failed?: string): WorkflowStep => ({ ...stepOf('check'), executor: 'daoris', settings: failed ? { failed } : {} });

  it('says a failed check waits for your Ask for a fix, in English and in 中文', () => {
    expect(partSaid(i18n.getFixedT('en'), { steps: [check()] })).toBe('Your part: you ask for a fix when a check fails (*Ask for a fix…*).');
    expect(partSaid(i18n.getFixedT('zh'), { steps: [check('wait')] })).toBe('你的部分：检查失败时，由你请人修复（*请人修复…*）。');
  });

  it('says a failed check asks for its fix without asking you where you set it, in English and in 中文', () => {
    expect(partSaid(i18n.getFixedT('en'), { steps: [check('ask')] }))
      .toBe('Your part: none. Nothing here waits for a press of yours. A failed verification asks for a fix without asking you.');
    expect(partSaid(i18n.getFixedT('zh'), { steps: [check('ask')] }))
      .toBe('你的部分：无。这里没有什么等你按下按钮。验证失败时，不问你便请人修复。');
    expect(partSaid(i18n.getFixedT('en'), { steps: [check('stop')] })).toBe('Your part: none. Nothing here waits for a press of yours.');
  });
});

describe('stepTitle', () => {
  it('names the integration step in English and in 中文', () => {
    expect(stepTitle(i18n.getFixedT('en'), stepOf('integrate'))).toBe('The integration request');
    expect(stepTitle(i18n.getFixedT('zh'), stepOf('integrate'))).toBe('集成请求');
  });

  // A graph's `request` node, another repository asked as a step, named as the driver's title names it.
  it('names the request step in English and in 中文', () => {
    expect(stepTitle(i18n.getFixedT('en'), stepOf('request'))).toBe('The quest to another repository');
    expect(stepTitle(i18n.getFixedT('zh'), stepOf('request'))).toBe('给另一个仓库的委托');
  });

  // The steps past a pull request, the watch on it and the tidy of the chain, named as the driver's title names them.
  it('names the monitor and the tidy steps in English and in 中文', () => {
    expect(stepTitle(i18n.getFixedT('en'), stepOf('monitor'))).toBe("The pull request's watch");
    expect(stepTitle(i18n.getFixedT('zh'), stepOf('monitor'))).toBe('等待拉取请求合并');
    expect(stepTitle(i18n.getFixedT('en'), stepOf('tidy'))).toBe("The chain's clean-up");
    expect(stepTitle(i18n.getFixedT('zh'), stepOf('tidy'))).toBe('清理委托链');
  });

  // Current's integrate step is named, said where it is read, and its limit said in the reader's words, not as recorded.
  it("says Current's integrate step: its press, its source and its limit in both languages", () => {
    for (const [language, title, limit] of [
      ['en', 'The integration request', /^Not integrated yet: the rule carries `integrate`/],
      ['zh', '集成请求', /^尚未集成/],
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

  // The integrate step says what the rule asks, composed from its three settings as the terminal composes it
  // (`WorkflowWords`), so the tab shows no step with a title and no setting.
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

    expect(stepSet(zh, INTEGRATE_OWNER)).toBe('把已落地的分支集成进它的主线，由你发起，不跑检查项；冲突退回它的会话。');
    expect(stepSet(zh, of({ request: 'done', gates: 'declared', conflicts: 'hold' }))).toBe(
      '把已落地的分支集成进它的主线，委托完成时发起，跑它声明的检查项；冲突会被暂停。');
    expect(stepSet(zh, of({ request: 'steward', gates: 'declared', conflicts: 'session' }))).toBe(
      '把已落地的分支集成进它的主线，由管家发起，跑它声明的检查项；冲突退回它的会话。');
  });

  it('keeps the words of the kinds beside it', () => {
    const t = i18n.getFixedT('en');
    expect(stepTitle(t, stepOf('line'))).toBe('On the line');
    expect(stepTitle(t, stepOf('check'))).toBe('The checks');
  });
});
