import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, within } from '@testing-library/react';

// An agent held by a key alone on the Agents place, over the mocked bridge.
const { invoke, notifyReady } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
}));

vi.mock('@shenora/react', async () => ({
  ...(await import('../test/sliceShell')).mockedShell((await import('./agentsFixtures')).sheeted(invoke), { notifyReady }),
  useShenoraEvent: () => {},
}));

import i18n from '../locales/i18n';
import { respond, show, WIRING } from '../test/shellHarness';
import { useListPanes } from '../work/frame/listPanes';
import { ViewFrame } from '../work/frame/ViewFrame';
import { useAgentsView } from './AgentsView';

function Place({ first }: { first: string }) {
  const lists = useListPanes();
  const [chosen, setChosen] = useState<string | null>(first);
  const [over, setOver] = useState(false);
  const layout = useAgentsView({ active: true, chosen, onChoose: setChosen, notify: () => {}, part: null });
  return <ViewFrame layout={layout} lists={lists} over={over} onOver={setOver} />;
}

describe('the Agents place, an agent held by a key alone', () => {
  const ROSTER = {
    settingsPath: 'C:/somewhere/.daoris/harnesses.json',
    adapter: 'claude-code',
    harnesses: [{
      door: 'dsh', product: 'DeepSeek Harness', maker: 'DeepSeek', transport: 'protocol',
      present: true, version: 'dsh 0.1.0', problem: null, pinned: null, managed: null, pinnable: true,
      signsIn: false, takesKey: true, ownLogin: 'unknown', ownRead: null,
      profiles: [{ name: 'account-1', home: 'C:/somewhere/.daoris/harnesses/dsh/account-1', login: 'out' }],
    }],
  };

  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES' ? ROSTER : WIRING));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  /** A harness held by a key alone has no sign-in, so its page's terminal line adds an account with `agent key`, never `agent login`. */
  it('Agents → DeepSeek Harness says the key’s way at the foot of its page, not a sign-in', async () => {
    show(<Place first="dsh" />);

    expect(await screen.findByRole('heading', { level: 1, name: 'DeepSeek Harness' })).toBeTruthy();
    const foot = within(screen.getByRole('main')).getByText(/The same from a terminal/);
    expect(foot.textContent).toContain('daoris agent key dsh');
    expect(foot.textContent).toContain('--account <account>');
    expect(foot.textContent).not.toContain('agent login');
    // The zh footer is a sentence of its own, not the sign-in one translated.
    expect(i18n.t('agents.page.terminalKey', { agent: 'dsh', lng: 'zh' })).toContain('daoris agent key dsh');
    expect(i18n.t('agents.page.terminalKey', { agent: 'dsh', lng: 'zh' })).not.toContain('agent login');
  });
});
