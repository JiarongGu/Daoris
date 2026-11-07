import { describe, expect, it } from 'vitest';
import { canonicalJson, fingerprintOf } from './draftFingerprint';

const graph = {
  schemaVersion: 2, entries: ['work'], end: 'line',
  nodes: [{ id: 'work', kind: 'claimed', version: 1, reach: 'observed' }, { id: 'line', kind: 'line', version: 1, reach: 'observed' }],
  wires: [{ id: 'a', kind: 'after', from: 'work', to: 'line' }],
};

/** The draft's fingerprint is the exact graph's digest, which the editor's judgement keys on. */
describe('a draft fingerprint', () => {
  it('is the same for the same graph whatever order its keys were written in', () => {
    const reordered = { wires: graph.wires, nodes: graph.nodes.map((node) => ({ reach: node.reach, version: node.version, kind: node.kind, id: node.id })), end: 'line', entries: ['work'], schemaVersion: 2 };
    expect(fingerprintOf(reordered)).toBe(fingerprintOf(graph));
    expect(fingerprintOf(graph)).toMatch(/^g1-[0-9a-f]{16}$/);
  });

  it('moves with any change, an order that means something among them', () => {
    const renamed = { ...graph, wires: [{ ...graph.wires[0]!, id: 'b' }] };
    const turned = { ...graph, nodes: [...graph.nodes].reverse() };
    const ended = { ...graph, end: 'work' };
    const fingerprints = new Set([graph, renamed, turned, ended].map(fingerprintOf));
    expect(fingerprints.size).toBe(4);
  });

  it('writes objects with their keys in order and arrays as they are', () => {
    expect(canonicalJson({ b: [2, 1], a: { d: null, c: 'x' } })).toBe('{"a":{"c":"x","d":null},"b":[2,1]}');
  });
});
