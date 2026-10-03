import type { Meta, StoryObj } from '@storybook/react-vite';
import type { PluginShown } from './catalog';
import { PluginMainNotice, PluginPage } from './PluginPage';
import { InTheme, PNG_ICON, SVG_ICON } from './storyIcons';

// A plugin's page (PLUGUI1b, D119 §3.2) in the main area, in every state the design names on today's answers: running,
// off, refused, a landing plugin, an agents-only plugin, no source record, an update's plan and a refused one, a trial
// passed and failed, Remove… asking; and the main area with no page: nothing chosen, gone, loading.

const RUNNING: PluginShown = {
  id: 'acme.gate', name: 'Acme gate', version: '1.2.0',
  description: 'Holds a quest overnight when its repository asks for quiet hours, and lets it start in the morning.',
  enabled: true, problem: null, harnesses: ['acme-agent'], points: ['quest/consider', 'session/ended'], running: true,
  folder: 'C:/somewhere/data/plugins/acme.gate', data: 'C:/somewhere/data/plugins/.data/acme.gate',
  source: { kind: 'folder', folder: 'C:/somewhere/checkouts/plugins/acme.gate' },
};

const KIT = [
  { name: 'quest/consider', kind: 'decision' },
  { name: 'session/ended', kind: 'observation' },
  { name: 'work/land', kind: 'act' },
];

const nothing = () => {};

const meta: Meta<typeof PluginPage> = {
  title: 'Plugins/PluginPage',
  component: PluginPage,
  args: {
    plugin: RUNNING, kitPoints: KIT, canTry: true,
    onSwitch: nothing, onTry: nothing, onAskUpdate: nothing, onApplyUpdate: nothing, onCancelUpdate: nothing,
    onAskRemove: nothing, onRemove: nothing, onCancelRemove: nothing,
  },
  // The main area's height and a width between the side bar's two states (D119 §3.6).
  decorators: [(Story) => (
    <div className="flex h-[40rem] w-[46rem] max-w-full border border-line bg-page">
      <Story />
    </div>
  )],
};
export default meta;

type Story = StoryObj<typeof PluginPage>;

/** Running: its process is up; the quiet neutral pill, its four acts. */
export const Running: Story = {};

/** Off: the person switched it off; *Turn on*. */
export const Off: Story = { args: { plugin: { ...RUNNING, enabled: false, running: false } } };

/** Refused: the driver's sentence leads, verbatim; it is taken nowhere, so it has nothing to try. */
export const Refused: Story = {
  args: {
    plugin: {
      ...RUNNING, id: 'future', name: 'Future', version: '', description: '', harnesses: [], points: [], running: false,
      problem: 'needs plugin API 99, and this build speaks 1 — update Daoris, or use a plugin written for 1.',
    },
  },
};

/** A landing plugin: it speaks at `work/land`, the act a landing starts it for. */
export const Landing: Story = {
  args: {
    plugin: {
      ...RUNNING, id: 'land-github', name: 'Land on GitHub', version: '0.2.0', harnesses: [], points: ['work/land'], running: false,
      description: 'Pushes a landed branch and opens its pull request.', source: { kind: 'offer', offer: 'land-github' },
    },
  },
};

/** Agents only: it declares agents and runs nothing itself, so there is nothing to try. */
export const AgentsOnly: Story = {
  args: { plugin: { ...RUNNING, id: 'acme.agents', name: 'Acme agents', points: [], harnesses: ['acme-agent', 'acme-review'], running: false } },
};

/** No source record: nothing to update from, so no *Update…*, and the page says why. */
export const NoSource: Story = { args: { plugin: { ...RUNNING, source: { kind: 'none' } } } };

/** *Update…* pressed: what an update changes, under the header, before *Update now*. */
export const UpdatePlan: Story = {
  args: {
    plan: {
      id: 'acme.gate', applied: false, source: 'C:/somewhere/checkouts/plugins/acme.gate',
      changes: [{ what: 'version', was: '1.2.0', now: '1.3.0' }, { what: 'points', was: 'quest/consider', now: 'quest/consider, session/ended' }],
    },
  },
};

