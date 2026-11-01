import { useEffect, type ReactNode } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import i18n from '../../locales/i18n';
import { LIST, PALETTE } from './editFixtures';

// WORKFLOWEDIT1b: the step editor composes a workflow from the palette, each change judged by a planned save, then saves it and uses it
// here, through a stubbed bridge: the documents workflow (work, then its landing) and the release workflow (work, its checks, your
// review, the landing, then the Local release plugin's find, release and publish), and a refusal said at the way on it names.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn(async (..._: unknown[]): Promise<unknown> => null) }));

vi.mock('@shenora/react', async (original) => ({
  ...(await original<typeof import('@shenora/react')>()),
  getBridge: () => ({ invoke }),
  useShenora: () => ({ isAvailable: true }),
}));

import { useWorkflowEditor } from '../../bridge/repositories/workflowEdit';
import { WorkflowEditor } from './WorkflowEditor';

type Graph = { nodes: { id: string }[]; wires: { id: string; kind: string; from: string; to: string }[] };

const STRUCTURE = {
  works: [], places: [], agents: [],
  roles: [
    { id: 'build', state: 'seed', seeded: true, nameEn: 'Build', nameZh: '开发', takes: ['build'], revision: 0 },
    { id: 'opinion', state: 'seed', seeded: true, nameEn: 'Second opinion', nameZh: '第二意见', takes: ['review'], revision: 0 },
  ],
};

