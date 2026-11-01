import { describe, expect, it } from 'vitest';
import { KEPT_ROW, planOf } from './placeFixtures';
import { askOf, deliveriesOf, doorOf, drivingOf, freshDraft, refusalsOf, suggestionsOf } from './placePlan';
import { SETUP_PRESETS } from './placeFixtures';

// SETUPUX1j (the setup-UX design §2.2): what the choices page reads of a plan: the suggestion each repository makes, with Keep where its landing
// was chosen or a saved workflow is followed; nothing preselected where they differ; the driving caption's sides; and each refusal's part.

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

  it('sends a Keep ask with its ticks and driving, as a delivery does', () => {
    const draft = { ...freshDraft(['engine']), preset: 'keep', opinion: true, look: true, adoption: false, drive: true };
    expect(askOf(draft)).toEqual({ repositories: ['engine'], preset: 'keep', opinion: true, look: true, adoption: false, drive: true });
  });
});
