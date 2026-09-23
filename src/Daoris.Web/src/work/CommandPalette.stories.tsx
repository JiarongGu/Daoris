import type { Meta, StoryObj } from '@storybook/react-vite';
import { commands } from '../commands';
import { CommandPalette } from './CommandPalette';

// Every state of the palette (SURF9), on the shipped component. The registry is a pure function, so
// "what a browser sees" and "what a shell sees" are arguments rather than arrangements — which is the
// whole reason it was built that way.

const LABELS: Record<string, string> = {
  'go.overview': 'Overview',
  'go.quests': 'Quests',
  'go.projects': 'Projects',
  'go.convergence': 'Convergence',
  'go.search': 'Search',
  'go.settings': 'Settings',
  'go.sessions': 'Sessions',
  'work.start': 'Start a session…',
  'work.review': 'Review what this session landed',
  'work.monitor': 'Open the monitor window',
  'work.detach': 'Open this session in its own window',
  'do.refresh': 'Refresh the index',
  'do.language': 'Switch language',
};

const GROUPS = { go: 'go to', do: 'do', work: 'work' } as const;

const world = (over: Partial<Parameters<typeof commands>[0]> = {}) => commands({
  label: (id) => LABELS[id] ?? id,
  group: (id) => GROUPS[id],
  attached: true,
  current: 'overview',
  waiting: 0,
  go: () => {},
  refresh: () => {},
  toggleLanguage: () => {},
  startSession: () => {},
  review: () => {},
  monitor: () => {},
  detach: () => {},
  ...over,
});

const meta: Meta<typeof CommandPalette> = {
  title: 'Chrome/CommandPalette',
  component: CommandPalette,
  args: { open: true, onClose: () => {} },
};
export default meta;

type Story = StoryObj<typeof CommandPalette>;

/** A shell on Overview: every other view, the session actions, the global two. */
export const InTheShell: Story = { args: { commands: world() } };

/** A shell on Sessions — every view but the one you are already on (D66). */
export const OnSessions: Story = { args: { commands: world({ current: 'sessions' }) } };

/**
 * A browser. Shorter by OMISSION: no Machine, no frame switch, no session actions — because a palette
 * is a promise that what it lists can be done (D47 §4).
 */
export const InABrowser: Story = { args: { commands: world({ attached: false }) } };

/** Nothing matches. It says so — an empty panel reads as broken where a sentence reads as an answer. */
export const NothingMatches: Story = { args: { commands: [] } };
