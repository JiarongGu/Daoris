import type { Meta, StoryObj } from '@storybook/react-vite';
import { Card } from '../ui';
import { PluginRow, type PluginShown } from './PluginRow';

// An installed plugin's row (D64), in the shape the driver's PLUGINS answer takes: running, switched off,
// and refused with the driver's sentence. PLUG10 (P9): running wears the in-progress hue, never done's.

const GATE: PluginShown = {
  id: 'acme.quiet-hours', name: 'Quiet hours', version: '1.1.0',
  description: 'Holds quests outside working hours.',
  enabled: true, problem: null, harnesses: [], points: ['quest/consider', 'session/ended'], running: true,
  folder: 'C:/daoris/data/plugins/acme.quiet-hours', data: 'C:/daoris/data/plugins/.data/acme.quiet-hours',
  source: { kind: 'folder', folder: 'C:/checkouts/house-plugins/quiet-hours' },
};

const meta = {
  title: 'Settings/PluginRow',
  component: PluginRow,
  args: { plugin: GATE, canTry: true, onTry: () => {}, onAskUpdate: () => {}, onSwitch: () => {}, onRemove: () => {} },
  decorators: [(Story) => <Card><Story /></Card>],
} satisfies Meta<typeof PluginRow>;

export default meta;
type Story = StoryObj<typeof meta>;

/** On, and its hook process up: the running pill in the in-progress hue. */
export const Running: Story = {};

/** Switched off by the person: no process, and the off pill in the neutral tone. */
export const Off: Story = { args: { plugin: { ...GATE, enabled: false, running: false } } };

/** Refused by the driver: its sentence, verbatim, and nothing to try. */
export const Refused: Story = {
  args: {
    plugin: {
      ...GATE, id: 'future', name: 'future', version: '', description: '', points: [], running: false, source: { kind: 'none' },
      problem: 'needs plugin API 99, and this build speaks 1 — update Daoris, or use a plugin written for 1.',
    },
  },
};
