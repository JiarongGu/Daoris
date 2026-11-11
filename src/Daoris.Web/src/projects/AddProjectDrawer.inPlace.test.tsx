import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// The add's choice of *Work in place*: a folder follows it in the same press that registers it, and a repository where the
// person ticks it, through a set-up's plan of one and its apply, as `daoris place <department> --preset in-place --apply` does.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));
vi.mock('@shenora/react', async () => ({
  ...(await import('../test/sliceShell')).mockedShell(invoke),
  useShenoraEvent: () => undefined,
}));

import { DRIVER_STATE, respond, show } from '../test/shellHarness';
import { AddProjectDrawer } from './ProjectManage';

const DIGEST = 'a'.repeat(64);
const FOLDER = {
  path: 'D:/docs/reports', name: 'reports', exists: true, adopted: false, git: false,
  owns: [], accepts: [], packs: [], join: false, shareKnowledge: false, uses: [],
};

const routes = () => invoke.mock.calls.map(([, type]) => type as string);
const applied = () => (invoke.mock.calls.find(([, type]) => type === 'PLACE_APPLY')?.[2] as { payload?: Record<string, unknown> } | undefined)?.payload;

describe('the add choosing Work in place', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url === '/api/registry' && init?.method === 'POST') return Response.json({ repository: 'reports', workspace: 'aurora' });
      return respond(url);
    }));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  const answering = (picked: typeof FOLDER) => invoke.mockImplementation(async (_module: string, type: string) => {
    if (type === 'PICK_FOLDER') return picked;
    if (type === 'PLACE_PLAN') return { refusals: [], digest: DIGEST, repositories: [], changes: [] };
    if (type === 'PLACE_APPLY') return { done: true, message: 'Applied.', digest: DIGEST, operations: [] };
    return DRIVER_STATE;
  });

  it('a folder is added following Work in place, chosen in the press that registers it', async () => {
    answering(FOLDER);
    const notify = vi.fn();
    show(<AddProjectDrawer onClose={() => {}} notify={notify} />);

    await userEvent.click(await screen.findByRole('button', { name: 'Choose a folder…' }));
    expect(await screen.findByText(/A folder follows Work in place/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Register' }));

    await waitFor(() => expect(applied()).toMatchObject({ repositories: ['reports'], preset: 'in-place', digest: DIGEST, accept: [] }));
    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringContaining('follows Work in place')));
  });

  it('a repository follows Work in place only where the person ticks it', async () => {
    answering({ ...FOLDER, git: true });
    show(<AddProjectDrawer onClose={() => {}} notify={() => {}} />);

    await userEvent.click(await screen.findByRole('button', { name: 'Choose a folder…' }));
    const tick = await screen.findByRole('checkbox', { name: /Work in place/ });
    expect(tick).not.toBeChecked();
    await userEvent.click(tick);
    await userEvent.click(screen.getByRole('button', { name: 'Register' }));

    await waitFor(() => expect(applied()).toMatchObject({ repositories: ['reports'], preset: 'in-place' }));
  });

  it('a repository left unticked follows what its workspace chooses, and no set-up is asked', async () => {
    answering({ ...FOLDER, git: true });
    const notify = vi.fn();
    show(<AddProjectDrawer onClose={() => {}} notify={notify} />);

    await userEvent.click(await screen.findByRole('button', { name: 'Choose a folder…' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Register' }));

    await waitFor(() => expect(notify).toHaveBeenCalled());
    expect(routes()).not.toContain('PLACE_PLAN');
  });
});
