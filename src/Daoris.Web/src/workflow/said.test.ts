import { describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import type { WorkflowStep } from './current';
import { stepTitle } from './said';

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

  it('keeps the words of the kinds beside it', () => {
    const t = i18n.getFixedT('en');
    expect(stepTitle(t, stepOf('line'))).toBe('On the line');
    expect(stepTitle(t, stepOf('check'))).toBe('The checks');
  });
});
