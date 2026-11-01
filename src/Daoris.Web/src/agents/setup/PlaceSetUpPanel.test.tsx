import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// AGENTSPLACE1j (the Agents-place design §4, §5's first flows): *Set up…* on a place's page reads one plan from the driver, says every
// refusal in place, and presses Apply as the person's own with the plan's digest and the acceptances chosen. Seen failing first.

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
import { APPLIED, MOVED, planOf } from './placeFixtures';
import type { PlaceApplied, PlaceAsk } from './placePlan';
import { PlaceSetUpPanel } from './PlaceSetUpPanel';
import { PLACES } from './setupFixtures';

function Place() {
  const lists = useListPanes();
  const modes = useAgentsMode(lists);
  const [chosen, setChosen] = useState<string | null>(null);
  const [over, setOver] = useState(false);
  const layout = useAgentsView({
    active: true, modes, chosen, onChoose: setChosen, notify: () => {},
    setUp: (place, page) => <PlaceSetUpPanel place={place} page={page} notify={() => {}} />,
  });
  return <ViewFrame layout={layout} lists={lists} over={over} onOver={setOver} />;
}

const rows = [
  { ...REGISTRY[0]!, repository: 'engine', workspace: 'aurora' },
  { ...REGISTRY[0]!, repository: 'site', workspace: 'aurora' },
];

const sent = (route: string) => invoke.mock.calls.filter(([, type]) => type === route).map(([, , options]) => (options as { payload: Record<string, unknown> }).payload);

describe('Set up…, on a place\'s page', () => {
  let apply: PlaceApplied = APPLIED;

  beforeEach(() => {
    apply = APPLIED;
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
      if (type === 'STATE' || type === 'SET_HOLD') return DRIVER_STATE;
      if (type === 'HARNESSES') return { settingsPath: 'x', adapter: 'claude-code', harnesses: [] };
      if (type === 'PLACES') return PLACES(options?.payload ?? {});
      if (type === 'PLACE_PLAN') return planOf(options!.payload as unknown as PlaceAsk);
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
    await screen.findByText('Works');
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
    const refusals = await screen.findByRole('list', { name: 'It cannot be applied yet' });
    expect(within(refusals).getByText(/no landing plugin here opens a pull request there/)).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Apply' })).toBeDisabled();
    // Choosing another delivery reads the plan again, and the refusal goes with it.
    await userEvent.click(screen.getByRole('radio', { name: /A branch to push/ }));
    await waitFor(() => expect(screen.queryByRole('list', { name: 'It cannot be applied yet' })).toBeNull());
    await waitFor(() => expect(screen.getByRole('button', { name: 'Apply' })).toBeEnabled());
  });

  it('sends Apply with the plan\'s digest and the acceptances chosen, and says what it came to', async () => {
    show(<Place />);
    await open(/^engine/);
    await userEvent.click(await screen.findByRole('radio', { name: /Integrated into the line/ }));
    const tick = await screen.findByRole('checkbox', { name: /Accept for integration/ });
    await waitFor(() => expect(screen.getByRole('button', { name: 'Apply' })).toBeEnabled());
    await userEvent.click(tick);
    await userEvent.click(screen.getByRole('button', { name: 'Apply' }));
    const ask: PlaceAsk = { repositories: ['engine'], preset: 'integrated', opinion: false, look: false, adoption: false, drive: false };
    await waitFor(() => expect(sent('PLACE_APPLY')).toEqual([{ ...ask, digest: planOf(ask).digest, accept: [{ repository: 'engine', purpose: 'integration' }] }]));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'PLACE_APPLY', expect.objectContaining({ timeoutMs: expect.any(Number) }));
    expect(await screen.findByRole('status')).toHaveTextContent('configuration applied; adoption pending');
  });

  it('sends no acceptance the person did not choose', async () => {
    show(<Place />);
    await open(/^engine/);
    await userEvent.click(await screen.findByRole('radio', { name: /Integrated into the line/ }));
    await screen.findByRole('checkbox', { name: /Accept for integration/ });
    await waitFor(() => expect(screen.getByRole('button', { name: 'Apply' })).toBeEnabled());
    await userEvent.click(screen.getByRole('button', { name: 'Apply' }));
    await waitFor(() => expect(sent('PLACE_APPLY')[0]).toMatchObject({ accept: [] }));
  });

  it('says a plan that moved in place, and reads it again', async () => {
    apply = MOVED;
    show(<Place />);
    await open(/^engine/);
    await userEvent.click(await screen.findByRole('radio', { name: /Integrated into the line/ }));
    await waitFor(() => expect(screen.getByRole('button', { name: 'Apply' })).toBeEnabled());
    const before = sent('PLACE_PLAN').length;
    await userEvent.click(screen.getByRole('button', { name: 'Apply' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('The plan moved while you read it');
    await waitFor(() => expect(sent('PLACE_PLAN').length).toBeGreaterThan(before));
  });

  it('holds a repository driving would release work from, then reads the plan again', async () => {
    show(<Place />);
    await open(/^engine/);
    await userEvent.click(await screen.findByRole('checkbox', { name: 'Enable general driving' }));
    const released = await screen.findByRole('list', { name: 'Driving releases this queued work' });
    expect(within(released).getByText('Tidy the docs')).toBeTruthy();
    const before = sent('PLACE_PLAN').length;
    await userEvent.click(screen.getByRole('button', { name: 'Hold it after driving' }));
    await waitFor(() => expect(sent('SET_HOLD')).toEqual([{ repository: 'engine', held: true }]));
    await waitFor(() => expect(sent('PLACE_PLAN').length).toBeGreaterThan(before));
  });

  it('covers a workspace\'s repositories with one plan, each change said once', async () => {
    show(<Place />);
    await open(/aurora\s*2 driven/);
    const group = await screen.findByRole('group', { name: 'Repositories' });
    expect(within(group).getByRole('checkbox', { name: 'engine' })).toBeChecked();
    expect(within(group).getByRole('checkbox', { name: 'site' })).toBeChecked();
    await userEvent.click(await screen.findByRole('checkbox', { name: 'Ask its session to set up for agents' }));
    await waitFor(() => expect(sent('PLACE_PLAN').at(-1)).toMatchObject({ repositories: ['engine', 'site'], adoption: true }));
    const changes = await screen.findByRole('list', { name: 'What it changes' });
    const asked = await within(changes).findAllByText(/a set-up asked of its own session/);
    expect(asked).toHaveLength(1);
    expect(asked[0]!.closest('li')).toHaveTextContent('(engine, site)');
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
});
