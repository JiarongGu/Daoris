import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// WORKFLOWCLEAN1g-i (the cleanup design §4.3): a place that follows an old landing rule says so in its head and offers Convert…, which opens the
// home-wide review in place of the page's sections, whichever place asked.

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
import { SETUP_PRESETS } from './placeFixtures';
import { PLACES } from './setupFixtures';

function Place() {
  const lists = useListPanes();
  const modes = useAgentsMode(lists);
  const [chosen, setChosen] = useState<string | null>(null);
  const [over, setOver] = useState(false);
  const [shown, setShown] = useState(false);
  const layout = useAgentsView({
    active: true, modes, chosen, onChoose: setChosen, notify: () => {},
    bodies: () => ({ workflow: <p>The workflow body</p>, sessions: <p>Sessions body</p> }),
    convert: {
      shown,
      onOpen: () => setShown(true),
      follows: (place) => place.repository === 'engine' || (!place.repository && place.workspace === 'aurora'),
      panel: <section aria-label="The conversion"><button type="button" onClick={() => setShown(false)}>Close the conversion</button></section>,
    },
  });
  return <ViewFrame layout={layout} lists={lists} over={over} onOver={setOver} />;
}

const rows = [
  { ...REGISTRY[0]!, repository: 'engine', workspace: 'aurora' },
  { ...REGISTRY[0]!, repository: 'site', workspace: 'aurora' },
];

describe('Convert…, on a place\'s head', () => {
  beforeEach(() => {
    window.localStorage.clear();
    window.localStorage.setItem('daoris.agents.setup', 'on');
    window.localStorage.setItem('daoris.list.agents.mode', 'setup');
    i18n.changeLanguage('en');
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => (String(input).startsWith('/api/registry') ? Response.json(rows) : Response.json([]))));
    invoke.mockImplementation(async (module: string, type: string, options?: { payload?: Record<string, unknown> }) => {
      if (module !== 'DAORIS.DRIVER') return WIRING;
      if (type === 'STATE') return DRIVER_STATE;
      if (type === 'HARNESSES') return { settingsPath: 'x', adapter: 'claude-code', harnesses: [] };
      if (type === 'PLACES') return PLACES(options?.payload ?? {});
      if (type === 'WORKFLOW_LIST') return { workflows: [], presets: SETUP_PRESETS };
      return undefined;
    });
  });
  afterEach(() => {
    cleanup();
    window.localStorage.clear();
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('says a place follows its old landing rule and offers Convert…; a place that does not offers none', async () => {
    show(<Place />);
    await userEvent.click(await screen.findByRole('button', { name: /^engine/ }));
    await screen.findByRole('heading', { level: 1, name: 'engine' });
    expect(screen.getByText(/Follows its old landing rule/)).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Convert…' })).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: /^site/ }));
    await screen.findByRole('heading', { level: 1, name: 'site' });
    expect(screen.queryByText(/Follows its old landing rule/)).toBeNull();
    expect(screen.queryByRole('button', { name: 'Convert…' })).toBeNull();
  });

  it('opens the review in place of the sections, and closing returns to the page', async () => {
    show(<Place />);
    await userEvent.click(await screen.findByRole('button', { name: /^engine/ }));
    await userEvent.click(await screen.findByRole('button', { name: 'Convert…' }));
    const review = await screen.findByRole('region', { name: 'The conversion' });
    expect(screen.queryByText('The workflow body')).toBeNull();
    await userEvent.click(within(review).getByRole('button', { name: 'Close the conversion' }));
    await screen.findByRole('heading', { level: 1, name: 'engine' });
    expect(screen.getByRole('button', { name: 'Convert…' })).toBeTruthy();
  });

  it('is the same review from a workspace\'s head', async () => {
    show(<Place />);
    await userEvent.click(await screen.findByRole('button', { name: /^aurora/ }));
    await userEvent.click(await screen.findByRole('button', { name: 'Convert…' }));
    await screen.findByRole('region', { name: 'The conversion' });
  });
});
