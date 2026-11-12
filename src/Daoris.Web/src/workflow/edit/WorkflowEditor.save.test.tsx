import { useEffect, type ReactNode } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import i18n from '../../locales/i18n';
import { MERGE_GRAPH } from '../graphFixtures';
import type { WorkflowSaveAnswer, WorkflowUseAnswer } from './answers';
import { APPEND, HOLDS, LIST, PALETTE, REFUSED } from './editFixtures';

// The editor's save and choice. *Review and save* waits for this draft's holding
// judgement and says the changed steps and every place an append reaches; an append's consent is bound to exactly the places the driver
// named and resets when that set changes; a new workflow is saved by its name alone; *Use here* is offered only on a draft that is the
// version it is saved as, and reads the plan of the choice where the driver announces one.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn(async (..._: unknown[]): Promise<unknown> => null) }));

vi.mock('@shenora/react', async (original) => ({
  ...(await original<typeof import('@shenora/react')>()),
  getBridge: () => ({ invoke }),
  useShenora: () => ({ isAvailable: true }),
}));

import { useWorkflowEditor } from '../../bridge/repositories/workflowEdit';
import { WorkflowEditor, type WorkflowEditorProps } from './WorkflowEditor';

const STRUCTURE = { works: [], places: [], agents: [], roles: [] };

/** What the driver answers: a plan and a save, a choice and its plan, and the newest version read. */
const driver: {
  plan: (payload: Record<string, unknown>) => WorkflowSaveAnswer;
  save: (payload: Record<string, unknown>) => WorkflowSaveAnswer;
  use: (payload: Record<string, unknown>) => WorkflowUseAnswer;
  announced: string[];
  show: (payload: Record<string, unknown>) => Promise<unknown> | unknown;
} = { plan: () => HOLDS, save: () => HOLDS, use: () => USED, announced: [], show: () => SHOWN };

const USED: WorkflowUseAnswer = { done: true, refused: null, message: 'engine now follows it.', changed: true, reach: [] };

const SHOWN = {
  id: 'docs-release', name: 'Docs release', version: 4, newest: true, versions: [1, 2, 3, 4], at: null, door: null, digest: 'cd', schema: 2,
  line: 'x', body: MERGE_GRAPH, problems: [],
};

