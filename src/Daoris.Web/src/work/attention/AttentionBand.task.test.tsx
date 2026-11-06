import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// NODEKIT1 HUMAN (D245's second note): *What needs you* lists a run waiting on a person's task, keyed by its run and node, with *Done…*, or
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

/** A run waiting on its `human` node, as .NET pushes the row (`HumanTaskRows`). */
const waiting = (acts: string[]) => ({
  kind: 'task', id: 'q7/sign', since: null, workspace: 'aurora', where: 'engine', task: { run: 'q7', node: 'sign', acts, work: 'q7' },
});

function start(acts: string[]) {
  vi.stubGlobal('fetch', vi.fn(async () => Response.json([])));
  invoke.mockImplementation(async (_target: string, route: string, ask?: { payload?: { act?: string } }) =>
    route === 'WORKFLOW_NODE' ? { run: 'q7', node: 'sign', act: ask?.payload?.act, done: true } : {});
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  client.setQueryData(keys.attention, composeAttention(new Map<string, unknown>([['rows', { 'task:q7/sign': waiting(acts) }], ['sessions', {}]]), true));
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
    expect(obligationOf(waiting(['done']))).toBe('task:q7/sign');
  });

  it('presses Done with the note the person gave, as the run’s node word', async () => {
    start(['done']);
    const item = await screen.findByRole('listitem', { name: '#q7' });
    expect(item).toHaveTextContent('The run waits at sign for your Done.');
    await userEvent.click(within(item).getByRole('button', { name: 'Done…' }));
    const asking = within(item).getByRole('group', { name: 'Done…' });
    await userEvent.type(within(asking).getByRole('textbox'), 'tagged by hand');
    await userEvent.click(within(asking).getByRole('button', { name: 'Done' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORKFLOW_NODE', { payload: { run: 'q7', node: 'sign', act: 'done', words: 'tagged by hand' } }));
  });

  it.each([
    ['en', 'Approve…', 'Approve', 'Reject…', 'Reject', 'The run waits at sign for your Approve or Reject.'],
    ['zh', '批准…', '批准', '驳回…', '驳回', '进展停在 sign，等你批准或驳回。'],
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
