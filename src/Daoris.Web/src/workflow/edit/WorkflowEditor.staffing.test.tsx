import { useEffect, type ReactNode } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import i18n from '../../locales/i18n';
import { MERGE_GRAPH } from '../graphFixtures';
import type { WorkflowSaveAnswer } from './answers';
import { HOLDS, LIST, PALETTE } from './editFixtures';

// SETUPUX1t (D221 §2.4; the Setup design's §3 flow 7): a step's *Taken by* offers the roles that take its work and cover this place, each
// named with its place, and *Give engine its own role for …* opens the one role editor beside the step: it saves the role over `SET_ROLE`
// at once, returns to the draft as it was, and the step then names the new role. A refused save keeps both forms.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn(async (..._: unknown[]): Promise<unknown> => null) }));

vi.mock('@shenora/react', async (original) => ({
  ...(await original<typeof import('@shenora/react')>()),
  getBridge: () => ({ invoke }),
  useShenora: () => ({ isAvailable: true }),
}));

import { useWorkflowEditor } from '../../bridge/repositories/workflowEdit';
import { WorkflowEditor, type WorkflowEditorProps } from './WorkflowEditor';

type Row = {
  id: string; name: Record<string, string>; seeded: boolean; state: string; takes: string[]; scopeKind: string; scopeId: string | null; revision: number;
  choices: { agent: string; model?: string | null; effort?: string | null }[];
};

const role = (id: string, name: string, takes: string[], scopeKind: string, scopeId: string | null, seeded = false): Row => ({
  id, name: { en: name, zh: name }, seeded, state: seeded ? 'seed' : 'set', takes, scopeKind, scopeId, revision: 0, choices: [{ agent: 'claude-code' }],
});

const ROSTER = {
  settingsPath: 'x', adapter: 'claude-code',
  harnesses: [{
    harness: 'claude-code', product: 'Claude Code', maker: 'Anthropic', wire: 'pipe', present: true, version: 'claude 9.9.9', problem: null,
    machineDefault: null, pinned: null, managed: null, pinnable: true, ownLogin: 'in', workspaceDefaults: [], profiles: [],
  }],
};

let roles: Row[] = [];
let refuse: string | false = false;

const setRoles = () => invoke.mock.calls.filter((call) => call[1] === 'SET_ROLE').map((call) => (call[2] as { payload: Record<string, unknown> }).payload);
const asked = (type: string) => invoke.mock.calls.filter((call) => call[1] === type).map((call) => (call[2] as { payload: Record<string, unknown> }).payload);

beforeEach(() => {
  refuse = false;
  roles = [
    role('build', 'Builder', ['build'], 'machine', null, true),
    role('aurora-build', 'Aurora builder', ['build'], 'workspace', 'aurora'),
    role('site-build', 'Site builder', ['build'], 'repository', 'site'),
    role('designer', 'Designer', ['design'], 'machine', null),
  ];
  invoke.mockImplementation(async (_module, type, options) => {
    const payload = (options as { payload: Record<string, unknown> }).payload;
    switch (type) {
      case 'WORKFLOW_LIST': return LIST;
      case 'WORKFLOW_PALETTE': return PALETTE;
      case 'ROLE_STRUCTURE': return { works: [], places: [], agents: [], roles };
      case 'ROLES': return { roles, works: ['build', 'design', 'review'], kept: true, suggested: [] };
      case 'HARNESSES': return ROSTER;
      case 'ACTORS': return { actors: [] };
      case 'SET_ROLE': {
        if (refuse) throw new Error(refuse);
        const press = payload as { id: string; role: { name: Record<string, string>; takes: string[]; scope: { kind: string; id: string | null } } };
        roles = [...roles.filter((each) => each.id !== press.id), {
          id: press.id, name: press.role.name, seeded: false, state: 'set', takes: press.role.takes, scopeKind: press.role.scope.kind,
          scopeId: press.role.scope.id, revision: 1, choices: [{ agent: 'claude-code' }],
        }];
        return null;
      }
      case 'WORKFLOW_SAVE': {
        const graph = payload.graph as { nodes: { id: string; options?: { role?: string } }[] };
        const named = graph.nodes.find((node) => node.options?.role && !roles.some((each) => each.id === node.options!.role));
        const answer: WorkflowSaveAnswer = named
          ? { ...HOLDS, done: false, refused: 'refused', version: null, problems: [{ code: 'role', wire: null, node: named.id, words: 'no such role.' }] }
          : { ...HOLDS, id: String(payload.id), version: 1, written: !payload.plan, message: payload.plan ? 'planned' : 'saved as the page.' };
        return answer;
      }
      case 'WORKFLOW_USE': return { done: true, refused: null, message: 'engine now follows it.', changed: true, reach: [] };
      default: return null;
    }
  });
});

