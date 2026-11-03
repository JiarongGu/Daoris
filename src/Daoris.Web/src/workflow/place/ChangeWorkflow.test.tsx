import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render as rtlRender, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import type { ReactElement } from 'react';
import i18n from '../../locales/i18n';
import { DOCUMENTS_GRAPH, LIST } from '../edit/editFixtures';
import type { WorkflowSaveAnswer, WorkflowShownAnswer, WorkflowUseAnswer } from '../edit/answers';
import { FULL } from '../fixtures';
import { MERGE_GRAPH } from '../graphFixtures';
import { ChangeWorkflow } from './ChangeWorkflow';
import { EFFECTIVE_CURRENT, EFFECTIVE_DOCS } from './effectiveFixtures';

// SETUPUX1p (D221; the Setup design's §2.3): Change… has three tabs, previews the selected candidate through the place's chart, reads the plan
// of a choice only where WORKFLOW_LIST announces it, and chooses with the plan's token. A molecule: the lists arrive as props, and the plan,
// the save and the choice go out.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const USED: WorkflowUseAnswer = { done: true, refused: null, message: null, changed: true, reach: [] };

const plan = (more: Partial<WorkflowUseAnswer> = {}): WorkflowUseAnswer => ({
  done: false, plan: true, refused: null, message: 'daoris: `engine` now chooses `verify-docs` as its default.', changed: true, reach: [],
  lowers: [], places: [{ repository: 'engine', was: 'Current', now: 'Verify the docs first', lowers: [] }], keeps: null, expected: 'use-aaaa', ...more,
});

const SAVED_ELSEWHERE = {
  ...LIST,
  workflows: [
    ...LIST.workflows,
    { id: 'verify-docs', name: 'Verify the docs first', problem: null, versions: [1, 2], schema: 2 as const, digest: 'cd', line: 'Docs are checked first.', pipeline: null,
      chosen: [{ level: 'workspace' as const, name: 'aurora', role: 'kind' as const, kind: 'docs' }] },
  ],
};
const ANNOUNCED = { ...SAVED_ELSEWHERE, use: ['plan'] };

const SHOWN: WorkflowShownAnswer = {
  id: 'verify-docs', name: 'Verify the docs first', version: 2, newest: true, versions: [1, 2], at: null, door: null, digest: 'cd', schema: 2,
  line: 'Docs are checked first.', body: MERGE_GRAPH, problems: [],
};

const SAVED_COPY: WorkflowSaveAnswer = {
  id: 'documents-workflow', done: true, refused: null, message: null, version: 1, written: true, appended: false, scopes: [], reach: [],
};

const DEFAULT_ROUTE = { key: 'default', kind: null, label: null, own: true };

const draw = (props: Partial<Parameters<typeof ChangeWorkflow>[0]> = {}) => {
  const onUse = vi.fn(async (_ask: unknown) => USED);
  const onPlan = vi.fn(async (_ask: unknown) => plan());
  const onSave = vi.fn(async (_ask: unknown) => SAVED_COPY);
  const onShow = vi.fn();
  const onStartFrom = vi.fn();
  const onClose = vi.fn();
  const view = render(
    <ChangeWorkflow
      list={SAVED_ELSEWHERE} current={FULL} place={{ repository: 'engine' }} route={DEFAULT_ROUTE} effective={EFFECTIVE_CURRENT} shown={SHOWN}
      onShow={onShow} onUse={onUse} onPlan={onPlan} onSave={onSave} onStartFrom={onStartFrom} onClose={onClose} {...props}
    />,
  );
  return { onUse, onPlan, onSave, onShow, onStartFrom, onClose, ...view };
};

const tab = (name: string) => screen.getByRole('tab', { name });
const pick = async (name: string) => { await userEvent.click(screen.getByRole('button', { name })); };

beforeEach(() => {
  vi.stubGlobal('ResizeObserver', class { observe() {} unobserve() {} disconnect() {} });
});

// The language goes back inside `act`, with the page still drawn (FRAMEACT1).
afterEach(async () => {
  vi.unstubAllGlobals();
  await act(async () => { await i18n.changeLanguage('en'); });
});

