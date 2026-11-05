import { describe, expect, it } from 'vitest';
import { variantOf, VARIANT_GLYPH } from './stepKinds';

// FLOWCHART2a (D223): a step is drawn by the variant of its kind, in the graph's words and in a Current's; a kind the page has none
// for is `unknown`, never dropped.

describe('the variant of a kind', () => {
  it.each([
    ['claimed', 'work'], ['work', 'work'],
    ['review', 'you'], ['look', 'you'], ['go-ahead', 'you'], ['human', 'you'],
    ['check', 'check'], ['opinion', 'check'],
    ['work.requests', 'holds'], ['quest.holds', 'holds'], ['content.holds', 'holds'],
    ['land', 'landing'], ['landing', 'landing'], ['handed', 'unknown'],
    ['line', 'line'], ['push', 'line'], ['pull-request', 'line'],
    ['request', 'other'], ['wait.unit', 'other'], ['integrate', 'other'], ['quest.close', 'other'], ['all', 'other'], ['stop', 'other'],
    ['local-release/record', 'plugin'],
    ['something-new', 'unknown'], ['', 'unknown'],
  ])('reads %s as %s', (kind, variant) => {
    expect(variantOf(kind, [])).toBe(variant);
  });

  it('reads a plugin step that declares outcomes as a decision, and no built-in as one', () => {
    expect(variantOf('local-release/find', ['release', 'none'])).toBe('decision');
    expect(variantOf('review', ['accepted'])).toBe('you');
  });

  it('has a glyph for every variant', () => {
    for (const variant of ['work', 'you', 'check', 'holds', 'decision', 'plugin', 'landing', 'line', 'other', 'unknown'] as const) {
      expect(VARIANT_GLYPH[variant], variant).toBeTruthy();
    }
  });
});
