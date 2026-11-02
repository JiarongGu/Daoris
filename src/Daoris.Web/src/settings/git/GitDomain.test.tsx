import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// Settings → Git in SHELL mode (GITAUTH1e): every call goes over the bridge to DAORIS.DRIVER and nothing over the service, since an
// identity's key is this machine's (D47 §4). The identities are `TOOLS_LIST`'s `identities`; each press is `GIT_IDENTITY` or `GIT_USE`.

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

import { SettingsView } from '../SettingsView';
import { DRIVER_STATE, respond, serviceCalls, show } from '../../test/shellHarness';
import { TOOLS } from '../toolsFixtures';
import { PAGE } from './gitFixtures';

type Payload = Record<string, unknown> | undefined;

const answers: Record<string, (payload: Payload) => unknown> = {
  TOOLS_LIST: () => ({ ...TOOLS, identities: PAGE }),
  GIT_IDENTITY: (payload) => payload?.action === 'add-ssh'
    ? { identity: 'gi-00000000000d', label: payload.label, host: payload.host, way: 'ssh', publicKey: 'ssh-ed25519 AAAAExample daoris' }
    : { identity: payload?.identity },
  GIT_USE: (payload) => ({ scope: payload?.scope }),
};

const calls = (type: string) => invoke.mock.calls.filter(([module, route]) => module === 'DAORIS.DRIVER' && route === type);

describe('the git domain', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (module: string, type: string, options?: { payload?: Payload }) => {
      if (module !== 'DAORIS.DRIVER') return undefined;
      return answers[type]?.(options?.payload) ?? DRIVER_STATE;
    });
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('is a domain of a desktop after Tools: the identities read from the machine, nothing over the service', async () => {
    show(<SettingsView notify={() => {}} section="git" />);

    expect(await within(screen.getByRole('navigation', { name: 'Settings domains' })).findByRole('button', { name: 'Git' }))
      .toHaveAttribute('aria-current', 'page');
    await waitFor(() => expect(document.querySelector('[data-git-identity="gi-00000000000c"]')).not.toBeNull());
    expect(screen.getAllByRole('article').map((article) => article.querySelector('header span')?.textContent))
      .toEqual(['Git identities', 'Defaults by host', 'Your own git sign-ins']);
    expect(serviceCalls()).toEqual([]);
  });

  it('adds an SSH identity as the terminal does, and gives the public key', async () => {
    show(<SettingsView notify={() => {}} section="git" />);
    await userEvent.click((await screen.findAllByRole('button', { name: 'Add a git identity…' }))[0]!);

    await userEvent.type(screen.getByLabelText('Host'), 'github.com');
    await userEvent.type(screen.getByLabelText('A name for this identity'), 'Side');
    await userEvent.click(screen.getByRole('button', { name: 'Add' }));

    await waitFor(() => expect(calls('GIT_IDENTITY')[0]?.[2]).toEqual({
      payload: { action: 'add-ssh', host: 'github.com', label: 'Side' },
    }));
    expect(await screen.findByText('Side is added')).toBeInTheDocument();
    expect(screen.getByText('ssh-ed25519 AAAAExample daoris')).toBeInTheDocument();
  });

  it('chooses the person\'s own sign-ins for a host only after the confirmation, and stops them in one press', async () => {
    show(<SettingsView notify={() => {}} section="git" />);
    await userEvent.click(await screen.findByRole('button', { name: 'Use for…' }));
    await userEvent.type(screen.getByLabelText('Host'), 'github.com');

    expect(calls('GIT_USE')).toHaveLength(0);
    await userEvent.click(screen.getByRole('button', { name: 'Use my own sign-ins' }));
    await waitFor(() => expect(calls('GIT_USE')[0]?.[2]).toEqual({ payload: { scope: 'machine', host: 'github.com', value: 'own' } }));

    await userEvent.click(screen.getByRole('button', { name: 'Stop using' }));
    await waitFor(() => expect(calls('GIT_USE')[1]?.[2]).toEqual({ payload: { scope: 'machine', host: 'gitlab.com' } }));
  });

  it('sets a default and removes an identity on the second press, each a GIT_IDENTITY press', async () => {
    show(<SettingsView notify={() => {}} section="git" />);
    await waitFor(() => expect(document.querySelector('[data-git-identity="gi-00000000000c"]')).not.toBeNull());
    const work = document.querySelector<HTMLElement>('[data-git-identity="gi-00000000000c"]')!;

    await userEvent.click(within(work).getByRole('button', { name: 'Remove…' }));
    expect(calls('GIT_IDENTITY')).toHaveLength(0);
    await userEvent.click(within(work).getByRole('button', { name: 'Remove' }));

    await waitFor(() => expect(calls('GIT_IDENTITY')[0]?.[2]).toEqual({ payload: { action: 'remove', identity: 'gi-00000000000c' } }));
  });
});
