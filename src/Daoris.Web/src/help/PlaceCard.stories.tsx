import type { Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../design/storyLanguage';
import { storyWidths } from '../design/storyWidths';
import { InTheme } from '../plugins/storyIcons';
import { type HelpProposal, ProposalCard } from './ProposalCard';

// AGENTSPLACE1k: Ask Daoris's card for a place's quick set-up, at the widths the frame gives the main area (80, 39 and 25rem), in both
// languages and both themes. Its Apply opens the place's Set up… and presses nothing.

const PLACE: HelpProposal = {
  id: 'pl1a2b3c', kind: 'setup',
  describe: 'Open the Agents place\'s Setup for `engine`, `site` in workspace `aurora`, with *Set up…* open on this ask — merge-after-accept, '
    + 'asking its session to set up for agents (service). The plan: its own tree (engine, site); a set-up asked of its own session (site). '
    + 'You read the plan there and press Apply yourself: nothing is written until you press Apply there.',
  terminal: 'daoris place engine site --preset merge-after-accept --with adoption --what-it-builds service --plan',
  why: 'you asked for both to be set up with a merge after you accept',
  place: {
    workspace: 'aurora', digest: 'd1',
    asked: { repositories: ['engine', 'site'], preset: 'merge-after-accept', opinion: false, look: false, adoption: true, builds: 'service', drive: false },
    changes: [
      { key: 'SET_TREES', part: 'delivery', said: 'its own tree for its sessions', before: 'off', after: 'on', repositories: ['engine', 'site'] },
      { key: 'SET_DRIVABLE', part: 'driving', said: 'general driving', before: 'off', after: 'on', repositories: ['engine', 'site'] },
      { key: 'SETUP_PRESS', part: 'adoption', said: 'a set-up asked of its own session', before: 'none', after: 'asked', repositories: ['site'] },
    ],
  },
};

const NOTHING: HelpProposal = { ...PLACE, id: 'pl4d5e6f', place: { ...PLACE.place!, changes: [] } };

const meta = {
  title: 'Help/PlaceCard',
  component: ProposalCard,
  decorators: [storyWidths.wide],
  render: (args) => <ul className="m-0 grid list-none p-0"><ProposalCard {...args} /></ul>,
  args: { proposal: PLACE, onApply: () => {}, onDismiss: () => {} },
} satisfies Meta<typeof ProposalCard>;

export default meta;
type Story = StoryObj<typeof meta>;

const dark: Story['render'] = (args) => <InTheme theme="dark"><ul className="m-0 grid list-none p-0"><ProposalCard {...args} /></ul></InTheme>;

/** The card across at 80rem: the ask in the sentence, each change once with the repositories it reaches, and the command. */
export const Wide: Story = {};
export const Half: Story = { decorators: [storyWidths.half] };
export const Narrow: Story = { decorators: [storyWidths.narrow] };

export const Chinese: Story = { decorators: [chinese] };
export const ChineseHalf: Story = { decorators: [storyWidths.half, chinese] };
export const ChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };

export const Dark: Story = { render: dark };
export const DarkHalf: Story = { render: dark, decorators: [storyWidths.half] };
export const DarkNarrow: Story = { render: dark, decorators: [storyWidths.narrow] };
export const ChineseDark: Story = { render: dark, decorators: [chinese] };

/** A plan that changes nothing now, as a repository already set up as asked. */
export const ChangesNothing: Story = { args: { proposal: NOTHING } };
