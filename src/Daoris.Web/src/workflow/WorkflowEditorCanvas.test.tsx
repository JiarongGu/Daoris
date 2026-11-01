import { useEffect, type ReactNode } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { zoomBy, zoomLevel, zoomWheel } from '../design/zoom';
import { openDraftNow } from '../help/openDraft';
import i18n from '../locales/i18n';
import { LIST, PALETTE } from './edit/editFixtures';

// CANVAS1a (D224; the canvas design §9 row a): the editor's chart standing in the canvas's viewport, which its pane of 900 px offers. The
// release workflow is composed from the Merge workflow by keyboard alone and by pointer as it is on the rail (WorkflowEditor.test.tsx);
// `+` on a step still adds a step after it, Ctrl+= and a Ctrl+wheel stay the window's zoom's to hear, and a pan, a zoom and a selection
// leave the draft as it was. jsdom lays nothing out, so the pane's width is stood in for; the ink and its ＋s are the measured spec's.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn(async (..._: unknown[]): Promise<unknown> => null) }));

vi.mock('@shenora/react', async (original) => ({
  ...(await original<typeof import('@shenora/react')>()),
  getBridge: () => ({ invoke }),
  useShenora: () => ({ isAvailable: true }),
}));

import { useWorkflowEditor } from '../bridge/repositories/workflowEdit';
import { WorkflowEditor } from './edit/WorkflowEditor';

type Wire = { id: string; kind: string; from: string; to: string; outcome?: string; on?: string };

const STRUCTURE = {
  works: [], places: [], agents: [],
  roles: [{ id: 'build', state: 'seed', seeded: true, name: { en: 'Build', zh: '开发' }, takes: ['build'], revision: 0 }],
};