describe('Change…: the three tabs', () => {
  it('lists the presets by name and line, the saved workflows with where they are chosen, and Current labelled live', async () => {
    draw();

    expect(screen.getAllByRole('tab').map((each) => each.textContent)).toEqual(['Presets', 'Saved here', 'Current']);
    // Current stands first while the route follows it; the presets are one tab away.
    expect(tab('Current')).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByText(/Follows its old landing rule/)).toBeInTheDocument();
    expect(screen.getByText('v4740fb017006')).toBeInTheDocument();

    await userEvent.click(tab('Presets'));
    expect(screen.getByText('Documents workflow')).toBeInTheDocument();
    expect(screen.getByText(/For a kind of task whose paths are documents/)).toBeInTheDocument();
    await pick('Documents workflow');
    expect(screen.getByText('The work merges into the line.')).toBeInTheDocument();

    await userEvent.click(tab('Saved here'));
    expect(screen.getByText('Docs release')).toBeInTheDocument();
    expect(screen.getAllByText('v2')).toHaveLength(2);
    expect(screen.getByText('Chosen here')).toBeInTheDocument();
    expect(screen.getByText('Chosen at aurora')).toBeInTheDocument();
  });

  it('opens on the tab of what the route follows now', () => {
    draw({ effective: { ...EFFECTIVE_DOCS, workflow: 'docs-release', route: 'default' } });
    expect(tab('Saved here')).toHaveAttribute('aria-selected', 'true');
  });

  it('moves between the tabs by the arrow keys, the chosen tab the one tab stop', async () => {
    draw();

    tab('Current').focus();
    await userEvent.keyboard('{ArrowRight}');
    expect(tab('Presets')).toHaveAttribute('aria-selected', 'true');
    expect(tab('Presets')).toHaveFocus();
    expect(tab('Current')).toHaveAttribute('tabindex', '-1');
    await userEvent.keyboard('{ArrowLeft}{ArrowLeft}');
    expect(tab('Saved here')).toHaveAttribute('aria-selected', 'true');
    await userEvent.keyboard('{End}');
    expect(tab('Current')).toHaveFocus();
  });

  it("says no workflow is saved here, and the lists' read when it is refused or on its way", () => {
    const { unmount } = draw({ list: { workflows: [], presets: LIST.presets } });
    expect(screen.getAllByRole('tab')).toHaveLength(3);
    unmount();

    const { unmount: second } = draw({ list: null });
    expect(screen.getByText('Reading the saved workflows…')).toBeInTheDocument();
    second();

    draw({ list: null, error: new Error('the driver is still coming up.') });
    expect(screen.getByRole('alert')).toHaveTextContent('the driver is still coming up.');
  });
});

