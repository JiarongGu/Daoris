import { describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import { landRetrySaid } from './landRetrySaid';

// LANDRETRY1: a run's `land` refused for a cause that passes says it is tried again by itself, and past its deadline names the line
// its wait climbed to, never plain working; words that are no retry's are left to the step's own sentence.

describe('landRetrySaid', () => {
  for (const language of ['en', 'zh'] as const) {
    const t = i18n.getFixedT(language);

    it(`says a wait tried again by itself, in ${language}`, () => {
      const said = landRetrySaid(t, 'busy');
      expect(said).toBeTruthy();
      expect(said).not.toContain('workflow.landRetry');
    });

    it(`names the line a wait past its deadline climbed to, in ${language}`, () => {
      const said = landRetrySaid(t, "climbed|waiting on the machine's supervisor: you");
      expect(said).toContain("waiting on the machine's supervisor: you");
      expect(said).not.toContain('workflow.landRetry');
    });
  }

  it('leaves words that are no retry to the step', () => {
    expect(landRetrySaid(i18n.getFixedT('en'), 'nothing was handed on at the bound commit yet')).toBeNull();
    expect(landRetrySaid(i18n.getFixedT('en'), '')).toBeNull();
  });
});
