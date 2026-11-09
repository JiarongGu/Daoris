import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

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

  it('opens the API-key form under the accounts it adds to, above Your own sign-in', async () => {
    show(<Place first="dsh" />);

    await userEvent.click(await screen.findByRole('button', { name: 'Add an API key' }));
    const field = screen.getByLabelText('API key for DeepSeek Harness');
    const own = screen.getByText('Your own sign-in');
    expect(field.compareDocumentPosition(own) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(screen.getByText('account-1').compareDocumentPosition(field) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('says an empty accounts list is empty and which press adds the first, with no column heads', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (
      type === 'HARNESSES' ? { ...ROSTER, harnesses: [{ ...ROSTER.harnesses[0], profiles: [] }] } : WIRING));
    show(<Place first="dsh" />);

    expect(await screen.findByRole('heading', { level: 1, name: 'DeepSeek Harness' })).toBeTruthy();
    const main = within(screen.getByRole('main'));
    expect(main.getByText('No Daoris account yet: these are the accounts its work runs as. “Add an API key” above adds the first.')).toBeTruthy();
    expect(main.queryByText('State')).toBeNull();
    expect(i18n.t('agents.account.none', { press: '添加 API 密钥', lng: 'zh' })).toBe('还没有 Daoris 账户：这里是它的工作所用的账户。上方的“添加 API 密钥”可添加第一个。');
  });
});