function answer(type: string, payload: Record<string, unknown>): unknown {
  switch (type) {
    case 'WORKFLOW_LIST': return LIST;
    case 'WORKFLOW_PALETTE': return PALETTE;
    case 'ROLE_STRUCTURE': return STRUCTURE;
    case 'WORKFLOW_SAVE': return {
      id: payload.id, version: 1, written: !payload.plan, appended: false, scopes: [], reach: [], done: true, refused: null,
      message: payload.plan ? 'planned' : `\`${String(payload.id)}\` v1, a new workflow, from the page.`, problems: [],
    };
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

const saved = () => (invoke.mock.calls.filter((call) => call[1] === 'WORKFLOW_SAVE')
  .map((call) => (call[2] as { payload: Record<string, unknown> }).payload).find((each) => each.plan === false)!.graph as { wires: Wire[] });
const said = (wire: Wire) => {
  const end = (named: string) => (wire.kind === 'data' ? named.split('.')[0] : named);
  return `${wire.kind}:${end(wire.from)}>${end(wire.to)}${wire.outcome ? `?${wire.outcome}` : ''}${wire.on ? `@${wire.on}` : ''}`;
};
const RELEASE_WAYS = [
  'after:content>check', 'after:check>review', 'back:review>work@rejected', 'after:line>find', 'when:find>release?release',
  'after:release>publish', 'data:find>release', 'data:release>publish',
];

const chart = () => screen.getByRole('region', { name: 'How the steps go on' });
const step = (name: string | RegExp) => within(screen.getByRole('list', { name: 'How the steps go on' })).getByRole('listitem', { name });
const focused = () => document.activeElement as HTMLElement;
const scale = () => Number(chart().querySelector('[data-canvas-world]')!.getAttribute('data-canvas-scale'));

let user: ReturnType<typeof userEvent.setup>;

beforeEach(() => {
  user = userEvent.setup();
  invoke.mockImplementation(async (_module, type, options) => answer(type as string, (options as { payload: Record<string, unknown> }).payload));
  // The chart's pane 900 px wide, past the canvas's 36rem floor; the editor's region narrow, so a step's inspector is a drawer.
  vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (this: HTMLElement) {
    const width = this.hasAttribute('data-flow-pane') ? 900 : 0;
    return { width, height: 0, top: 0, left: 0, right: width, bottom: 0, x: 0, y: 0, toJSON: () => ({}) } as DOMRect;
  });
});

afterEach(async () => {
  invoke.mockReset();
  vi.restoreAllMocks();
  zoomBy('reset');
  await i18n.changeLanguage('en');
});

async function tabTo(target: HTMLElement) {
  for (let presses = 0; presses < 80 && focused() !== target; presses += 1) await user.keyboard('{Tab}');
  expect(focused()).toBe(target);
}

async function chooseByKeys(name: string, option: string) {
  await tabTo(screen.getByRole('combobox', { name }));
  await user.keyboard('{Enter}');
  await screen.findByRole('listbox');
  for (let presses = 0; presses < 30 && focused().textContent !== option; presses += 1) await user.keyboard('{ArrowDown}');
  await user.keyboard('{Enter}');
  await waitFor(() => expect(screen.queryByRole('listbox')).toBeNull());
}

async function pickByKeys(search: string, title: string) {
  const box = await screen.findByRole('combobox', { name: 'Find a step' });
  expect(focused()).toBe(box);
  await user.keyboard(search);
  await user.keyboard('{Enter}');
  await waitFor(() => expect(focused()).toBe(step(title)));
}

describe('the editor\'s chart in the viewport', () => {
  it('stands in a region with its sizing, and composes the release workflow by keyboard alone, + on a step adding after it', async () => {
    render(wrap(<Editor />));
    await tabTo(await screen.findByRole('button', { name: 'Begin from Merge workflow' }));
    await user.keyboard('{Enter}');
    await waitFor(() => expect(focused()).toBe(step('The work')));
    expect(chart()).toHaveAttribute('aria-roledescription', "a workflow's drawing: drag or the wheel to move, Ctrl+wheel to zoom");
    expect(screen.getByRole('button', { name: 'Sizing: 100%' })).toBeInTheDocument();

    await user.keyboard('{ArrowDown}');
    expect(focused()).toBe(step('Daoris makes sure · 3'));
    await user.keyboard('+');
    await pickByKeys('checks', 'The checks');

    await user.keyboard('{ArrowDown}');
    expect(focused()).toBe(step('Your review'));
    await user.keyboard('{Enter}');
    await screen.findByRole('dialog', { name: 'Your review' });
    await chooseByKeys('Where If you ask for changes goes from Your review', 'Back to The work');
    await user.keyboard('{Escape}');
    await waitFor(() => expect(focused()).toBe(step('Your review')));

    await user.keyboard('{ArrowDown}{ArrowDown}');
    expect(focused()).toBe(step('On the line'));
    await user.keyboard('+');
    await pickByKeys('find', 'find by local-release');
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

    await tabTo(screen.getByRole('textbox', { name: 'Its name' }));
    await user.keyboard('Release for engine');
    // The save is reviewed first (SETUPUX1s): the review takes the focus, and the save is its next stop.
    await waitFor(() => expect(screen.getByRole('button', { name: 'Review and save' })).toBeEnabled());
    await tabTo(screen.getByRole('button', { name: 'Review and save' }));
    await user.keyboard('{Enter}');
    await tabTo(await screen.findByRole('button', { name: 'Save as a new workflow' }));
    await user.keyboard('{Enter}');
    await screen.findByText(/v1, a new workflow, from the page\./);
    expect(saved().wires.map(said)).toEqual(expect.arrayContaining(RELEASE_WAYS));
  });

  it('composes it by pointer: the steps pressed, the ◎\'s ＋, the inspector\'s rows', async () => {
    render(wrap(<Editor />));
    await user.click(await screen.findByRole('button', { name: 'Begin from Merge workflow' }));
    // The holds opened, their last step chosen, and a step added after it from the head.
    await user.click(within(step('Daoris makes sure · 3')).getAllByRole('button')[0]!);
    await user.click(step('What the line holds'));
    await user.click(screen.getByRole('button', { name: 'Close' }));
    await user.click(screen.getByRole('button', { name: 'Add a step…' }));
    await user.click(await screen.findByRole('option', { name: /^The checks/ }));

    await user.click(step('Your review'));
    await screen.findByRole('dialog', { name: 'Your review' });
    await user.click(screen.getByRole('combobox', { name: 'Where If you ask for changes goes from Your review' }));
    await user.click(await screen.findByRole('option', { name: 'Back to The work' }));
    await user.click(screen.getByRole('button', { name: 'Close' }));

    await user.click(step('On the line'));
    await user.click(await screen.findByRole('button', { name: 'Add a new step where Once done goes from On the line' }));
    await user.click(await screen.findByRole('option', { name: /^find by local-release/ }));
    await user.click(await screen.findByRole('button', { name: 'Add a step when find by local-release comes to release' }));
    await user.click(await screen.findByRole('option', { name: /^release by local-release/ }));
    await user.click(step('release by local-release'));
    await user.click(await screen.findByRole('button', { name: 'Add a new step where Once done goes from release by local-release' }));
    await user.click(await screen.findByRole('option', { name: /^publish by local-release/ }));

    await user.type(screen.getByRole('textbox', { name: 'Its name' }), 'Release for engine');
    const review = screen.getByRole('button', { name: 'Review and save' });
    await waitFor(() => expect(review).toBeEnabled());
    await user.click(review);
    await user.click(await screen.findByRole('button', { name: 'Save as a new workflow' }));
    await screen.findByText(/v1, a new workflow, from the page\./);
    expect(saved().wires.map(said)).toEqual(expect.arrayContaining(RELEASE_WAYS));
  });

  it('leaves Ctrl+= and a Ctrl+wheel to the window\'s zoom, which a canvas wheel never moves (trap 4)', async () => {
    const steps = vi.fn();
    const hear = zoomWheel(steps);
    window.addEventListener('wheel', hear);
    try {
      render(wrap(<Editor />));
      await user.click(await screen.findByRole('button', { name: 'Begin from Merge workflow' }));
      await waitFor(() => expect(focused()).toBe(step('The work')));
      expect(fireEvent.keyDown(step('The work'), { key: '=', code: 'Equal', ctrlKey: true })).toBe(true);
      const wheel = new WheelEvent('wheel', { bubbles: true, cancelable: true, ctrlKey: true, deltaY: -100 });
      act(() => { step('The work').dispatchEvent(wheel); });
      expect(wheel.defaultPrevented).toBe(true);
      expect(scale()).toBeCloseTo(1.25, 6);
      expect(steps).not.toHaveBeenCalled();
      expect(zoomLevel()).toBe(100);
    } finally {
      window.removeEventListener('wheel', hear);
    }
  });

  it('keeps the draft as it was across a pan, a zoom, a fit and a selection (trap 19)', async () => {
    render(wrap(<Editor />));
    await user.click(await screen.findByRole('button', { name: 'Begin from Merge workflow' }));
    await waitFor(() => expect(openDraftNow()).not.toBeNull());
    const before = JSON.stringify(openDraftNow()!.graph);
    act(() => { chart().dispatchEvent(new WheelEvent('wheel', { bubbles: true, cancelable: true, ctrlKey: true, deltaY: 100 })); });
    fireEvent.pointerDown(chart(), { button: 0, clientX: 300, clientY: 100, pointerId: 1 });
    fireEvent.pointerMove(chart(), { clientX: 200, clientY: 60, pointerId: 1 });
    fireEvent.pointerUp(chart(), { pointerId: 1 });
    act(() => step('The work').focus());
    await user.keyboard('{Shift>}!{/Shift}');
    await user.click(step('Your review'));
    await user.click(screen.getByRole('button', { name: 'Close' }));
    expect(JSON.stringify(openDraftNow()!.graph)).toBe(before);
    expect(JSON.stringify(openDraftNow()!.graph)).not.toMatch(/"(x|y|k|zoom|view)":/);
  });
});
