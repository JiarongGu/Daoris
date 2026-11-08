import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady: () => Promise.resolve() }),
  useShenora: () => ({ isAvailable: true, bridge: {} }),
  useShenoraEvent: () => undefined,
}));

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import i18n from '../../locales/i18n';
import { RepositoryFiles } from './RepositoryFiles';

// The Files tab's holder asks the files route with the query it names, sends the person's presses on the window's routes, and
// words a refusal: a file that does not read refuses an allow, and nothing is written over it.

const LOOK = {
  repository: 'engine', commit: 'def1234567', foundCommit: 'abc1234567',
  files: [{ path: 'node_modules/', kind: 'dependency', layer: 'found', files: 3, bytes: 10, below: [] }],
  acts: [{ act: 'run(npm ci)', path: 'node_modules/', verdict: 'held' }],
  permissions: [{ scope: 'repository', deny: [], ask: ['run(npm *)'], allow: [] }, { scope: 'machine', deny: [], ask: [], allow: [] }],
  rules: [], problems: [],
};

function renderHolder() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}><RepositoryFiles repository="engine" /></QueryClientProvider>);
}

describe("a repository's Files tab, held", () => {
  afterEach(async () => {
    invoke.mockReset();
    await i18n.changeLanguage('en');
  });

  it('writes the allow an ask waits for on the window route, and says a file that does not read refused it', async () => {
    invoke.mockImplementation((_module: string, route: string) => Promise.resolve(
      route === 'FILES' ? { look: LOOK }
        : route === 'FILES_PERMIT' ? { done: false, why: 'unread', message: 'files.json is not valid JSON, so nothing was written over it.' }
          : null));
    renderHolder();

    await userEvent.click(await screen.findByRole('button', { name: 'Allow run(npm ci)' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'FILES_PERMIT', {
      payload: { repository: 'engine', act: 'add', list: 'allow', rule: 'run(npm ci)' },
    });
    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('Your file rules do not read, so nothing was written over them'));
  });

  it('says why a repository has no reading', async () => {
    invoke.mockResolvedValue({ refusal: 'no-checkout' });
    renderHolder();

    expect(await screen.findByText('This repository has no checkout on this machine, so its files cannot be read.')).toBeInTheDocument();
  });

  it('sends the rules and the read again on their own window routes', async () => {
    invoke.mockImplementation((_module: string, route: string) => Promise.resolve(route === 'FILES' ? { look: LOOK } : { done: true }));
    renderHolder();

    await userEvent.click(await screen.findByRole('button', { name: 'Read again' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'FILES_READ', { payload: { repository: 'engine' } });

    await userEvent.click(screen.getByRole('button', { name: 'Add a rule' }));
    await userEvent.type(screen.getByRole('textbox', { name: 'Paths' }), 'tmp/');
    await userEvent.click(screen.getByRole('button', { name: 'Add' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'FILES_SET', {
      payload: { repository: 'engine', kind: 'scratch', paths: ['tmp/'], machine: false },
    }));
  });
});
