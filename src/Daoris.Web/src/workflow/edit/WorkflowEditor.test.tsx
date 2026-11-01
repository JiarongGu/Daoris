import { useEffect, type ReactNode } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { openDraftNow } from '../../help/openDraft';
import i18n from '../../locales/i18n';
import { EDIT_EVENT } from '../../menus/editing';
import { RELEASE_GRAPH as SHIPPED_RELEASE } from '../graphFixtures';
import { LIST, PALETTE, REFUSED_GRAPH } from './editFixtures';

// WORKFLOWEDIT1b, FLOWCHART2b (D223 §3, §7 row b, §8): the editor composes a workflow on its chart, through a stubbed bridge. The release
// workflow is composed from the Merge workflow by keyboard alone and by pointer, both data wires included: a step put between two, a way back
// on changes the person never names a kind for, a decision's outcome given a new step, and what each step carries. Pick mode never focuses a
// step that cannot take the way; a port dragged to a step routes it; removing joins only a plain chain; and every change is undone and redone
// by the keys and the Edit menu, each said aloud.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn(async (..._: unknown[]): Promise<unknown> => null) }));

vi.mock('@shenora/react', async (original) => ({
  ...(await original<typeof import('@shenora/react')>()),
  getBridge: () => ({ invoke }),
  useShenora: () => ({ isAvailable: true }),
}));

import { useWorkflowEditor } from '../../bridge/repositories/workflowEdit';
import { WorkflowEditor, type WorkflowEditorProps } from './WorkflowEditor';

type Wire = { id: string; kind: string; from: string; to: string; outcome?: string; on?: string };
type Graph = { nodes: { id: string }[]; wires: Wire[]; end: string };

