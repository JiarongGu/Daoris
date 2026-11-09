import { useEffect, type ReactNode } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import i18n from '../../locales/i18n';
import { GIT_PALETTE, LIST } from './editFixtures';

// The picker's Git group, as the cut-over offers it: a rebase added between two steps comes with its work from the run's entry, its ready
// way on and its resolution's tail in one change, said aloud; the inspector names its input and what feeds it; and the save holds every way.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn(async (..._: unknown[]): Promise<unknown> => null) }));

vi.mock('@shenora/react', async (original) => ({
  ...(await original<typeof import('@shenora/react')>()),
  getBridge: () => ({ invoke }),
  useShenora: () => ({ isAvailable: true }),
}));

import { useWorkflowEditor } from '../../bridge/repositories/workflowEdit';
import { WorkflowEditor } from './WorkflowEditor';

type Wire = { id: string; kind: string; from: string; to: string; outcome?: string; on?: string };

function answer(type: string, payload: Record<string, unknown>): unknown {
  switch (type) {
    case 'WORKFLOW_LIST': return LIST;
    case 'WORKFLOW_PALETTE': return GIT_PALETTE;
    case 'ROLE_STRUCTURE': return { works: [], places: [], agents: [], roles: [] };
    case 'WORKFLOW_SAVE': return { id: payload.id, version: 1, written: !payload.plan, appended: false, scopes: [], reach: [], done: true, refused: null, message: 'saved', problems: [] };
    default: return null;
  }
}

function Editor() {
  const bridge = useWorkflowEditor({ repository: 'engine', workspace: 'aurora' });
  const { onOpen } = bridge;
  useEffect(() => onOpen?.(true), [onOpen]);
  return <WorkflowEditor {...bridge} place={{ repository: 'engine' }} />;
}

const wrap = (node: ReactNode) => (
  <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
    <Tooltip.Provider>{node}</Tooltip.Provider>
  </QueryClientProvider>
);

const step = (name: string | RegExp) => within(screen.getByRole('list', { name: 'How the steps go on' })).getByRole('listitem', { name });
const focused = () => document.activeElement as HTMLElement;
const said = (wire: Wire) => `${wire.kind}:${wire.from}>${wire.to}${wire.outcome ? `?${wire.outcome}` : ''}${wire.on ? `@${wire.on}` : ''}`;

let user: ReturnType<typeof userEvent.setup>;

beforeEach(() => {
  user = userEvent.setup();
  invoke.mockImplementation(async (_module, type, options) => answer(type as string, (options as { payload: Record<string, unknown> }).payload));
});

afterEach(async () => {
  invoke.mockReset();
  vi.restoreAllMocks();
  await i18n.changeLanguage('en');
});

describe('the Git group in the editor', () => {
  it('adds a rebase with its tail in one change, names its input and what feeds it, and saves every way', async () => {
    render(wrap(<Editor />));
    await user.click(await screen.findByRole('button', { name: 'Begin from Merge workflow' }));
    await waitFor(() => expect(step('Your review')).toBeInTheDocument());

    step('Your review').focus();
    await user.keyboard('+');
    const listbox = await screen.findByRole('listbox', { name: 'Add a step' });
    expect(within(listbox).getByText('Git')).toBeInTheDocument();
    await user.keyboard('rebase');
    expect(screen.getByRole('option', { selected: true })).toHaveTextContent('Rebase the work onto its target');
    await user.keyboard('{Enter}');
    await waitFor(() => expect(focused()).toBe(step('Rebase the work onto its target')));
    expect(screen.getByRole('status')).toHaveTextContent(
      'Added Rebase the work onto its target. With it: Hand a conflict to its resolver when it is stuck, Your task when that fails, and their ways back to it.');
    expect(step('Hand a conflict to its resolver')).toBeInTheDocument();

    await user.keyboard('{Enter}');
    const drawer = await screen.findByRole('dialog', { name: 'Rebase the work onto its target' });
    expect(within(drawer).getByRole('combobox', { name: "What Rebase the work onto its target's input work is given" })).toHaveTextContent("The work's work");
    await user.keyboard('{Escape}');

    await user.type(screen.getByRole('textbox', { name: 'Its name' }), 'Rebased for engine');
    const review = screen.getByRole('button', { name: 'Review and save' });
    await waitFor(() => expect(review).toBeEnabled());
    await user.click(review);
    await user.click(await screen.findByRole('button', { name: 'Save as a new workflow' }));
    const saving = () => invoke.mock.calls.filter((call) => call[1] === 'WORKFLOW_SAVE')
      .map((call) => (call[2] as { payload: { plan: boolean; graph: { wires: Wire[] } } }).payload).find((payload) => payload.plan === false);
    await waitFor(() => expect(saving()).toBeDefined());
    expect(saving()!.graph.wires.map(said)).toEqual(expect.arrayContaining([
      'when:rebase>landing?ready', 'data:work.work>rebase.work', 'when:rebase>resolve?conflicted', 'when:rebase>resolve?uncommitted',
      'data:rebase.problem>resolve.problem', 'data:work.work>resolve.work', 'back:resolve>rebase@resolved', 'failed:resolve>human',
      'back:human>rebase@done',
    ]));
  });
});
