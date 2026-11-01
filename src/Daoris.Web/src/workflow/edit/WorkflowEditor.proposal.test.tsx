import { useEffect, type ReactNode } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { registerAskFill } from '../../help/askFill';
import { draftToTell, noteTold } from '../../help/openDraft';
import type { HelpProposal } from '../../help/ProposalCard';
import i18n from '../../locales/i18n';
import { MERGE_GRAPH, RELEASE_GRAPH } from '../graphFixtures';
import { LIST, PALETTE } from './editFixtures';
import { draftOf, graphOf } from './graphDraft';
import { proposalOf, RELEASE_CARD, RELEASE_PROPOSAL } from './proposalFixtures';
import { canon, type ProposalShown } from './proposalTake';

// FLOWCHART2f (D223 §4, §7 row f, §8.6): Ask Daoris's proposal drawn on the editor's chart and taken whole or change by change, through a
// stubbed bridge. Each state the person meets: a proposal fresh, partly taken, a change gone stale, every change taken, the proposal set
// aside, and a take undone with its record. Opening the editor settles nothing, a take saves and chooses nothing, and the press naming every
// change taken or left goes once.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn(async (..._: unknown[]): Promise<unknown> => null) }));

vi.mock('@shenora/react', async (original) => ({
  ...(await original<typeof import('@shenora/react')>()),
  getBridge: () => ({ invoke }),
  useShenora: () => ({ isAvailable: true }),
}));

import { useWorkflowEditor } from '../../bridge/repositories/workflowEdit';
import { WorkflowEditor, type WorkflowEditorProps } from './WorkflowEditor';

const STRUCTURE = { works: [], places: [], agents: [], roles: [] };

function answer(type: string, payload: Record<string, unknown>): unknown {
  switch (type) {
    case 'WORKFLOW_LIST': return LIST;
    case 'WORKFLOW_PALETTE': return PALETTE;
    case 'ROLE_STRUCTURE': return STRUCTURE;
    case 'WORKFLOW_SAVE': return { id: payload.id, version: 1, written: false, appended: false, scopes: [], reach: [], done: true, refused: null, message: 'planned', problems: [] };
    default: return null;
  }
}

const PROPOSAL: ProposalShown = {
  ...RELEASE_PROPOSAL, why: 'You asked for a release after the line.', problems: 0, maker: { agent: 'Claude Code', models: ['claude-opus-5-5'] },
};

function Editor(props: Partial<WorkflowEditorProps>) {
  const bridge = useWorkflowEditor({ repository: 'engine', workspace: 'aurora' });
  const { onOpen } = bridge;
  useEffect(() => onOpen?.(true), [onOpen]);
  return <WorkflowEditor {...bridge} place={{ repository: 'engine' }} startFrom={{ graph: MERGE_GRAPH, name: 'Merge workflow' }} {...props} />;
}

const wrap = (node: ReactNode) => (
  <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
    <Tooltip.Provider>{node}</Tooltip.Provider>
  </QueryClientProvider>
);

const asked = (type: string) => invoke.mock.calls.filter((call) => call[1] === type).map((call) => (call[2] as { payload: Record<string, unknown> }).payload);
/** The graph the newest judge was handed: the draft as it is. */
const judgedGraph = () => asked('WORKFLOW_SAVE').at(-1)!.graph as Record<string, unknown>;
const stepIds = () => (judgedGraph().nodes as { id: string }[]).map((node) => node.id);
/** Nothing saved and nothing chosen: no save but a plan, and no use. */
const nothingSaved = () => {
  expect(asked('WORKFLOW_SAVE').every((each) => each.plan === true)).toBe(true);
  expect(asked('WORKFLOW_USE')).toEqual([]);
};

const bar = () => screen.getByRole('region', { name: /Ask Daoris proposes a change/ });
const rows = () => within(bar()).getByRole('list', { name: 'Its changes' });
const row = (id: string) => rows().querySelector<HTMLElement>(`[data-change="${id}"]`)!;
const chart = () => screen.getByRole('list', { name: 'How the steps go on' });
const counts = (taken: number, left: number, open: number) => within(bar()).getByText(`${taken} taken, ${left} left, ${open} to decide.`);

let user: ReturnType<typeof userEvent.setup>;
let settle: ReturnType<typeof vi.fn<(id: string, taken: readonly string[], left: readonly string[]) => Promise<unknown>>>;
let dismiss: ReturnType<typeof vi.fn<(id: string) => Promise<unknown>>>;
let ask: ReturnType<typeof vi.fn<(text: string) => void>>;
let letGo: () => void;

