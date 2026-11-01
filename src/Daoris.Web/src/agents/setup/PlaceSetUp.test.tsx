import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// AGENTSPLACE1j (the Agents-place design §4) and SETUPUX1j (the setup-UX design §2.2): a place's quick set-up as a molecule, its choices page.

import i18n from '../../locales/i18n';
import { APPLIED, KEPT_ROW, MOVED, planOf, SETUP_PRESETS } from './placeFixtures';
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
      {...on}
      {...props}
    />,
  );
  return on;
};

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

  it('offers Keep how it lands as a choice that sends no delivery, and leaves the ticks and driving open under it', async () => {
    const on = show({ draft: draftOf({ preset: 'keep' }) });
    const keep = screen.getByRole('radio', { name: 'Keep how it lands' });
    expect(keep).toBeChecked();
    for (const name of ['A second opinion before it lands', 'Your look before it lands', 'Drive all its work']) {
      expect(screen.getByRole('checkbox', { name })).toBeEnabled();
    }
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
    expect(screen.getByRole('button', { name: 'Apply' })).toBeDisabled();
  });

  it('offers Not driven from its own words, with no second opinion, look or driving', () => {
    show({ draft: draftOf({ preset: 'not-driven' }) });
    expect(screen.getByRole('radio', { name: 'Not driven' })).toBeChecked();
    expect(screen.getByText('Daoris starts no work here.')).toBeTruthy();
    for (const name of ['A second opinion before it lands', 'Your look before it lands', 'Drive all its work']) {
      expect(screen.getByRole('checkbox', { name })).toBeDisabled();
    }
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

  it('reads the plan: the changes said once for the repositories they are made at, what it lowers, and the terminal twin', () => {
    const draft = draftOf({ repositories: ['engine', 'site'], preset: 'branch-to-push', adoption: true, builds: 'service' });
    show({ place: 'aurora', kind: 'workspace', draft, plan: planOf(askOf(draft)) });
    const plan = screen.getByRole('region', { name: 'The plan' });
    const changes = within(plan).getByRole('list', { name: 'What it changes' });
    // The adoption is asked of both repositories: said once, at both.
    const asked = within(changes).getAllByRole('listitem').find((each) => /set-up asked of its own session/.test(each.textContent ?? ''))!;
    expect(asked.textContent).toContain('asked, for service');
    expect(asked.textContent).toContain('engine, site');
    expect(within(changes).getAllByText(/a set-up asked of its own session/)).toHaveLength(1);
    expect(within(plan).getAllByText(/your part in what lands/).length).toBeGreaterThan(0);
    expect(within(plan).getByText(/daoris-driver place engine site --preset branch-to-push/)).toBeTruthy();
  });

  it('counts the plan\'s changes and what stops it in one line, with a jump to each part that refuses', async () => {
    const draft = draftOf({ repositories: ['site'], preset: 'pull-request', adoption: true });
    show({ draft, plan: planOf(askOf(draft)) });
    const plan = screen.getByRole('region', { name: 'The plan' });
    expect(within(plan).getByText('2 changes · 2 stop it')).toBeTruthy();
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
    expect(screen.getByRole('button', { name: 'Apply' })).toBeDisabled();
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

  it('shows each declaration once, offers its acceptances unchosen, and sends the ones the person ticks', async () => {
    const draft = draftOf({ preset: 'integrated' });
    const on = show({ draft, plan: planOf(askOf(draft)) });
    const declaration = screen.getByRole('group', { name: /^The declaration of engine/ });
    expect(within(declaration).getAllByText(/gates: \[lint, tests\]/)).toHaveLength(1);
    const tick = within(declaration).getByRole('checkbox', { name: /Accept for integration/ });
    expect(tick).not.toBeChecked();
    await userEvent.click(tick);
    expect(on.onChange).toHaveBeenLastCalledWith({ accept: [{ repository: 'engine', purpose: 'integration' }] });
  });

  it('presses Apply once the plan applies, and not while it is read again or applied', async () => {
    // Adoption is a change the plan makes; a plan that changes nothing offers none.
    const draft = draftOf({ adoption: true });
    const on = show({ draft, plan: planOf(askOf(draft)) });
    await userEvent.click(screen.getByRole('button', { name: 'Apply' }));
    expect(on.onApply).toHaveBeenCalledTimes(1);
    cleanup();
    show({ draft, plan: planOf(askOf(draft)), reading: true, stale: true });
    expect(screen.getByRole('button', { name: 'Apply' })).toBeDisabled();
    cleanup();
    show({ draft, plan: planOf(askOf(draft)), applying: true });
    expect(screen.getByRole('button', { name: 'Applying…' })).toBeDisabled();
    cleanup();
    show();
    expect(screen.getByRole('button', { name: 'Apply' })).toBeDisabled();
    expect(screen.getByText('Nothing to change: it is set so already.')).toBeTruthy();
  });

  it('lists the queued work driving releases by title, and offers to hold it after driving', async () => {
    const draft = draftOf({ drive: true });
    const on = show({ draft, plan: planOf(askOf(draft)) });
    const released = screen.getByRole('list', { name: 'Driving releases this queued work' });
    expect(within(released).getAllByRole('listitem').map((each) => each.textContent)).toEqual(['Tidy the docs', 'Add the retry']);
    await userEvent.click(screen.getByRole('button', { name: 'Hold it after driving' }));
    expect(on.onHold).toHaveBeenCalledWith(['engine']);
  });

  it('says a plan that moved in place, and what an apply came to', () => {
    show({ draft: draftOf({ result: MOVED }) });
    expect(screen.getByRole('alert')).toHaveTextContent('The plan moved while you read it');
    cleanup();
    show({ draft: draftOf({ result: APPLIED }) });
    const done = screen.getByRole('status');
    expect(done).toHaveTextContent('configuration applied; adoption pending');
    expect(done).toHaveTextContent('the set-up quest asked of engine');
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
    expect(screen.getByRole('button', { name: '应用' })).toBeTruthy();
  });
});