describe('Change…: selecting a candidate against a driver that announces no plan', () => {
  it('draws it through the place chart and writes nothing, nor reads a plan', async () => {
    const { onUse, onPlan, onSave, onShow } = draw();

    await userEvent.click(tab('Saved here'));
    await pick('Verify the docs first');

    expect(onShow).toHaveBeenLastCalledWith('verify-docs');
    expect(screen.getByRole('region', { name: 'The chart of default work' })).toBeInTheDocument();
    expect(screen.getByText('Preview: Verify the docs first v2')).toBeInTheDocument();
    expect(document.querySelector('[data-flow-node="landing"]')).not.toBeNull();
    expect(onUse).not.toHaveBeenCalled();
    expect(onPlan).not.toHaveBeenCalled();
    expect(onSave).not.toHaveBeenCalled();
  });

  it("chooses with today's press: the version read, and no token", async () => {
    const { onUse, onPlan } = draw();

    await userEvent.click(tab('Saved here'));
    await pick('Verify the docs first');
    await userEvent.click(screen.getByRole('button', { name: 'Use Verify the docs first here' }));

    expect(onUse).toHaveBeenCalledWith({ repository: 'engine', workflow: 'verify-docs', version: 2 });
    expect(onPlan).not.toHaveBeenCalled();
  });

  it('chooses the version the preview draws, not the newest the list holds (REVIEW2 P8)', async () => {
    const { onUse } = draw({ shown: { ...SHOWN, version: 1, newest: false } });

    await userEvent.click(tab('Saved here'));
    await pick('Verify the docs first');
    await userEvent.click(screen.getByRole('button', { name: 'Use Verify the docs first here' }));

    expect(onUse).toHaveBeenCalledWith({ repository: 'engine', workflow: 'verify-docs', version: 1 });
  });

  it('offers no *Use here* on a saved workflow until its preview is read (REVIEW2 P8)', async () => {
    draw({ shown: null });

    await userEvent.click(tab('Saved here'));
    await pick('Verify the docs first');

    expect(screen.queryByRole('button', { name: 'Use Verify the docs first here' })).toBeDisabled();
  });

  // WORKFLOWCLEAN1h-b: Current is no candidate; its tab is the conversion's door.
  it('offers Current only as the conversion, with no choice and no copy of it', async () => {
    const onConvert = vi.fn();
    const { onUse, onSave } = draw({ onConvert });

    await userEvent.click(screen.getByRole('button', { name: 'Convert…' }));

    expect(onConvert).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole('button', { name: 'Use Current here' })).toBeNull();
    expect(onUse).not.toHaveBeenCalled();
    expect(onSave).not.toHaveBeenCalled();
  });

  it('does not read a plan when the driver announces one but the page has no sender for it', async () => {
    const { onUse } = draw({ list: ANNOUNCED, onPlan: undefined });

    await userEvent.click(tab('Saved here'));
    await pick('Verify the docs first');
    await userEvent.click(screen.getByRole('button', { name: 'Use Verify the docs first here' }));

    expect(onUse).toHaveBeenCalledWith({ repository: 'engine', workflow: 'verify-docs', version: 2 });
  });

  it('draws a preset from its steps, and offers it no *Use here*, since a preset is not stored', async () => {
    const { onUse } = draw();

    await userEvent.click(tab('Presets'));
    await pick('Documents workflow');

    expect(screen.getByText('Preview: Documents workflow')).toBeInTheDocument();
    expect(document.querySelector('[data-flow-node="landing"]')).not.toBeNull();
    expect(screen.queryByRole('button', { name: /^Use .* here$/ })).toBeNull();
    expect(onUse).not.toHaveBeenCalled();
  });

  it('says a saved version made of steps is drawn once chosen, rather than drawing a guess', async () => {
    draw({ shown: { ...SHOWN, schema: 1, body: [] } });

    await userEvent.click(tab('Saved here'));
    await pick('Verify the docs first');

    expect(screen.getByText(/made of steps/)).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'The chart of default work' })).toBeNull();
  });
});

