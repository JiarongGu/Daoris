import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// A place's quick set-up as a molecule, its choices page.
// The choices end at the plan's line and *Review the plan*, which opens the review (`PlaceReview.test.tsx`) over the same draft.

import i18n from '../../locales/i18n';
import { KEPT_ROW, planOf, SETUP_PRESETS } from './placeFixtures';
import { askOf, deliveriesOf, freshDraft, type SetUpDraft } from './placePlan';
import { PlaceSetUp, type PlaceSetUpProps } from './PlaceSetUp';

const draftOf = (patch: Partial<SetUpDraft> = {}): SetUpDraft => ({ ...freshDraft(['engine']), preset: 'merge-after-accept', adoption: false, ...patch });

const show = (props: Partial<PlaceSetUpProps> = {}) => {
  const draft = props.draft ?? draftOf();
  const on = { onChange: vi.fn(), onApply: vi.fn(), onHold: vi.fn(), onClose: vi.fn() };
  render(
    <PlaceSetUp
      place="engine"
      kind="repository"
      choices={[]}
      deliveries={deliveriesOf(SETUP_PRESETS)}
      draft={draft}
      plan={planOf(askOf(draft))}
      reading={false}
      stale={false}
      problem={null}
      applying={false}
      holding={false}
      holds={{}}
      {...on}
      {...props}
    />,
  );
  return on;
};

/** The page as the application holds it: the draft kept above, a change replacing what it names. */
function Held({ start, ...props }: { start: SetUpDraft } & Partial<PlaceSetUpProps>) {
  const [draft, setDraft] = useState(start);
  return (
    <PlaceSetUp
      place="aurora"
      kind="workspace"
      choices={[]}
      deliveries={deliveriesOf(SETUP_PRESETS)}
      draft={draft}
      onChange={(patch) => setDraft((was) => ({ ...was, ...patch }))}
      plan={planOf(askOf(draft))}
      reading={false}
      stale={false}
      problem={null}
      applying={false}
      holding={false}
      holds={{}}
      onApply={() => {}}
      onHold={() => {}}
      onClose={() => {}}
      {...props}
    />
  );
}

/** The names of the choices of a radio group, in the order drawn. */
const names = (group: HTMLElement) => within(group).getAllByRole('radio').map((each) => document.getElementById(each.getAttribute('aria-labelledby')!)!.textContent);

