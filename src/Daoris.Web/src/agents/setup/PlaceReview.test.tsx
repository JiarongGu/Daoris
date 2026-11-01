import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// SETUPUX1k (the setup-UX design §2.2, §3 flow 1, the reads' B1, B9, Codex 4, N13, N14, N19): *Set up…*'s review, a molecule: what changes, what
// stops it, what it starts and lowers, the work it releases, the acceptances, the terminal line, and one *Apply* that waits while a plan is read.

import i18n from '../../locales/i18n';
import { APPLIED, MOVED, planOf } from './placeFixtures';
import { askOf, freshDraft, type PlaceRepositoryPlan, type SetUpDraft } from './placePlan';
import { PlaceReview, type PlaceReviewProps } from './PlaceReview';

const draftOf = (patch: Partial<SetUpDraft> = {}): SetUpDraft => ({ ...freshDraft(['engine']), preset: 'merge-after-accept', adoption: false, review: true, ...patch });

const show = (props: Partial<PlaceReviewProps> & { mix?: Record<string, Partial<PlaceRepositoryPlan>> } = {}) => {
  const draft = props.draft ?? draftOf();
  const on = { onBack: vi.fn(), onLeaveOut: vi.fn(), onAccept: vi.fn(), onApply: vi.fn(), onHold: vi.fn(), onOpenQuest: vi.fn(), onClose: vi.fn() };
  render(
    <PlaceReview
      place="engine"
      kind="repository"
      plan={planOf(askOf(draft), props.mix)}
      draft={draft}
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

const headings = () => screen.getAllByRole('heading', { level: 3 }).map((each) => each.textContent);

describe('Set up…, the review', () => {
  beforeEach(() => { i18n.changeLanguage('en'); });
  afterEach(cleanup);

  it('reads in its order: what changes, what stops it, what it starts, your part, the work it releases, the acceptances, the terminal, then Apply', () => {
    const draft = draftOf({ repositories: ['engine', 'site'], preset: 'integrated', adoption: true, drive: true, opinion: true });
    show({ place: 'aurora', kind: 'workspace', draft });
    expect(headings()).toEqual(['What it changes', 'What stops it', 'What it starts', 'Your part', 'Driving releases this queued work', 'Acceptances']);
    expect(screen.getByText('In a terminal')).toBeTruthy();
    const apply = screen.getByRole('button', { name: 'Apply to 2 repositories' });
    // Apply is the last press, after the terminal line.
    expect(screen.getByText('In a terminal').compareDocumentPosition(apply) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('says each identical change once with the repositories it is made at, and the driver\'s reach and unread lines', () => {
    const draft = draftOf({ repositories: ['engine', 'site'], preset: 'branch-to-push', adoption: true, builds: 'service' });
    show({
      place: 'aurora',
      kind: 'workspace',
      draft,
      mix: {
        engine: { reach: [{ repository: 'engine', cannots: [], noGates: false, said: 'A change here reaches `api`, which has no gates.' }] },
        site: { unread: 'its remote did not answer.' },
      },
    });
    const changes = screen.getByRole('list', { name: 'What it changes' });
    expect(within(changes).getAllByText(/a set-up asked of its own session/)).toHaveLength(1);
    expect(changes.textContent).toContain('(engine, site)');
    expect(screen.getByText('A change here reaches', { exact: false })).toHaveTextContent('which has no gates');
    expect(screen.getByText(/site's line could not be read:/)).toHaveTextContent('its remote did not answer.');
  });

  it('says a plan that changes nothing, and what it starts: adoption once per repository and driving by the plan\'s sides', () => {
    show();
    expect(screen.getByText('Nothing to change: it is set so already.')).toBeTruthy();
    cleanup();
    const draft = draftOf({ repositories: ['engine', 'site'], preset: 'keep', adoption: true });
    show({ place: 'aurora', kind: 'workspace', draft });
    const starts = screen.getByRole('region', { name: 'What it starts' });
    expect(starts).toHaveTextContent('Adoption: 2 quests, one per repository (engine, site)');
    expect(starts).toHaveTextContent('engine: driven now, and after.');
    expect(starts).toHaveTextContent('site: not driven; only its adoption quest would run.');
  });

  it('lists what stops the plan whole, linked back to its part, with a change of selection and never a partial apply', async () => {
    const draft = draftOf({ repositories: ['engine', 'site'], preset: 'pull-request', adoption: true });
    const on = show({ place: 'aurora', kind: 'workspace', draft });
    const stops = screen.getByRole('region', { name: 'What stops it' });
    expect(within(stops).getByText(/no landing plugin here opens a pull request there/)).toBeTruthy();
    expect(within(stops).getByText(/has no checkout on this machine/)).toBeTruthy();
    await userEvent.click(within(stops).getAllByRole('button', { name: '↑ How it lands' })[0]!);
    expect(on.onBack).toHaveBeenLastCalledWith('delivery');
    await userEvent.click(within(stops).getByRole('button', { name: '↑ Adoption' }));
    expect(on.onBack).toHaveBeenLastCalledWith('adoption');
    await userEvent.click(within(stops).getAllByRole('button', { name: 'Leave site out' })[0]!);
    expect(on.onLeaveOut).toHaveBeenCalledWith('site');
    expect(screen.getByRole('button', { name: 'Apply to 2 repositories' })).toBeDisabled();
    expect(screen.getByText('Put right what is refused, and the plan reads again.')).toBeTruthy();
  });

  it('offers to leave nothing out of a one-repository plan', () => {
    show({ draft: draftOf({ repositories: ['site'], preset: 'pull-request' }) });
    expect(screen.queryByRole('button', { name: /Leave .* out/ })).toBeNull();
  });

  it('says what lowers the person\'s part in the driver\'s words', () => {
    show({ draft: draftOf({ preset: 'branch-to-push' }) });
    const part = screen.getByRole('region', { name: 'Your part' });
    expect(part).toHaveTextContent('your part in what lands: it lands after your accept now, and would not');
  });

  it('lists the work driving releases and holds it on a press, saying each repository\'s outcome beside it, partial holds included', async () => {
    const draft = draftOf({ repositories: ['engine', 'site'], drive: true });
    const on = show({
      place: 'aurora',
      kind: 'workspace',
      draft,
      mix: { site: { released: [{ quest: 'q-20', title: 'Fix the login' }] } },
      holds: { engine: { held: true }, site: { held: false, said: 'the driver refused the hold: site is already held.' } },
    });
    const released = screen.getByRole('list', { name: 'Driving releases this queued work' });
    expect(within(released).getAllByRole('listitem').map((each) => each.textContent)).toEqual(['Tidy the docs — engine', 'Add the retry — engine', 'Fix the login — site']);
    await userEvent.click(screen.getByRole('button', { name: 'Hold it after driving' }));
    expect(on.onHold).toHaveBeenCalledWith(['engine', 'site']);
    const outcomes = screen.getByRole('list', { name: 'What holding did' });
    expect(within(outcomes).getAllByRole('listitem').map((each) => each.textContent)).toEqual([
      'engine: held.',
      'site: not held. the driver refused the hold: site is already held.',
    ]);
  });

  it('draws the acceptances through the one molecule: unticked, the text folded once per read, each with its standing and consequence', async () => {
    const draft = draftOf({ preset: 'integrated' });
    const on = show({ draft });
    const region = screen.getByRole('region', { name: 'Acceptances' });
    expect(region).toHaveTextContent('engine: the declaration at main aa11bb2, blob 3b18e51');
    const fold = within(region).getByText('Read the declaration').closest('details')!;
    expect(fold).toHaveTextContent('gates: [lint, tests]');
    expect(within(region).getAllByText(/gates: \[lint, tests\]/)).toHaveLength(1);
    const tick = within(region).getByRole('checkbox', { name: 'Accept for integration' });
    expect(tick).not.toBeChecked();
    expect(region).toHaveTextContent('Not accepted yet.');
    expect(region).toHaveTextContent('Until accepted, none of it is used.');
    await userEvent.click(tick);
    expect(on.onAccept).toHaveBeenCalledWith([{ repository: 'engine', purpose: 'integration', line: 'main', blob: '3b18e512dba79e4c8300dd08aeb37f8e728b8dad' }]);
  });

  it('says what a changed landed declaration stops, and that integration goes on under its last accepted blob', () => {
    const changed = (purposes: ('landed' | 'integration')[]) => ({
      engine: {
        declaration: {
          line: 'main',
          commit: 'aa11bb2c3d4e',
          blob: 'ab12cd34ef56',
          text: 'gates: []\n',
          acceptances: purposes.map((purpose) => ({ purpose, standing: 'changed', said: 'The driver says so.' })),
        },
      },
    });
    show({ draft: draftOf({ preset: 'integrated' }), mix: changed(['landed', 'integration']) });
    const region = screen.getByRole('region', { name: 'Acceptances' });
    expect(region).toHaveTextContent('Changed since you accepted it. Changed landed gates stop verification until you accept them.');
    expect(region).toHaveTextContent('Changed since you accepted it. Integration goes on under its last accepted blob until you accept this one.');
  });

  it('says a tick the page cleared because the declaration changed, with its reason', () => {
    show({
      draft: draftOf({
        preset: 'integrated',
        cleared: [{ repository: 'engine', purpose: 'integration', line: 'main', blob: '3b18e512dba79e4c8300dd08aeb37f8e728b8dad' }],
      }),
    });
    expect(screen.getByRole('status')).toHaveTextContent('engine: the declaration changed to blob 3b18e51 at main since you chose Accept for integration, so the choice was cleared.');
  });

  it('keeps the last plan marked updating while a plan is read, and offers no Apply until it has been', () => {
    const draft = draftOf({ adoption: true });
    show({ draft, reading: true, stale: true });
    expect(screen.getByText('Updating…')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Apply to 1 repository' })).toBeDisabled();
    expect(screen.getByRole('list', { name: 'What it changes' })).toBeTruthy();
    cleanup();
    show({ draft });
    expect(screen.queryByText('Updating…')).toBeNull();
    expect(screen.getByRole('button', { name: 'Apply to 1 repository' })).toBeEnabled();
    cleanup();
    show({ draft, applying: true });
    expect(screen.getByRole('button', { name: 'Applying…' })).toBeDisabled();
  });

  it('presses Apply once, and goes back to the choices from its head', async () => {
    const on = show({ draft: draftOf({ adoption: true }) });
    await userEvent.click(screen.getByRole('button', { name: 'Apply to 1 repository' }));
    expect(on.onApply).toHaveBeenCalledTimes(1);
    await userEvent.click(screen.getByRole('button', { name: '← Back to the choices' }));
    expect(on.onBack).toHaveBeenCalledWith();
  });

  it('is worked by keyboard alone: Back, a tick and Apply by Enter or the space bar', async () => {
    const on = show({ draft: draftOf({ preset: 'integrated', adoption: true }) });
    screen.getByRole('button', { name: '← Back to the choices' }).focus();
    await userEvent.keyboard('{Enter}');
    expect(on.onBack).toHaveBeenCalledTimes(1);
    screen.getByRole('checkbox', { name: 'Accept for integration' }).focus();
    await userEvent.keyboard(' ');
    expect(on.onAccept).toHaveBeenCalledTimes(1);
    screen.getByRole('button', { name: 'Apply to 1 repository' }).focus();
    await userEvent.keyboard('{Enter}');
    expect(on.onApply).toHaveBeenCalledTimes(1);
  });

  it('says a moved plan in the driver\'s own sentence and nothing of the page\'s', () => {
    show({ draft: draftOf({ result: MOVED }) });
    expect(screen.getByRole('alert')).toHaveTextContent('the plan you read no longer holds: a value, a declaration or the line moved since, so nothing was written.');
    expect(screen.queryByText(/The plan moved while you read it/)).toBeNull();
  });

  it('says what an apply came to as itself, where each repository ended, and opens the adoption quest by a door', async () => {
    const draft = draftOf({ result: APPLIED, ended: [{ state: 'adoptionOnly', repositories: ['engine'] }] });
    const on = show({ draft });
    const done = screen.getByRole('status');
    expect(done).toHaveTextContent('configuration applied; adoption pending');
    expect(done).toHaveTextContent('the set-up quest asked of engine');
    expect(done).toHaveTextContent('engine: Not driven · adoption running');
    await userEvent.click(within(done).getByRole('button', { name: "Open engine's adoption quest" }));
    expect(on.onOpenQuest).toHaveBeenCalledWith('q-77');
  });

  it('ends Driven where driving was chosen, and Not driven where there was no adoption', () => {
    show({ draft: draftOf({ result: APPLIED, ended: [{ state: 'starts', repositories: ['engine'] }, { state: 'notDriven', repositories: ['site'] }] }) });
    const done = screen.getByRole('status');
    expect(done).toHaveTextContent('engine: Driven');
    expect(done).toHaveTextContent('site: Not driven');
    expect(done).not.toHaveTextContent('adoption running');
  });

  it('says a plan the driver could not give', () => {
    show({ plan: null, problem: 'the driver is still coming up' });
    expect(screen.getByRole('alert')).toHaveTextContent('the driver is still coming up');
    expect(screen.getByRole('button', { name: 'Apply to 1 repository' })).toBeDisabled();
  });

  it('says its words in Chinese', () => {
    i18n.changeLanguage('zh');
    show({ draft: draftOf({ adoption: true }) });
    expect(screen.getByRole('button', { name: '← 返回选项' })).toBeTruthy();
    expect(screen.getByRole('button', { name: '应用到 1 个仓库' })).toBeTruthy();
  });
});
