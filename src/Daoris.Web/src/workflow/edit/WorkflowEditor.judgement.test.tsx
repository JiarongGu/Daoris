import { useEffect, type ReactNode } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { registerAskFill } from '../../help/askFill';
import i18n from '../../locales/i18n';
import { RELEASE_GRAPH as SHIPPED_RELEASE } from '../graphFixtures';
import type { WorkflowSaveAnswer } from './answers';
import { HOLDS, LIST, PALETTE, REFUSED, REFUSED_GRAPH } from './editFixtures';

// FLOWCHART2c (D223 §3, §7 row c, §8.7): the driver's judgement on the editor's chart. Each answer belongs to the draft it judged, by its
// fingerprint: an edit makes the last answer invalid at once, a failed request leaves it invalid, and an answer older than the newest
// request is dropped. A refusal is marked where it is, in words, and the head lists what is to put right with a door to each.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn(async (..._: unknown[]): Promise<unknown> => null) }));

vi.mock('@shenora/react', async (original) => ({
  ...(await original<typeof import('@shenora/react')>()),
  getBridge: () => ({ invoke }),
  useShenora: () => ({ isAvailable: true }),
}));

import { useWorkflowEditor } from '../../bridge/repositories/workflowEdit';
import { WorkflowEditor, type WorkflowEditorProps } from './WorkflowEditor';

const STRUCTURE = { works: [], places: [], agents: [], roles: [] };

/** A planned save held open until the test answers it, in the order the editor asked them. */
type Plan = { graph: { nodes: { id: string }[] }; resolve: (answer: WorkflowSaveAnswer) => void; reject: (why: Error) => void };
let plans: Plan[] = [];

