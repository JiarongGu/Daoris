import type { Meta, StoryObj } from '@storybook/react-vite';
import type { QuestMove } from '../data/api';
import { atWidths, inDark, inLight } from '../design/flowFrames';
import { chinese } from '../design/storyLanguage';
import { DECLINED, DONE, OPEN, TAKEN } from './fixtures';
import { QuestFlow, QuestLifeFlow } from './QuestFlow';

// A quest's statuses drawn as a flowchart (FLOWCHART1a1; the flowchart design §2.1, §4.4): the four statuses and the moves
// `QuestTransitions.Allows` allows, the one it stands at lit, at the frame's widths, 300 px the side bar's floor (the rail),
// 400 and 544 top to bottom, 1200 left to right. With its moves (FLOWCHART1a2): the way it went drawn heavier, what happened
// inside each status listed in it, a conflict refused, a held Done in open's hue. The first decorator is the innermost, so a
// theme's page wraps the frames.

const meta: Meta<typeof QuestLifeFlow> = {
  title: 'Quests/QuestFlow',
  component: QuestLifeFlow,
};
export default meta;

type Story = StoryObj<typeof QuestLifeFlow>;

/** At every width, in light: a story's own decorators, so a story in another look draws no frame inside another. */
const light = [atWidths(), inLight];

const at = (minute: number) => `2026-10-20T09:${String(minute).padStart(2, '0')}:00Z`;
const move = (kind: string, machine: string, sequence: number, minute: number, rest: Partial<QuestMove> = {}): QuestMove =>
  ({ kind, machine, sequence, at: at(minute), applied: true, number: minute + 1, ...rest });

/** Published, taken, waited on another quest, another machine's take kept as a conflict and dismissed, then done. */
const WENT: QuestMove[] = [
  move('published', 'desk', 1, 0),
  move('taken', 'm3', 1, 5, { from: 'Open' }),
  move('waited', 'm3', 2, 6, { from: 'Taken', waitsOn: 'q2q2q2' }),
  move('conflict', 'm4', 7, 7, { from: 'Taken', attempted: 'Taken' }),
  move('dismissed', 'm3', 3, 20, { from: 'Taken', dismisses: { machine: 'm4', sequence: 7 } }),
  move('done', 'm3', 4, 30, { from: 'Taken' }),
  // Another machine's done after the close: recorded, and not applied.
  move('done', 'm5', 1, 31, { from: 'Done', applied: false }),
];

/** Declined, and another machine's done that reached the remote second, kept as a conflict: a refused edge toward Done. */
const REFUSED: QuestMove[] = [
  move('published', 'desk', 1, 0),
  move('taken', 'm3', 1, 5, { from: 'Open' }),
  move('declined', 'm3', 2, 30, { from: 'Taken' }),
  move('conflict', 'm4', 7, 31, { from: 'Declined', attempted: 'Done' }),
];

/** Published, nobody has taken it: Open lit, its three moves said. */
export const Open: Story = { args: { quest: OPEN }, decorators: light };
/** Taken: Taken lit, Done and Declined still to come. */
export const Taken: Story = { args: { quest: TAKEN }, decorators: light };
/** The same in dark. */
export const TakenDark: Story = { args: { quest: TAKEN }, decorators: [atWidths(), inDark] };
/** The same in 中文. */
export const TakenChinese: Story = { args: { quest: TAKEN }, decorators: [chinese, atWidths(), inLight] };
/** The same in 中文, in dark. */
export const TakenChineseDark: Story = { args: { quest: TAKEN }, decorators: [chinese, atWidths(), inDark] };
/** Its agent made its work ready: Taken lit, saying *submitted*, as the quest's own pill does. */
export const Submitted: Story = {
  args: { quest: { ...TAKEN, submission: { machine: 'm1', sequence: 3, at: TAKEN.updated } } }, decorators: light,
};
/** Done, its moves not read: nothing leaves a close, and whether it was taken first is not drawn. */
export const Done: Story = { args: { quest: DONE }, decorators: light };
/** Declined, the same. */
export const Declined: Story = { args: { quest: DECLINED }, decorators: light };

/** The way it went (FLOWCHART1a2): its path heavier, and inside Taken a wait, a conflict and its dismissal; a late done not applied. */
export const Went: Story = { args: { quest: DONE, moves: WENT }, decorators: light };
/** The same in 中文. */
export const WentChinese: Story = { args: { quest: DONE, moves: WENT }, decorators: [chinese, atWidths(), inLight] };
/** The same in dark. */
export const WentDark: Story = { args: { quest: DONE, moves: WENT }, decorators: [atWidths(), inDark] };
/** A conflict that attempted another status: a refused edge from Declined toward Done, ended in ✕. */
export const Refused: Story = { args: { quest: DECLINED, moves: REFUSED }, decorators: light };
/** A Done its departure holds: it awaits the person's yes, in open's hue. */
export const HeldDone: Story = {
  args: { quest: { ...DONE, hold: 'departed' }, moves: WENT.slice(0, 6).map((each, index) => (index === 5 ? { ...each, hold: 'departed' } : each)) },
  decorators: light,
};
/** An older host, which keeps no moves (trap 18): the statuses alone, as without them. */
export const NotKept: Story = { args: { quest: DONE, moves: null }, decorators: light };

/** The section as the page draws it: folded to its line until it is opened. */
export const Folded: StoryObj<typeof QuestFlow> = {
  render: () => <QuestFlow quest={TAKEN} />,
  decorators: [atWidths([400]), inLight],
};
