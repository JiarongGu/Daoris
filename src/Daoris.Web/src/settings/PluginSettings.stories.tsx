import type { Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../design/storyLanguage';
import { storyWidths } from '../design/storyWidths';
import type { PluginShown } from '../plugins/catalog';
import { PluginSettings } from './PluginSettings';

// Settings → Driver's Plugins section (PLUGSET1, the modes design §4.1): each installed plugin with what it holds set and
// its door to its page; an install with none, one and several, in English and 中文, at the frame's widths.

const nothing = () => {};

const plugin = (extra: Partial<PluginShown> & Pick<PluginShown, 'id' | 'name'>): PluginShown => ({
  version: '1.0.0', description: '', enabled: true, problem: null, harnesses: [], points: [], running: false,
  folder: `C:/somewhere/plugins/${extra.id}`, data: `C:/somewhere/plugins/.data/${extra.id}`, ...extra,
});

const BROWSER = plugin({
  id: 'in-app-browser', name: 'In-app browser',
  helpServers: [{ name: 'browser', allow: ['browser_snapshot'], writes: ['browser_click', 'browser_type'], state: 'approved', accepted: ['browser_click'] }],
});

const SEVERAL: PluginShown[] = [
  BROWSER,
  plugin({ id: 'github-pull-request', name: 'GitHub pull request', enabled: false }),
  plugin({
    id: 'acme.gate', name: 'Acme gate', running: true, points: ['quest/consider'],
    helpServers: [
      { name: 'gate', allow: ['look'], state: 'waits', changed: true },
      { name: 'notes', allow: [], state: 'waits' },
    ],
  }),
  plugin({ id: 'broken', name: 'Broken', problem: 'its manifest does not read' }),
];

const meta: Meta<typeof PluginSettings> = {
  title: 'Settings/PluginSettings',
  component: PluginSettings,
  args: { plugins: SEVERAL, onOpen: nothing },
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof PluginSettings>;

/** Several: off, running and refused pills, approvals, a server changed since approved, one holding nothing but its switch. */
export const Several: Story = {};

/** One plugin, its server approved with one of its two writes accepted. */
export const One: Story = { args: { plugins: [BROWSER] } };

/** An install with none says so, and offers no door. */
export const None: Story = { args: { plugins: [] } };

/** Several at the half width. */
export const SeveralHalf: Story = { decorators: [storyWidths.half] };

/** Several at the 400 px floor: each door under its words. */
export const SeveralNarrow: Story = { decorators: [storyWidths.narrow] };

/** In 中文. */
export const SeveralChinese: Story = { decorators: [chinese] };

/** One in 中文. */
export const OneChinese: Story = { args: { plugins: [BROWSER] }, decorators: [chinese] };

/** None in 中文. */
export const NoneChinese: Story = { args: { plugins: [] }, decorators: [chinese] };

/** Several in 中文 at the floor. */
export const SeveralChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };
