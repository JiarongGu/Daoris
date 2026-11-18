import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// *What needs you* lists a run waiting on a person's task, keyed by its run and node, with *Done…*, or
// *Approve…* and *Reject…*, each the run's node word with a note the person may give; over a mocked shell.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', async () => {
  const actual = await vi.importActual<typeof import('@shenora/react')>('@shenora/react');
  return {
    ...actual,
    ...(await import('../../test/sliceShell')).mockedShell(invoke, { notifyReady: () => Promise.resolve() }),
    useShenoraEvent: () => {},
    useWindowMaximized: () => false,
  };
});

import i18n from '../../locales/i18n';
import { keys } from '../../data/queries';
import { WorkspaceScopeProvider } from '../../data/scope';
import { composeAttention } from '../../bridge/shell';
import { AttentionBand } from './AttentionBand';
import { obligationOf } from './attention';

/** A run waiting on its `task` step, as .NET pushes the row (`HeldSteps`): its wait on a role the person holds, and its presses. */
const waiting = (acts: string[], since: string | null = null, over: Record<string, unknown> = {}) => ({
  kind: 'step', id: 'q7/sign', since, workspace: 'aurora', where: 'engine',
  step: { run: 'q7', node: 'sign', why: acts.includes('approve') ? 'approval' : 'task', role: acts.includes('approve') ? 'approver' : 'worker', holder: 'person', presses: acts, work: 'q7', ...over },
});

