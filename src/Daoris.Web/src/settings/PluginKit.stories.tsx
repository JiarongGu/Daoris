import type { Meta, StoryObj } from '@storybook/react-vite';
import { PluginKitCard, type PluginTrialResult } from './PluginKit';

// The kit a plugin is made with (PLUG8, D101), in the shape the driver's PLUGINS and PLUGIN_TRY answers
// take: an empty form, a folder just made, and a trial that failed with what the plugin said.

const POINTS = [
  { name: 'quest/consider', kind: 'decision' },
  { name: 'session/ended', kind: 'observation' },
  { name: 'work/land', kind: 'act' },
];

const FAILED: PluginTrialResult = {
  plugin: 'acme.quiet-hours',
  folder: 'C:/work/plugins/acme.quiet-hours',
  command: ['node', 'C:/work/plugins/acme.quiet-hours/plugin.mjs'],
  passed: false,
  summary: '`acme.quiet-hours` failed 2 of 5 checks.',
  steps: [
    { name: 'handshake', ok: true, sentence: 'speaks hook wire 1 and listens on quest/consider, work/land.' },
    {
      name: 'quest/consider',
      ok: false,
      sentence: 'plugin `acme.quiet-hours` answered {"kind":"later"}, which is not a decision — `{ "kind": "allow" }` or '
        + '`{ "kind": "hold", "reason": "…" }`. So the driver would hold the quest, naming this plugin.',
    },
    { name: 'work/land', ok: true, sentence: 'not pushed — acme.quiet-hours does not push yet; push `feature/0fda18-expose-a-streaming-budget` by hand.' },
    { name: 'shutdown', ok: true, sentence: 'left when told (exit 0).' },
    { name: 'stdout', ok: false, sentence: 'it wrote 1 line on stdout that is not a frame: `ready` — stdout is the wire, and anything else belongs on stderr.' },
  ],
  said: ['quiet hours: reading the calendar', 'quiet hours: 09:00–18:00'],
};

const meta = {
  title: 'Settings/PluginKit',
  component: PluginKitCard,
  args: { points: POINTS, onPick: async () => 'C:/work/plugins', onMake: () => {}, onTry: () => {} },
} satisfies Meta<typeof PluginKitCard>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Empty: Story = {};

export const JustMade: Story = { args: { made: 'C:/work/plugins/acme.quiet-hours' } };

export const Trying: Story = { args: { made: 'C:/work/plugins/acme.quiet-hours', busy: true } };

export const AFailedTrial: Story = { args: { made: 'C:/work/plugins/acme.quiet-hours', trial: FAILED } };

export const APassedTrial: Story = {
  args: {
    made: 'C:/work/plugins/acme.quiet-hours',
    trial: {
      ...FAILED,
      passed: true,
      summary: '`acme.quiet-hours` answered as the driver reads it.',
      steps: FAILED.steps.map((step) => ({ ...step, ok: true })).filter((step) => step.name !== 'quest/consider'),
      said: [],
    },
  },
};
