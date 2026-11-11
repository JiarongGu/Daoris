import { describe, expect, it } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { KEPT_ROW, planOf } from './placeFixtures';
import {
  acceptOf, askOf, deliveriesOf, doorOf, drivingOf, freshDraft, offered, type PlacePlanned, reconcileTicks, refusalsOf, suggestionsOf, useSetUpPage,
} from './placePlan';
import { SETUP_PRESETS } from './placeFixtures';

// What the choices page reads of a plan: the suggestion each repository makes, with Keep where its landing
// is chosen or a saved workflow is followed; nothing preselected where they differ; the driving caption's sides; and each refusal's part.

const plan = (repositories: string[], preset = 'not-driven', rest: Partial<ReturnType<typeof askOf>> = {}, mix = {}) =>
  planOf({ repositories, preset, opinion: false, look: false, adoption: false, drive: false, ...rest }, mix);

describe('the deliveries a set-up offers', () => {
  it('are the announced presets that a set-up writes, in the data\'s order', () => {
    expect(deliveriesOf(SETUP_PRESETS).map((each) => each.id)).toEqual(['merge-after-accept', 'branch-to-push', 'pull-request', 'integrated']);
    expect(deliveriesOf([])).toEqual([]);
  });
});

describe('the suggestion a plan makes', () => {
  it('is the driver\'s delivery for one repository, with its why', () => {
    expect(suggestionsOf(plan(['engine']))).toMatchObject({ preset: 'merge-after-accept', adoption: false, mixed: false, why: { code: 'default' } });
  });

  it('is Keep where the landing was chosen, and where a saved workflow is followed', () => {
    expect(suggestionsOf(plan(['engine'], 'not-driven', {}, { engine: KEPT_ROW }))).toMatchObject({ preset: 'keep', why: { code: 'kept' } });
    const saved = { engine: { ...KEPT_ROW, suggested: { preset: 'merge-after-accept', adoption: false, why: { code: 'default' as const, said: 'x' } } } };
    expect(suggestionsOf(plan(['engine'], 'not-driven', {}, saved))).toMatchObject({ preset: 'keep', why: { code: 'saved' } });
  });

  it('is the driver\'s delivery where the workflow followed is the machine\'s default, which is no choice of the place\'s', () => {
    const machine = { engine: { ...KEPT_ROW, before: { ...KEPT_ROW.before!, values: { workflow: { value: 'merge-after-accept', source: 'machine' } } },
      suggested: { preset: 'merge-after-accept', adoption: false, why: { code: 'default' as const, said: 'x' } } } };
    expect(suggestionsOf(plan(['engine'], 'not-driven', {}, machine))).toMatchObject({ preset: 'merge-after-accept', why: { code: 'default' }, following: null });
  });

  it('preselects nothing where the repositories differ, and names the ones each choice suits', () => {
    const mixed = suggestionsOf(plan(['engine', 'site']));
    expect(mixed).toMatchObject({ preset: null, mixed: true, adoption: false });
    expect(mixed!.suits).toEqual({ 'merge-after-accept': ['engine'], 'pull-request': ['site'] });
  });

  it('preselects what all agree on, adoption too', () => {
    expect(suggestionsOf(plan(['site', 'site']))).toMatchObject({ preset: 'pull-request', adoption: true, mixed: false });
    expect(suggestionsOf(null)).toBeNull();
  });
});

describe('the driving a plan reads', () => {
  it('says each repository by its before and after', () => {
    const states = (repositories: string[], rest = {}) => drivingOf(plan(repositories, 'merge-after-accept', rest)).map((each) => [each.state, each.repositories]);
    expect(states(['engine', 'site'])).toEqual([['driven', ['engine']], ['notDriven', ['site']]]);
    expect(states(['engine', 'site'], { drive: true })).toEqual([['driven', ['engine']], ['starts', ['site']]]);
    expect(states(['site'], { adoption: true })).toEqual([['adoptionOnly', ['site']]]);
    expect(drivingOf(plan(['engine'], 'not-driven')).map((each) => each.state)).toEqual(['stops']);
  });
});