const STRUCTURE = {
  works: [], places: [], agents: [],
  roles: [
    { id: 'build', state: 'seed', seeded: true, name: { en: 'Build', zh: '开发' }, takes: ['build'], revision: 0 },
    { id: 'opinion', state: 'seed', seeded: true, name: { en: 'Second opinion', zh: '第二意见' }, takes: ['review'], revision: 0 },
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
      const forward = graph.wires.find((wire) => wire.kind === 'back' && wire.to === 'landing');
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
const saved = () => asked('WORKFLOW_SAVE').find((each) => each.plan === false)!.graph as Graph;
/** A wire in short, a data wire by the steps it joins. */
const said = (wire: Wire) => {
  const end = (named: string) => (wire.kind === 'data' ? named.split('.')[0] : named);
  return `${wire.kind}:${end(wire.from)}>${end(wire.to)}${wire.outcome ? `?${wire.outcome}` : ''}${wire.on ? `@${wire.on}` : ''}`;
};

/** The chart's step by its name. */
const step = (name: string | RegExp) => within(screen.getByRole('list', { name: 'How the steps go on' })).getByRole('listitem', { name });
const focused = () => document.activeElement as HTMLElement;
const live = () => screen.getByRole('status');

/** The release workflow's ways, as both compositions must leave them. */
const RELEASE_WAYS = [
  'after:content>check', 'after:check>review', 'back:review>work@rejected', 'after:line>find', 'when:find>release?release',
  'after:release>publish', 'data:find>release', 'data:release>publish',
];

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

/** Tab until `target` holds the focus: the keyboard's own way there. */
async function tabTo(target: HTMLElement, back = false) {
  for (let presses = 0; presses < 80 && focused() !== target; presses += 1) await user.keyboard(back ? '{Shift>}{Tab}{/Shift}' : '{Tab}');
  expect(focused()).toBe(target);
}

/** A select chosen by its keys alone: opened with Enter, walked to the option, taken with Enter. */
async function chooseByKeys(name: string, option: string) {
  await tabTo(screen.getByRole('combobox', { name }));
  await user.keyboard('{Enter}');
  await screen.findByRole('listbox');
  for (let presses = 0; presses < 30 && focused().textContent !== option; presses += 1) await user.keyboard('{ArrowDown}');
  expect(focused()).toHaveTextContent(option);
  await user.keyboard('{Enter}');
  await waitFor(() => expect(screen.queryByRole('listbox')).toBeNull());
}

/** A select chosen by pointer. */
async function chooseByPointer(name: string, option: string) {
  await user.click(screen.getByRole('combobox', { name }));
  await user.click(await screen.findByRole('option', { name: option }));
}

/** The step picker searched and its first match taken by its keys. */
async function pickByKeys(search: string, title: string) {
  const box = await screen.findByRole('combobox', { name: 'Find a step' });
  expect(focused()).toBe(box);
  await user.keyboard(search);
  expect(screen.getByRole('option', { selected: true })).toHaveTextContent(title);
  await user.keyboard('{Enter}');
  await waitFor(() => expect(focused()).toBe(step(title)));
}

describe('the release workflow composed from the Merge workflow', () => {
  it('by keyboard alone: a step between two, a way back on changes, a decision\'s outcome and a step after it, and what each carries', async () => {
    render(wrap(<Editor />));

    await tabTo(await screen.findByRole('button', { name: 'Begin from Merge workflow' }));
    await user.keyboard('{Enter}');
    await waitFor(() => expect(focused()).toBe(step('The work')));
    expect(live()).toHaveTextContent('Began from Merge workflow.');

    // The checks, between the holds and your review: the way's ＋ is the step's + key.
    await user.keyboard('{ArrowDown}');
    expect(focused()).toBe(step('Daoris makes sure · 3'));
    await user.keyboard('+');
    expect(await screen.findByText('Between What the line holds and Your review: What the line holds goes on to the new step, and it to Your review.')).toBeInTheDocument();
    await pickByKeys('checks', 'The checks');
    expect(live()).toHaveTextContent('Added The checks.');

    // Your review's changes go back to the work: chosen as a target, the kind is never named.
    await user.keyboard('{ArrowDown}');
    expect(focused()).toBe(step('Your review'));
    await user.keyboard('{Enter}');
    const drawer = await screen.findByRole('dialog', { name: 'Your review' });
    await chooseByKeys('Where If you ask for changes goes from Your review', 'Back to The work');
    expect(within(drawer).getByRole('combobox', { name: 'Where If you ask for changes goes from Your review' })).toHaveTextContent('Back to The work');
    await user.keyboard('{Escape}');
    await waitFor(() => expect(focused()).toBe(step('Your review')));

    // The release found after the line: the + key on a step with no way on.
    await user.keyboard('{ArrowDown}{ArrowDown}');
    expect(focused()).toBe(step('On the line'));
    await user.keyboard('+');
    await pickByKeys('find', 'find by local-release');

    // Its release outcome gets a new step, and that step one after it, each from its row.
    await user.keyboard('{Enter}');
    await screen.findByRole('dialog', { name: 'find by local-release' });
    await tabTo(screen.getByRole('button', { name: 'Add a new step where release goes from find by local-release' }));
    await user.keyboard('{Enter}');
    await pickByKeys('release by', 'release by local-release');
    await user.keyboard('{Enter}');
    await screen.findByRole('dialog', { name: 'release by local-release' });
    await tabTo(screen.getByRole('button', { name: 'Add a new step where Once done goes from release by local-release' }));
    await user.keyboard('{Enter}');
    await pickByKeys('publish', 'publish by local-release');

    // What each carries: the copy takes the record's release, the record the find's.
    await user.keyboard('{Enter}');
    await screen.findByRole('dialog', { name: 'publish by local-release' });
    await chooseByKeys("What publish by local-release's input release is given", "release by local-release's release");
    await user.keyboard('{Escape}');
    await waitFor(() => expect(focused()).toBe(step('publish by local-release')));
    // Up past the ◎ of the release's other outcome, which stands beside the record in reading order.
    for (let presses = 0; presses < 3 && focused() !== step('release by local-release'); presses += 1) await user.keyboard('{ArrowUp}');
    expect(focused()).toBe(step('release by local-release'));
    await user.keyboard('{Enter}');
    await screen.findByRole('dialog', { name: 'release by local-release' });
    await chooseByKeys("What release by local-release's input release is given", "find by local-release's release");
    await user.keyboard('{Escape}');

    await tabTo(screen.getByRole('textbox', { name: 'Its name' }));
    await user.keyboard('Release for engine');
    await tabTo(screen.getByRole('button', { name: 'Save as new' }));
    await user.keyboard('{Enter}');
    await screen.findByText(/v1, a new workflow, from the page\./);

    expect(asked('WORKFLOW_SAVE').find((each) => each.plan === false)).toMatchObject({ id: 'release-for-engine', name: 'Release for engine' });
    const graph = saved();
    expect(graph.end).toBe('line');
    expect(graph.wires.map(said)).toEqual(expect.arrayContaining(RELEASE_WAYS));
    expect(graph.wires.some((wire) => wire.from === 'content' && wire.to === 'review')).toBe(false);
  });

  it('by pointer: the ＋ on a way and in a ◎, a row\'s target and new step, and each carry', async () => {
    render(wrap(<Editor />));

    await user.click(await screen.findByRole('button', { name: 'Begin from Merge workflow' }));
    await user.click(await screen.findByRole('button', { name: 'Add a step between What the line holds and Your review' }));
    await user.click(await screen.findByRole('option', { name: /^The checks/ }));

    await user.click(step('Your review'));
    await screen.findByRole('dialog', { name: 'Your review' });
    await chooseByPointer('Where If you ask for changes goes from Your review', 'Back to The work');
    await user.click(screen.getByRole('button', { name: 'Close' }));

    await user.click(step('On the line'));
    await user.click(await screen.findByRole('button', { name: 'Add a new step where Once done goes from On the line' }));
    await user.click(await screen.findByRole('option', { name: /^find by local-release/ }));

    await user.click(await screen.findByRole('button', { name: 'Add a step when find by local-release comes to release' }));
    await user.click(await screen.findByRole('option', { name: /^release by local-release/ }));

    await user.click(step('release by local-release'));
    await user.click(await screen.findByRole('button', { name: 'Add a new step where Once done goes from release by local-release' }));
    await user.click(await screen.findByRole('option', { name: /^publish by local-release/ }));

    await user.click(step('publish by local-release'));
    await chooseByPointer("What publish by local-release's input release is given", "release by local-release's release");
    await user.click(screen.getByRole('button', { name: 'Close' }));
    await user.click(step('release by local-release'));
    await chooseByPointer("What release by local-release's input release is given", "find by local-release's release");
    await user.click(screen.getByRole('button', { name: 'Close' }));

    await user.type(screen.getByRole('textbox', { name: 'Its name' }), 'Release for engine');
    await user.click(screen.getByRole('button', { name: 'Save as new' }));
    await screen.findByText(/v1, a new workflow, from the page\./);
    expect(saved().wires.map(said)).toEqual(expect.arrayContaining(RELEASE_WAYS));
  });
});

describe('choosing where a way goes on the chart', () => {
  it('walks only the steps that can take the way, never focusing one that cannot, and gives the focus back on Escape', async () => {
    render(wrap(<Editor />));
    await user.click(await screen.findByRole('button', { name: 'Begin from Merge workflow' }));
    await user.click(step('The work'));
    await user.click(await screen.findByRole('button', { name: 'Choose on the chart where Once done goes from The work' }));

    // The work goes on to its requests already, and is no target of its own: neither, nor the group they stand in, takes the focus.
    expect(live()).toHaveTextContent('Choose where Once done goes from The work');
    const never = [step('The work'), step('Daoris makes sure · 3')];
    for (const key of ['{Home}', '{ArrowDown}', '{ArrowDown}', '{ArrowUp}', '{ArrowUp}', '{ArrowUp}', '{End}', '{ArrowDown}']) {
      await user.keyboard(key);
      expect(never, key).not.toContain(focused());
      expect(focused()).toHaveAttribute('data-picking', 'true');
    }
    await user.click(step('The work'));
    expect(never).not.toContain(focused());
    await user.keyboard('{Escape}');
    await waitFor(() => expect(focused()).toBe(step('The work')));

    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('button', { name: 'Choose on the chart where Once done goes from The work' }));
    await user.keyboard('{End}{ArrowUp}{Enter}');
    expect(live()).toHaveTextContent('Once done, The work goes to The landing.');
  });

  it('routes a decision\'s outcome by a drag from its port to a step, and gives open ground a new step', async () => {
    render(wrap(<Editor startFrom={{ graph: SHIPPED_RELEASE, name: 'Release workflow' }} />));
    await screen.findByRole('list', { name: 'How the steps go on' });
    const port = document.querySelector<HTMLElement>('[data-flow-port="find:none"]')!;
    await user.pointer([{ keys: '[MouseLeft>]', target: port }, { target: step(/^publish by/) }, { keys: '[/MouseLeft]' }]);
    expect(live()).toHaveTextContent('none, find by local-release goes to publish by local-release.');

    await user.pointer([
      { keys: '[MouseLeft>]', target: document.querySelector<HTMLElement>('[data-flow-port="find:release"]')! },
      { target: document.querySelector<HTMLElement>('[data-flow-pane]')! }, { keys: '[/MouseLeft]' },
    ]);
    expect(await screen.findByText('After find by local-release, when it comes to release.')).toBeInTheDocument();
  });
});

describe('removing, moving, and every change undone', () => {
  it('removes a step joining its plain way in to its way on, undone and redone by the keys and by the Edit menu', async () => {
    render(wrap(<Editor />));
    await user.click(await screen.findByRole('button', { name: 'Begin from Merge workflow' }));
    await user.click(step('Your review'));
    await user.keyboard('{Escape}');
    await waitFor(() => expect(focused()).toBe(step('Your review')));
    await user.keyboard('{Delete}');
    await user.click(await screen.findByRole('button', { name: 'Remove and join What the line holds to The landing' }));
    expect(live()).toHaveTextContent('Removed Your review; What the line holds goes on to The landing.');
    expect(screen.queryByRole('listitem', { name: 'Your review' })).toBeNull();

    step('The landing').focus();
    await user.keyboard('{Control>}z{/Control}');
    expect(step('Your review')).toBeInTheDocument();
    expect(live()).toHaveTextContent('Undone: Removed Your review; What the line holds goes on to The landing.');
    await user.keyboard('{Control>}y{/Control}');
    expect(screen.queryByRole('listitem', { name: 'Your review' })).toBeNull();
    await user.keyboard('{Control>}{Shift>}z{/Shift}{/Control}');

    // The Edit menu's Undo reaches the editor as the event Edit sends a region with a history of its own.
    const region = document.querySelector<HTMLElement>('[data-edit-history]')!;
    region.dispatchEvent(new CustomEvent(EDIT_EVENT, { detail: { act: 'undo' }, cancelable: true }));
    await waitFor(() => expect(step('Your review')).toBeInTheDocument());

    // A key aimed at a field is the field's own: Ctrl+Z in the name undoes the words, not the draft.
    await user.type(screen.getByRole('textbox', { name: 'Its name' }), 'Mine');
    await user.keyboard('{Control>}z{/Control}');
    expect(step('Your review')).toBeInTheDocument();
  });

  it('asks where the end goes when the end step is removed, and moves a step within its chain from the preview', async () => {
    render(wrap(<Editor />));
    await user.click(await screen.findByRole('button', { name: 'Begin from Merge workflow' }));
    await user.click(step('On the line'));
    await user.click(await screen.findByRole('button', { name: 'Remove…' }));
    await chooseByPointer('Where the work is done instead', 'The landing');
    await user.click(screen.getByRole('button', { name: 'Remove' }));
    expect(within(step('The landing')).getByText('Work done here')).toBeInTheDocument();

    await user.click(step('Your review'));
    await user.click(await screen.findByRole('button', { name: 'Move earlier' }));
    expect(screen.getByText('Your review comes before What the line holds: The quest\'s holds → Your review → What the line holds → The landing.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Move' }));
    expect(live()).toHaveTextContent('Moved Your review.');
    await user.click(screen.getByRole('button', { name: 'Close' }));

    await user.type(screen.getByRole('textbox', { name: 'Its name' }), 'Moved');
    await user.click(screen.getByRole('button', { name: 'Save as new' }));
    await screen.findByText(/v1, a new workflow, from the page\./);
    expect(saved().end).toBe('landing');
    expect(saved().wires.map(said)).toEqual(expect.arrayContaining(['after:holds>review', 'after:review>content', 'after:content>landing']));
  });

  it('moves a step by Alt+↑ and its preview, duplicates one, and starts a step each time another is reached, by keyboard alone', async () => {
    render(wrap(<Editor />));
    await tabTo(await screen.findByRole('button', { name: 'Begin from Merge workflow' }));
    await user.keyboard('{Enter}');
    await waitFor(() => expect(focused()).toBe(step('The work')));
    for (let presses = 0; presses < 4 && focused() !== step('Your review'); presses += 1) await user.keyboard('{ArrowDown}');

    await user.keyboard('{Alt>}{ArrowUp}{/Alt}');
    await screen.findByText(/^Your review comes before What the line holds/);
    await tabTo(screen.getByRole('button', { name: 'Move' }));
    await user.keyboard('{Enter}');
    expect(live()).toHaveTextContent('Moved Your review.');

    await tabTo(screen.getByRole('button', { name: 'Duplicate' }));
    await user.keyboard('{Enter}');
    expect(live()).toHaveTextContent('Duplicated Your review.');
    await waitFor(() => expect(focused()).toHaveAttribute('data-flow-node', 'review-2'));

    // More ways: a trigger, which no row of What happens next draws.
    await user.keyboard('{Enter}');
    await screen.findByRole('dialog', { name: 'Your review' });
    await tabTo(screen.getByText('More ways'));
    await user.keyboard('{Enter}');
    await chooseByKeys('Start a step each time Your review is reached', 'On the line');
    expect(live()).toHaveTextContent('Each time Your review is reached, On the line starts.');
    expect(screen.getByRole('button', { name: 'Remove the way on from Your review: On the line, each time it is reached' })).toBeInTheDocument();
  });

  it('duplicates a step, and copies one and pastes it unconnected under a fresh id', async () => {
    render(wrap(<Editor />));
    await user.click(await screen.findByRole('button', { name: 'Begin from Merge workflow' }));
    await user.click(step('The landing'));
    await user.click(await screen.findByRole('button', { name: 'Duplicate' }));
    expect(live()).toHaveTextContent('Duplicated The landing.');
    expect(within(screen.getByRole('list', { name: 'How the steps go on' })).getAllByRole('listitem', { name: 'The landing' })).toHaveLength(2);

    step('The work').focus();
    await user.keyboard('{Control>}c{/Control}');
    expect(live()).toHaveTextContent('Copied The work.');
    await user.keyboard('{Control>}v{/Control}');
    expect(live()).toHaveTextContent('Pasted The work; ways to steps not copied left out: 1.');
    expect(within(screen.getByRole('list', { name: 'How the steps go on' })).getAllByRole('listitem', { name: 'The work' })).toHaveLength(2);
  });
});