beforeEach(() => {
  user = userEvent.setup();
  invoke.mockImplementation(async (_module, type, options) => answer(type as string, (options as { payload: Record<string, unknown> }).payload));
  settle = vi.fn(async (_id: string, _taken: readonly string[], _left: readonly string[]): Promise<unknown> => ({ message: 'Applied.' }));
  dismiss = vi.fn(async (_id: string): Promise<unknown> => ({ message: 'Dismissed.' }));
  // The window's Ask Daoris, whose box *Ask again* fills in.
  ask = vi.fn((_text: string) => {});
  letGo = registerAskFill(ask);
});

afterEach(async () => {
  letGo();
  invoke.mockReset();
  vi.restoreAllMocks();
  await i18n.changeLanguage('en');
});

/** The editor open on the proposal, once its reads are answered and the proposal drawn. */
async function open(props: Partial<WorkflowEditorProps> = {}) {
  render(wrap(<Editor proposal={PROPOSAL} onSettleProposal={settle} onDismissProposal={dismiss} {...props} />));
  await screen.findByRole('region', { name: /Ask Daoris proposes a change|问道衍提议一项更改/ });
}

describe('a proposal through the bridge', () => {
  /** The Merge workflow told to conversation `s1` as the editor holds it, and the card Ask Daoris drew against it. */
  function told() {
    const sent = draftToTell({ place: 'engine', graph: graphOf(draftOf(MERGE_GRAPH)) }, undefined)!;
    noteTold('s1', sent);
    const card: HelpProposal = { ...RELEASE_CARD, workflow: { ...RELEASE_CARD.workflow!, draft: sent.fingerprint } };
    invoke.mockImplementation(async (_module, type, options) => {
      const payload = (options as { payload: Record<string, unknown> }).payload;
      if (type === 'HELP_PROPOSALS') return payload.session === 's1' ? { session: 's1', proposals: [card, { ...card, id: 'p8', workflow: { ...card.workflow!, scope: 'docs' } }] } : null;
      if (type === 'HELP_APPLY' || type === 'HELP_DISMISS') return { message: 'Applied.', applied: type === 'HELP_APPLY' };
      return answer(type as string, payload);
    });
  }

  it("reads the place's proposal against a draft it told, and presses HELP_APPLY naming every change taken or left, once", async () => {
    told();
    render(wrap(<Editor />));
    await screen.findByRole('region', { name: /Ask Daoris proposes a change/ });
    counts(0, 0, 16);
    await user.click(within(bar()).getByRole('button', { name: 'Take all' }));
    await waitFor(() => expect(asked('HELP_APPLY')).toHaveLength(1));
    expect(asked('HELP_APPLY')[0]).toEqual({ id: 'p7', revision: 'r7', taken: RELEASE_PROPOSAL.changes.map((change) => change.id), left: [] });
    expect(asked('HELP_DISMISS')).toEqual([]);
    nothingSaved();
  });

  it('presses HELP_DISMISS for *Not now*', async () => {
    told();
    render(wrap(<Editor />));
    await screen.findByRole('region', { name: /Ask Daoris proposes a change/ });
    await user.click(within(bar()).getByRole('button', { name: 'Not now' }));
    await waitFor(() => expect(asked('HELP_DISMISS')).toEqual([{ id: 'p7' }]));
    expect(asked('HELP_APPLY')).toEqual([]);
  });
});

