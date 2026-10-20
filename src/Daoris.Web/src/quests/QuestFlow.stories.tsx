import type { Meta, StoryObj } from '@storybook/react-vite';
import { useTranslation } from 'react-i18next';
import type { Quest } from '../data/api';
import { Flow } from '../design/Flow';
import { atWidths, inDark, inLight } from '../design/flowFrames';
import { chinese } from '../design/storyLanguage';
import { DECLINED, DONE, OPEN, TAKEN } from './fixtures';
import { QuestFlow } from './QuestFlow';
import { questFlow } from './questLife';

// A quest's statuses drawn as a flowchart (FLOWCHART1a1; the flowchart design §2.1, §4.4): the four statuses and the moves
// `QuestTransitions.Allows` allows, the one it stands at lit, at the frame's widths, 300 px the side bar's floor (the rail),
// 400 and 544 top to bottom, 1200 left to right. The first decorator is the innermost, so a theme's page wraps the frames.

/** The drawing as the quest's page opens it: what its section holds once unfolded. */
function QuestLife({ quest }: { quest: Pick<Quest, 'id' | 'status' | 'submission'> }) {
  const { t } = useTranslation();
  return <Flow {...questFlow(t, quest)} />;
}

const meta: Meta<typeof QuestLife> = {
  title: 'Quests/QuestFlow',
  component: QuestLife,
};
export default meta;

type Story = StoryObj<typeof QuestLife>;

/** At every width, in light: a story's own decorators, so a story in another look draws no frame inside another. */
const light = [atWidths(), inLight];

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
/** Done: nothing leaves a close; whether it was taken first is not drawn, since the page does not hold its moves. */
export const Done: Story = { args: { quest: DONE }, decorators: light };
/** Declined, the same. */
export const Declined: Story = { args: { quest: DECLINED }, decorators: light };

/** The section as the page draws it: folded to its line until it is opened. */
export const Folded: StoryObj<typeof QuestFlow> = {
  render: () => <QuestFlow quest={TAKEN} />,
  decorators: [atWidths([400]), inLight],
};