beforeEach(() => {
  driver.plan = () => HOLDS;
  driver.save = (payload) => ({ ...HOLDS, id: String(payload.id), version: 1, written: true, message: `${String(payload.id)} v1, a new workflow, from the page.` });
  driver.use = () => USED;
  driver.announced = [];
  driver.show = () => SHOWN;
  invoke.mockImplementation(async (_module, type, options) => {
    const payload = (options as { payload: Record<string, unknown> }).payload;
    switch (type) {
      case 'WORKFLOW_LIST': return { ...LIST, ...(driver.announced.length > 0 ? { use: driver.announced } : {}) };
      case 'WORKFLOW_PALETTE': return PALETTE;
      case 'ROLE_STRUCTURE': return STRUCTURE;
      case 'WORKFLOW_SHOW': return driver.show(payload);
      case 'WORKFLOW_SAVE': return payload.plan ? driver.plan(payload) : driver.save(payload);
      case 'WORKFLOW_USE': return driver.use(payload);
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

const asked = (type: string) => invoke.mock.calls.filter((call) => call[1] === type).map((call) => (call[2] as { payload: Record<string, unknown> }).payload);
const savedWith = () => asked('WORKFLOW_SAVE').filter((each) => each.plan === false);

const APPENDING = { graph: MERGE_GRAPH, id: 'docs-release', name: 'Docs release', base: 2 };
const chart = () => screen.getByRole('list', { name: 'How the steps go on' });
const step = (name: string | RegExp) => within(chart()).getByRole('listitem', { name });

/** A step duplicated: one change to the draft. */
async function edit(user: ReturnType<typeof userEvent.setup>, which = 'Your review') {
  await screen.findByRole('list', { name: 'How the steps go on' });
  await user.click(step(which));
  await user.click(await screen.findByRole('button', { name: 'Duplicate' }));
}

const reviewButton = () => screen.getByRole('button', { name: 'Review and save' });
const holds = async () => { await waitFor(() => expect(reviewButton()).toBeEnabled()); };

describe('Review and save', () => {
  it('waits for this draft\'s holding judgement, and for the next after an edit', async () => {
    const user = userEvent.setup();
    let refuse = false;
    driver.plan = () => (refuse ? REFUSED : HOLDS);
    render(wrap(<Editor startFrom={APPENDING} />));
    await holds();
    refuse = true;
    await edit(user);
    // The answer for the draft before is no answer for this one: nothing to review until the new one lands, and none to save when it refuses.
    expect(reviewButton()).toBeDisabled();
    await screen.findByText('3 to put right');
    expect(reviewButton()).toBeDisabled();
    expect(asked('WORKFLOW_SAVE').every((each) => each.plan === true)).toBe(true);
  });

  it('says the steps it changes, then the places an append reaches, and saves only on consent to exactly those', async () => {
    const user = userEvent.setup();
    driver.plan = () => APPEND;
    driver.save = (payload) => ({ ...APPEND, written: true, message: 'docs-release v2 → v3.', version: payload.base === 2 ? 3 : null });
    render(wrap(<Editor startFrom={APPENDING} />));
    await edit(user);
    await holds();
    await user.click(reviewButton());

    const review = await screen.findByRole('region', { name: 'Before it is saved' });
    expect(within(review).getByText(/New: Your review/)).toBeInTheDocument();
    expect(within(review).getByText('the repository engine')).toBeInTheDocument();
    expect(within(review).getByText('the workspace aurora, for its kind of task docs')).toBeInTheDocument();
    const save = within(review).getByRole('button', { name: 'Save v3' });
    expect(save).toBeDisabled();
    await user.click(within(review).getByRole('checkbox', { name: 'Let these places follow it' }));
    expect(save).toBeEnabled();
    await user.click(save);

    await waitFor(() => expect(savedWith()).toHaveLength(1));
    expect(savedWith()[0]).toMatchObject({ id: 'docs-release', base: 2, authorize: ['repository:engine:default', 'workspace:aurora:kind:docs'] });
    expect(await screen.findByText('docs-release v2 → v3.')).toBeInTheDocument();
  });

  it('resets consent when the places an append reaches are not the ones consent was given to', async () => {
    const user = userEvent.setup();
    const third = { level: 'repository' as const, name: 'wheel', role: 'default' as const, kind: null, key: 'repository:wheel:default' };
    driver.plan = () => APPEND;
    // The driver finds a third place that chose it since the plan: it refuses, naming all three.
    driver.save = (payload) => (Array.isArray(payload.authorize) && payload.authorize.length === 3
      ? { ...APPEND, scopes: [...APPEND.scopes, third], written: true, message: 'saved' }
      : { ...APPEND, scopes: [...APPEND.scopes, third], done: false, refused: 'reach', version: null, message: 'docs-release is chosen in 3 places.' });
    render(wrap(<Editor startFrom={APPENDING} />));
    await edit(user);
    await holds();
    await user.click(reviewButton());
    await user.click(await screen.findByRole('checkbox', { name: 'Let these places follow it' }));
    await user.click(screen.getByRole('button', { name: 'Save v3' }));

    expect(await screen.findByText('the repository wheel')).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: 'Let these places follow it' })).not.toBeChecked();
    expect(screen.getByRole('button', { name: 'Save v3' })).toBeDisabled();
    await user.click(screen.getByRole('checkbox', { name: 'Let these places follow it' }));
    await user.click(screen.getByRole('button', { name: 'Save v3' }));
    await waitFor(() => expect(savedWith()).toHaveLength(2));
    expect([...(savedWith()[1]!.authorize as string[])].sort()).toEqual(['repository:engine:default', 'repository:wheel:default', 'workspace:aurora:kind:docs']);
  });

  it('closes the review when the draft changes under it', async () => {
    const user = userEvent.setup();
    render(wrap(<Editor startFrom={APPENDING} />));
    await holds();
    await user.click(reviewButton());
    await screen.findByRole('region', { name: 'Before it is saved' });
    await edit(user);
    expect(screen.queryByRole('region', { name: 'Before it is saved' })).toBeNull();
  });

  it('saves a new workflow by its name alone, with no base and no consent', async () => {
    const user = userEvent.setup();
    render(wrap(<Editor startFrom={{ graph: MERGE_GRAPH, name: 'Merge workflow' }} />));
    await holds();
    const name = screen.getByRole('textbox', { name: 'Its name' });
    await user.clear(name);
    await user.type(name, 'Release for engine');
    await user.click(reviewButton());
    const review = await screen.findByRole('region', { name: 'Before it is saved' });
    expect(within(review).getByText('A new workflow of 8 steps.')).toBeInTheDocument();
    expect(within(review).queryByRole('checkbox')).toBeNull();
    await user.click(within(review).getByRole('button', { name: 'Save as a new workflow' }));

    await waitFor(() => expect(savedWith()).toHaveLength(1));
    expect(savedWith()[0]).toEqual({ id: 'release-for-engine', graph: expect.anything(), name: 'Release for engine', plan: false });
    // Saved, it is the version the draft is, and chosen next.
    expect(await screen.findByRole('button', { name: 'Use here' })).toBeInTheDocument();
  });

  it('says a stale base in the driver\'s words and reads the newest version again, keeping the draft', async () => {
    const user = userEvent.setup();
    driver.save = () => ({ ...HOLDS, done: false, refused: 'stale', version: null, message: 'docs-release is at v4 now, not v2: read it again.' });
    render(wrap(<Editor startFrom={APPENDING} />));
    await edit(user);
    await holds();
    await user.click(reviewButton());
    await user.click(await screen.findByRole('button', { name: 'Save v3' }));
    expect(await screen.findByText('docs-release is at v4 now, not v2: read it again.')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Read it again' }));
    await waitFor(() => expect(asked('WORKFLOW_SHOW').some((each) => each.id === 'docs-release')).toBe(true));
    await user.click(await screen.findByRole('button', { name: 'Review and save' }));
    expect(await screen.findByRole('button', { name: 'Save v5' })).toBeInTheDocument();
    // The draft is still the edited one: the duplicate stays.
    expect(within(screen.getByRole('region', { name: 'Before it is saved' })).getByText(/New: Your review/)).toBeInTheDocument();
  });
});

describe('Use here', () => {
  async function saved(user: ReturnType<typeof userEvent.setup>) {
    await edit(user);
    await holds();
    await user.click(reviewButton());
    await user.click(await screen.findByRole('button', { name: 'Save as a new workflow' }));
  }

  it('is offered only while the draft is the version it was saved as, and again once an edit is undone', async () => {
    const user = userEvent.setup();
    render(wrap(<Editor startFrom={{ graph: MERGE_GRAPH, name: 'Merge workflow' }} />));
    expect(screen.queryByRole('button', { name: /^Use here/ })).toBeNull();
    await saved(user);
    expect(await screen.findByRole('button', { name: 'Use here' })).toBeInTheDocument();

    await edit(user, 'The landing');
    expect(screen.queryByRole('button', { name: /^Use here/ })).toBeNull();
    expect(screen.getByText('Save these changes first: Use here chooses the version as it is saved.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Undo' }));
    expect(await screen.findByRole('button', { name: 'Use here' })).toBeInTheDocument();
  });

  it('is offered on an opened saved version that has not been changed, and asks for the route\'s kind', async () => {
    const user = userEvent.setup();
    render(wrap(<Editor startFrom={APPENDING} route="kind:docs" />));
    await user.click(await screen.findByRole('button', { name: 'Use here' }));
    await waitFor(() => expect(asked('WORKFLOW_USE')).toHaveLength(1));
    expect(asked('WORKFLOW_USE')[0]).toEqual({ repository: 'engine', workflow: 'docs-release', version: 2, kind: 'docs' });
  });

  it('reads the plan of the choice where the driver announces one, and presses it with the plan\'s token', async () => {
    const user = userEvent.setup();
    driver.announced = ['plan'];
    driver.use = (payload) => (payload.plan
      ? { ...USED, done: false, plan: true, expected: 'tok-1', lowers: ['Your review stays yours.'], places: [{ repository: 'engine', was: 'Current', now: 'Docs release v2', lowers: [] }] }
      : USED);
    render(wrap(<Editor startFrom={APPENDING} />));
    await user.click(await screen.findByRole('button', { name: 'Use here…' }));
    expect(await screen.findByText('Your review stays yours.')).toBeInTheDocument();
    expect(asked('WORKFLOW_USE')[0]).toMatchObject({ plan: true, workflow: 'docs-release', version: 2 });
    await user.click(await screen.findByRole('button', { name: 'Use here' }));
    await waitFor(() => expect(asked('WORKFLOW_USE')).toHaveLength(2));
    expect(asked('WORKFLOW_USE')[1]).toEqual({ repository: 'engine', workflow: 'docs-release', version: 2, expected: 'tok-1' });
    expect(await screen.findByText('engine now follows it.')).toBeInTheDocument();
  });

  it('keeps what a save said beside a choice the driver refuses', async () => {
    const user = userEvent.setup();
    driver.use = () => ({ ...USED, done: false, refused: 'stale', changed: false, message: 'docs-release is at v2 now, not v1: read it again.' });
    render(wrap(<Editor startFrom={{ graph: MERGE_GRAPH, name: 'Merge workflow' }} />));
    await saved(user);
    await user.click(await screen.findByRole('button', { name: 'Use here' }));
    expect(await screen.findByText('docs-release is at v2 now, not v1: read it again.')).toBeInTheDocument();
    expect(screen.getByText('merge-workflow v1, a new workflow, from the page.')).toBeInTheDocument();
  });

  it('says it in Chinese', async () => {
    await i18n.changeLanguage('zh');
    const user = userEvent.setup();
    render(wrap(<Editor startFrom={APPENDING} />));
    await waitFor(() => expect(screen.getByRole('button', { name: '审阅并保存' })).toBeEnabled());
    await user.click(screen.getByRole('button', { name: '审阅并保存' }));
    expect(await screen.findByRole('region', { name: '保存之前' })).toBeInTheDocument();
    await act(async () => {});
    expect(screen.getByRole('button', { name: '在此使用' })).toBeInTheDocument();
  });
});