beforeEach(() => {
  plans = [];
  invoke.mockImplementation(async (_module, type, options) => {
    const payload = (options as { payload: Record<string, unknown> }).payload;
    switch (type) {
      case 'WORKFLOW_LIST': return LIST;
      case 'WORKFLOW_PALETTE': return PALETTE;
      case 'ROLE_STRUCTURE': return STRUCTURE;
      case 'WORKFLOW_SAVE':
        return new Promise((resolve, reject) => {
          plans.push({ graph: payload.graph as Plan['graph'], resolve: resolve as Plan['resolve'], reject });
        });
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

const chart = () => screen.getByRole('list', { name: /How the steps go on|各步骤如何继续/ });
const step = (name: string | RegExp) => within(chart()).getByRole('listitem', { name });
const refusedSteps = () => [...document.querySelectorAll<HTMLElement>('[data-refused="true"]')];

/** The nth planned save asked, once the editor has asked it (a change holds still before it is judged). */
const planned = async (count: number) => { await waitFor(() => expect(plans.length).toBeGreaterThanOrEqual(count)); return plans[count - 1]!; };

/** The nth planned save answered, once the editor has asked it. */
const answered = async (count: number, answer: WorkflowSaveAnswer) => {
  const plan = await planned(count);
  await act(async () => { plan.resolve(answer); });
};

/** A step duplicated: one change to the draft, which makes a new fingerprint. */
async function edit(user: ReturnType<typeof userEvent.setup>) {
  await user.click(step('The landing'));
  await user.click(await screen.findByRole('button', { name: 'Duplicate' }));
}

describe('the judgement is of the draft it was asked of', () => {
  it('drops an answer older than the newest request, whichever arrives last', async () => {
    const user = userEvent.setup();
    render(wrap(<Editor startFrom={{ graph: REFUSED_GRAPH }} />));
    const first = await planned(1);
    await edit(user);
    await answered(2, REFUSED);
    expect(await screen.findByText('3 to put right')).toBeInTheDocument();
    await act(async () => { first.resolve(HOLDS); });
    expect(screen.getByText('3 to put right')).toBeInTheDocument();
    expect(screen.queryByText('These steps hold, and can be saved.')).toBeNull();
  });

  it('makes the last answer invalid at once on an edit, before the new request is asked', async () => {
    const user = userEvent.setup();
    render(wrap(<Editor startFrom={{ graph: REFUSED_GRAPH }} />));
    await answered(1, REFUSED);
    expect(await screen.findByText('3 to put right')).toBeInTheDocument();
    expect(refusedSteps().length).toBeGreaterThan(0);

    await edit(user);
    expect(plans).toHaveLength(1);
    expect(screen.getByText('Judging the steps…')).toBeInTheDocument();
    expect(screen.queryByText('3 to put right')).toBeNull();
    expect(refusedSteps()).toHaveLength(0);
    expect(document.querySelector('[data-flow-refusal-chip]')).toBeNull();
  });

  it('stays invalid after a request fails, and says it could not be judged', async () => {
    const user = userEvent.setup();
    render(wrap(<Editor startFrom={{ graph: REFUSED_GRAPH }} />));
    await answered(1, REFUSED);
    await screen.findByText('3 to put right');

    await edit(user);
    const second = await planned(2);
    await act(async () => { second.reject(new Error('the driver is away')); });
    expect(await screen.findByText('These steps could not be judged just now, so they are not known to hold.')).toBeInTheDocument();
    expect(screen.queryByText('3 to put right')).toBeNull();
    expect(screen.queryByText('These steps hold, and can be saved.')).toBeNull();
    expect(refusedSteps()).toHaveLength(0);
  });

  it('takes the answer back as valid when the draft is undone to the one it judged', async () => {
    const user = userEvent.setup();
    render(wrap(<Editor startFrom={{ graph: REFUSED_GRAPH }} />));
    await answered(1, REFUSED);
    await screen.findByText('3 to put right');
    await edit(user);
    expect(screen.queryByText('3 to put right')).toBeNull();
    await user.click(screen.getByRole('button', { name: 'Undo' }));
    expect(await screen.findByText('3 to put right')).toBeInTheDocument();
  });
});

describe('a refusal where it is', () => {
  it('marks the step it names and the way it names, each in words, and lists what is not on the chart in the head only', async () => {
    render(wrap(<Editor startFrom={{ graph: REFUSED_GRAPH }} />));
    await answered(1, REFUSED);
    expect(await screen.findByText('3 to put right')).toBeInTheDocument();

    // The role at its step: declined's outline and a chip beside it (the rail's, here, since jsdom lays nothing out), the sentence said in
    // its description and never as a line that would move the ranks below (CANVAS1b).
    const work = step('The work');
    expect(work).toHaveAttribute('data-refused', 'true');
    expect(work.className).toContain('border-st-declined');
    expect(work.className).not.toContain('border-2');
    expect(work.querySelector('[data-flow-refusal]')).toBeNull();
    expect(work.parentElement!.querySelector('[data-flow-mark-kind="refused"]')).toHaveTextContent('To put right');
    expect(work).toHaveAccessibleDescription(/To put right: Its role does not do this step's work\./);

    // The way back at its way: the source's description says it (its foot gains no line), and the chip wears a word beside the ⚠.
    expect(refusedSteps().map((each) => each.getAttribute('data-flow-node'))).toEqual(['work']);
    const review = step('Your review');
    expect(within(review).queryByRole('button', { name: /to put right/ })).toBeNull();
    expect(review).toHaveAccessibleDescription(/to put right: A way back goes only to a step that comes before this one\./);
    for (const chip of document.querySelectorAll('[data-flow-refusal-chip]')) expect(chip.textContent).toMatch(/⚠ \S+/);

    // The rule this page has no words for names no step: the head holds it, in the driver's words.
    expect(screen.getByText(/a rule this page has no words for refuses/)).toBeInTheDocument();
  });

  it('never marks a step or a way without its words', async () => {
    render(wrap(<Editor startFrom={{ graph: REFUSED_GRAPH }} />));
    await answered(1, REFUSED);
    await screen.findByText('3 to put right');
    for (const marked of refusedSteps()) {
      expect(marked.parentElement!.querySelector('[data-flow-mark-kind="refused"]')?.textContent).toMatch(/\S{3,}/);
      expect(marked.getAttribute('aria-describedby')).not.toBeNull();
      expect(marked).toHaveAccessibleDescription(/To put right: \S/);
    }
    for (const chip of document.querySelectorAll('[data-flow-refusal-chip]')) expect(chip.textContent?.replace('⚠', '').trim().length).toBeGreaterThan(0);
  });

  it('gives a door to each refusal that names a step, in the head', async () => {
    const user = userEvent.setup();
    render(wrap(<Editor startFrom={{ graph: REFUSED_GRAPH }} />));
    await answered(1, REFUSED);
    await screen.findByText('3 to put right');
    await user.click(screen.getByRole('button', { name: 'The work: Its role does not do this step\'s work.' }));
    expect(await screen.findByRole('dialog', { name: 'The work' })).toBeInTheDocument();
  });

  it('opens the folded holds where a refusal lies inside them', async () => {
    render(wrap(<Editor startFrom={{ graph: SHIPPED_RELEASE }} />));
    await screen.findByRole('list', { name: 'How the steps go on' });
    expect(step(/Daoris makes sure · 3/)).toBeInTheDocument();
    expect(document.querySelector('[data-flow-node="content"]')).toBeNull();
    const refused: WorkflowSaveAnswer = {
      ...HOLDS, done: false, refused: 'refused', version: null,
      problems: [{ code: 'unreachable', wire: null, node: 'content', words: 'no way from the start reaches `content`.' }],
    };
    await answered(1, refused);
    expect(await screen.findByText('1 to put right')).toBeInTheDocument();
    const inside = document.querySelector('[data-flow-node="content"]');
    expect(inside).not.toBeNull();
    expect(inside).toHaveAttribute('data-refused', 'true');
    expect(inside).toHaveAccessibleDescription(/No way from the start reaches this step\./);
  });

  it('says it in Chinese, as the page words it', async () => {
    await i18n.changeLanguage('zh');
    const release = registerAskFill(() => {});
    render(wrap(<Editor startFrom={{ graph: REFUSED_GRAPH }} />));
    await answered(1, REFUSED);
    expect(await screen.findByText('3 处待改正')).toBeInTheDocument();
    const work = step('工作本身');
    expect(work).toHaveAttribute('data-refused', 'true');
    expect(work).toHaveAccessibleDescription(/待改正：它的角色不做这个步骤的工作。/);
    expect(screen.getByRole('button', { name: '工作本身：它的角色不做这个步骤的工作。' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '问原因：工作本身：它的角色不做这个步骤的工作。' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '帮我改好：工作本身：它的角色不做这个步骤的工作。' })).toBeInTheDocument();
    release();
  });
});

describe('Ask why and Fix it', () => {
  it('fill in Ask Daoris with the refusal\'s sentence and its step, and offer neither where there is no Ask Daoris', async () => {
    const user = userEvent.setup();
    render(wrap(<Editor startFrom={{ graph: REFUSED_GRAPH }} />));
    await answered(1, REFUSED);
    await screen.findByText('3 to put right');
    expect(screen.queryByRole('button', { name: /^Ask why/ })).toBeNull();
    expect(screen.queryByRole('button', { name: /^Fix it/ })).toBeNull();

    const fill = vi.fn();
    let release = () => {};
    act(() => { release = registerAskFill(fill); });
    await user.click(await screen.findByRole('button', { name: 'Ask why: The work: Its role does not do this step\'s work.' }));
    expect(fill).toHaveBeenLastCalledWith('Why is this refused at The work: Its role does not do this step\'s work.');
    await user.click(screen.getByRole('button', { name: 'Fix it: The work: Its role does not do this step\'s work.' }));
    expect(fill).toHaveBeenLastCalledWith('Put this right at The work: Its role does not do this step\'s work.');

    // The way names its source step; the rule with no step, the sentence alone, in the driver's words.
    await user.click(screen.getByRole('button', { name: 'Ask why: Your review: A way back goes only to a step that comes before this one.' }));
    expect(fill).toHaveBeenLastCalledWith('Why is this refused at Your review: A way back goes only to a step that comes before this one.');
    const rule = 'a rule this page has no words for refuses `publish`.';
    await user.click(screen.getByRole('button', { name: `Fix it: ${rule}` }));
    expect(fill).toHaveBeenLastCalledWith(`Put this right: ${rule}`);
    act(() => release());
  });
});
