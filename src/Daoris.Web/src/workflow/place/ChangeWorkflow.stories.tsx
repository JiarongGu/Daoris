import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { InTheme } from '../../plugins/storyIcons';
import type { WorkflowShownAnswer, WorkflowUseAnswer } from '../edit/answers';
import { LIST } from '../edit/editFixtures';
import { FULL } from '../fixtures';
import { MERGE_GRAPH } from '../graphFixtures';
import { ChangeWorkflow } from './ChangeWorkflow';
import { EFFECTIVE_CURRENT } from './effectiveFixtures';

// Change… on a repository place's Workflow: three tabs, the selected candidate drawn
// through the place's chart, the plan of choosing it where the driver announces one, Save a copy… of a preset and Use the workspace's, at
// the main area's 80, 39 and 25 rem, in 中文 and in dark. A `play` presses what a person would, by keyboard-reachable controls.

const nothing = () => {};

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const list = {
  ...LIST,
  use: ['plan'],
  workflows: [
    ...LIST.workflows,
    {
      id: 'verify-docs', name: 'Verify the docs first', problem: null, versions: [1, 2], schema: 2 as const, digest: 'cd', line: 'Docs are checked before the work lands.',
      pipeline: null, chosen: [{ level: 'workspace' as const, name: 'aurora', role: 'kind' as const, kind: 'docs' }],
    },
  ],
};

const shown: WorkflowShownAnswer = {
  id: 'verify-docs', name: 'Verify the docs first', version: 2, newest: true, versions: [1, 2], at: null, door: null, digest: 'cd', schema: 2,
  line: 'Docs are checked before the work lands.', body: MERGE_GRAPH, problems: [],
};

const used = async (): Promise<WorkflowUseAnswer> => ({ done: true, refused: null, message: null, changed: true, reach: [] });

const planned = (more: Partial<WorkflowUseAnswer> = {}) => async (): Promise<WorkflowUseAnswer> => ({
  done: false, plan: true, refused: null, changed: true, reach: [], expected: 'use-aaaa',
  message: 'daoris: `engine` now chooses `verify-docs` as its default — Verify the docs first, v2 now.\n  Each new piece of work binds what is chosen at its first start; work already started keeps what it bound.',
  lowers: ['You no longer review the work before it lands.'],
  places: [{ repository: 'engine', was: 'Current', now: 'Verify the docs first', lowers: [] }],
  keeps: null, ...more,
});

const meta: Meta<typeof ChangeWorkflow> = {
  title: 'Workflow/ChangeWorkflow',
  component: ChangeWorkflow,
  args: {
    list, current: FULL, place: { repository: 'engine' }, route: { key: 'default', kind: null, label: null, own: true }, effective: EFFECTIVE_CURRENT, shown,
    onShow: nothing, onUse: used, onPlan: planned(), onSave: async () => ({ id: 'documents-workflow', done: true, refused: null, message: null, version: 1, written: true, appended: false, scopes: [], reach: [] }),
    onStartFrom: nothing, onClose: nothing,
  },
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof ChangeWorkflow>;

const press = (canvasElement: HTMLElement, name: RegExp) => {
  const button = [...canvasElement.querySelectorAll<HTMLElement>('button, [role="tab"]')].find((each) => name.test(each.getAttribute('aria-label') ?? each.textContent ?? ''));
  button?.click();
  return button;
};
const settle = () => new Promise((resolve) => setTimeout(resolve, 120));

/** The tabs, opening on Current while the route follows it; keyboard: the arrow keys move along the tabs. */
export const Lists: Story = {};
export const ListsHalf: Story = { decorators: [storyWidths.half] };
export const ListsNarrow: Story = { decorators: [storyWidths.narrow] };

export const ListsChinese: Story = { decorators: [chinese] };
export const ListsChineseHalf: Story = { decorators: [storyWidths.half, chinese] };
export const ListsChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };

export const ListsDark: Story = { decorators: [dark] };
export const ListsDarkHalf: Story = { decorators: [storyWidths.half, dark] };
export const ListsDarkNarrow: Story = { decorators: [storyWidths.narrow, dark] };

/** The arrow keys on the tabs: the chosen tab the one tab stop, and the focus moving with it. */
const arrows: Story['play'] = async ({ canvasElement }) => {
  const tab = canvasElement.querySelector<HTMLElement>('[role="tab"][aria-selected="true"]');
  tab?.focus();
  tab?.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowLeft', bubbles: true }));
  await settle();
};
export const TabsByKeyboard: Story = { play: arrows };
export const TabsByKeyboardNarrow: Story = { play: arrows, decorators: [storyWidths.narrow] };