describe('Change…: the plan of one, where the driver announces it', () => {
  it('reads the plan on selecting, writing nothing, and says the part it lowers and the places it changes', async () => {
    const onPlan = vi.fn(async () => plan({ lowers: ['you no longer review before it lands'] }));
    const { onUse } = draw({ list: ANNOUNCED, onPlan });

    await userEvent.click(tab('Saved here'));
    await pick('Verify the docs first');

    expect(onPlan).toHaveBeenCalledWith({ repository: 'engine', workflow: 'verify-docs', version: 2, plan: true });
    expect(await screen.findByText('you no longer review before it lands')).toBeInTheDocument();
    expect(screen.getByText('engine: Current → Verify the docs first')).toBeInTheDocument();
    expect(onUse).not.toHaveBeenCalled();
  });

  it('offers *Use here* only with a fresh token, and carries it', async () => {
    let answer!: (value: WorkflowUseAnswer) => void;
    const onPlan = vi.fn(() => new Promise<WorkflowUseAnswer>((resolve) => { answer = resolve; }));
    const { onUse } = draw({ list: ANNOUNCED, onPlan });

    await userEvent.click(tab('Saved here'));
    await pick('Verify the docs first');
    expect(screen.getByText('Reading what this would change…')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Use Verify the docs first here' })).toBeDisabled();

    await act(async () => { answer(plan({ expected: 'use-1234' })); });
    const use = screen.getByRole('button', { name: 'Use Verify the docs first here' });
    expect(use).toBeEnabled();
    await userEvent.click(use);

    expect(onUse).toHaveBeenCalledWith({ repository: 'engine', workflow: 'verify-docs', version: 2, expected: 'use-1234' });
  });

  it('keeps *Use here* off where the plan is refused, in the driver\'s words', async () => {
    const onPlan = vi.fn(async () => plan({ refused: 'reach', message: '`aurora/web` cannot start v2: its line has no gates.', expected: undefined }));
    draw({ list: ANNOUNCED, onPlan });

    await userEvent.click(tab('Saved here'));
    await pick('Verify the docs first');

    expect(await screen.findByRole('alert')).toHaveTextContent('aurora/web cannot start v2: its line has no gates.');
    expect(screen.getByRole('button', { name: 'Use Verify the docs first here' })).toBeDisabled();
  });

  it('says a plan the driver could not read, and offers no *Use here*', async () => {
    const onPlan = vi.fn(async () => { throw new Error('the driver is still coming up.'); });
    draw({ list: ANNOUNCED, onPlan });

    await userEvent.click(tab('Saved here'));
    await pick('Verify the docs first');

    expect(await screen.findByRole('alert')).toHaveTextContent('the driver is still coming up.');
    expect(screen.getByRole('button', { name: 'Use Verify the docs first here' })).toBeDisabled();
  });

  it("says the driver's refusal of a stale version where it moved, and chooses nothing", async () => {
    const onUse = vi.fn(async (): Promise<WorkflowUseAnswer> => ({
      done: false, refused: 'stale', message: '`verify-docs` is at v3 now, not v2: read it again and choose that. Nothing was written.', changed: false, reach: [],
    }));
    draw({ list: ANNOUNCED, onUse });

    await userEvent.click(tab('Saved here'));
    await pick('Verify the docs first');
    await act(async () => { await userEvent.click(await screen.findByRole('button', { name: 'Use Verify the docs first here' })); });

    expect(await screen.findByRole('alert')).toHaveTextContent('verify-docs is at v3 now, not v2');
  });

  it("says the driver's refusal of a moved plan, and reads the plan again before it offers the press again", async () => {
    const onPlan = vi.fn()
      .mockResolvedValueOnce(plan({ expected: 'use-old' }))
      .mockResolvedValue(plan({ expected: 'use-new' }));
    const onUse = vi.fn(async (): Promise<WorkflowUseAnswer> => ({
      done: false, refused: 'moved', message: 'What this choice does has moved since you read its plan: read the plan again and choose then. Nothing was written.',
      changed: false, reach: [],
    }));
    draw({ list: ANNOUNCED, onPlan, onUse });

    await userEvent.click(tab('Saved here'));
    await pick('Verify the docs first');
    await act(async () => { await userEvent.click(await screen.findByRole('button', { name: 'Use Verify the docs first here' })); });

    expect(await screen.findByRole('alert')).toHaveTextContent('has moved since you read its plan');
    await waitFor(() => expect(onPlan).toHaveBeenCalledTimes(2));
    expect(onUse).toHaveBeenLastCalledWith({ repository: 'engine', workflow: 'verify-docs', version: 2, expected: 'use-old' });

    onUse.mockClear();
    await act(async () => { await userEvent.click(await screen.findByRole('button', { name: 'Use Verify the docs first here' })); });
    expect(onUse).toHaveBeenLastCalledWith({ repository: 'engine', workflow: 'verify-docs', version: 2, expected: 'use-new' });
  });

  it('discards the plan of a candidate left, so another is never chosen with its token', async () => {
    let first!: (value: WorkflowUseAnswer) => void;
    const onPlan = vi.fn()
      .mockImplementationOnce(() => new Promise<WorkflowUseAnswer>((resolve) => { first = resolve; }))
      .mockResolvedValue(plan({ expected: 'use-clear' }));
    const { onUse } = draw({ list: ANNOUNCED, onPlan });

    await userEvent.click(tab('Saved here'));
    await pick('Verify the docs first');
    await userEvent.click(screen.getByRole('button', { name: "Use the workspace's" }));
    await screen.findByText('engine: Current → Verify the docs first');
    await act(async () => { first(plan({ expected: 'use-late' })); });
    await userEvent.click(screen.getByRole('button', { name: "Use the workspace's" }));

    expect(onUse).toHaveBeenCalledWith({ repository: 'engine', expected: 'use-clear' });
  });

  it('asks per route: a kind\'s plan and press carry the kind, and chosen here is read at that kind', async () => {
    const onPlan = vi.fn(async () => plan({ expected: 'use-kind' }));
    const { onUse } = draw({
      list: { ...ANNOUNCED, workflows: [...ANNOUNCED.workflows.slice(0, 1), { ...ANNOUNCED.workflows[1]!, chosen: [{ level: 'repository' as const, name: 'engine', role: 'kind' as const, kind: 'docs' }] }] },
      onPlan,
      route: { key: 'kind:docs', kind: 'docs', label: 'Documentation', own: false },
      effective: EFFECTIVE_DOCS,
      shown: { ...SHOWN, id: 'docs-release', name: 'Docs release' },
    });

    await userEvent.click(tab('Saved here'));
    // Docs release is chosen for the default; Verify the docs first for this kind: chosen here at the kind alone.
    expect(screen.getAllByText('Chosen here')).toHaveLength(1);
    await pick('Docs release');
    await userEvent.click(await screen.findByRole('button', { name: 'Use Docs release here' }));

    expect(onPlan).toHaveBeenCalledWith({ repository: 'engine', kind: 'docs', workflow: 'docs-release', version: 2, plan: true });
    expect(onUse).toHaveBeenCalledWith({ repository: 'engine', kind: 'docs', workflow: 'docs-release', version: 2, expected: 'use-kind' });
  });

  it('chooses for a workspace by its name', async () => {
    const { onPlan } = draw({ list: ANNOUNCED, place: { workspace: 'aurora' } });

    await userEvent.click(tab('Saved here'));
    await pick('Verify the docs first');
    expect(onPlan).toHaveBeenCalledWith({ workspace: 'aurora', workflow: 'verify-docs', version: 2, plan: true });
  });
});

describe('Change…: Save a copy… of a preset', () => {
  it('saves a copy by a name alone, the id made from it, and then offers the copy *Use here*', async () => {
    const { onSave, onShow } = draw();

    await userEvent.click(tab('Presets'));
    await pick('Documents workflow');
    await userEvent.click(screen.getByRole('button', { name: 'Save a copy…' }));
    const name = screen.getByRole('textbox', { name: 'Name of the copy' });
    expect(name).toHaveValue('Documents workflow');
    await userEvent.clear(name);
    await userEvent.type(name, 'Docs for engine');
    await userEvent.click(screen.getByRole('button', { name: 'Save a copy' }));

    expect(onSave).toHaveBeenCalledWith({ id: 'docs-for-engine', graph: DOCUMENTS_GRAPH, name: 'Docs for engine' });
    // The copy is a saved workflow now: its tab, selected.
    expect(tab('Saved here')).toHaveAttribute('aria-selected', 'true');
    expect(onShow).toHaveBeenLastCalledWith('documents-workflow');
  });

  it('numbers the id until it is unique among the saved ones, and falls back to the preset\'s id for a name with no letters', async () => {
    const { onSave } = draw({
      list: { ...SAVED_ELSEWHERE, workflows: [...SAVED_ELSEWHERE.workflows, { ...SAVED_ELSEWHERE.workflows[1]!, id: 'documents-workflow' }] },
    });

    await userEvent.click(tab('Presets'));
    await pick('Documents workflow');
    await userEvent.click(screen.getByRole('button', { name: 'Save a copy…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Save a copy' }));
    expect(onSave).toHaveBeenLastCalledWith(expect.objectContaining({ id: 'documents-workflow-2' }));

    await userEvent.click(tab('Presets'));
    await pick('Documents workflow');
    await userEvent.click(screen.getByRole('button', { name: 'Save a copy…' }));
    const name = screen.getByRole('textbox', { name: 'Name of the copy' });
    await userEvent.clear(name);
    await userEvent.type(name, '文档');
    await userEvent.click(screen.getByRole('button', { name: 'Save a copy' }));
    expect(onSave).toHaveBeenLastCalledWith(expect.objectContaining({ id: 'documents' }));
  });

  it("says the driver's refusal of the save, and stays on the preset", async () => {
    const onSave = vi.fn(async (): Promise<WorkflowSaveAnswer> => ({ ...SAVED_COPY, done: false, written: false, refused: 'refused', message: '`documents-workflow` is not an id.' }));
    draw({ onSave });

    await userEvent.click(tab('Presets'));
    await pick('Documents workflow');
    await userEvent.click(screen.getByRole('button', { name: 'Save a copy…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Save a copy' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('documents-workflow is not an id.');
    expect(tab('Presets')).toHaveAttribute('aria-selected', 'true');
  });

  it("starts the editor from a preset's steps, and not from one that has none", async () => {
    const { onStartFrom } = draw();

    await userEvent.click(tab('Presets'));
    expect(screen.queryByRole('button', { name: 'Edit steps from A pull request with no press' })).toBeNull();
    await pick('Documents workflow');
    await userEvent.click(screen.getByRole('button', { name: 'Edit steps from Documents workflow' }));

    expect(onStartFrom).toHaveBeenCalledWith({ graph: expect.objectContaining({ nodes: expect.any(Array) }), name: 'Documents workflow' });
  });
});

describe("Change…: Use the workspace's", () => {
  const KEEPS = plan({
    changed: true, expected: 'use-clear',
    keeps: {
      said: "`engine` still maps kind `docs` to `documents`: while a repository maps a kind of its own, its default work follows Current, not the workspace's. Clearing a kind's mapping is that kind's own press.",
      kinds: [{ kind: 'docs', workflow: 'documents' }],
    },
  });

  it('reads the plan of the clear, says what the kinds keep, and clears the default alone with the token', async () => {
    const onPlan = vi.fn(async () => KEEPS);
    const { onUse } = draw({ list: ANNOUNCED, onPlan });

    await userEvent.click(screen.getByRole('button', { name: "Use the workspace's" }));

    expect(onPlan).toHaveBeenCalledWith({ repository: 'engine', plan: true });
    expect(await screen.findByText(/its default work follows Current, not the workspace's/)).toBeInTheDocument();
    expect(onUse).not.toHaveBeenCalled();

    await userEvent.click(screen.getByRole('button', { name: "Use the workspace's" }));
    expect(onUse).toHaveBeenCalledWith({ repository: 'engine', expected: 'use-clear' });
  });

  it('is a kind row\'s own press on a kind\'s route, carrying the kind', async () => {
    const onPlan = vi.fn(async () => plan({ expected: 'use-kind-clear' }));
    const { onUse } = draw({ list: ANNOUNCED, onPlan, route: { key: 'kind:docs', kind: 'docs', label: 'Documentation', own: true }, effective: EFFECTIVE_DOCS });

    await userEvent.click(screen.getByRole('button', { name: "Use the workspace's" }));
    expect(onPlan).toHaveBeenCalledWith({ repository: 'engine', kind: 'docs', plan: true });
    await screen.findByText('engine: Current → Verify the docs first');
    await userEvent.click(screen.getByRole('button', { name: "Use the workspace's" }));
    expect(onUse).toHaveBeenCalledWith({ repository: 'engine', kind: 'docs', expected: 'use-kind-clear' });
  });

  it("is today's press where the driver announces no plan", async () => {
    const { onUse, onPlan } = draw();

    await userEvent.click(screen.getByRole('button', { name: "Use the workspace's" }));

    expect(onUse).toHaveBeenCalledWith({ repository: 'engine' });
    expect(onPlan).not.toHaveBeenCalled();
  });

  it('is offered only where the repository chose a workflow of its own for the route, and not on a workspace', () => {
    const { unmount } = draw({ route: { ...DEFAULT_ROUTE, own: false } });
    expect(screen.queryByRole('button', { name: "Use the workspace's" })).toBeNull();
    unmount();

    draw({ place: { workspace: 'aurora' } });
    expect(screen.queryByRole('button', { name: "Use the workspace's" })).toBeNull();
  });

  it("says the driver's refusal, moved, and reads the plan again", async () => {
    const onPlan = vi.fn(async () => KEEPS);
    const onUse = vi.fn(async (): Promise<WorkflowUseAnswer> => ({
      done: false, refused: 'moved', message: 'What this choice does has moved since you read its plan: read the plan again and choose then. Nothing was written.',
      changed: false, reach: [],
    }));
    draw({ list: ANNOUNCED, onPlan, onUse });

    await userEvent.click(screen.getByRole('button', { name: "Use the workspace's" }));
    await act(async () => { await userEvent.click(await screen.findByRole('button', { name: "Use the workspace's" })); });

    expect(await screen.findByRole('alert')).toHaveTextContent('has moved since you read its plan');
    await waitFor(() => expect(onPlan).toHaveBeenCalledTimes(2));
  });
});

describe('Change…: closing, and its words in Chinese', () => {
  it('closes, and stops reading a saved version', async () => {
    const { onClose, onShow, unmount } = draw();
    await userEvent.click(screen.getByRole('button', { name: 'Close' }));
    expect(onClose).toHaveBeenCalledOnce();
    unmount();
    expect(onShow).toHaveBeenLastCalledWith(null);
  });

  it('says its words in Chinese, by the glossary\'s rows', async () => {
    draw();
    await act(async () => { await i18n.changeLanguage('zh'); });

    const region = screen.getByRole('region', { name: '更改它的工作所遵循的工作流' });
    expect(within(region).getByRole('tab', { name: '预设' })).toBeInTheDocument();
    expect(within(region).getByRole('tab', { name: '现行' })).toBeInTheDocument();
    expect(within(region).getByRole('button', { name: '沿用工作区的' })).toBeInTheDocument();
  });
});
