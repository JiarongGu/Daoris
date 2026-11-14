import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, cleanup, render, screen, within } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';

const { invoke, available } = vi.hoisted(() => ({ invoke: vi.fn(), available: { value: true } }));
vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => available.value,
  getBridge: () => ({ isAvailable: available.value, invoke }),
  useShenora: () => ({ isAvailable: available.value }),
  useShenoraEvent: () => undefined,
}));

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import i18n from '../../locales/i18n';
import { stateChanged } from '../../bridge/state/stateHub';
import { ENGINE } from '../fixtures';
import { ProjectPage } from '../ProjectPage';
import { RepositoryTreePool } from './RepositoryTreePool';

const clients: QueryClient[] = [];

function page(tab: 'details' | 'branches' = 'details') {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  clients.push(client);
  render(<QueryClientProvider client={client}><Tooltip.Provider>
    <ProjectPage registration={ENGINE} tab={tab} onTab={vi.fn()} branches={<p>Branches body</p>}
      treePool={<RepositoryTreePool workspace="aurora" repository="engine" />} />
  </Tooltip.Provider></QueryClientProvider>);
  return client;
}

describe("a repository page's tree pool", () => {
  afterEach(async () => {
    cleanup();
    clients.splice(0).forEach((client) => client.clear());
    invoke.mockReset();
    available.value = true;
    await i18n.changeLanguage('en');
  });

  it.each([
    ['en', 'Tree pool', 'Slot 2', 'Daoris is restoring this slot to its previous state. Starts wait until it is restored or freed.'],
    ['zh', '工作树池', '槽位 2', 'Daoris 正在将此槽位恢复到原来的状态。恢复或释放槽位后，等待的工作才能启动。'],
  ])('says why a slot waits in %s, then removes the wait when the next look frees it', async (language, title, slot, reason) => {
    await i18n.changeLanguage(language);
    invoke.mockResolvedValue({ slots: [
      { number: 1, state: 'held', line: 's-first', reason: '' },
      { number: 2, state: 'restoring', line: '', reason: 'tree.kept-restoring' },
    ] });
    const client = page();
    const pool = await screen.findByRole('region', { name: title });
    expect(await within(pool).findByText('s-first')).toBeInTheDocument();
    const waiting = within(pool).getByRole('listitem', { name: slot });
    expect(waiting).toHaveTextContent(reason);
    expect(waiting).not.toHaveTextContent('s-first');
    expect(pool).not.toHaveTextContent('tree.kept-restoring');
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'TREE_POOL', {
      payload: { workspace: 'aurora', repository: 'engine', held: expect.any(String) },
    });

    invoke.mockResolvedValue({ slots: [{ number: 2, state: 'kept', line: '', reason: '' }] });
    await act(async () => stateChanged(client, { scopes: ['driver'] }));
    expect(await within(pool).findByText(language === 'en' ? 'Available' : '可用')).toBeInTheDocument();
    expect(within(pool).queryByText(reason)).toBeNull();
  });

  it('reads an older shell with no pool answer as empty', async () => {
    invoke.mockResolvedValue(null);
    page();
    expect(await screen.findByText('There are no tree pool slots on this machine.')).toBeInTheDocument();
  });

  it('does not ask for machine slots in a browser', () => {
    available.value = false;
    page();
    expect(screen.queryByRole('region', { name: 'Tree pool' })).toBeNull();
    expect(invoke).not.toHaveBeenCalled();
  });

  it('reads no slots while another repository tab is open', () => {
    page('branches');
    expect(screen.getByText('Branches body')).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Tree pool' })).toBeNull();
    expect(invoke).not.toHaveBeenCalled();
  });
});
