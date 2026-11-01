import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// AGENTSPLACE1j (the Agents-place design §4): a place's quick set-up as a molecule. Seen failing first: the component did not exist.

import i18n from '../../locales/i18n';
import { APPLIED, MOVED, planOf } from './placeFixtures';
import { askOf, freshDraft, type SetUpDraft } from './placePlan';
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

describe('Set up…, a place\'s quick set-up', () => {
  beforeEach(() => { i18n.changeLanguage('en'); });
  afterEach(cleanup);

  it('keeps delivery, adoption and general driving apart, with one delivery chosen', () => {
    show();
    const delivery = screen.getByRole('radiogroup', { name: 'Delivery' });
    expect(within(delivery).getAllByRole('radio').map((each) => each.getAttribute('aria-labelledby') && document.getElementById(each.getAttribute('aria-labelledby')!)!.textContent)).toEqual([
      'Not driven', 'Merge after you accept', 'A branch to push', 'A pull request', 'Integrated into the line',
    ]);
    expect(within(delivery).getByRole('radio', { name: /Merge after you accept/ })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: 'Ask its session to set up for agents' })).not.toBeChecked();
    expect(screen.getByRole('checkbox', { name: 'Enable general driving' })).not.toBeChecked();
    // What it builds is the adoption's: it shows only while adoption is ticked.
    expect(screen.queryByRole('radiogroup', { name: 'What it builds' })).toBeNull();
  });

  it('marks the suggested delivery and sends the choice, a tick and what it builds', async () => {
    const on = show({ draft: draftOf({ adoption: true }) });
    expect(within(screen.getByRole('radio', { name: /Merge after you accept/ }).closest('label')!).getByText('Suggested')).toBeTruthy();
    await userEvent.click(screen.getByRole('radio', { name: /A branch to push/ }));
    expect(on.onChange).toHaveBeenLastCalledWith({ preset: 'branch-to-push' });
    await userEvent.click(screen.getByRole('checkbox', { name: 'Your look before it lands' }));
    expect(on.onChange).toHaveBeenLastCalledWith({ look: true });
    const builds = screen.getByRole('radiogroup', { name: 'What it builds' });
    expect(within(builds).getAllByRole('radio')).toHaveLength(5);
    await userEvent.click(within(builds).getByRole('radio', { name: /A web app or service/ }));
    expect(on.onChange).toHaveBeenLastCalledWith({ builds: 'service' });
    await userEvent.click(screen.getByRole('checkbox', { name: 'Enable general driving' }));
    expect(on.onChange).toHaveBeenLastCalledWith({ drive: true });
  });

  it('offers no second opinion, look or driving with Not driven', () => {
    show({ draft: draftOf({ preset: 'not-driven' }) });
    for (const name of ['A second opinion before it lands', 'Your look before it lands', 'Enable general driving']) {
      expect(screen.getByRole('checkbox', { name })).toBeDisabled();
    }
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

  it('says every refusal in place and offers no Apply for a plan that refuses', () => {
    const draft = draftOf({ repositories: ['site'], preset: 'pull-request' });
    show({ draft, plan: planOf(askOf(draft)) });
    const refusals = screen.getByRole('list', { name: 'It cannot be applied yet' });
    expect(within(refusals).getByText(/no landing plugin here opens a pull request there/)).toBeTruthy();
    expect(refusals.textContent).toMatch(/^site/);
    expect(screen.getByRole('button', { name: 'Apply' })).toBeDisabled();
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

  it('says a workspace\'s repositories as ticks, and what a plan the driver could not give is', async () => {
    const choices = [{ repository: 'engine', driven: true }, { repository: 'site', driven: false }];
    const on = show({ place: 'aurora', kind: 'workspace', choices, draft: draftOf({ repositories: ['engine'] }) });
    const group = screen.getByRole('group', { name: 'Repositories' });
    expect(within(group).getByRole('checkbox', { name: 'engine' })).toBeChecked();
    await userEvent.click(within(group).getByRole('checkbox', { name: 'site' }));
    expect(on.onChange).toHaveBeenLastCalledWith({ repositories: ['engine', 'site'] });
    cleanup();
    show({ plan: null, problem: 'the driver is still coming up' });
    expect(screen.getByRole('alert')).toHaveTextContent('the driver is still coming up');
  });

  it('is named in Chinese', () => {
    i18n.changeLanguage('zh');
    show();
    expect(screen.getByRole('radiogroup', { name: '交付' })).toBeTruthy();
    expect(screen.getByRole('button', { name: '应用' })).toBeTruthy();
  });
});
