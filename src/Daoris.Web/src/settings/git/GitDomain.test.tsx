import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// Settings → Git in SHELL mode: every call goes over the bridge to DAORIS.DRIVER and nothing over the service, since an
// identity's key is this machine's. The identities are `TOOLS_LIST`'s `identities`; each press is `GIT_IDENTITY` or `GIT_USE`.

const { invoke, notifyReady } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
}));

vi.mock('@shenora/react', async () => ({ ...(await import('../../test/sliceShell')).mockedShell(invoke, { notifyReady }), useShenoraEvent: () => {} }));

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
  TOOLS_PICK: () => ({ can: true, file: 'X:/keys/id_ed25519' }),
};

const calls = (type: string) => invoke.mock.calls.filter(([module, route]) => module === 'DAORIS.DRIVER' && route === type);

describe('the git domain', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (module: string, type: string, options?: { payload?: Payload }) => {
      if (module !== 'DAORIS.DRIVER') return undefined;
      return answers[type]?.(options?.payload) ?? { ...DRIVER_STATE, machineGitAccess: [{ class: 'read', value: 'ask' }] };
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
    // The machine's access is a standing choice of the driver's state, which the shell pushes beside the identities' read.
    await screen.findByText('Git access on this machine');
    expect(screen.getAllByRole('article').map((article) => article.querySelector('header span')?.textContent))
      .toEqual(['Git identities', 'Defaults by host', 'Your own git sign-ins', 'Git reads', 'Git access on this machine']);
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

  it('browses for the key to import as a key file, and fills the path it answers', async () => {
    show(<SettingsView notify={() => {}} section="git" />);
    await userEvent.click((await screen.findAllByRole('button', { name: 'Add a git identity…' }))[0]!);

    await userEvent.click(screen.getByRole('radio', { name: 'Import a key…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Browse…' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith(
      'DAORIS.DRIVER', 'TOOLS_PICK', { payload: { title: 'Choose the key file', kind: 'key' }, timeoutMs: Infinity }));
    expect(await screen.findByDisplayValue('X:/keys/id_ed25519')).toBeInTheDocument();
  });

  it('hands an import passphrase to the local bridge only after confirmation', async () => {
    show(<SettingsView notify={() => {}} section="git" gitHost="github.com" />);
    await userEvent.type(await screen.findByLabelText('A name for this identity'), 'Import');
    await userEvent.click(screen.getByRole('radio', { name: 'Import a key…' }));
    await userEvent.type(screen.getByLabelText('The key file to import'), 'X:/keys/source.ppk');
    await userEvent.type(screen.getByLabelText('Key passphrase (if encrypted)'), 'one passphrase');
    await userEvent.click(screen.getByRole('button', { name: 'Add' }));
    expect(calls('GIT_IDENTITY')).toHaveLength(0);
    await userEvent.click(screen.getByRole('button', { name: 'Import this key' }));
    await waitFor(() => expect(calls('GIT_IDENTITY')[0]?.[2]).toEqual({ payload: {
      action: 'add-ssh', host: 'github.com', label: 'Import', import: 'X:/keys/source.ppk', passphrase: 'one passphrase',
    } }));
    expect(serviceCalls()).toEqual([]);
  });

  // A held remote's door opens Settings → Git with its add form on the remote's host, and tells the door when it closes.
  it('opens its add form on the host a door names', async () => {
    const closed = vi.fn();
    show(<SettingsView notify={() => {}} section="git" gitHost="gitlab.com" onGitHostClosed={closed} />);

    expect(await screen.findByLabelText('Host')).toHaveValue('gitlab.com');
    await userEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    expect(closed).toHaveBeenCalledOnce();
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
