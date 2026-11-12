import { afterEach, describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import i18n from '../locales/i18n';
import type { RunStep } from './run';
import { StepThrough } from './StepThrough';

const step = (node: string, state: string, through: RunStep['through']) =>
  ({ step: { id: node }, node, state, detail: state, through, done: true }) as unknown as RunStep;

describe("a run's step passed on the person's through words", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it.each([
    ['en', 'review', 'Accepted on your words: “please do without my accept”, kept on ask #a1'],
    ['en', 'quest.holds', 'Passed on your words: “please do without my accept”, kept on ask #a1'],
    ['zh', 'review', '已按你的原话采纳：“please do without my accept”，留在需求 #a1 上'],
    ['zh', 'quest.holds', '已按你的原话放行：“please do without my accept”，留在需求 #a1 上'],
  ])('says the quote and the ask it came from (%s, %s)', async (language, node, words) => {
    await i18n.changeLanguage(language);
    render(<StepThrough step={step(node, node === 'review' ? 'reached' : 'skipped', { ask: 'a1', quote: 'please do without my accept' })} />);
    expect(screen.getByText(words)).toBeTruthy();
  });

  it('says nothing for a step its own press moved', () => {
    const { container } = render(<StepThrough step={step('review', 'reached', null)} />);
    expect(container.textContent).toBe('');
  });
});
