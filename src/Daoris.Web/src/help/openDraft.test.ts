import { afterEach, describe, expect, it } from 'vitest';
import {
  draftToTell, draftWhere, noteTold, onTold, openDraftNow, registerOpenDraft, toldGraph, toldSessions, type OpenDraft,
} from './openDraft';
import { fingerprintOf } from '../workflow/draftFingerprint';

const graph = {
  schemaVersion: 2, entries: ['work'], end: 'line',
  nodes: [{ id: 'work', kind: 'claimed', version: 1, reach: 'observed' }, { id: 'line', kind: 'line', version: 1, reach: 'observed' }],
  wires: [{ id: 'a', kind: 'after', from: 'work', to: 'line' }],
};
const draft: OpenDraft = { place: 'engine', graph, workflow: 'release-flow', base: 2, source: 'release-flow@v2', pipeline: 'development', selected: 'work', problems: [] };

describe('the open draft', () => {
  const unregister: (() => void)[] = [];
  afterEach(() => unregister.splice(0).forEach((each) => each()));

  it('is the one the editor registered while it is mounted, and none once it lets go', () => {
    expect(openDraftNow()).toBeNull();
    unregister.push(registerOpenDraft(draft));
    expect(openDraftNow()).toEqual(draft);
    unregister.splice(0).forEach((each) => each());
    expect(openDraftNow()).toBeNull();
  });

  it('is let go only by the registration that holds it', () => {
    const first = registerOpenDraft(draft);
    unregister.push(registerOpenDraft({ ...draft, selected: 'line' }));
    first();
    expect(openDraftNow()?.selected).toBe('line');
  });

  /** Sent where its fingerprint moved since this conversation is told, never again unchanged. */
  it('is told with its fingerprint, and not again while the fingerprint is the one told', () => {
    const sent = draftToTell(draft, undefined)!;
    expect(sent.fingerprint).toBe(fingerprintOf(graph));
    expect(sent).toMatchObject({ place: 'engine', workflow: 'release-flow', base: 2, graph });
    expect(draftToTell(draft, sent.fingerprint)).toBeUndefined();
    expect(draftToTell({ ...draft, graph: { ...graph, end: 'work' } }, sent.fingerprint)?.fingerprint).not.toBe(sent.fingerprint);
    expect(draftToTell(null, undefined)).toBeUndefined();
  });

  /** A proposal names the draft it is made against by its fingerprint, and the editor takes its changes against that graph. */
  it('is kept as told, its graph by its fingerprint and the conversations told it, newest last', () => {
    const heard: string[][] = [];
    const stop = onTold(() => heard.push([...toldSessions()]));
    const sent = draftToTell(draft, undefined)!;
    expect(toldGraph(sent.fingerprint)).toBeNull();
    noteTold('s1', sent);
    expect(toldGraph(sent.fingerprint)).toEqual(graph);
    const other = draftToTell({ ...draft, graph: { ...graph, end: 'work' } }, undefined)!;
    noteTold('s2', other);
    noteTold('s1', other);
    expect(toldSessions()).toEqual(['s2', 's1']);
    expect(toldGraph(other.fingerprint)).toMatchObject({ end: 'work' });
    // The same list while nothing new is told: a reader holding it is not drawn again.
    expect(toldSessions()).toBe(toldSessions());
    stop();
    noteTold('s3', sent);
    expect(heard).toEqual([['s1'], ['s1', 's2'], ['s2', 's1']]);
  });

  it('is named where the person is by its workflow, its place, its steps and what is open on it', () => {
    expect(draftWhere({ ...draft, problems: [{ code: 'input-unfed', node: 'work', words: '…' }] })).toEqual({
      workflow: 'release-flow', place: 'engine', base: 2, steps: 2, problems: 1, selected: 'work',
    });
    expect(draftWhere(null)).toBeNull();
  });
});
