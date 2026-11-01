import { useEffect, useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// AGENTSPLACE1j (the Agents-place design §4, §5's first flows): *Set up…* on a place's page reads one plan from the driver, says every
// refusal in place, and presses Apply as the person's own with the plan's digest and the acceptances chosen. Seen failing first.
// SETUPUX1j (the setup-UX design §2.2): Keep how it lands is offered and sent, mixed suggestions preselect nothing, and a refusal's door
// repairs its cause and returns, the plan read again.
// SETUPUX1k (the setup-UX design §2.2, §3 flow 1): the choices end at *Review the plan*; the review applies, says what holding did per repository,
// clears a tick whose declaration changed, waits while a plan is read, says `moved` in the driver's words, ends flow 1 as *Not driven · adoption
// running*, opens the adoption quest by a door, and is where a seed from Ask Daoris opens.

const { invoke, notifyReady } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
}));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
  useShenoraEvent: () => {},
}));

import i18n from '../../locales/i18n';
import { DRIVER_STATE, REGISTRY, show, WIRING } from '../../test/shellHarness';
import { useListPanes } from '../../work/frame/listPanes';
import { ViewFrame } from '../../work/frame/ViewFrame';
import { useAgentsMode, useAgentsView } from '../AgentsView';
import { APPLIED, KEPT_ROW, MOVED, planOf, SETUP_PRESETS } from './placeFixtures';
import type { PlaceApplied, PlaceAsk, PlaceRepositoryPlan } from './placePlan';
import { PlaceSetUpPanel } from './PlaceSetUpPanel';
import { PLACES } from './setupFixtures';

/** The quests the review's door opened. */
const quests: string[] = [];

/** The row a door asked for at a page's Workflow section, as the body the application hands in sees it. */
const asked: (string | null)[] = [];
function WorkflowBody({ at }: { at: string | null }) {
  useEffect(() => { asked.push(at); }, [at]);
  return <p data-place-row="landing" tabIndex={-1}>The landing rows</p>;
}

function Place() {
  const lists = useListPanes();
  const modes = useAgentsMode(lists);
  const [chosen, setChosen] = useState<string | null>(null);
  const [over, setOver] = useState(false);
  const layout = useAgentsView({
    active: true, modes, chosen, onChoose: setChosen, notify: () => {},
    bodies: (place) => ({ workflow: <WorkflowBody at={place.asked ?? null} />, sessions: <p>Sessions body</p> }),
    setUp: (place, page, doorFor) => <PlaceSetUpPanel place={place} page={page} notify={() => {}} doorFor={doorFor} onOpenQuest={(quest) => quests.push(quest)} />,
  });
  return <ViewFrame layout={layout} lists={lists} over={over} onOver={setOver} />;
}

/** The application's side of a seeded door: choose the place and hand over the ask, as `App`'s opener does. */
function SeededPlace({ ask, place }: { ask: PlaceAsk; place: string }) {
  const lists = useListPanes();
  const modes = useAgentsMode(lists);
  const [chosen, setChosen] = useState<string | null>(null);
  const [over, setOver] = useState(false);
  const layout = useAgentsView({
    active: true, modes, chosen, onChoose: setChosen, notify: () => {},
    setUp: (found, page, doorFor) => <PlaceSetUpPanel place={found} page={page} notify={() => {}} doorFor={doorFor} />,
  });
  return (
    <>
      <button type="button" onClick={() => { modes.onPlace(place); modes.onSeed(place, ask); }}>Seed</button>
      <ViewFrame layout={layout} lists={lists} over={over} onOver={setOver} />
    </>
  );
}

const rows = [
  { ...REGISTRY[0]!, repository: 'engine', workspace: 'aurora' },
  { ...REGISTRY[0]!, repository: 'site', workspace: 'aurora' },
];

const sent = (route: string) => invoke.mock.calls.filter(([, type]) => type === route).map(([, , options]) => (options as { payload: Record<string, unknown> }).payload);

