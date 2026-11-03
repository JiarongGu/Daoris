import type { Meta, StoryObj } from '@storybook/react-vite';
import { atWidths, inDark, inLight } from '../../design/flowFrames';
import { chinese } from '../../design/storyLanguage';
import { toTurns } from './conversation';
import { ConversationView } from './ConversationView';
import { exchangesOf } from './exchanges';
import { askedEvents, askRow, nameOf } from './exchanges.fixtures';

// COMMS1h (the comms design §7): an agent's ask for a helper drawn once, as its card at its call, before and after its answer, at the
// main area's three widths (80, 39 and 25rem: `storyWidths.tsx`) and the side bar's 300 px dock floor, each in English in light and in
// 中文 in dark.

const nothing = () => {};

function Asked({ answered, state }: { answered: boolean; state: string | null }) {
  const { turns } = toTurns(askedEvents(answered));
  return (
    <ConversationView
      turns={turns}
      structured
      exchanges={exchangesOf(turns, [askRow(state, state === null || state === 'waiting' ? null : 'helper-7')], nameOf)}
      onOpenHelper={nothing}
      onShow={nothing}
    />
  );
}

const meta: Meta<typeof Asked> = {
  title: 'Work/ExchangeCard',
  component: Asked,
  args: { answered: false, state: 'started' },
};
export default meta;

type Story = StoryObj<typeof Asked>;

const WIDTHS = [300, 400, 624, 1280] as const;
const light = [atWidths(WIDTHS), inLight];
const zhDark = [chinese, atWidths(WIDTHS), inDark];

/** Asked, the helper reading: where its answer will go. */
export const Reading: Story = { decorators: light };
export const ReadingChinese: Story = { decorators: zhDark };

/** Asked under the `ask` rule, waiting for the person's yes. */
export const WaitingForYes: Story = { args: { state: 'waiting' }, decorators: light };
export const WaitingForYesChinese: Story = { args: { state: 'waiting' }, decorators: zhDark };

/** Answered: the answer folded into the card, and the next turn one line back to it. */
export const Answered: Story = { args: { answered: true, state: 'answered' }, decorators: light };
export const AnsweredChinese: Story = { args: { answered: true, state: 'answered' }, decorators: zhDark };

/** Refused: no line saying an answer will come. */
export const Refused: Story = { args: { state: 'refused' }, decorators: light };
export const RefusedChinese: Story = { args: { state: 'refused' }, decorators: zhDark };