describe('Set up…, a place\'s quick set-up', () => {
  beforeEach(() => { i18n.changeLanguage('en'); });
  afterEach(cleanup);

  it('keeps how it lands, adoption and driving apart, with the deliveries the data offers worded by id, and Not driven last', () => {
    show();
    const lands = screen.getByRole('radiogroup', { name: 'How it lands' });
    expect(names(lands)).toEqual([
      'Keep how it lands', 'Merge after you accept', 'A branch to push', 'A pull request', 'Integrated into the line', 'Not driven',
    ]);
    expect(within(lands).getByRole('radio', { name: /Merge after you accept/ })).toBeChecked();
    // A delivery's purpose is the catalogue's, by its id.
    expect(within(lands).getByText(/once the work's requests are answered you review it, and it merges into the line/)).toBeTruthy();
    expect(screen.getByRole('checkbox', { name: 'Ask its session to adopt Daoris' })).not.toBeChecked();
    expect(screen.getByRole('checkbox', { name: 'Drive all its work' })).not.toBeChecked();
    // What it builds is the adoption's: it shows only while adoption is ticked.
    expect(screen.queryByRole('radiogroup', { name: 'What it builds' })).toBeNull();
  });

  it('words a delivery in the viewer\'s language by its id, and falls back to the data\'s English for an id the catalogue lacks', async () => {
    await i18n.changeLanguage('zh');
    show({ deliveries: [...deliveriesOf(SETUP_PRESETS), { id: 'custom', name: 'Custom', graph: true, line: '', preset: true, order: 9, setUp: true, delivery: { id: 'custom', method: 'merge', accept: 'you', name: 'A custom way', for: 'It lands as the data says.' } }] });
    const lands = screen.getByRole('radiogroup', { name: '落地方式' });
    expect(names(lands)).toEqual(expect.arrayContaining(['保持落地方式', '未驱动', 'A custom way']));
    expect(within(lands).getByText('It lands as the data says.')).toBeTruthy();
    expect(screen.getByRole('checkbox', { name: '驱动它的全部工作' })).toBeTruthy();
  });

  it('offers Keep how it lands as a choice that sends no delivery, and leaves driving open under it', async () => {
    const on = show({ draft: draftOf({ preset: 'keep' }) });
    const keep = screen.getByRole('radio', { name: 'Keep how it lands' });
    expect(keep).toBeChecked();
    expect(screen.getByRole('checkbox', { name: 'Drive all its work' })).toBeEnabled();
    await userEvent.click(screen.getByRole('radio', { name: /A branch to push/ }));
    expect(on.onChange).toHaveBeenLastCalledWith({ preset: 'branch-to-push' });
    cleanup();
    const other = show();
    await userEvent.click(screen.getByRole('radio', { name: 'Keep how it lands' }));
    expect(other.onChange).toHaveBeenLastCalledWith({ preset: 'keep' });
  });

  it('marks the suggestion with its why worded by code and the driver\'s sentence beneath, and sends the choice, a tick and what it builds', async () => {
    const draft = draftOf({ repositories: ['site'], preset: 'pull-request', adoption: true });
    const on = show({ draft });
    const lands = screen.getByRole('radiogroup', { name: 'How it lands' });
    // The suggestion leads the deliveries.
    expect(names(lands).slice(0, 3)).toEqual(['Keep how it lands', 'A pull request', 'Merge after you accept']);
    const row = screen.getByRole('radio', { name: /A pull request/ }).closest('label')!;
    expect(within(row).getByText('Suggested')).toBeTruthy();
    expect(within(row).getByText('Suggested: a landing plugin serves its remote\'s platform.')).toBeTruthy();
    expect(within(row).getByText('A landing plugin serves its remote\'s platform.')).toBeTruthy();
    await userEvent.click(screen.getByRole('radio', { name: /A branch to push/ }));
    expect(on.onChange).toHaveBeenLastCalledWith({ preset: 'branch-to-push' });
    await userEvent.click(screen.getByRole('checkbox', { name: 'Your look before it lands' }));
    expect(on.onChange).toHaveBeenLastCalledWith({ look: true });
    const builds = screen.getByRole('radiogroup', { name: 'What it builds' });
    expect(within(builds).getAllByRole('radio')).toHaveLength(5);
    await userEvent.click(within(builds).getByRole('radio', { name: /A web app or service/ }));
    expect(on.onChange).toHaveBeenLastCalledWith({ builds: 'service' });
    await userEvent.click(screen.getByRole('checkbox', { name: 'Drive all its work' }));
    expect(on.onChange).toHaveBeenLastCalledWith({ drive: true });
  });

  it('suggests Keep where the landing was chosen or a saved workflow is followed, saying what it follows', () => {
    const draft = draftOf({ preset: 'keep' });
    show({ draft, plan: planOf(askOf(draft), { engine: KEPT_ROW }) });
    const row = screen.getByRole('radio', { name: 'Keep how it lands' }).closest('label')!;
    expect(within(row).getByText('Suggested')).toBeTruthy();
    expect(within(row).getByText('Suggested: it is how this place lands its work now.')).toBeTruthy();
    expect(within(row).getByText('It is how this place already lands its work.')).toBeTruthy();
    expect(row).toHaveTextContent('It follows Release for engine v2 now.');
    // No delivery wears the pill beside it.
    expect(screen.getAllByText('Suggested')).toHaveLength(1);
  });

  it('preselects nothing where the repositories differ, and names the ones each choice suits', () => {
    const draft = draftOf({ repositories: ['engine', 'site'], preset: null });
    show({ place: 'aurora', kind: 'workspace', draft, plan: planOf({ ...askOf(draft) }) });
    const lands = screen.getByRole('radiogroup', { name: 'How it lands' });
    expect(within(lands).queryAllByRole('radio').filter((each) => (each as HTMLInputElement).checked)).toEqual([]);
    expect(within(screen.getByRole('radio', { name: /Merge after you accept/ }).closest('label')!).getByText('Suits engine')).toBeTruthy();
    expect(within(screen.getByRole('radio', { name: /A pull request/ }).closest('label')!).getByText('Suits site')).toBeTruthy();
    // Neither wears Suggested, and the plan waits on the person's choice.
    expect(screen.queryByText('Suggested')).toBeNull();
    expect(screen.getByText(/What suits these repositories differs, so nothing is chosen yet/)).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Review the plan' })).toBeDisabled();
  });

  it('offers Not driven from its own words, with no second opinion, look or driving', () => {
    show({ draft: draftOf({ preset: 'not-driven' }) });
    expect(screen.getByRole('radio', { name: 'Not driven' })).toBeChecked();
    expect(screen.getByText('Daoris starts no work here.')).toBeTruthy();
    for (const name of ['A second opinion before it lands', 'Your look before it lands', 'Drive all its work']) {
      expect(screen.getByRole('checkbox', { name })).toBeDisabled();
    }
  });

  it('leaves the ticks off under Keep how it lands, and driving open', () => {
    show({ draft: draftOf({ preset: 'keep', opinion: true, look: true }) });
    for (const name of ['A second opinion before it lands', 'Your look before it lands']) {
      expect(screen.getByRole('checkbox', { name })).toBeDisabled();
      expect(screen.getByRole('checkbox', { name })).not.toBeChecked();
    }
    expect(screen.getByRole('checkbox', { name: 'Drive all its work' })).toBeEnabled();
  });

  it('captions driving from the plan\'s own before and after, repository by repository', () => {
    const caption = (patch: Partial<SetUpDraft>, repositories = ['engine', 'site']) => {
      cleanup();
      const draft = draftOf({ repositories, ...patch });
      show({ place: 'aurora', kind: 'workspace', draft, plan: planOf(askOf(draft)) });
      return within(screen.getByRole('list', { name: 'Driving' })).getAllByRole('listitem').map((each) => each.textContent);
    };
    expect(caption({})).toEqual(['engine: driven now, and after.', 'site: not driven, and stays so.']);
    expect(screen.getByText('Off: driving stays as it is.')).toBeTruthy();
    expect(caption({ adoption: true })).toEqual(['engine: driven now, and after.', 'site: not driven; only its adoption quest would run.']);
    expect(caption({ drive: true })).toEqual(['engine: driven now, and after.', 'site: not driven now, driven after.']);
    expect(screen.queryByText('Off: driving stays as it is.')).toBeNull();
    expect(caption({ preset: 'not-driven' })).toEqual(['engine: driven now, not after.', 'site: not driven, and stays so.']);
  });

  it('names a few repositories and counts the rest', () => {
    const repositories = ['engine', 'a1', 'a2', 'a3', 'a4'];
    const draft = draftOf({ repositories, preset: 'keep' });
    show({ place: 'aurora', kind: 'workspace', draft, plan: planOf(askOf(draft)) });
    expect(screen.getByText('engine, a1, a2 and 2 more: driven now, and after.')).toBeTruthy();
  });

  it('ends at the plan\'s line and Review the plan, which opens the review and writes nothing', async () => {
    const on = show({ draft: draftOf({ adoption: true }) });
    const plan = screen.getByRole('region', { name: 'The plan' });
    expect(within(plan).getByText('1 change · nothing stops it')).toBeTruthy();
    // The review's own parts and its Apply are not on this page.
    expect(screen.queryByRole('list', { name: 'What it changes' })).toBeNull();
    expect(screen.queryByRole('button', { name: /^Apply/ })).toBeNull();
    await userEvent.click(within(plan).getByRole('button', { name: 'Review the plan' }));
    expect(on.onChange).toHaveBeenLastCalledWith({ review: true });
    expect(on.onApply).not.toHaveBeenCalled();
  });

  it('offers Review the plan only for a plan read of a choice made', () => {
    show({ plan: null, problem: null });
    expect(screen.getByRole('button', { name: 'Review the plan' })).toBeDisabled();
    cleanup();
    show({ draft: draftOf({ repositories: [] }), plan: null });
    expect(screen.getByRole('button', { name: 'Review the plan' })).toBeDisabled();
    cleanup();
    // A plan that refuses is reviewed too: the review lists what stops it.
    const refusing = draftOf({ repositories: ['site'], preset: 'pull-request', adoption: true });
    show({ draft: refusing, plan: planOf(askOf(refusing)) });
    expect(screen.getByRole('button', { name: 'Review the plan' })).toBeEnabled();
  });

  it('opens on the review where the draft is on it, and goes back to the choices at the part a refusal names, the draft kept', async () => {
    const start = draftOf({ repositories: ['engine', 'site'], preset: 'pull-request', adoption: true, review: true });
    render(<Held start={start} />);
    expect(screen.getByRole('heading', { name: 'Review: set up aurora' })).toBeTruthy();
    expect(screen.queryByRole('radiogroup', { name: 'How it lands' })).toBeNull();
    await userEvent.click(within(screen.getByRole('region', { name: 'What stops it' })).getByRole('button', { name: '↑ Adoption' }));
    expect(screen.getByRole('radiogroup', { name: 'How it lands' })).toBeTruthy();
    expect(document.activeElement).toBe(screen.getByRole('heading', { name: 'Adoption' }));
    expect(screen.getByRole('radio', { name: /A pull request/ })).toBeChecked();
    // And the review again from there.
    await userEvent.click(screen.getByRole('button', { name: 'Review the plan' }));
    await userEvent.click(screen.getByRole('button', { name: '← Back to the choices' }));
    expect(screen.getByRole('radiogroup', { name: 'How it lands' })).toBeTruthy();
  });

  it('leaves a repository out of a workspace\'s plan from the review: a change of selection', async () => {
    const on = show({
      place: 'aurora',
      kind: 'workspace',
      draft: draftOf({ repositories: ['engine', 'site'], preset: 'pull-request', adoption: true, review: true }),
      plan: planOf(askOf(draftOf({ repositories: ['engine', 'site'], preset: 'pull-request', adoption: true }))),
    });
    await userEvent.click(screen.getByRole('button', { name: 'Leave site out' }));
    expect(on.onChange).toHaveBeenLastCalledWith({ repositories: ['engine'] });
  });

  it('counts the plan\'s changes and what stops it in one line, with a jump to each part that refuses', async () => {
    const draft = draftOf({ repositories: ['site'], preset: 'pull-request', adoption: true });
    show({ draft, plan: planOf(askOf(draft)) });
    const plan = screen.getByRole('region', { name: 'The plan' });
    expect(within(plan).getByText('3 changes · 2 stop it')).toBeTruthy();
    await userEvent.click(within(plan).getByRole('button', { name: '↑ Adoption' }));
    expect(document.activeElement).toBe(screen.getByRole('heading', { name: 'Adoption' }));
    await userEvent.click(within(plan).getByRole('button', { name: '↑ How it lands' }));
    expect(document.activeElement).toBe(screen.getByRole('heading', { name: 'How it lands' }));
    cleanup();
    show();
    expect(within(screen.getByRole('region', { name: 'The plan' })).getByText('0 changes · nothing stops it')).toBeTruthy();
  });

  it('says each refusal whole under the part it is about, and offers no Apply for a plan that refuses', () => {
    const draft = draftOf({ repositories: ['site'], preset: 'pull-request', adoption: true, drive: true });
    show({ draft, plan: planOf(askOf(draft)) });
    const part = (title: string) => screen.getByRole('heading', { name: title }).closest('section')!;
    const delivery = within(part('How it lands')).getByRole('list', { name: 'It cannot be applied yet' });
    expect(within(delivery).getByText(/no landing plugin here opens a pull request there/)).toBeTruthy();
    expect(delivery.textContent).toMatch(/^site/);
    expect(within(part('Adoption')).getByText(/has no checkout on this machine/)).toBeTruthy();
    expect(within(part('Driving')).queryByRole('list', { name: 'It cannot be applied yet' })).toBeNull();
    expect(screen.queryByRole('button', { name: /^Apply/ })).toBeNull();
    cleanup();
    const driving = draftOf({ preset: 'not-driven', drive: true });
    show({ draft: driving, plan: planOf({ ...askOf(driving), drive: true }) });
    expect(within(part('Driving')).getByText(/takes no general driving/)).toBeTruthy();
  });

  it('puts a door under a refusal whose cause is repaired elsewhere, and none under one that has none', async () => {
    const draft = draftOf({ repositories: ['site'], preset: 'pull-request', opinion: true, look: true });
    const doorFor = vi.fn((refusal: { code: string }) => (refusal.code === 'no-landing-plugin' ? null : () => { opened.push(refusal.code); }));
    const opened: string[] = [];
    show({ draft, plan: planOf(askOf(draft)), doorFor });
    const delivery = screen.getByRole('list', { name: 'It cannot be applied yet' });
    expect(within(delivery).getAllByRole('button')).toHaveLength(2);
    await userEvent.click(within(delivery).getByRole('button', { name: 'Name them: Workflow › Second opinion' }));
    await userEvent.click(within(delivery).getByRole('button', { name: 'Add one: Workflow › Your look' }));
    expect(opened).toEqual(['no-reviewers', 'no-review-environment']);
  });

  it('is worked by keyboard alone: a choice by the arrow keys, a tick by the space bar, a door and the plan\'s jump by Enter', async () => {
    const draft = draftOf({ repositories: ['site'], preset: 'branch-to-push', opinion: true });
    const opened: string[] = [];
    const on = show({ draft, plan: planOf(askOf(draft)), doorFor: (refusal) => (refusal.code === 'no-reviewers' ? () => { opened.push(refusal.code); } : null) });
    screen.getByRole('radio', { name: /A branch to push/ }).focus();
    await userEvent.keyboard('{ArrowDown}');
    // The suggestion leads the deliveries, so the next after the branch is the integration.
    expect(on.onChange).toHaveBeenLastCalledWith({ preset: 'integrated' });
    screen.getByRole('checkbox', { name: 'Your look before it lands' }).focus();
    await userEvent.keyboard(' ');
    expect(on.onChange).toHaveBeenLastCalledWith({ look: true });
    const door = screen.getByRole('button', { name: 'Name them: Workflow › Second opinion' });
    door.focus();
    await userEvent.keyboard('{Enter}');
    expect(opened).toEqual(['no-reviewers']);
    const jump = within(screen.getByRole('region', { name: 'The plan' })).getByRole('button', { name: '↑ How it lands' });
    jump.focus();
    await userEvent.keyboard('{Enter}');
    expect(document.activeElement).toBe(screen.getByRole('heading', { name: 'How it lands' }));
  });

  it('hands the review\'s ticks, Apply and hold to the application, a tick clearing the reasons said before it', async () => {
    const draft = draftOf({ preset: 'integrated', review: true, drive: true, cleared: [{ repository: 'engine', purpose: 'integration', line: 'main', blob: 'x' }] });
    const on = show({ draft, plan: planOf(askOf(draft)) });
    await userEvent.click(screen.getByRole('checkbox', { name: 'Accept for integration' }));
    expect(on.onChange).toHaveBeenLastCalledWith({
      accept: [{ repository: 'engine', purpose: 'integration', line: 'main', blob: '3b18e512dba79e4c8300dd08aeb37f8e728b8dad' }], cleared: [],
    });
    await userEvent.click(screen.getByRole('button', { name: 'Hold it after driving' }));
    expect(on.onHold).toHaveBeenCalledWith(['engine']);
    await userEvent.click(screen.getByRole('button', { name: 'Apply to 1 repository' }));
    expect(on.onApply).toHaveBeenCalledTimes(1);
  });

  it('chooses a workspace\'s repositories from its whole list: All, Driven, Not driven or None among those a search matches, with a count', async () => {
    const choices = [
      ...['engine', 'api'].map((repository) => ({ repository, driven: true })),
      ...Array.from({ length: 9 }, (_, at) => ({ repository: `archive-${at + 1}`, driven: false })),
    ];
    const on = show({ place: 'aurora', kind: 'workspace', choices, draft: draftOf({ repositories: ['engine'] }) });
    const group = screen.getByRole('group', { name: 'Repositories' });
    expect(within(group).getByText('1 of 11 chosen')).toBeTruthy();
    expect(within(group).getByRole('checkbox', { name: 'engine' })).toBeChecked();
    await userEvent.click(within(group).getByRole('checkbox', { name: 'archive-1 · Not driven' }));
    expect(on.onChange).toHaveBeenLastCalledWith({ repositories: ['engine', 'archive-1'] });
    await userEvent.click(within(group).getByRole('button', { name: 'Driven' }));
    expect(on.onChange).toHaveBeenLastCalledWith({ repositories: ['engine', 'api'] });
    await userEvent.click(within(group).getByRole('button', { name: 'Not driven' }));
    expect(on.onChange).toHaveBeenLastCalledWith({ repositories: choices.slice(2).map((each) => each.repository) });
    await userEvent.click(within(group).getByRole('button', { name: 'All' }));
    expect(on.onChange).toHaveBeenLastCalledWith({ repositories: choices.map((each) => each.repository) });
    await userEvent.click(within(group).getByRole('button', { name: 'None' }));
    expect(on.onChange).toHaveBeenLastCalledWith({ repositories: [] });
    // A search narrows what All and the others reach.
    await userEvent.type(within(group).getByRole('searchbox', { name: 'Search repositories' }), 'archive-1');
    expect(within(group).queryByRole('checkbox', { name: 'engine' })).toBeNull();
    await userEvent.click(within(group).getByRole('button', { name: 'All' }));
    expect(on.onChange).toHaveBeenLastCalledWith({ repositories: ['archive-1'] });
  });

  it('says a plan the driver could not give', () => {
    show({ plan: null, problem: 'the driver is still coming up' });
    expect(screen.getByRole('alert')).toHaveTextContent('the driver is still coming up');
  });

  it('is named in Chinese', () => {
    i18n.changeLanguage('zh');
    show();
    expect(screen.getByRole('radiogroup', { name: '落地方式' })).toBeTruthy();
    expect(screen.getByRole('button', { name: '审阅计划' })).toBeTruthy();
  });
});
