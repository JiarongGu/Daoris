import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// An agent's page's *Used by*, over the driver's one read of the structure.

const { invoke, notifyReady, eventHandlers } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
  eventHandlers: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', async () => ({
  ...(await import('../../test/sliceShell')).mockedShell(invoke, { notifyReady }),
  useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
    eventHandlers.set(`${module}.${type}`, handler);
  },
}));

import i18n from '../../locales/i18n';
import { DRIVER_STATE, respond, show, WIRING } from '../../test/shellHarness';
import { useListPanes } from '../../work/frame/listPanes';
import { ViewFrame } from '../../work/frame/ViewFrame';
import { CLAUDE_USE, CODEX_USE, DOORS } from '../agentsFixtures';
import { useAgentsMode, useAgentsView } from '../AgentsView';
import { DRIVER, LINES, PLUGINS, REGISTRY, STRUCTURE } from './structureFixtures';

const doors = {
  agent: vi.fn(), repository: vi.fn(), workspace: vi.fn(), plugin: vi.fn(),
};

function AgentsPlace() {
  const lists = useListPanes();
  const modes = useAgentsMode(lists);
  const [chosen, setChosen] = useState<string | null>(null);
  const [over, setOver] = useState(false);
  const layout = useAgentsView({ active: true, modes, chosen, onChoose: setChosen, notify: () => {}, roleDoors: doors });
  return <ViewFrame layout={layout} lists={lists} over={over} onOver={setOver} />;
}

const ROSTER = { settingsPath: 'x', adapter: 'claude-code', harnesses: DOORS };

const REGISTRATIONS = REGISTRY.map((row) => ({
  repository: row.repository, adopted: true, registered: true, owns: [], accepts: [], packs: [], entries: 1, ...(row.workspace ? { workspace: row.workspace } : {}),
}));

describe("an agent's page", () => {
  let structure: 'ok' | 'failed' | 'pending' = 'ok';

  beforeEach(() => {
    structure = 'ok';
    window.localStorage.clear();
    window.localStorage.setItem('daoris.list.agents.mode', 'agents');
    i18n.changeLanguage('en');
    Object.values(doors).forEach((door) => door.mockReset());
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      return url.startsWith('/api/registry') ? Response.json(REGISTRATIONS) : respond(url);
    }));
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module !== 'DAORIS.DRIVER') return WIRING;
      switch (type) {
        case 'STATE': return { ...DRIVER_STATE, drivable: DRIVER.drivable, holds: DRIVER.holds };
        case 'HARNESSES': return ROSTER;
        case 'ACCOUNTS': return { agents: [CLAUDE_USE, CODEX_USE] };
        case 'ROLES': return { roles: [], works: ['build', 'design'], kept: true };
        case 'STARTS': return { adapter: 'claude-code', starts: [] };
        case 'PLUGINS': return { folder: 'plugins', plugins: PLUGINS };
        case 'LINES': return LINES;
        case 'ROLE_STRUCTURE':
          if (structure === 'failed') throw new Error('the structure did not read');
          if (structure === 'pending') return { pending: true };
          return STRUCTURE;
        default: return undefined;
      }
    });
  });
  afterEach(() => {
    cleanup();
    window.localStorage.clear();
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('says the roles that use the agent, from the structure, and a failed read in words', async () => {
    show(<AgentsPlace />);
    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: /^Claude Code/ }));
    await user.click(await screen.findByRole('button', { name: 'Show Used by' }));
    const used = await screen.findByRole('list', { name: 'Used by' });
    expect(within(used).getAllByRole('listitem').length).toBeGreaterThanOrEqual(3);
    expect(used.textContent).toMatch(/Build · building and design and decisions · choice 1/);
  });

  it('says a read that failed, never an empty list', async () => {
    structure = 'failed';
    show(<AgentsPlace />);
    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: /^Claude Code/ }));
    await user.click(await screen.findByRole('button', { name: 'Show Used by' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('what uses it could not be read');
  });
});