function start(acts: string[], since: string | null = null, over: Record<string, unknown> = {}) {
  vi.stubGlobal('fetch', vi.fn(async () => Response.json([])));
  invoke.mockImplementation(async (_target: string, route: string, ask?: { payload?: { act?: string } }) =>
    route === 'WORKFLOW_NODE' ? { run: 'q7', node: 'sign', act: ask?.payload?.act, done: true } : {});
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  client.setQueryData(keys.attention, composeAttention(new Map<string, unknown>([['rows', { 'step:q7/sign': waiting(acts, since, over) }], ['sessions', {}]]), true));
  render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <WorkspaceScopeProvider initial={null}>
          <AttentionBand notify={vi.fn()} doors={{}} />
        </WorkspaceScopeProvider>
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

afterEach(async () => {
  cleanup();
  vi.unstubAllGlobals();
  invoke.mockReset();
  await i18n.changeLanguage('en');
});

describe("a person's task on What needs you", () => {
  it('is keyed by its run and node, as the other waits are', () => {
    expect(obligationOf(waiting(['done']))).toBe('step:q7/sign');
  });

  it('says a plugin step’s wait on a role the person holds, the act in its plugin’s words, its page, and its pass over', async () => {
    start(['skip'], null, { why: 'actor', role: 'approver', words: 'Approve Production in Azure DevOps.', url: 'https://dev.azure.com/acme/Proj/_releaseProgress?releaseId=90' });
    const item = await screen.findByRole('listitem', { name: '#q7' });
    expect(item).toHaveTextContent('sign: waiting on approver: you. Approve Production in Azure DevOps.');
    expect(within(item).getByRole('link')).toHaveAttribute('href', 'https://dev.azure.com/acme/Proj/_releaseProgress?releaseId=90');
    expect(within(item).getByRole('button', { name: 'Not needed this run…' })).toBeTruthy();
  });

  it('offers a step’s stop where the driver offers it, asked once, as the run’s node stop with the person’s reason', async () => {
    start(['skip', 'stop'], null, { why: 'actor', role: 'operations', words: 'Make the release in Azure DevOps.' });
    const item = await screen.findByRole('listitem', { name: '#q7' });
    await userEvent.click(within(item).getByRole('button', { name: 'Stop this step…' }));
    const asking = within(item).getByRole('group', { name: 'Stop this step…' });
    expect(asking).toHaveTextContent('End this work at sign');
    await userEvent.type(within(asking).getByRole('textbox'), 'released by hand');
    await userEvent.click(within(asking).getByRole('button', { name: 'End this work' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORKFLOW_NODE', { payload: { run: 'q7', node: 'sign', act: 'stop', words: 'released by hand' } }));
  });

  it('offers no stop where the driver offers none', async () => {
    start(['done']);
    const item = await screen.findByRole('listitem', { name: '#q7' });
    expect(within(item).queryByRole('button', { name: 'Stop this step…' })).toBeNull();
  });

  it.each([['en', /waiting 3 ?h/i], ['zh', /已等待.*3/]])('says since when it has waited, in %s', async (language, said) => {
    await i18n.changeLanguage(language);
    start(['done'], new Date(Date.now() - 3 * 3_600_000).toISOString());
    expect(await screen.findByRole('listitem', { name: '#q7' })).toHaveTextContent(said);
  });

  it('presses Done with the note the person gave, as the run’s node word', async () => {
    start(['done']);
    const item = await screen.findByRole('listitem', { name: '#q7' });
    expect(item).toHaveTextContent('sign: waiting on worker: you.');
    await userEvent.click(within(item).getByRole('button', { name: 'Done…' }));
    const asking = within(item).getByRole('group', { name: 'Done…' });
    await userEvent.type(within(asking).getByRole('textbox'), 'tagged by hand');
    await userEvent.click(within(asking).getByRole('button', { name: 'Done' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORKFLOW_NODE', { payload: { run: 'q7', node: 'sign', act: 'done', words: 'tagged by hand' } }));
  });

  it('offers the holder presses of its step, and every settling press passes the epoch of the tenure it read', async () => {
    start(['done'], null, { epoch: 2, holds: ['hand', 'give-back'] });
    const item = await screen.findByRole('listitem', { name: '#q7' });
    expect(within(item).getByRole('button', { name: 'Pass to…' })).toBeTruthy();
    expect(within(item).queryByRole('button', { name: 'Take' })).toBeNull();

    await userEvent.click(within(item).getByRole('button', { name: 'Give back' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORKFLOW_HOLD', { payload: { run: 'q7', node: 'sign', act: 'give-back', holder: null, epoch: 2 } }));

    await userEvent.click(within(item).getByRole('button', { name: 'Done…' }));
    await userEvent.click(within(within(item).getByRole('group', { name: 'Done…' })).getByRole('button', { name: 'Done' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORKFLOW_NODE', { payload: { run: 'q7', node: 'sign', act: 'done', epoch: 2 } }));
  });

  it.each([
    ['en', 'Approve…', 'Approve', 'Reject…', 'Reject', 'sign: waiting on approver: you.'],
    ['zh', '批准…', '批准', '驳回…', '驳回', 'sign：等待 approver：你。'],
  ])('offers an approval its Approve, a note left out where none is given, and its Reject, which waits for its reason, in %s', async (language, approveOpen, approve, rejectOpen, reject, why) => {
    await i18n.changeLanguage(language);
    start(['approve', 'reject']);
    const item = await screen.findByRole('listitem', { name: '#q7' });
    expect(item).toHaveTextContent(why);
    expect(within(item).queryByRole('button', { name: 'Done…' })).toBeNull();

    await userEvent.click(within(item).getByRole('button', { name: approveOpen }));
    await userEvent.click(within(within(item).getByRole('group', { name: approveOpen })).getByRole('button', { name: approve }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORKFLOW_NODE', { payload: { run: 'q7', node: 'sign', act: 'approve' } }));

    await userEvent.click(within(item).getByRole('button', { name: rejectOpen }));
    // A refusal is the run's rejection: it waits for its reason.
    const refusing = within(item).getByRole('group', { name: rejectOpen });
    expect(within(refusing).getByRole('button', { name: reject })).toBeDisabled();
    await userEvent.type(within(refusing).getByRole('textbox'), 'the notes miss it');
    await userEvent.click(within(refusing).getByRole('button', { name: reject }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORKFLOW_NODE', { payload: { run: 'q7', node: 'sign', act: 'reject', words: 'the notes miss it' } }));
  });
});