// FLOWCHART2d's half (D223 §4, §8.3): Ask Daoris is told the draft open in the editor, as the editor registers it while it is mounted.
describe('the draft Ask Daoris is told', () => {
  it('follows the draft, its place, source, save target and selected step, and is let go when the editor closes', async () => {
    const { unmount } = render(wrap(<Editor />));
    await user.click(await screen.findByRole('button', { name: 'Begin from Merge workflow' }));
    expect(openDraftNow()).toMatchObject({ place: 'engine', source: 'preset:merge-after-accept', workflow: 'merge-after-accept-2', base: null, selected: null });
    expect(openDraftNow()!.graph.nodes).toHaveLength(7);

    await user.click(await screen.findByRole('button', { name: 'Add a step between What the line holds and Your review' }));
    await user.click(await screen.findByRole('option', { name: /^The checks/ }));
    await waitFor(() => expect(openDraftNow()!.graph.nodes).toHaveLength(8));
    expect(openDraftNow()).toMatchObject({ selected: 'check' });
    await user.type(screen.getByRole('textbox', { name: 'Its name' }), 'Checked');
    expect(openDraftNow()).toMatchObject({ workflow: 'checked' });

    unmount();
    expect(openDraftNow()).toBeNull();
  });
});

describe('the judgement, the inspector beside the chart, and the languages', () => {
  it('says each refusal with a door to its step, and at the way it names in the inspector', async () => {
    vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (this: HTMLElement) {
      const width = this.hasAttribute('data-editor-region') ? 1290 : 0;
      return { width, height: 0, top: 0, left: 0, right: width, bottom: 0, x: 0, y: 0, toJSON: () => ({}) } as DOMRect;
    });
    render(wrap(<Editor startFrom={{ graph: REFUSED_GRAPH }} />));

    const door = await screen.findByRole('button', { name: 'Your review: A way back goes only to a step that comes before this one.' });
    expect(screen.getByText('1 to put right')).toBeInTheDocument();
    await user.click(door);

    // Wide, the inspector stands beside the chart, no drawer.
    expect(screen.queryByRole('dialog')).toBeNull();
    const inspector = screen.getByRole('region', { name: 'Your review' });
    expect(within(inspector).getByText('A way back goes only to a step that comes before this one.')).toBeInTheDocument();
    await user.click(within(inspector).getByRole('button', { name: 'Remove the way If you ask for changes to The landing' }));
    await screen.findByText('These steps hold, and can be saved.');
  });

  it("draws the editor in both languages, Daoris's words translated and a step's authored words as written", async () => {
    render(wrap(<Editor startFrom={{ graph: SHIPPED_RELEASE, name: 'Release workflow' }} />));
    expect(await screen.findByRole('heading', { name: 'Compose a workflow' })).toBeInTheDocument();
    await i18n.changeLanguage('zh');
    expect(await screen.findByRole('heading', { name: '编排工作流' })).toBeInTheDocument();
    expect(within(screen.getByRole('list', { name: '各步骤如何继续' })).getByRole('listitem', { name: '进入主线' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '添加步骤…' })).toBeInTheDocument();
  });
});