describe('Set up…, on a place\'s page', () => {
  let apply: PlaceApplied = APPLIED;
  let mix: Record<string, Partial<PlaceRepositoryPlan>> = {};
  /** Whether the reviewers have been named since: the plan then refuses no second opinion. */
  let named = false;
  /** The blob the engine's declaration reads as now, where a test moves it. */
  let blob: string | null = null;
  /** A repository whose hold the driver refuses, with its sentence. */
  let refuseHold: { repository: string; said: string } | null = null;
  /** The plans read while it is set wait for it. */
  let gate: Promise<void> | null = null;

  beforeEach(() => {
    apply = APPLIED;
    mix = {};
    named = false;
    blob = null;
    refuseHold = null;
    gate = null;
    quests.length = 0;
    asked.length = 0;
    window.localStorage.clear();
    window.localStorage.setItem('daoris.agents.setup', 'on');
    window.localStorage.setItem('daoris.list.agents.mode', 'setup');
    i18n.changeLanguage('en');
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/registry')) return Response.json(rows);
      return Response.json([]);
    }));
    invoke.mockImplementation(async (module: string, type: string, options?: { payload?: Record<string, unknown> }) => {
      if (module !== 'DAORIS.DRIVER') return WIRING;
      if (type === 'SET_HOLD' && refuseHold && options?.payload?.repository === refuseHold.repository) throw new Error(refuseHold.said);
      if (type === 'STATE' || type === 'SET_HOLD') return DRIVER_STATE;
      if (type === 'HARNESSES') return { settingsPath: 'x', adapter: 'claude-code', harnesses: [] };
      if (type === 'PLACES') return PLACES(options?.payload ?? {});
      if (type === 'WORKFLOW_LIST') return { workflows: [], presets: SETUP_PRESETS };
      if (type === 'PLACE_PLAN') {
        if (gate) await gate;
        const plan = planOf(options!.payload as unknown as PlaceAsk, mix);
        const refusals = named ? plan.refusals.filter((each) => each.code !== 'no-reviewers') : plan.refusals;
        const moved = blob === null
          ? plan.repositories
          : plan.repositories.map((row) => (row.declaration ? { ...row, declaration: { ...row.declaration, blob } } : row));
        return { ...plan, repositories: moved, refusals, applies: refusals.length === 0 };
      }
      if (type === 'PLACE_APPLY') return apply;
      return undefined;
    });
  });
  afterEach(() => {
    cleanup();
    window.localStorage.clear();
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  const open = async (place: RegExp) => {
    await userEvent.click(await screen.findByRole('button', { name: place }));
    await userEvent.click(await screen.findByRole('button', { name: 'Set up…' }));
  };

  /** From the choices to the review, once the plan for them has been read. */
  const review = async () => {
    const button = await screen.findByRole('button', { name: 'Review the plan' });
    await waitFor(() => expect(button).toBeEnabled());
    await userEvent.click(button);
    return screen.findByRole('heading', { name: /^Review: set up / });
  };
  const applyButton = (repositories = 1) => screen.getByRole('button', { name: new RegExp(`^Apply to ${repositories} repositor`) });

  it('opens from the head of a repository, reads the plan as not driven, and preselects the suggestion', async () => {
    show(<Place />);
    await open(/^engine/);
    expect(await screen.findByRole('radio', { name: /Merge after you accept/ })).toBeChecked();
    // The first read asks nothing and learns the suggestion; the next is the plan the person reads.
    expect(sent('PLACE_PLAN')[0]).toMatchObject({ repositories: ['engine'], preset: 'not-driven', adoption: false });
    await waitFor(() => expect(sent('PLACE_PLAN').at(-1)).toEqual({
      repositories: ['engine'], preset: 'merge-after-accept', opinion: false, look: false, adoption: false, drive: false,
    }));
    expect(screen.getByRole('heading', { name: 'Set up engine' })).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Set up…' })).toBeNull();
  });

  it('offers no Set up… on This machine', async () => {
    show(<Place />);
    await userEvent.click(await screen.findByRole('button', { name: /This machine/ }));
    await screen.findByRole('heading', { level: 1, name: 'This machine' });
    expect(screen.queryByRole('button', { name: 'Set up…' })).toBeNull();
  });

  it('opens on a repository that is imported and not driven', async () => {
    show(<Place />);
    await userEvent.click(await screen.findByRole('button', { name: /41 imported, not driven/ }));
    await open(/archive-1/);
    expect(await screen.findByRole('heading', { name: 'Set up archive-1' })).toBeTruthy();
    await waitFor(() => expect(sent('PLACE_PLAN').at(-1)).toMatchObject({ repositories: ['archive-1'] }));
  });

  it('says every refusal in place and offers no Apply for a plan that refuses', async () => {
    show(<Place />);
    await open(/^site/);
    const part = (title: string) => screen.getByRole('heading', { name: title }).closest('section')!;
    const refusals = await within(part('How it lands')).findByRole('list', { name: 'It cannot be applied yet' });
    expect(within(refusals).getByText(/no landing plugin here opens a pull request there/)).toBeTruthy();
    // The adoption suggested for it is refused for its own cause, under its own part.
    expect(within(part('Adoption')).getByText(/has no checkout on this machine/)).toBeTruthy();
    // The review lists what stops it and offers no Apply for it.
    await review();
    expect(within(screen.getByRole('region', { name: 'What stops it' })).getByText(/no landing plugin here opens a pull request there/)).toBeTruthy();
    expect(applyButton()).toBeDisabled();
    // Back to the choices: another delivery reads the plan again, and that refusal goes with it; leaving adoption out takes the other.
    await userEvent.click(screen.getByRole('button', { name: '← Back to the choices' }));
    await userEvent.click(screen.getByRole('radio', { name: /A branch to push/ }));
    await waitFor(() => expect(within(part('How it lands')).queryByRole('list', { name: 'It cannot be applied yet' })).toBeNull());
    expect(within(part('Adoption')).getByText(/has no checkout on this machine/)).toBeTruthy();
    await userEvent.click(screen.getByRole('checkbox', { name: 'Ask its session to adopt Daoris' }));
    await waitFor(() => expect(screen.queryByRole('list', { name: 'It cannot be applied yet' })).toBeNull());
    await review();
    await waitFor(() => expect(applyButton()).toBeEnabled());
  });

  it('sends Apply with the plan\'s digest and the acceptances chosen, and says what it came to', async () => {
    show(<Place />);
    await open(/^engine/);
    await userEvent.click(await screen.findByRole('radio', { name: /Integrated into the line/ }));
    await review();
    const tick = await screen.findByRole('checkbox', { name: /Accept for integration/ });
    expect(tick).not.toBeChecked();
    await waitFor(() => expect(applyButton()).toBeEnabled());
    await userEvent.click(tick);
    await userEvent.click(applyButton());
    const ask: PlaceAsk = { repositories: ['engine'], preset: 'integrated', opinion: false, look: false, adoption: false, drive: false };
    await waitFor(() => expect(sent('PLACE_APPLY')).toEqual([{ ...ask, digest: planOf(ask).digest, accept: [{ repository: 'engine', purpose: 'integration' }] }]));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'PLACE_APPLY', expect.objectContaining({ timeoutMs: expect.any(Number) }));
    expect(await screen.findByRole('status')).toHaveTextContent('configuration applied; adoption pending');
  });

  it('sends no acceptance the person did not choose', async () => {
    show(<Place />);
    await open(/^engine/);
    await userEvent.click(await screen.findByRole('radio', { name: /Integrated into the line/ }));
    await review();
    await screen.findByRole('checkbox', { name: /Accept for integration/ });
    await waitFor(() => expect(applyButton()).toBeEnabled());
    await userEvent.click(applyButton());
    await waitFor(() => expect(sent('PLACE_APPLY')[0]).toMatchObject({ accept: [] }));
  });

  it('clears a tick whose declaration changed since, saying so, and sends no acceptance for it', async () => {
    show(<Place />);
    await open(/^engine/);
    await userEvent.click(await screen.findByRole('radio', { name: /Integrated into the line/ }));
    await review();
    await userEvent.click(await screen.findByRole('checkbox', { name: /Accept for integration/ }));
    expect(screen.getByRole('checkbox', { name: /Accept for integration/ })).toBeChecked();
    // The blob moves while the person reads; the next plan read is of another ask, and the tick it was made at no longer holds.
    blob = 'ffffffffffffffffffffffffffffffffffffffff';
    await userEvent.click(screen.getByRole('button', { name: '← Back to the choices' }));
    await userEvent.click(screen.getByRole('checkbox', { name: 'Your look before it lands' }));
    await review();
    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('the declaration changed to blob fffffff at main since you ticked Accept for integration, so the tick was cleared.'));
    expect(screen.getByRole('checkbox', { name: /Accept for integration/ })).not.toBeChecked();
    await waitFor(() => expect(applyButton()).toBeEnabled());
    await userEvent.click(applyButton());
    await waitFor(() => expect(sent('PLACE_APPLY')[0]).toMatchObject({ accept: [] }));
  });

  it('says a plan that moved in the driver\'s own words, and reads it again', async () => {
    apply = MOVED;
    show(<Place />);
    await open(/^engine/);
    await userEvent.click(await screen.findByRole('radio', { name: /Integrated into the line/ }));
    await review();
    await waitFor(() => expect(applyButton()).toBeEnabled());
    const before = sent('PLACE_PLAN').length;
    await userEvent.click(applyButton());
    expect(await screen.findByRole('alert')).toHaveTextContent('the plan you read no longer holds');
    expect(screen.queryByText(/The plan moved while you read it/)).toBeNull();
    await waitFor(() => expect(sent('PLACE_PLAN').length).toBeGreaterThan(before));
  });

  it('offers no Apply while the plan is read again, the last plan marked updating', async () => {
    show(<Place />);
    await open(/^engine/);
    await userEvent.click(await screen.findByRole('radio', { name: /A branch to push/ }));
    await userEvent.click(screen.getByRole('checkbox', { name: 'Drive all its work' }));
    await review();
    await waitFor(() => expect(applyButton()).toBeEnabled());
    let release = () => {};
    gate = new Promise<void>((resolve) => { release = resolve; });
    // Holding reads the plan again; the last plan stays, marked, and Apply waits.
    await userEvent.click(await screen.findByRole('button', { name: 'Hold it after driving' }));
    expect(await screen.findByText('Updating…')).toBeTruthy();
    expect(applyButton()).toBeDisabled();
    expect(screen.getByRole('list', { name: 'What it changes' })).toBeTruthy();
    release();
    await waitFor(() => expect(screen.queryByText('Updating…')).toBeNull());
    await waitFor(() => expect(applyButton()).toBeEnabled());
  });

  it('holds a repository driving would release work from, then reads the plan again', async () => {
    show(<Place />);
    await open(/^engine/);
    await userEvent.click(await screen.findByRole('checkbox', { name: 'Drive all its work' }));
    await review();
    const released = await screen.findByRole('list', { name: 'Driving releases this queued work' });
    expect(within(released).getByText('Tidy the docs')).toBeTruthy();
    const before = sent('PLACE_PLAN').length;
    await userEvent.click(screen.getByRole('button', { name: 'Hold it after driving' }));
    await waitFor(() => expect(sent('SET_HOLD')).toEqual([{ repository: 'engine', held: true }]));
    await waitFor(() => expect(sent('PLACE_PLAN').length).toBeGreaterThan(before));
    expect(await screen.findByRole('list', { name: 'What holding did' })).toHaveTextContent('engine: held.');
  });

  it('says a partial hold per repository: the ones held, and the driver\'s sentence for the one it could not hold', async () => {
    mix = { site: { released: [{ quest: 'q-20', title: 'Fix the login' }] } };
    refuseHold = { repository: 'site', said: 'site is already held by the steward.' };
    show(<Place />);
    await open(/aurora\s*2 driven/);
    await userEvent.click(await screen.findByRole('radio', { name: /A branch to push/ }));
    await userEvent.click(await screen.findByRole('checkbox', { name: 'Drive all its work' }));
    await review();
    await userEvent.click(await screen.findByRole('button', { name: 'Hold it after driving' }));
    const outcomes = await screen.findByRole('list', { name: 'What holding did' });
    await waitFor(() => expect(within(outcomes).getAllByRole('listitem').map((each) => each.textContent)).toEqual([
      'engine: held.',
      'site: not held. site is already held by the steward.',
    ]));
    // Both were tried: the failure of one stopped nothing.
    expect(sent('SET_HOLD')).toEqual([{ repository: 'engine', held: true }, { repository: 'site', held: true }]);
  });

  /** An imported repository, not driven: the suggestion is *Not driven* with its adoption asked. */
  const openImported = async () => {
    apply = { ...APPLIED, operations: [{ id: 'quest:archive-1', kind: 'quest', repository: 'archive-1', said: 'the set-up quest asked of `archive-1`', outcome: 'done', quest: 'q-77' }] };
    show(<Place />);
    await userEvent.click(await screen.findByRole('button', { name: /41 imported, not driven/ }));
    await open(/archive-1/);
  };

  it('ends flow 1 as Not driven · adoption running, and opens the adoption quest by a door', async () => {
    await openImported();
    await waitFor(() => expect(screen.getByRole('radio', { name: 'Not driven' })).toBeChecked());
    await waitFor(() => expect(screen.getByRole('checkbox', { name: 'Ask its session to adopt Daoris' })).toBeChecked());
    await review();
    await waitFor(() => expect(applyButton()).toBeEnabled());
    await userEvent.click(applyButton());
    const done = await screen.findByRole('status');
    expect(done).toHaveTextContent('configuration applied; adoption pending');
    expect(within(done).getByRole('list', { name: 'Where each stands now' })).toHaveTextContent('archive-1: Not driven · adoption running');
    await userEvent.click(within(done).getByRole('button', { name: "Open archive-1's adoption quest" }));
    expect(quests).toEqual(['q-77']);
  });

  it('ends Driven where general driving was chosen', async () => {
    await openImported();
    await userEvent.click(await screen.findByRole('radio', { name: /A branch to push/ }));
    await userEvent.click(screen.getByRole('checkbox', { name: 'Drive all its work' }));
    await review();
    await waitFor(() => expect(applyButton()).toBeEnabled());
    await userEvent.click(applyButton());
    const done = await screen.findByRole('status');
    expect(within(done).getByRole('list', { name: 'Where each stands now' })).toHaveTextContent('archive-1: Driven');
  });

  it('covers a workspace\'s repositories with one plan, each change said once', async () => {
    show(<Place />);
    await open(/aurora\s*2 driven/);
    const group = await screen.findByRole('group', { name: 'Repositories' });
    expect(within(group).getByRole('checkbox', { name: 'engine' })).toBeChecked();
    expect(within(group).getByRole('checkbox', { name: 'site' })).toBeChecked();
    // Their suggestions differ, so one is chosen before the plan is read.
    await userEvent.click(await screen.findByRole('radio', { name: /A branch to push/ }));
    await userEvent.click(await screen.findByRole('checkbox', { name: 'Ask its session to adopt Daoris' }));
    await waitFor(() => expect(sent('PLACE_PLAN').at(-1)).toMatchObject({ repositories: ['engine', 'site'], adoption: true }));
    await review();
    const changes = await screen.findByRole('list', { name: 'What it changes' });
    const asked = await within(changes).findAllByText(/a set-up asked of its own session/);
    expect(asked).toHaveLength(1);
    expect(asked[0]!.closest('li')).toHaveTextContent('(engine, site)');
  });

  // AGENTSPLACE1k, SETUPUX1k: Ask Daoris's place card hands the application a seeded set-up; the page opens on its review, and the plan it reads is that ask's.
  it('opens seeded from an ask Ask Daoris proposed on its review, and plans that ask rather than the suggestion', async () => {
    const ask: PlaceAsk = { repositories: ['engine'], preset: 'pull-request', opinion: true, look: false, adoption: true, builds: 'service', drive: false };
    show(<SeededPlace ask={ask} place="repo:aurora/engine" />);
    await userEvent.click(await screen.findByRole('button', { name: 'Seed' }));

    expect(await screen.findByRole('heading', { name: 'Review: set up engine' })).toBeTruthy();
    expect(screen.queryByRole('radiogroup', { name: 'How it lands' })).toBeNull();
    await waitFor(() => expect(sent('PLACE_PLAN').at(-1)).toEqual(ask));
    // Nothing was pressed: the seed plans only, and Apply stays the person's.
    expect(sent('PLACE_APPLY')).toEqual([]);
    // Back to the choices finds the ask as seeded.
    await userEvent.click(screen.getByRole('button', { name: '← Back to the choices' }));
    expect(await screen.findByRole('radio', { name: /A pull request/ })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: 'Ask its session to adopt Daoris' })).toBeChecked();
  });

  it('is found as left after a door out and back', async () => {
    show(<Place />);
    await open(/^engine/);
    await userEvent.click(await screen.findByRole('radio', { name: /A branch to push/ }));
    await userEvent.click(screen.getByRole('button', { name: /^site/ }));
    expect(await screen.findByRole('button', { name: 'Set up…' })).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: /^engine/ }));
    expect(await screen.findByRole('radio', { name: /A branch to push/ })).toBeChecked();
  });

  it('preselects Keep how it lands where the landing was chosen, and offers and sends a keep plan with driving and a tick', async () => {
    mix = { engine: KEPT_ROW };
    show(<Place />);
    await open(/^engine/);
    const keep = await screen.findByRole('radio', { name: 'Keep how it lands' });
    await waitFor(() => expect(keep).toBeChecked());
    await waitFor(() => expect(sent('PLACE_PLAN').at(-1)).toEqual({ repositories: ['engine'], preset: 'keep', opinion: false, look: false, adoption: false, drive: false }));
    await userEvent.click(screen.getByRole('checkbox', { name: 'Your look before it lands' }));
    await userEvent.click(screen.getByRole('checkbox', { name: 'Ask its session to adopt Daoris' }));
    await waitFor(() => expect(sent('PLACE_PLAN').at(-1)).toMatchObject({ preset: 'keep', look: true, adoption: true }));
    await review();
    await waitFor(() => expect(applyButton()).toBeEnabled());
    await userEvent.click(applyButton());
    const ask: PlaceAsk = { repositories: ['engine'], preset: 'keep', opinion: false, look: true, adoption: true, drive: false };
    await waitFor(() => expect(sent('PLACE_APPLY')).toEqual([{ ...ask, digest: planOf(ask, mix).digest, accept: [] }]));
  });

  it('preselects nothing where a workspace\'s repositories suggest different deliveries, and reads the plan once one is chosen', async () => {
    show(<Place />);
    await open(/aurora\s*2 driven/);
    await screen.findByText(/What suits these repositories differs/);
    const lands = screen.getByRole('radiogroup', { name: 'How it lands' });
    expect(within(lands).queryAllByRole('radio').filter((each) => (each as HTMLInputElement).checked)).toEqual([]);
    expect(within(screen.getByRole('radio', { name: /A pull request/ }).closest('label')!).getByText('Suits site')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Review the plan' })).toBeDisabled();
    // Nothing but the read of the choices has been asked of the driver.
    expect(sent('PLACE_PLAN').every((one) => one.preset === 'not-driven')).toBe(true);
    await userEvent.click(screen.getByRole('radio', { name: /A branch to push/ }));
    await waitFor(() => expect(sent('PLACE_PLAN').at(-1)).toMatchObject({ repositories: ['engine', 'site'], preset: 'branch-to-push' }));
    await review();
    expect(await screen.findByRole('list', { name: 'What it changes' })).toBeTruthy();
  });

  it('chooses a workspace\'s repositories from its whole list, with the driving each has', async () => {
    show(<Place />);
    await open(/aurora\s*2 driven/);
    const group = await screen.findByRole('group', { name: 'Repositories' });
    expect(await within(group).findByText('2 of 43 chosen')).toBeTruthy();
    expect(within(group).getByRole('checkbox', { name: 'archive-1 · Not driven' })).not.toBeChecked();
    await userEvent.click(within(group).getByRole('button', { name: 'None' }));
    expect(await screen.findByText('Choose at least one repository.')).toBeTruthy();
    await userEvent.click(within(group).getByRole('button', { name: 'Driven' }));
    await waitFor(() => expect(within(group).getByText('2 of 43 chosen')).toBeTruthy());
  });

  it('repairs a refusal at its door and returns: the draft as left, the plan read again, the refusal gone', async () => {
    show(<Place />);
    await open(/^site/);
    await userEvent.click(await screen.findByRole('checkbox', { name: 'A second opinion before it lands' }));
    const door = await screen.findByRole('button', { name: 'Name them: Workflow › Second opinion' });
    expect(within(screen.getByRole('heading', { name: 'How it lands' }).closest('section')!).getByText(/names the agents that read/)).toBeTruthy();
    expect(screen.queryByRole('button', { name: /^Apply/ })).toBeNull();

    // The door leaves the set-up, opens Workflow at the landing rows and keeps the way back.
    await userEvent.click(door);
    expect(await screen.findByRole('heading', { level: 1, name: 'site' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Workflow' })).toHaveAttribute('aria-expanded', 'true');
    expect(await screen.findByText('The landing rows')).toBeTruthy();
    expect(asked).toContain('landing');
    expect(screen.queryByRole('radiogroup', { name: 'How it lands' })).toBeNull();

    // The reviewers are named there; the way back finds the draft as left and reads the plan again.
    named = true;
    const before = sent('PLACE_PLAN').length;
    await userEvent.click(screen.getByRole('button', { name: '← Back to site · Set up…' }));
    expect(await screen.findByRole('heading', { name: 'Set up site' })).toBeTruthy();
    expect(screen.getByRole('checkbox', { name: 'A second opinion before it lands' })).toBeChecked();
    expect(screen.getByRole('radio', { name: /A pull request/ })).toBeChecked();
    await waitFor(() => expect(sent('PLACE_PLAN').length).toBeGreaterThan(before));
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Name them: Workflow › Second opinion' })).toBeNull());
    expect(screen.queryByRole('button', { name: /← Back/ })).toBeNull();
  });

  it('opens a repository\'s page from a workspace\'s refusal, and returns to the workspace\'s set-up', async () => {
    show(<Place />);
    await open(/aurora\s*2 driven/);
    await userEvent.click(await screen.findByRole('radio', { name: /A branch to push/ }));
    await userEvent.click(screen.getByRole('checkbox', { name: 'A second opinion before it lands' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Name them: Workflow › Second opinion' }));
    expect(await screen.findByRole('heading', { level: 1, name: 'site' })).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: '← Back to aurora · Set up…' }));
    expect(await screen.findByRole('heading', { name: 'Set up aurora' })).toBeTruthy();
    expect(screen.getByRole('checkbox', { name: 'A second opinion before it lands' })).toBeChecked();
  });
});