/** A saved workflow selected: its newest version drawn through the place's chart, and the plan of choosing it, with *Use here* waiting on its token. */
const saved: Story['play'] = async ({ canvasElement }) => {
  press(canvasElement, /^(Saved here|保存在这里的)$/);
  await settle();
  press(canvasElement, /^Verify the docs first$/);
  await settle();
};
export const SavedPreview: Story = { play: saved };
export const SavedPreviewHalf: Story = { play: saved, decorators: [storyWidths.half] };
export const SavedPreviewNarrow: Story = { play: saved, decorators: [storyWidths.narrow] };
export const SavedPreviewChinese: Story = { play: saved, decorators: [chinese] };
export const SavedPreviewChineseNarrow: Story = { play: saved, decorators: [storyWidths.narrow, chinese] };
export const SavedPreviewDark: Story = { play: saved, decorators: [dark] };
export const SavedPreviewDarkNarrow: Story = { play: saved, decorators: [storyWidths.narrow, dark] };

/** A driver that announces no plan: the preview draws, and *Use here* is today's press. */
export const SavedPreviewUnannounced: Story = { play: saved, args: { list: { ...list, use: undefined } } };

/** The plan refused in the driver's words: nothing to press. */
export const PlanRefused: Story = {
  play: saved,
  args: { onPlan: planned({ refused: 'reach', message: '`aurora/web` cannot start v2: its line has no gates.', expected: undefined, lowers: [], places: [] }) },
};

/** A preset selected: drawn from its steps, to be saved as a copy by a name alone or edited from. */
const preset: Story['play'] = async ({ canvasElement }) => {
  press(canvasElement, /^(Presets|预设)$/);
  await settle();
  press(canvasElement, /^Documents workflow$|^文档工作流$/);
  await settle();
  press(canvasElement, /^(Save a copy…|保存副本…)$/);
  await settle();
};
export const PresetCopy: Story = { play: preset };
export const PresetCopyHalf: Story = { play: preset, decorators: [storyWidths.half] };
export const PresetCopyNarrow: Story = { play: preset, decorators: [storyWidths.narrow] };
export const PresetCopyChinese: Story = { play: preset, decorators: [chinese] };
export const PresetCopyDarkNarrow: Story = { play: preset, decorators: [storyWidths.narrow, dark] };

/** *Use the workspace's* pressed: its plan says what the kinds keep, in the driver's words, and the press carries the token. */
const clear: Story['play'] = async ({ canvasElement }) => {
  press(canvasElement, /^(Use the workspace's|沿用工作区的)$/);
  await settle();
};
const KEEPS = planned({
  lowers: [], places: [],
  message: 'daoris: `engine` no longer chooses a workflow as its default.',
  keeps: {
    said: "`engine` still maps kind `docs` to `documents`: while a repository maps a kind of its own, its default work follows Current, not the workspace's. Clearing a kind's mapping is that kind's own press.",
    kinds: [{ kind: 'docs', workflow: 'documents' }],
  },
});
export const ClearKeepsKinds: Story = { play: clear, args: { onPlan: KEEPS } };
export const ClearKeepsKindsNarrow: Story = { play: clear, args: { onPlan: KEEPS }, decorators: [storyWidths.narrow] };
export const ClearKeepsKindsChinese: Story = { play: clear, args: { onPlan: KEEPS }, decorators: [chinese] };
export const ClearKeepsKindsDark: Story = { play: clear, args: { onPlan: KEEPS }, decorators: [dark] };

/** A kind's route: the choice, the plan and *Use the workspace's* are that kind's own. */
export const KindRoute: Story = {
  play: saved,
  args: { route: { key: 'kind:docs', kind: 'docs', label: 'Documentation', own: true } },
};

/** The lists on their way, and the read refused. */
export const Reading: Story = { args: { list: null } };
export const ReadRefused: Story = { args: { list: null, error: new Error('the driver is still coming up — try again in a moment.') } };

/** No workflow saved here yet: the presets and Current alone. */
export const NothingSaved: Story = {
  args: { list: { workflows: [], presets: LIST.presets } },
  play: async ({ canvasElement }) => { press(canvasElement, /^(Saved here|保存在这里的)$/); await settle(); },
};