describe('a plan\'s refusals', () => {
  it('stand under the part each is about, and a code with a door names where it leads', () => {
    const refused = plan(['site'], 'pull-request', { opinion: true, look: true, adoption: true });
    expect(refusalsOf(refused, 'delivery').map((each) => each.code)).toEqual(['no-landing-plugin', 'no-reviewers', 'no-review-environment']);
    expect(refusalsOf(refused, 'adoption').map((each) => each.code)).toEqual(['no-checkout']);
    expect(refusalsOf(refused, 'driving')).toEqual([]);
    expect(refusalsOf(plan(['engine'], 'not-driven', { drive: true }), 'driving').map((each) => each.code)).toEqual(['not-driven']);
    expect(doorOf('no-reviewers')).toBe('landing');
    expect(doorOf('no-review-environment')).toBe('landing');
    expect(doorOf('no-line')).toBe('landing');
    expect(doorOf('no-landing-plugin')).toBeNull();
  });

  it('sends a Keep ask with driving and no ticks, since the driver refuses them under Keep', () => {
    const draft = { ...freshDraft(['engine']), preset: 'keep', opinion: true, look: true, adoption: false, drive: true };
    expect(askOf(draft)).toEqual({ repositories: ['engine'], preset: 'keep', opinion: false, look: false, adoption: false, drive: true });
  });

  it('gives a workflow kept by a tick the door Edit steps', () => {
    expect(doorOf('keeps-workflow')).toBe('steps');
  });
});


const integrated = () => plan(['engine'], 'integrated');
const moved = (read: PlacePlanned, blob: string): PlacePlanned => ({
  ...read,
  repositories: read.repositories.map((row) => (row.declaration ? { ...row, declaration: { ...row.declaration, blob } } : row)),
});

describe('the acceptances a plan offers, bound to what was read', () => {
  it('carry the repository, purpose, line and blob of the declaration', () => {
    expect(offered(integrated())).toEqual([{ repository: 'engine', purpose: 'integration', line: 'main', blob: '3b18e512dba79e4c8300dd08aeb37f8e728b8dad' }]);
    expect(offered(plan(['engine']))).toEqual([]);
  });

  it('keep a tick while the declaration read is the one it was made at', () => {
    const read = integrated();
    const kept = reconcileTicks(offered(read), read);
    expect(kept.accept).toHaveLength(1);
    expect(kept.cleared).toEqual([]);
  });

  it('clear a tick whose blob changed, saying which and what it is now; the others stand', () => {
    const read = integrated();
    const ticks = [...offered(read), { repository: 'site', purpose: 'landed', line: 'main', blob: 'f00' }];
    const after = reconcileTicks(ticks, moved(read, 'ffffffffffffffffffffffffffffffffffffffff'));
    expect(after.accept).toEqual([{ repository: 'site', purpose: 'landed', line: 'main', blob: 'f00' }]);
    expect(after.cleared).toEqual([{ repository: 'engine', purpose: 'integration', line: 'main', blob: 'ffffffffffffffffffffffffffffffffffffffff' }]);
  });

  it('are sent only for the ones still offered at the blob ticked, without the identity the page keeps', () => {
    const read = integrated();
    const ticks = [...offered(read), { repository: 'engine', purpose: 'landed', line: 'main', blob: 'x' }];
    expect(acceptOf(ticks, read)).toEqual([{ repository: 'engine', purpose: 'integration' }]);
    expect(acceptOf(offered(read), moved(read, 'ffffffffffffffffffffffffffffffffffffffff'))).toEqual([]);
  });
});

describe('where a set-up stands', () => {
  it('opens a fresh draft on the choices', () => {
    expect(freshDraft(['engine']).review).toBe(false);
  });

  it('drops the last result and the state it ended in at any change but its own', () => {
    const { result } = renderHook(() => useSetUpPage());
    act(() => result.current.open('p', ['engine']));
    act(() => result.current.change('p', { result: { done: true, message: 'x', digest: 'd', operations: [] }, ended: [{ state: 'driven', repositories: ['engine'] }] }));
    expect(result.current.draftOf('p')).toMatchObject({ result: { done: true }, ended: [{ state: 'driven' }] });
    act(() => result.current.change('p', { review: false }));
    expect(result.current.draftOf('p')).toMatchObject({ result: null, ended: null });
  });
});