describe('a proposal drawn on the chart', () => {
  it('fresh: names who made it, marks its steps and ways in words, lists each change, and settles nothing by being opened', async () => {
    await open();
    expect(await screen.findByRole('region', { name: /Ask Daoris proposes a change/ })).toBeInTheDocument();
    expect(within(bar()).getByText('Proposed by Claude Code · claude-opus-5-5')).toBeInTheDocument();
    expect(within(bar()).getByText('Why: You asked for a release after the line.')).toBeInTheDocument();
    expect(within(bar()).getByText('Taken whole, its steps hold.')).toBeInTheDocument();
    counts(0, 0, 16);
    expect(within(rows()).getAllByRole('listitem')).toHaveLength(16);
    expect(row('step:+find')).toHaveTextContent('find by local-release, a new step');
    expect(row('way:+h')).toHaveTextContent('Takes with it: find by local-release, a new step');

    // The union: the new steps drawn beside the draft's, each marked *new* in words; the renumbered way drawn to remove and new.
    expect(within(within(chart()).getByRole('listitem', { name: /^find by local-release/ })).getByText('new')).toBeInTheDocument();
    expect(within(within(chart()).getByRole('listitem', { name: /^The checks/ })).getByText('new')).toBeInTheDocument();
    expect(within(within(chart()).getByRole('listitem', { name: /^The landing/ })).queryByText('new')).toBeNull();
    await waitFor(() => expect(asked('WORKFLOW_SAVE').length).toBeGreaterThan(0));
    // Drawn, the draft is unchanged: what is judged is the Merge workflow still.
    expect(canon(judgedGraph())).toBe(canon(MERGE_GRAPH));
    expect(settle).not.toHaveBeenCalled();
    expect(dismiss).not.toHaveBeenCalled();
    nothingSaved();
  });

  it('partly taken: a change taken with what it needs, in one step, and nothing saved', async () => {
    await open();
    await user.click(within(row('way:+h')).getByRole('button', { name: /^Take: A new way/ }));
    counts(4, 0, 12);
    for (const id of ['way:+h', 'step:+find', 'step:+release', 'way:~l']) expect(row(id)).toHaveAttribute('data-change-state', 'taken');
    await waitFor(() => expect(stepIds()).toEqual(expect.arrayContaining(['find', 'release'])));
    expect(stepIds()).not.toContain('publish');
    expect(screen.getByRole('status')).toHaveTextContent(/^Took into the draft: /);
    expect(settle).not.toHaveBeenCalled();
    nothingSaved();
  });

  it('undone after a take: one undo takes back the change and its record, and a redo brings both', async () => {
    await open();
    await user.click(within(row('step:+check')).getByRole('button', { name: /^Take: / }));
    counts(1, 0, 15);
    await waitFor(() => expect(stepIds()).toContain('check'));
    await user.click(screen.getByRole('button', { name: 'Undo' }));
    counts(0, 0, 16);
    expect(row('step:+check')).toHaveAttribute('data-change-state', 'open');
    await waitFor(() => expect(stepIds()).not.toContain('check'));
    await user.click(screen.getByRole('button', { name: 'Redo' }));
    counts(1, 0, 15);
    await waitFor(() => expect(stepIds()).toContain('check'));
  });

  it('all taken: the draft becomes the proposed graph, and the press naming every change goes once', async () => {
    await open();
    await user.click(within(bar()).getByRole('button', { name: 'Take all' }));
    await waitFor(() => expect(settle).toHaveBeenCalledTimes(1));
    expect(settle).toHaveBeenCalledWith('p7', RELEASE_PROPOSAL.changes.map((change) => change.id), []);
    expect(await within(bar()).findByText('Every change is decided, and Ask Daoris is told what you took.')).toBeInTheDocument();
    await waitFor(() => expect(stepIds()).toContain('publish'));
    const byId = (graph: Record<string, unknown>) => canon({
      ...graph, nodes: Object.fromEntries((graph.nodes as { id: string }[]).map((node) => [node.id, node])),
      wires: Object.fromEntries((graph.wires as { id: string }[]).map((wire) => [wire.id, wire])),
    });
    expect(byId(judgedGraph())).toBe(byId(RELEASE_GRAPH));
    // An undo and a redo bring back the same record, which is not told again.
    await user.click(screen.getByRole('button', { name: 'Undo' }));
    await user.click(screen.getByRole('button', { name: 'Redo' }));
    expect(settle).toHaveBeenCalledTimes(1);
    nothingSaved();
  });

  it('leaving: a step left leaves the ways that need it, and *Leave the rest* names every change once', async () => {
    await open();
    await user.click(within(row('step:+publish')).getByRole('button', { name: /^Leave: / }));
    expect(row('step:+publish')).toHaveAttribute('data-change-state', 'left');
    expect(row('way:+j')).toHaveAttribute('data-change-state', 'left');
    expect(row('way:+k')).toHaveAttribute('data-change-state', 'left');
    counts(0, 3, 13);
    await user.click(within(row('step:+check')).getByRole('button', { name: /^Take: / }));
    await user.click(within(bar()).getByRole('button', { name: 'Leave the rest' }));
    await waitFor(() => expect(settle).toHaveBeenCalledTimes(1));
    const [id, taken, left] = settle.mock.calls[0]! as [string, string[], string[]];
    expect(id).toBe('p7');
    expect(taken).toEqual(['step:+check']);
    expect([...left].sort()).toEqual(RELEASE_PROPOSAL.changes.map((change) => change.id).filter((each) => each !== 'step:+check').sort());
    nothingSaved();
  });

  it('a stale change: refused by name with *Ask again*, the rest still takeable', async () => {
    // The person took away the landing's way to the line, which the proposal renumbers, since Ask Daoris was told the draft.
    await open({ startFrom: { graph: { ...MERGE_GRAPH, wires: MERGE_GRAPH.wires.filter((wire) => wire.id !== 'c') } } });
    await screen.findByRole('region', { name: /Ask Daoris proposes a change/ });
    expect(row('way:~e')).toHaveAttribute('data-change-state', 'stale');
    expect(row('way:+c')).toHaveAttribute('data-change-state', 'stale');
    expect(row('way:~e')).toHaveTextContent('Your draft changed here since Ask Daoris proposed this.');
    expect(row('way:+c')).toHaveTextContent(/^.*It needs The way The landing → On the line/);
    expect(within(row('way:+c')).queryByRole('button', { name: /^Take: / })).toBeNull();

    await user.click(within(row('way:~e')).getByRole('button', { name: 'Ask again' }));
    expect(ask).toHaveBeenCalledTimes(1);
    expect(ask.mock.calls[0]![0]).toMatch(/^Your proposed change no longer fits my draft, which changed since: /);

    await user.click(within(bar()).getByRole('button', { name: 'Take all' }));
    counts(14, 0, 2);
    expect(settle).not.toHaveBeenCalled();
    await user.click(within(row('way:~e')).getByRole('button', { name: /^Leave: / }));
    await waitFor(() => expect(settle).toHaveBeenCalledTimes(1));
    expect([...(settle.mock.calls[0]![2] as string[])].sort()).toEqual(['way:+c', 'way:~e']);
    nothingSaved();
  });

  it('discarded: *Not now* sets the proposal aside, and nothing of it is taken', async () => {
    await open();
    await user.click(within(bar()).getByRole('button', { name: 'Not now' }));
    await waitFor(() => expect(dismiss).toHaveBeenCalledWith('p7'));
    await waitFor(() => expect(screen.queryByRole('region', { name: /Ask Daoris proposes a change/ })).toBeNull());
    expect(screen.getByRole('status')).toHaveTextContent("Ask Daoris's proposal is set aside.");
    expect(settle).not.toHaveBeenCalled();
    expect(canon(judgedGraph())).toBe(canon(MERGE_GRAPH));
    nothingSaved();
  });

  it('a draft this window no longer holds: nothing takeable, *Ask again* and *Not now*', async () => {
    await open({ proposal: { ...PROPOSAL, base: null } });
    expect(await within(await screen.findByRole('region', { name: /Ask Daoris proposes a change/ })).findByText(/no longer holds the draft Ask Daoris was shown/)).toBeInTheDocument();
    expect(within(bar()).queryByRole('button', { name: 'Take all' })).toBeNull();
    expect(within(bar()).queryByRole('list', { name: 'Its changes' })).toBeNull();
    await user.click(within(bar()).getByRole('button', { name: 'Ask again' }));
    expect(ask).toHaveBeenCalledTimes(1);
  });

  it('marks a step to remove and a step changed, in words, on the draft it changes', async () => {
    const proposal = { ...PROPOSAL, ...proposalOf(RELEASE_GRAPH, MERGE_GRAPH) };
    await open({ proposal, startFrom: { graph: RELEASE_GRAPH } });
    expect(within(within(await screen.findByRole('list', { name: 'How the steps go on' })).getByRole('listitem', { name: /^The checks/ })).getByText('to remove')).toBeInTheDocument();
    expect(row('step:-check')).toHaveTextContent('The checks, removed');
    expect(row('step:-check')).toHaveTextContent(/Takes with it: /);
  });

  it('by keyboard: a change taken from its row with the keys alone', async () => {
    await open();
    const press = within(row('step:+check')).getByRole('button', { name: /^Take: / });
    for (let presses = 0; presses < 120 && document.activeElement !== press; presses += 1) await user.keyboard('{Tab}');
    expect(document.activeElement).toBe(press);
    await user.keyboard('{Enter}');
    counts(1, 0, 15);
  });

  it('in Chinese: the marks and the presses in the catalogue\'s words', async () => {
    await i18n.changeLanguage('zh');
    await open();
    const region = await screen.findByRole('region', { name: /问道衍提议一项更改/ });
    expect(within(region).getByRole('button', { name: '全部接入' })).toBeInTheDocument();
    expect(within(region).getByText('已接入 0 项，不接 0 项，待定 16 项。')).toBeInTheDocument();
    expect(within(region).getAllByText('新增').length).toBeGreaterThan(0);
  });
});