/** What the driver answers, a plan judged by the graph it is handed: a way back into the landing goes forward, refused at that wire. */
function answer(type: string, payload: Record<string, unknown>): unknown {
  switch (type) {
    case 'WORKFLOW_LIST': return LIST;
    case 'WORKFLOW_PALETTE': return PALETTE;
    case 'ROLE_STRUCTURE': return STRUCTURE;
    case 'WORKFLOW_SAVE': {
      const graph = payload.graph as Graph;
      const forward = graph.wires.find((wire) => wire.kind === 'back' && wire.to === 'land');
      const base = { id: payload.id, version: 1, written: !payload.plan, appended: false, scopes: [], reach: [] };
      return forward
        ? { ...base, done: false, refused: 'refused', written: false, message: 'v1: its graph does not hold.',
          problems: [{ code: 'back-forward', wire: forward.id, node: null, words: `the back wire \`${forward.id}\` goes forward.` }] }
        : { ...base, done: true, refused: null, message: payload.plan ? 'planned' : `\`${String(payload.id)}\` v1, a new workflow, from the page.`, problems: [] };
    }
    case 'WORKFLOW_USE': return { done: true, refused: null, message: '`engine` now chooses it as its default.', changed: true, reach: [] };
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

const user = () => userEvent.setup();

async function choose(scope: HTMLElement, name: string, option: string) {
  within(scope).getByRole('combobox', { name }).focus();
  await user().keyboard('{Enter}');
  await user().click(await screen.findByRole('option', { name: option }));
}

const steps = () => screen.getByRole('list', { name: 'Steps' });
const step = (title: string) => within(steps()).getByRole('listitem', { name: title });

async function add(title: string) {
  await choose(document.body, 'Add a step', title);
  await screen.findByRole('heading', { name: title, level: 4 });
}

/** The way `from` goes on to `to`, as the step's own form adds it. */
async function goOn(from: string, how: string, to: string, more: [string, string][] = []) {
  const item = step(from);
  await choose(item, 'How it goes on', how);
  await choose(item, 'To', to);
  for (const [name, option] of more) await choose(item, name, option);
  await user().click(within(item).getByRole('button', { name: `Add the way on from ${from}` }));
}

const asked = (type: string) => invoke.mock.calls.filter((call) => call[1] === type).map((call) => (call[2] as { payload: Record<string, unknown> }).payload);

describe('the step editor', () => {
  afterEach(async () => { invoke.mockReset(); await i18n.changeLanguage('en'); });

  it('composes the documents workflow, judges it as it is drawn, saves it under a new id and uses it here', async () => {
    invoke.mockImplementation(async (_module, type, options) => answer(type as string, (options as { payload: Record<string, unknown> }).payload));
    render(wrap(<Editor />));

    await screen.findByRole('combobox', { name: 'Add a step' });
    await add('The work');
    await add('The landing');
    await goOn('The work', 'Once it is reached', 'The landing (land)');
    await choose(step('The landing'), 'method', 'merge');

    // The landing may end a workflow, so the first such step added is where the work ends.
    expect(within(step('The landing')).getByText('ends here')).toBeInTheDocument();
    expect(within(step('The work')).getByText('The landing, once it is reached')).toBeInTheDocument();
    await screen.findByText('These steps hold, and can be saved.');
    expect(asked('WORKFLOW_SAVE').at(-1)).toMatchObject({ plan: true });

    await user().type(screen.getByRole('textbox', { name: 'Its id' }), 'docs');
    await user().type(screen.getByRole('textbox', { name: 'Its name' }), 'Docs');
    await user().click(screen.getByRole('button', { name: 'Save as new' }));

    await screen.findByText(/v1, a new workflow, from the page\./);
    expect(asked('WORKFLOW_SAVE').find((each) => each.plan === false)).toEqual({
      id: 'docs', name: 'Docs', plan: false,
      graph: {
        schemaVersion: 2, entries: ['claimed'], end: 'land',
        nodes: [
          { id: 'claimed', kind: 'claimed', version: 1, reach: 'observed' },
          { id: 'land', kind: 'land', version: 1, reach: 'performed', options: { method: 'merge' } },
        ],
        wires: [{ id: 'a', kind: 'after', from: 'claimed', to: 'land' }],
      },
    });

    // Saved, it is a version this place can choose; another change now appends against it.
    expect(screen.getByRole('button', { name: 'Save v2' })).toBeInTheDocument();
    await user().click(screen.getByRole('button', { name: 'Use here' }));
    await screen.findByText(/now chooses it as its default\./);
    expect(asked('WORKFLOW_USE')).toEqual([{ repository: 'engine', workflow: 'docs', version: 1 }]);
  });

  it('composes the release workflow with a decision, a way back and what each step hands the next, and says a refusal at its way on', async () => {
    invoke.mockImplementation(async (_module, type, options) => answer(type as string, (options as { payload: Record<string, unknown> }).payload));
    render(wrap(<Editor />));

    await screen.findByRole('combobox', { name: 'Add a step' });
    for (const title of ['The work', 'The checks', 'Your review', 'The landing', 'find by local-release', 'release by local-release', 'publish by local-release']) {
      await add(title);
    }
    await goOn('The work', 'Once it is reached', 'The checks (check)');
    await goOn('The checks', 'Once it is reached', 'Your review (review)');
    await goOn('Your review', 'Once it is reached', 'The landing (land)');
    await goOn('The landing', 'Once it is reached', 'find by local-release (find)');
    await goOn('find by local-release', 'When it comes to an outcome', 'release by local-release (release)', [['Outcome', 'release']]);
    await goOn('find by local-release', 'With what it made', 'release by local-release (release)', [['What it made', 'release'], ['As the input', 'release']]);
    await goOn('release by local-release', 'Once it is reached', 'publish by local-release (publish)');
    await goOn('release by local-release', 'With what it made', 'publish by local-release (publish)', [['What it made', 'release'], ['As the input', 'release']]);
    await goOn('Your review', 'Back to an earlier step', 'The work (claimed)', [['When', 'When you ask for changes']]);
    await user().click(within(step('The landing')).getByRole('button', { name: 'End the work at The landing' }));

    // The person's role for the work is offered from the roles that do its work.
    await choose(step('The work'), 'Its role', 'Build');

    await screen.findByText('These steps hold, and can be saved.');

    // A way back into the landing goes forward: the refusal is said at that way, in the page's words, with the driver's beneath.
    await goOn('Your review', 'Back to an earlier step', 'The landing (land)', [['When', 'When you ask for changes']]);
    const way = await within(step('Your review')).findByText('A way back goes only to a step that comes before this one.');
    // The refusal is its own row under the way it names.
    expect(way.parentElement!.closest('li')).toHaveTextContent('Back to The landing, when you ask for changes');
    expect(screen.getByText('These steps do not hold yet:')).toBeInTheDocument();
    await user().click(within(step('Your review')).getByRole('button', {
      name: 'Remove the way on from Your review: Back to The landing, when you ask for changes',
    }));
    await screen.findByText('These steps hold, and can be saved.');

    await user().type(screen.getByRole('textbox', { name: 'Its id' }), 'release');
    await user().click(screen.getByRole('button', { name: 'Save as new' }));
    await screen.findByText(/v1, a new workflow, from the page\./);

    const graph = asked('WORKFLOW_SAVE').find((each) => each.plan === false)!.graph as Graph & { end: string; nodes: Record<string, unknown>[] };
    expect(graph.end).toBe('land');
    expect(graph.nodes.map((node) => node.id)).toEqual(['claimed', 'check', 'review', 'land', 'find', 'release', 'publish']);
    expect(graph.nodes[0]).toMatchObject({ options: { role: 'build' } });
    expect(graph.nodes[4]).toMatchObject({ kind: 'local-release/find', outcomes: ['release', 'none'], outputs: { release: 'local-release/release@1' } });
    expect(graph.wires).toEqual(expect.arrayContaining([
      { id: 'e', kind: 'when', from: 'find', outcome: 'release', to: 'release' },
      { id: 'f', kind: 'data', from: 'find.release', to: 'release.release' },
      { id: 'h', kind: 'data', from: 'release.release', to: 'publish.release' },
      { id: 'i', kind: 'back', from: 'review', on: 'rejected', to: 'claimed' },
    ]));
  });

  it("starts from one of Daoris's defaults, its steps as the default holds them, in both languages", async () => {
    invoke.mockImplementation(async (_module, type, options) => answer(type as string, (options as { payload: Record<string, unknown> }).payload));
    render(wrap(<Editor />));

    await screen.findByRole('combobox', { name: 'Start from' });
    await choose(document.body, 'Start from', 'Documents');
    expect(within(steps()).getAllByRole('heading', { level: 4 }).map((head) => head.textContent)).toEqual(['The work', 'The landing', 'On the line']);
    expect(within(step('On the line')).getByText('ends here')).toBeInTheDocument();

    await i18n.changeLanguage('zh');
    expect(await screen.findByRole('heading', { name: '编排工作流' })).toBeInTheDocument();
    expect(within(screen.getByRole('list', { name: '步骤' })).getByRole('heading', { name: '进入主线', level: 4 })).toBeInTheDocument();
  });
});