afterEach(async () => {
  invoke.mockReset();
  vi.restoreAllMocks();
  await i18n.changeLanguage('en');
});

function Editor(props: Partial<WorkflowEditorProps>) {
  const bridge = useWorkflowEditor({ repository: 'engine', workspace: 'aurora' });
  const { onOpen } = bridge;
  useEffect(() => onOpen?.(true), [onOpen]);
  return <WorkflowEditor {...bridge} place={{ repository: 'engine' }} {...props} />;
}

const wrap = (node: ReactNode) => (
  <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
    <Tooltip.Provider>{node}</Tooltip.Provider>
  </QueryClientProvider>
);

/** The work's step selected, its inspector beside the chart. */
async function selectWork(user: ReturnType<typeof userEvent.setup>) {
  const chart = await screen.findByRole('list', { name: 'How the steps go on' });
  await user.click(within(chart).getByRole('listitem', { name: 'The work' }));
  return screen.findByRole('combobox', { name: 'Taken by' });
}

describe('a step\'s Taken by', () => {
  it('offers the roles that take its work and cover this place, each with its place', async () => {
    const user = userEvent.setup();
    render(wrap(<Editor startFrom={{ graph: MERGE_GRAPH, name: 'Merge workflow' }} />));
    await waitFor(() => expect(asked('ROLE_STRUCTURE').length).toBeGreaterThan(0));
    const taken = await selectWork(user);
    await waitFor(() => expect(taken).toBeEnabled());
    await user.click(taken);
    const list = await screen.findByRole('listbox');
    const names = within(list).getAllByRole('option').map((each) => each.textContent);
    expect(names).toEqual(['Its work\'s own role', 'Builder · this machine', 'Aurora builder · aurora', 'Designer · this machine']);
  });

  it('gives the place a role of its own, saves it at once and returns to the draft with the step naming it, to be saved and used', async () => {
    const user = userEvent.setup();
    render(wrap(<Editor startFrom={{ graph: MERGE_GRAPH, name: 'Merge workflow' }} />));
    await selectWork(user);
    await user.click(await screen.findByRole('button', { name: 'Give engine its own role for design and decisions…' }));

    const form = await screen.findByRole('form', { name: 'A new role' });
    expect(within(form).getByText('It will then take all of engine\'s design and decisions, not only this step\'s.')).toBeInTheDocument();
    await user.click(within(form).getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(setRoles()).toHaveLength(1));
    expect(setRoles()[0]).toMatchObject({
      change: 'set', id: 'designer-2', role: { takes: ['design'], scope: { kind: 'repository', id: 'engine' } }, choices: [{ agent: 'claude-code' }],
    });
    // The form is given up, the step names the role, and both saves are said.
    await waitFor(() => expect(screen.queryByRole('form', { name: 'A new role' })).toBeNull());
    expect(await screen.findByText(/Designer · engine is saved for engine, and this step now names it/)).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Taken by' })).toHaveTextContent('Designer · engine');

    // The draft is judged with the role, saved with it, and used.
    await user.click(screen.getByRole('button', { name: 'Close' }));
    const review = screen.getByRole('button', { name: 'Review and save' });
    await waitFor(() => expect(review).toBeEnabled());
    await user.click(review);
    await user.click(await screen.findByRole('button', { name: 'Save as a new workflow' }));
    await waitFor(() => expect(asked('WORKFLOW_SAVE').some((each) => each.plan === false)).toBe(true));
    const written = asked('WORKFLOW_SAVE').find((each) => each.plan === false)!.graph as { nodes: { id: string; options?: { role?: string } }[] };
    expect(written.nodes.find((node) => node.id === 'work')?.options?.role).toBe('designer-2');
    await user.click(await screen.findByRole('button', { name: 'Use here' }));
    await waitFor(() => expect(asked('WORKFLOW_USE')).toHaveLength(1));
  });

  it('keeps both forms when the role\'s save is refused, in the driver\'s sentence', async () => {
    const user = userEvent.setup();
    refuse = 'The role door refused it.';
    render(wrap(<Editor startFrom={{ graph: MERGE_GRAPH, name: 'Merge workflow' }} />));
    await selectWork(user);
    await user.click(await screen.findByRole('button', { name: 'Give engine its own role for design and decisions…' }));
    const form = await screen.findByRole('form', { name: 'A new role' });
    await user.click(within(form).getByRole('button', { name: 'Save' }));

    expect(await screen.findByText('The role door refused it.')).toBeInTheDocument();
    expect(screen.getByRole('form', { name: 'A new role' })).toBeInTheDocument();
    // The step still shows its own choice, and the draft is as it was.
    expect(screen.getByRole('combobox', { name: 'Taken by' })).toHaveTextContent('Its work\'s own role');
    expect(screen.getByRole('button', { name: 'Undo', hidden: true })).toBeDisabled();

    // Given up, the draft goes on.
    await user.click(within(form).getByRole('button', { name: 'Never mind' }));
    expect(screen.queryByRole('form', { name: 'A new role' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Give engine its own role for design and decisions…' })).toBeInTheDocument();
  });

  it('offers to edit the role the place already has for the work, instead of giving it another', async () => {
    const user = userEvent.setup();
    roles = [...roles, role('designer-engine', 'Designer', ['design'], 'repository', 'engine')];
    render(wrap(<Editor startFrom={{ graph: MERGE_GRAPH, name: 'Merge workflow' }} />));
    await selectWork(user);
    await user.click(await screen.findByRole('button', { name: 'Edit Designer · engine…' }));
    expect(screen.queryByRole('button', { name: /^Give engine/ })).toBeNull();
    const form = await screen.findByRole('form', { name: 'Designer' });
    await user.click(within(form).getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(setRoles()).toHaveLength(1));
    expect(setRoles()[0]).toMatchObject({ change: 'set', id: 'designer-engine', revision: 0 });
  });

  it('does not carry an open role editor to the next step chosen (REVIEW2 P6)', async () => {
    const user = userEvent.setup();
    const two = {
      ...MERGE_GRAPH,
      nodes: [
        { id: 'work', kind: 'claimed', version: 1, reach: 'observed' },
        { id: 'second', kind: 'claimed', version: 1, reach: 'observed' },
      ],
      wires: [{ id: 'a', kind: 'after', from: 'work', to: 'second' }],
    };
    render(wrap(<Editor startFrom={{ graph: two, name: 'Two' }} />));
    const chart = await screen.findByRole('list', { name: 'How the steps go on' });
    await user.click(within(chart).getAllByRole('listitem', { name: 'The work' })[0]!);
    await user.click(await screen.findByRole('button', { name: 'Give engine its own role for design and decisions…' }));
    expect(await screen.findByRole('form', { name: 'A new role' })).toBeInTheDocument();

    const second = chart.querySelector<HTMLElement>('[data-flow-node="second"]')!;
    await fireEvent.click(second.closest<HTMLElement>('[role="listitem"]') ?? second);
    expect(screen.queryByRole('form', { name: 'A new role' })).toBeNull();
  });

  it('says it in Chinese', async () => {
    await i18n.changeLanguage('zh');
    const user = userEvent.setup();
    render(wrap(<Editor startFrom={{ graph: MERGE_GRAPH, name: 'Merge workflow' }} />));
    const chart = await screen.findByRole('list', { name: '各步骤如何继续' });
    await user.click(within(chart).getByRole('listitem', { name: '工作本身' }));
    expect(await screen.findByRole('combobox', { name: '由谁接下' })).toBeInTheDocument();
    expect(await screen.findByRole('button', { name: /^让 engine 有自己的角色/ })).toBeInTheDocument();
  });
});
