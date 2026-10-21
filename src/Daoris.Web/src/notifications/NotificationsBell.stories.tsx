import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../design/storyLanguage';
import { InTheme } from '../plugins/storyIcons';
import { NotificationsBell } from './NotificationsBell';

// The title bar's bell (NOTIFY2b, the notification design §2.1, §3.3): what needs the person in open's hue; the presses' own count
// in the warning hue while one waits unshown, which must be seen at a glance beside the strip's other controls in both themes (§10).

/** The strip's trailing group, beside a stand-in for the browser's door, at the strip's height. */
const strip: Decorator = (Story) => (
  <div className="flex h-9 items-center gap-2 border-b border-line bg-raised px-3"><Story /><span className="text-small text-ink-faint">◎</span></div>
);
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const meta = {
  title: 'Shell/NotificationsBell',
  component: NotificationsBell,
  args: { needs: 3, presses: 1, warn: false, onPress: () => {} },
  decorators: [strip],
} satisfies Meta<typeof NotificationsBell>;

export default meta;
type Story = StoryObj<typeof meta>;

export const NeedsYou: Story = {};
export const NeedsYouDark: Story = { decorators: [dark] };
/** A press waits unshown: the warning hue, with the presses' own count. */
export const PressUnshown: Story = { args: { needs: 5, presses: 2, warn: true } };
export const PressUnshownDark: Story = { args: { needs: 5, presses: 2, warn: true }, decorators: [dark] };
export const PressUnshownChinese: Story = { args: { needs: 5, presses: 2, warn: true }, decorators: [chinese] };
export const Nothing: Story = { args: { needs: 0, presses: 0 } };

/** Something unread in *Done and failed* (NOTIFY2f): a dot, never a second count. */
export const Unread: Story = { args: { unread: true } };
export const UnreadDark: Story = { args: { unread: true }, decorators: [dark] };
export const UnreadAlone: Story = { args: { needs: 0, presses: 0, unread: true } };
