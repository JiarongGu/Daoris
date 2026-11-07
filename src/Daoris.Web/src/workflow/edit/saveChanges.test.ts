import { describe, expect, it } from 'vitest';
import { MERGE_GRAPH } from '../graphFixtures';
import { draftOf, duplicateStep, removeStep, setOption } from './graphDraft';
import { changesBetween, consentKey } from './saveChanges';

// What a save changes against the version it starts from, and the key an append's consent is bound to.

describe('changesBetween', () => {
  const base = draftOf(MERGE_GRAPH);

  it('finds nothing in a draft that is the version it began from', () => {
    expect(changesBetween(base, draftOf(MERGE_GRAPH))).toEqual({ added: [], removed: [], changed: [], ways: 0 });
  });

  it('names a step added, a step removed and a step whose options changed, and counts the ways that differ', () => {
    const made = duplicateStep(base, 'review');
    const added = changesBetween(base, made.draft);
    expect(added.added.map((each) => each.id)).toEqual([made.id]);
    expect(added.removed).toEqual([]);

    const removed = changesBetween(base, removeStep(base, 'content', { join: true }));
    expect(removed.removed.map((each) => each.id)).toEqual(['content']);
    expect(removed.ways).toBeGreaterThan(0);

    const changed = changesBetween(base, setOption(base, 'landing', 'method', 'branch'));
    expect(changed.changed.map((each) => each.id)).toEqual(['landing']);
    expect(changed.ways).toBe(0);
  });

  it('takes a draft with no start as all new', () => {
    const all = changesBetween(null, base);
    expect(all.added).toHaveLength(base.nodes.length);
    expect(all.ways).toBe(base.wires.length);
  });
});

describe('consentKey', () => {
  it('is one key for one set of places, whatever their order, and another when the set changes', () => {
    const a = { key: 'repository:engine:default' };
    const b = { key: 'workspace:aurora:kind:docs' };
    expect(consentKey([a, b])).toBe(consentKey([b, a]));
    expect(consentKey([a])).not.toBe(consentKey([a, b]));
    expect(consentKey([])).toBe('');
  });
});
