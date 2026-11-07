import { describe, expect, it } from 'vitest';
import { NODE_KINDS } from '../../data/generated/service';
import { variantOf, VARIANT_GLYPH } from './stepKinds';

// A step is drawn by the variant of its kind, in the graph's words and in a Current's; a kind the page has none
// for is `unknown`, never dropped.

describe('the variant of a kind', () => {
  it.each([
    ['claimed', 'work'], ['work', 'work'],
    ['review', 'you'], ['look', 'you'], ['go-ahead', 'you'], ['human', 'you'],
    ['check', 'check'], ['opinion', 'check'],
    ['work.requests', 'holds'], ['quest.holds', 'holds'], ['content.holds', 'holds'],
    ['land', 'landing'], ['landing', 'landing'], ['handed', 'unknown'],
    ['line', 'line'], ['push', 'line'], ['pull-request', 'line'], ['monitor', 'line'], ['tidy', 'line'],
    ['request', 'other'], ['wait.unit', 'other'], ['integrate', 'other'], ['quest.close', 'other'], ['all', 'other'], ['stop', 'other'],
    ['local-release/record', 'plugin'], ['acme-host/pull-request', 'line'],
    ['something-new', 'unknown'], ['', 'unknown'],
  ])('reads %s as %s', (kind, variant) => {
    expect(variantOf(kind, [])).toBe(variant);
  });

  it('reads a plugin step that declares outcomes as a decision, and no built-in as one', () => {
    expect(variantOf('local-release/find', ['release', 'none'])).toBe('decision');
    expect(variantOf('review', ['accepted'])).toBe('you');
  });

  it('draws every kind of the kit, by its id and by the step a run draws it as, as the kit says', () => {
    expect(NODE_KINDS.length).toBeGreaterThan(20);
    for (const row of NODE_KINDS) {
      expect(variantOf(row.kind), row.kind).toBe(row.look);
      expect(variantOf(row.drawn), row.drawn).toBe(row.look);
      expect(VARIANT_GLYPH[row.look as keyof typeof VARIANT_GLYPH], row.look).toBeTruthy();
    }
  });

  it('has a glyph for every variant', () => {
    for (const variant of ['work', 'you', 'check', 'holds', 'decision', 'plugin', 'landing', 'line', 'other', 'unknown'] as const) {
      expect(VARIANT_GLYPH[variant], variant).toBeTruthy();
    }
  });
});