/** An update that cannot be made: the driver's refusal, verbatim, and only *Not now*. */
export const RefusedPlan: Story = {
  args: {
    plan: { id: 'acme.gate', applied: false, changes: [], refusal: 'The folder it was added from is not there: C:/somewhere/checkouts/plugins/acme.gate.' },
  },
};

/** A trial that passed: each check with the driver's own sentence. */
export const TrialPassed: Story = {
  args: {
    trial: {
      plugin: 'acme.gate', folder: RUNNING.folder, command: ['node', 'wire.mjs'], passed: true, summary: 'Every check passed.',
      steps: [
        { name: 'handshake', ok: true, sentence: 'It said it listens on quest/consider and session/ended.' },
        { name: 'quest/consider', ok: true, sentence: 'It answered allow in 41 ms.' },
        { name: 'session/ended', ok: true, sentence: 'It answered in 12 ms.' },
      ],
      said: [],
    },
  },
};

/** A trial that failed: the failed check in declined's red, and what it wrote to stderr. */
export const TrialFailed: Story = {
  args: {
    trial: {
      plugin: 'acme.gate', folder: RUNNING.folder, command: ['node', 'wire.mjs'], passed: false, summary: 'One check failed.',
      steps: [
        { name: 'handshake', ok: true, sentence: 'It said it listens on quest/consider and session/ended.' },
        { name: 'quest/consider', ok: false, sentence: 'It answered after 10 s, which the driver reads as no answer.' },
      ],
      said: ['acme.gate: reading the quiet hours from C:/somewhere/quiet.json', 'acme.gate: timed out waiting for the calendar'],
    },
  },
};

/** *Remove…* pressed once: the sentence saying what the second press does, *Remove plugin* and *Never mind*. */
export const RemoveAsking: Story = { args: { asking: true } };

/** Its own icon leads the header (D140 §2), and *update available* sits beside its state: light, chosen. */
export const IconAndUpdateLight: Story = {
  args: { plugin: { ...RUNNING, icon: SVG_ICON, update: 'waits' } },
  decorators: [(Story) => <InTheme theme="light"><Story /></InTheme>],
};

/** The same in dark, chosen. */
export const IconAndUpdateDark: Story = {
  args: { plugin: { ...RUNNING, icon: SVG_ICON, update: 'waits' } },
  decorators: [(Story) => <InTheme theme="dark"><Story /></InTheme>],
};

/** Its monogram, in dark: a plugin with no icon of its own, or one whose icon does not draw. */
export const MonogramDark: Story = { decorators: [(Story) => <InTheme theme="dark"><Story /></InTheme>] };

/**
 * A declared icon that does not draw (D140 §3.1): the monogram, and its Source says why in the reader's sentence. Never
 * a refusal: the plugin is sound, and nothing leads the page.
 */
export const IconNotDrawn: Story = {
  args: { plugin: { ...RUNNING, iconProblem: "`icon` `assets/icon.svg` is not a file in the plugin's folder." } },
};

/** Off, with its own icon: drawn faint, as its name is. */
export const OffWithIcon: Story = { args: { plugin: { ...RUNNING, enabled: false, running: false, icon: PNG_ICON } } };

/** Nothing chosen: how to choose, and the list's ＋ kinds. */
export const NothingChosen: StoryObj<typeof PluginMainNotice> = {
  render: () => (
    <PluginMainNotice
      state="none"
      actions={[{ id: 'ask', label: 'Ask Daoris for a plugin' }, { id: 'make', label: 'Make a plugin…' }]}
      onAct={nothing}
    />
  ),
};

/** Gone: the chosen plugin was removed at a terminal or by hand. */
export const Gone: StoryObj<typeof PluginMainNotice> = { render: () => <PluginMainNotice state="gone" /> };

/** Loading: skeleton rows, never the empty state. */
export const Loading: StoryObj<typeof PluginMainNotice> = { render: () => <PluginMainNotice state="loading" /> };
