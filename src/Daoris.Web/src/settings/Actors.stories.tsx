import type { Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../design/storyLanguage';
import { storyWidths } from '../design/storyWidths';
import type { ActorRow } from '../bridge/driver';
import { ActorsCard } from './Actors';

// The actors on AI features (MODELROLE1c, D205 §7): each kind of work Daoris starts, its order folded to one line, in the shapes
// `ACTORS` answers with; Build set, the rest as before or off.

const row = (id: string, state: ActorRow['state'], choices: ActorRow['choices'] = [], error?: string): ActorRow => ({ id, state, choices, error });

const ROWS: ActorRow[] = [
  row('build', 'set', [
    { agent: 'claude-code', model: 'opus', effort: 'high', from: '1' },
    { agent: 'codex-acp', from: '2' },
  ]),
  row('intake', 'off'),
  row('opinion', 'before'),
  row('helper', 'before'),
  row('partner', 'before'),
  row('brainstorm', 'set', [{ agent: 'claude-code', from: '1' }]),
  row('ask-daoris', 'before', [{ agent: 'claude-code-acp', from: 'before' }]),
  row('chat', 'set', [{ agent: 'claude-code', from: '1' }]),
];

const meta: Meta<typeof ActorsCard> = {
  title: 'Settings/Actors',
  component: ActorsCard,
  args: {
    rows: ROWS,
    agents: [
      { value: 'claude-code', label: 'Claude Code — claude-code' },
      { value: 'claude-code-acp', label: 'Claude Code — claude-code-acp' },
      { value: 'codex-acp', label: 'Codex — codex-acp' },
    ],
    onSave: () => {},
  },
  // The main area at its wide width, as the frame gives it (the UI/UX design R6, UIFIT1).
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof ActorsCard>;

/** Build set to two choices after a change of its agent, Chat and Brainstorm written for the agent they ran. */
export const BuildSet: Story = {};

/** The same at the half width. */
export const BuildSetHalf: Story = { decorators: [storyWidths.half] };

/** The same at the 400 px floor: each value under its words. */
export const BuildSetNarrow: Story = { decorators: [storyWidths.narrow] };

/** The same in 中文, wide. */
export const BuildSetChinese: Story = { decorators: [chinese] };

/** The same in 中文 at the floor. */
export const BuildSetChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };

/** Build's entry in driver.json does not read: the driver's sentence, and nothing starts. */
export const Unreadable: Story = {
  args: { rows: [row('build', 'unreadable', [], 'choice 1 names no agent'), ...ROWS.slice(1)] },
};
