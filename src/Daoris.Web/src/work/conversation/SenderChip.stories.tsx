import type { Meta, StoryObj } from '@storybook/react-vite';
import { atWidths, inDark, inLight } from '../../design/flowFrames';
import { chinese } from '../../design/storyLanguage';
import { toTurns } from './conversation';
import { ConversationView } from './ConversationView';
import { exchangesOf } from './exchanges';
import { channelHomeOf } from './channelHome';
import { CHANNEL, nameOf, personsAsk, senderEvents } from './senderChips.fixtures';

// Each word's turn headed by its sender chip, from the one sender rule: the person's, Daoris's own,
// another session's claim with the door to the channel, a helper's answer to the person's own ask as that agent's block,
// the supervisor's press and a second opinion's findings. At the main area's three widths (80, 39 and 25rem:
// `storyWidths.tsx`) and the side bar's 300 px dock floor, each in English in light and in 中文 in dark.

const nothing = () => {};

function Senders({ channel }: { channel: boolean }) {
  const { turns } = toTurns(senderEvents());
  return (
    <ConversationView
      turns={turns}
      structured
      exchanges={exchangesOf(turns, [personsAsk()], nameOf)}
      channel={channel ? channelHomeOf(CHANNEL, nameOf, nothing) : undefined}
    />
  );
}

const meta: Meta<typeof Senders> = {
  title: 'Work/SenderChip',
  component: Senders,
  args: { channel: true },
};
export default meta;

type Story = StoryObj<typeof Senders>;

const WIDTHS = [300, 400, 624, 1280] as const;
const light = [atWidths(WIDTHS), inLight];
const zhDark = [chinese, atWidths(WIDTHS), inDark];

/** Every sender, the channel's message named by its poster and folded under the door to the channel. */
export const EverySender: Story = { decorators: light };
export const EverySenderChinese: Story = { decorators: zhDark };

/** A page that holds no channel: the claim named as one and drawn whole where it is taken. */
export const NoChannel: Story = { args: { channel: false }, decorators: light };
export const NoChannelChinese: Story = { args: { channel: false }, decorators: zhDark };
